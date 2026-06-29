using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Shows local runtime information such as date, time, weather, and local mail status.
/// </summary>
public sealed class RuntimeInformationPanel : MonoBehaviour
{
    private const float MinimumWeatherRefreshIntervalSeconds = 60f;
    private const float MinimumMailRefreshIntervalSeconds = 10f;
    private const int DefaultRequestTimeoutSeconds = 12;
    private const int MaxMailPreviewCount = 5;
    private const string IpLocationEndpoint = "https://ipwho.is/";
    private const string OpenMeteoEndpointFormat = "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m,wind_direction_10m&timezone=auto";

    private const string QueryOutlookMailScript = @"
$ErrorActionPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$separator = [char]9

function Encode-Field([string]$value) {
    if ($null -eq $value) {
        $value = ''
    }

    [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes([string]$value))
}

function Write-EncodedLine([string[]]$fields) {
    ($fields | ForEach-Object { Encode-Field $_ }) -join $separator
}

try {
    $outlook = New-Object -ComObject Outlook.Application
    $namespace = $outlook.GetNamespace('MAPI')
    $inbox = $namespace.GetDefaultFolder(6)
    $items = $inbox.Items
    $unread = $items.Restrict('[Unread] = true')
    $unread.Sort('[ReceivedTime]', $true)
    $unreadCount = [int]$unread.Count
    $accountName = ''

    if ($namespace.Accounts -and $namespace.Accounts.Count -gt 0) {
        $accountName = [string]$namespace.Accounts.Item(1).DisplayName
    }

    if ([string]::IsNullOrWhiteSpace($accountName) -and $inbox.Store -ne $null) {
        $accountName = [string]$inbox.Store.DisplayName
    }

    Write-EncodedLine @('SUMMARY', [string]$unreadCount, $accountName)

    $limit = [Math]::Min($unreadCount, 5)
    for ($i = 1; $i -le $limit; $i++) {
        $mail = $unread.Item($i)
        if ($null -eq $mail) {
            continue
        }

        $sender = [string]$mail.SenderName
        if ([string]::IsNullOrWhiteSpace($sender) -and $mail.SenderEmailAddress) {
            $sender = [string]$mail.SenderEmailAddress
        }

        $subject = [string]$mail.Subject
        $received = ''
        if ($mail.ReceivedTime) {
            $received = ([DateTime]$mail.ReceivedTime).ToString('yyyy-MM-dd HH:mm')
        }

        Write-EncodedLine @('MAIL', $sender, $subject, $received)
    }
}
catch {
    Write-EncodedLine @('ERROR', $_.Exception.Message)
}
";

    [Header("Panel")]
    public bool showPanel = true;
    public KeyCode togglePanelKey = KeyCode.F9;
    public Rect panelRect = new Rect(460f, 16f, 440f, 520f);
    public float mailListHeight = 150f;

    [Header("Screen Canvas Panel")]
    [Tooltip("Renders this panel inside Screen/Canvas/InfoPanel instead of the legacy IMGUI overlay.")]
    public bool useScreenCanvasPanel = true;

    [Tooltip("Optional target panel under Screen/Canvas. When empty, Screen/Canvas/InfoPanel is used.")]
    public RectTransform screenCanvasPanelRoot;

    [Tooltip("How often the Screen/Canvas panel text is refreshed.")]
    [Range(0.05f, 1f)]
    public float screenCanvasRefreshInterval = 0.2f;

    [Header("Weather")]
    public bool autoRefreshWeather = true;
    public bool useIpLocationForWeather = true;
    public bool useManualWeatherLocation;
    public double manualLatitude;
    public double manualLongitude;
    public string manualWeatherLocationLabel = "Manual Location";
    public float weatherRefreshIntervalSeconds = 900f;

    [Header("Mail")]
    public bool autoRefreshMail = true;
    public float mailRefreshIntervalSeconds = 300f;
    public float mailQueryTimeoutSeconds = 8f;

    private static RuntimeInformationPanel instance;

    private readonly object mailLock = new object();
    private readonly List<MailPreview> mailPreviews = new List<MailPreview>();

    private CancellationTokenSource mailCancellation;
    private Vector2 mailScrollPosition;
    private GUIStyle wrapLabelStyle;
    private GUIStyle strongLabelStyle;
    private GUIStyle mutedLabelStyle;
    private RectTransform screenCanvasContent;
    private Text localTimeText;
    private Text weatherText;
    private Text mailText;
    private Text systemText;
    private Font screenCanvasFont;
    private float nextScreenCanvasRefreshTime;

    private bool weatherRefreshInProgress;
    private bool mailRefreshInProgress;
    private DateTime lastWeatherRefreshUtc;
    private DateTime lastMailRefreshUtc;
    private WeatherSnapshot weatherSnapshot;
    private string weatherError;
    private int unreadMailCount;
    private string mailAccountName = string.Empty;
    private string mailError;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        mailCancellation = new CancellationTokenSource();
    }

    private void Start()
    {
        RequestWeatherRefresh(true);
        RequestMailRefresh(true);
    }

    private void Update()
    {
        if (togglePanelKey != KeyCode.None && Input.GetKeyDown(togglePanelKey))
        {
            showPanel = !showPanel;
            UpdateScreenCanvasPanel(true);
        }

        if (autoRefreshWeather
            && (DateTime.UtcNow - lastWeatherRefreshUtc).TotalSeconds >= Mathf.Max(MinimumWeatherRefreshIntervalSeconds, weatherRefreshIntervalSeconds))
        {
            RequestWeatherRefresh(false);
        }

        if (autoRefreshMail
            && (DateTime.UtcNow - lastMailRefreshUtc).TotalSeconds >= Mathf.Max(MinimumMailRefreshIntervalSeconds, mailRefreshIntervalSeconds))
        {
            RequestMailRefresh(false);
        }

        UpdateScreenCanvasPanel(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (mailCancellation != null)
        {
            mailCancellation.Cancel();
            mailCancellation.Dispose();
            mailCancellation = null;
        }
    }

    public void RequestWeatherRefresh(bool force)
    {
        if (weatherRefreshInProgress)
        {
            return;
        }

        if (!force && (DateTime.UtcNow - lastWeatherRefreshUtc).TotalSeconds < Mathf.Max(MinimumWeatherRefreshIntervalSeconds, weatherRefreshIntervalSeconds))
        {
            return;
        }

        StartCoroutine(RefreshWeather());
    }

    public void RequestMailRefresh(bool force)
    {
        if (!IsWindowsRuntime())
        {
            lock (mailLock)
            {
                mailError = "Outlook mail detection is available only on Windows.";
                lastMailRefreshUtc = DateTime.UtcNow;
                mailRefreshInProgress = false;
            }

            return;
        }

        lock (mailLock)
        {
            if (mailRefreshInProgress)
            {
                return;
            }

            if (!force && (DateTime.UtcNow - lastMailRefreshUtc).TotalSeconds < Mathf.Max(MinimumMailRefreshIntervalSeconds, mailRefreshIntervalSeconds))
            {
                return;
            }

            mailRefreshInProgress = true;
            mailError = null;
        }

        if (mailCancellation == null || mailCancellation.IsCancellationRequested)
        {
            mailCancellation = new CancellationTokenSource();
        }

        int timeoutMilliseconds = Mathf.Max(1000, Mathf.RoundToInt(mailQueryTimeoutSeconds * 1000f));
        CancellationToken token = mailCancellation.Token;

        Task.Run(() => QueryOutlookMail(timeoutMilliseconds, token), token)
            .ContinueWith(task =>
            {
                MailQueryResult result = null;
                string error = null;

                if (task.IsCanceled || token.IsCancellationRequested)
                {
                    error = "Mail refresh was canceled.";
                }
                else if (task.IsFaulted)
                {
                    error = task.Exception != null ? task.Exception.GetBaseException().Message : "Mail refresh failed.";
                }
                else
                {
                    result = task.Result;
                    error = result.ErrorMessage;
                }

                lock (mailLock)
                {
                    mailRefreshInProgress = false;

                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    mailPreviews.Clear();
                    if (result != null)
                    {
                        unreadMailCount = result.UnreadCount;
                        mailAccountName = result.AccountName;
                        mailPreviews.AddRange(result.Previews);
                    }

                    mailError = error;
                    lastMailRefreshUtc = DateTime.UtcNow;
                }
            }, CancellationToken.None);
    }

    private IEnumerator RefreshWeather()
    {
        weatherRefreshInProgress = true;
        weatherError = null;

        WeatherLocation location = null;

        if (useIpLocationForWeather)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(IpLocationEndpoint))
            {
                request.timeout = DefaultRequestTimeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    location = ParseIpLocation(request.downloadHandler.text);
                }
                else
                {
                    weatherError = "Location lookup failed: " + request.error;
                }
            }
        }

        if (location == null && useManualWeatherLocation)
        {
            location = new WeatherLocation(manualLatitude, manualLongitude, manualWeatherLocationLabel);
        }

        if (location == null)
        {
            weatherError = string.IsNullOrWhiteSpace(weatherError)
                ? "Weather location is unavailable. Enable IP lookup or set manual coordinates."
                : weatherError;
            lastWeatherRefreshUtc = DateTime.UtcNow;
            weatherRefreshInProgress = false;
            yield break;
        }

        string latitude = location.Latitude.ToString("0.######", CultureInfo.InvariantCulture);
        string longitude = location.Longitude.ToString("0.######", CultureInfo.InvariantCulture);
        string weatherUrl = string.Format(CultureInfo.InvariantCulture, OpenMeteoEndpointFormat, latitude, longitude);

        using (UnityWebRequest request = UnityWebRequest.Get(weatherUrl))
        {
            request.timeout = DefaultRequestTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                WeatherSnapshot snapshot = ParseWeather(request.downloadHandler.text, location);
                if (snapshot != null)
                {
                    weatherSnapshot = snapshot;
                    weatherError = null;
                }
                else
                {
                    weatherError = "Weather response could not be parsed.";
                }
            }
            else
            {
                weatherError = "Weather refresh failed: " + request.error;
            }
        }

        lastWeatherRefreshUtc = DateTime.UtcNow;
        weatherRefreshInProgress = false;
    }

    private void OnGUI()
    {
        if (useScreenCanvasPanel && EnsureScreenCanvasPanel(false))
        {
            return;
        }

        if (!showPanel)
        {
            return;
        }

        EnsureStyles();
        panelRect.width = Mathf.Max(panelRect.width, 400f);
        panelRect.height = Mathf.Max(panelRect.height, 420f);
        panelRect = GUILayout.Window(GetInstanceID(), panelRect, DrawPanel, "Information Panel");
    }

    private void DrawPanel(int windowId)
    {
        DrawLocalTimeSection();
        GUILayout.Space(8f);
        DrawWeatherSection();
        GUILayout.Space(8f);
        DrawMailSection();
        GUILayout.Space(8f);
        DrawSystemSection();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
    }

    private void DrawLocalTimeSection()
    {
        DateTime now = DateTime.Now;
        GUILayout.Label("Local Time", strongLabelStyle);
        GUILayout.Label(now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        GUILayout.Label(now.ToString("yyyy-MM-dd dddd", CultureInfo.CurrentCulture), wrapLabelStyle);
        GUILayout.Label(TimeZoneInfo.Local.DisplayName, mutedLabelStyle);
    }

    private void DrawWeatherSection()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Weather", strongLabelStyle);
        GUILayout.FlexibleSpace();
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && !weatherRefreshInProgress;
        if (GUILayout.Button(weatherRefreshInProgress ? "Refreshing..." : "Refresh", GUILayout.Width(96f)))
        {
            RequestWeatherRefresh(true);
        }

        GUI.enabled = wasEnabled;
        autoRefreshWeather = GUILayout.Toggle(autoRefreshWeather, "Auto", GUILayout.Width(58f));
        GUILayout.EndHorizontal();

        if (weatherSnapshot != null)
        {
            GUILayout.Label(weatherSnapshot.LocationLabel, wrapLabelStyle);
            GUILayout.Label(string.Format(
                CultureInfo.CurrentCulture,
                "{0:0.#} C | feels {1:0.#} C | {2}",
                weatherSnapshot.TemperatureCelsius,
                weatherSnapshot.ApparentTemperatureCelsius,
                weatherSnapshot.Condition),
                wrapLabelStyle);
            GUILayout.Label(string.Format(
                CultureInfo.CurrentCulture,
                "Humidity {0:0}% | Wind {1:0.#} km/h {2:0} deg | Rain {3:0.#} mm",
                weatherSnapshot.RelativeHumidity,
                weatherSnapshot.WindSpeedKmh,
                weatherSnapshot.WindDirectionDegrees,
                weatherSnapshot.PrecipitationMm),
                wrapLabelStyle);
        }
        else
        {
            GUILayout.Label("No weather data yet.", wrapLabelStyle);
        }

        if (lastWeatherRefreshUtc != default(DateTime))
        {
            GUILayout.Label("Last weather refresh: " + lastWeatherRefreshUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture), mutedLabelStyle);
        }

        if (!string.IsNullOrWhiteSpace(weatherError))
        {
            GUILayout.Label(weatherError, wrapLabelStyle);
        }
    }

    private void DrawMailSection()
    {
        List<MailPreview> previews;
        int unreadCount;
        string accountName;
        string error;
        bool refreshing;
        DateTime refreshedUtc;

        lock (mailLock)
        {
            previews = new List<MailPreview>(mailPreviews);
            unreadCount = unreadMailCount;
            accountName = mailAccountName;
            error = mailError;
            refreshing = mailRefreshInProgress;
            refreshedUtc = lastMailRefreshUtc;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Mail", strongLabelStyle);
        GUILayout.FlexibleSpace();
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && !refreshing;
        if (GUILayout.Button(refreshing ? "Refreshing..." : "Refresh", GUILayout.Width(96f)))
        {
            RequestMailRefresh(true);
        }

        GUI.enabled = wasEnabled;
        autoRefreshMail = GUILayout.Toggle(autoRefreshMail, "Auto", GUILayout.Width(58f));
        GUILayout.EndHorizontal();

        GUILayout.Label("Unread: " + unreadCount, wrapLabelStyle);
        if (!string.IsNullOrWhiteSpace(accountName))
        {
            GUILayout.Label("Account: " + accountName, wrapLabelStyle);
        }

        if (refreshedUtc != default(DateTime))
        {
            GUILayout.Label("Last mail refresh: " + refreshedUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture), mutedLabelStyle);
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            GUILayout.Label(error, wrapLabelStyle);
        }

        mailScrollPosition = GUILayout.BeginScrollView(mailScrollPosition, GUILayout.Height(mailListHeight));
        if (previews.Count == 0)
        {
            GUILayout.Label("No unread mail previews from the local Outlook inbox.", wrapLabelStyle);
        }
        else
        {
            for (int i = 0; i < previews.Count; i++)
            {
                MailPreview preview = previews[i];
                GUILayout.Label(preview.ReceivedAtLabel + " | " + preview.Sender, strongLabelStyle);
                GUILayout.Label(preview.Subject, wrapLabelStyle);
                GUILayout.Space(4f);
            }
        }

        GUILayout.EndScrollView();
    }

    private void DrawSystemSection()
    {
        GUILayout.Label("System", strongLabelStyle);
        GUILayout.Label("Machine: " + Environment.MachineName + " | User: " + Environment.UserName, wrapLabelStyle);
        GUILayout.Label("Platform: " + Application.platform + " | Network: " + Application.internetReachability, wrapLabelStyle);
    }

    private void EnsureStyles()
    {
        if (wrapLabelStyle != null)
        {
            return;
        }

        wrapLabelStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true
        };

        strongLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };

        mutedLabelStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true
        };
        mutedLabelStyle.normal.textColor = new Color(0.72f, 0.72f, 0.72f, 1f);
    }

    private void UpdateScreenCanvasPanel(bool force)
    {
        if (!useScreenCanvasPanel || !EnsureScreenCanvasPanel(false))
        {
            return;
        }

        screenCanvasContent.gameObject.SetActive(showPanel);
        if (!showPanel)
        {
            return;
        }

        if (!force && Time.unscaledTime < nextScreenCanvasRefreshTime)
        {
            return;
        }

        nextScreenCanvasRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, screenCanvasRefreshInterval);
        UpdateLocalTimeText();
        UpdateWeatherText();
        UpdateMailText();
        UpdateSystemText();
    }

    private bool EnsureScreenCanvasPanel(bool forceRebuild)
    {
        if (!useScreenCanvasPanel)
        {
            return false;
        }

        if (screenCanvasPanelRoot == null)
        {
            GameObject panelObject = GameObject.Find("Screen/Canvas/InfoPanel");
            if (panelObject != null)
            {
                screenCanvasPanelRoot = panelObject.GetComponent<RectTransform>();
            }
        }

        if (screenCanvasPanelRoot == null)
        {
            return false;
        }

        if (screenCanvasContent != null && localTimeText != null && weatherText != null && mailText != null && systemText != null && !forceRebuild)
        {
            return true;
        }

        screenCanvasFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (screenCanvasFont == null)
        {
            UnityEngine.Debug.LogError("[RuntimeInformationPanel] LegacyRuntime.ttf built-in font was not found. Screen canvas panel cannot be created.");
            return false;
        }

        screenCanvasContent = FindOrCreateRect("RuntimeInfoCanvasContent", screenCanvasPanelRoot, new Vector2(270f, 480f));
        screenCanvasContent.localScale = Vector3.one * 0.1f;

        VerticalLayoutGroup layout = screenCanvasContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = screenCanvasContent.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ScreenCanvasArtTheme.ApplyPanelArt(
            screenCanvasContent,
            ScreenCanvasArtTheme.InfoPanelBase,
            ScreenCanvasArtTheme.InfoPrimaryAccent,
            ScreenCanvasArtTheme.InfoSecondaryAccent,
            true);

        localTimeText = FindOrCreateText("LocalTimeText", screenCanvasContent, string.Empty, 15, FontStyle.Bold, TextAnchor.UpperLeft, 70f);

        RectTransform weatherHeader = FindOrCreateHeaderRow("WeatherHeader", screenCanvasContent, "Weather");
        Button weatherRefreshButton = FindOrCreateButton("WeatherRefreshButton", weatherHeader, "Refresh");
        weatherRefreshButton.onClick.RemoveAllListeners();
        weatherRefreshButton.onClick.AddListener(() => RequestWeatherRefresh(true));
        Toggle weatherAutoToggle = FindOrCreateToggle("WeatherAutoToggle", weatherHeader, "Auto", autoRefreshWeather);
        weatherAutoToggle.onValueChanged.RemoveAllListeners();
        weatherAutoToggle.onValueChanged.AddListener(value => autoRefreshWeather = value);
        weatherText = FindOrCreateText("WeatherText", screenCanvasContent, string.Empty, 10, FontStyle.Normal, TextAnchor.UpperLeft, 110f);

        RectTransform mailHeader = FindOrCreateHeaderRow("MailHeader", screenCanvasContent, "Mail");
        Button mailRefreshButton = FindOrCreateButton("MailRefreshButton", mailHeader, "Refresh");
        mailRefreshButton.onClick.RemoveAllListeners();
        mailRefreshButton.onClick.AddListener(() => RequestMailRefresh(true));
        Toggle mailAutoToggle = FindOrCreateToggle("MailAutoToggle", mailHeader, "Auto", autoRefreshMail);
        mailAutoToggle.onValueChanged.RemoveAllListeners();
        mailAutoToggle.onValueChanged.AddListener(value => autoRefreshMail = value);
        mailText = FindOrCreateText("MailText", screenCanvasContent, string.Empty, 9, FontStyle.Normal, TextAnchor.UpperLeft, 150f);

        systemText = FindOrCreateText("SystemText", screenCanvasContent, string.Empty, 9, FontStyle.Normal, TextAnchor.UpperLeft, 60f);

        UpdateScreenCanvasPanel(true);
        return true;
    }

    private void UpdateLocalTimeText()
    {
        DateTime now = DateTime.Now;
        localTimeText.text = "Local Time\n"
            + now.ToString("HH:mm:ss", CultureInfo.CurrentCulture)
            + "\n"
            + now.ToString("yyyy-MM-dd dddd", CultureInfo.CurrentCulture)
            + "\n"
            + TimeZoneInfo.Local.DisplayName;
    }

    private void UpdateWeatherText()
    {
        StringBuilder builder = new StringBuilder();
        if (weatherSnapshot != null)
        {
            builder.AppendLine(weatherSnapshot.LocationLabel);
            builder.AppendLine(string.Format(
                CultureInfo.CurrentCulture,
                "{0:0.#} C | feels {1:0.#} C | {2}",
                weatherSnapshot.TemperatureCelsius,
                weatherSnapshot.ApparentTemperatureCelsius,
                weatherSnapshot.Condition));
            builder.AppendLine(string.Format(
                CultureInfo.CurrentCulture,
                "Humidity {0:0}% | Wind {1:0.#} km/h {2:0} deg",
                weatherSnapshot.RelativeHumidity,
                weatherSnapshot.WindSpeedKmh,
                weatherSnapshot.WindDirectionDegrees));
            builder.AppendLine(string.Format(CultureInfo.CurrentCulture, "Rain {0:0.#} mm", weatherSnapshot.PrecipitationMm));
        }
        else
        {
            builder.AppendLine("No weather data yet.");
        }

        if (weatherRefreshInProgress)
        {
            builder.AppendLine("Refreshing...");
        }

        if (lastWeatherRefreshUtc != default(DateTime))
        {
            builder.AppendLine("Last refresh: " + lastWeatherRefreshUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        }

        if (!string.IsNullOrWhiteSpace(weatherError))
        {
            builder.AppendLine(weatherError);
        }

        weatherText.text = builder.ToString().TrimEnd();
    }

    private void UpdateMailText()
    {
        List<MailPreview> previews;
        int unreadCount;
        string accountName;
        string error;
        bool refreshing;
        DateTime refreshedUtc;

        lock (mailLock)
        {
            previews = new List<MailPreview>(mailPreviews);
            unreadCount = unreadMailCount;
            accountName = mailAccountName;
            error = mailError;
            refreshing = mailRefreshInProgress;
            refreshedUtc = lastMailRefreshUtc;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Unread: " + unreadCount);
        if (!string.IsNullOrWhiteSpace(accountName))
        {
            builder.AppendLine("Account: " + accountName);
        }

        if (refreshing)
        {
            builder.AppendLine("Refreshing...");
        }

        if (refreshedUtc != default(DateTime))
        {
            builder.AppendLine("Last refresh: " + refreshedUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            builder.AppendLine(error);
        }

        if (previews.Count == 0)
        {
            builder.AppendLine("No unread mail previews from the local Outlook inbox.");
        }
        else
        {
            for (int i = 0; i < previews.Count; i++)
            {
                MailPreview preview = previews[i];
                builder.AppendLine(preview.ReceivedAtLabel + " | " + preview.Sender);
                builder.AppendLine(preview.Subject);
            }
        }

        mailText.text = builder.ToString().TrimEnd();
    }

    private void UpdateSystemText()
    {
        systemText.text = "System\n"
            + "Machine: " + Environment.MachineName + " | User: " + Environment.UserName
            + "\nPlatform: " + Application.platform + " | Network: " + Application.internetReachability;
    }

    private RectTransform FindOrCreateHeaderRow(string name, RectTransform parent, string title)
    {
        RectTransform row = FindOrCreateRect(name, parent, new Vector2(250f, 30f));
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        }
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        Text label = FindOrCreateText(title + "Label", row, title, 12, FontStyle.Bold, TextAnchor.MiddleLeft, 28f);
        LayoutElement labelLayout = label.GetComponent<LayoutElement>();
        if (labelLayout == null)
        {
            labelLayout = label.gameObject.AddComponent<LayoutElement>();
        }
        labelLayout.flexibleWidth = 1f;
        labelLayout.preferredWidth = 90f;
        ScreenCanvasArtTheme.StyleText(label, title + "Label", 12, FontStyle.Bold);
        return row;
    }

    private RectTransform FindOrCreateRect(string name, RectTransform parent, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        RectTransform rectTransform = obj.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = obj.AddComponent<RectTransform>();
        }

        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;
        rectTransform.localScale = Vector3.one;
        return rectTransform;
    }

    private Text FindOrCreateText(string name, RectTransform parent, string text, int fontSize, FontStyle fontStyle, TextAnchor alignment, float height)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        obj.layer = parent.gameObject.layer;
        RectTransform rectTransform = obj.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = obj.AddComponent<RectTransform>();
        }

        rectTransform.SetParent(parent, false);
        rectTransform.sizeDelta = new Vector2(250f, height);

        if (obj.GetComponent<CanvasRenderer>() == null)
        {
            obj.AddComponent<CanvasRenderer>();
        }

        Text label = obj.GetComponent<Text>();
        if (label == null)
        {
            label = obj.AddComponent<Text>();
        }

        label.font = screenCanvasFont;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.color = Color.white;
        label.text = text;
        ScreenCanvasArtTheme.StyleText(label, name, fontSize, fontStyle);
        return label;
    }

    private Button FindOrCreateButton(string name, RectTransform parent, string label)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        obj.layer = parent.gameObject.layer;
        RectTransform rectTransform = obj.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = obj.AddComponent<RectTransform>();
        }

        rectTransform.SetParent(parent, false);
        rectTransform.sizeDelta = new Vector2(62f, 28f);

        if (obj.GetComponent<CanvasRenderer>() == null)
        {
            obj.AddComponent<CanvasRenderer>();
        }

        Image image = obj.GetComponent<Image>();
        if (image == null)
        {
            image = obj.AddComponent<Image>();
        }
        image.color = new Color(0.18f, 0.24f, 0.28f, 0.88f);
        image.raycastTarget = true;

        Button button = obj.GetComponent<Button>();
        if (button == null)
        {
            button = obj.AddComponent<Button>();
        }
        button.targetGraphic = image;
        ScreenCanvasArtTheme.StyleSelectable(button, image, true);

        Text text = FindOrCreateText("Label", rectTransform, label, 9, FontStyle.Normal, TextAnchor.MiddleCenter, 28f);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(3f, 2f);
        textRect.offsetMax = new Vector2(-3f, -2f);
        return button;
    }

    private Toggle FindOrCreateToggle(string name, RectTransform parent, string label, bool initialValue)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Toggle));
        obj.layer = parent.gameObject.layer;
        RectTransform rectTransform = obj.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = obj.AddComponent<RectTransform>();
        }

        rectTransform.SetParent(parent, false);
        rectTransform.sizeDelta = new Vector2(68f, 28f);

        Transform backgroundExisting = rectTransform.Find("Background");
        GameObject backgroundObject = backgroundExisting != null ? backgroundExisting.gameObject : new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backgroundObject.layer = obj.layer;
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        if (backgroundRect == null)
        {
            backgroundRect = backgroundObject.AddComponent<RectTransform>();
        }

        backgroundRect.SetParent(rectTransform, false);
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(12f, 12f);
        backgroundRect.anchoredPosition = new Vector2(8f, 0f);
        if (backgroundObject.GetComponent<CanvasRenderer>() == null)
        {
            backgroundObject.AddComponent<CanvasRenderer>();
        }

        Image backgroundImage = backgroundObject.GetComponent<Image>();
        if (backgroundImage == null)
        {
            backgroundImage = backgroundObject.AddComponent<Image>();
        }
        backgroundImage.color = new Color(0.18f, 0.24f, 0.28f, 0.88f);

        Transform checkExisting = backgroundRect.Find("Checkmark");
        GameObject checkObject = checkExisting != null ? checkExisting.gameObject : new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        checkObject.layer = obj.layer;
        RectTransform checkRect = checkObject.GetComponent<RectTransform>();
        if (checkRect == null)
        {
            checkRect = checkObject.AddComponent<RectTransform>();
        }

        checkRect.SetParent(backgroundRect, false);
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.offsetMin = new Vector2(2f, 2f);
        checkRect.offsetMax = new Vector2(-2f, -2f);
        if (checkObject.GetComponent<CanvasRenderer>() == null)
        {
            checkObject.AddComponent<CanvasRenderer>();
        }

        Image checkImage = checkObject.GetComponent<Image>();
        if (checkImage == null)
        {
            checkImage = checkObject.AddComponent<Image>();
        }
        checkImage.color = new Color(0.35f, 0.82f, 0.62f, 1f);

        Text text = FindOrCreateText("Label", rectTransform, label, 9, FontStyle.Normal, TextAnchor.MiddleLeft, 28f);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(22f, 0f);
        textRect.offsetMax = Vector2.zero;

        Toggle toggle = obj.GetComponent<Toggle>();
        if (toggle == null)
        {
            toggle = obj.AddComponent<Toggle>();
        }
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkImage;
        toggle.isOn = initialValue;
        ScreenCanvasArtTheme.StyleSelectable(toggle, backgroundImage, false);
        ScreenCanvasArtTheme.StyleText(text, name, 9, FontStyle.Normal);
        return toggle;
    }

    private static WeatherLocation ParseIpLocation(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            IpLocationResponse response = JsonUtility.FromJson<IpLocationResponse>(json);
            if (response == null || !response.success)
            {
                return null;
            }

            string label = BuildLocationLabel(response.city, response.region, response.country);
            return new WeatherLocation(response.latitude, response.longitude, label);
        }
        catch
        {
            return null;
        }
    }

    private static WeatherSnapshot ParseWeather(string json, WeatherLocation location)
    {
        if (string.IsNullOrWhiteSpace(json) || location == null)
        {
            return null;
        }

        try
        {
            OpenMeteoResponse response = JsonUtility.FromJson<OpenMeteoResponse>(json);
            if (response == null || response.current == null)
            {
                return null;
            }

            OpenMeteoCurrent current = response.current;
            return new WeatherSnapshot(
                location.Label,
                current.temperature_2m,
                current.apparent_temperature,
                current.relative_humidity_2m,
                current.precipitation,
                current.wind_speed_10m,
                current.wind_direction_10m,
                WeatherCodeToText(current.weather_code));
        }
        catch
        {
            return null;
        }
    }

    private static string BuildLocationLabel(string city, string region, string country)
    {
        List<string> parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(city))
        {
            parts.Add(city);
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            parts.Add(region);
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            parts.Add(country);
        }

        return parts.Count == 0 ? "Current network location" : string.Join(", ", parts);
    }

    private static string WeatherCodeToText(int code)
    {
        switch (code)
        {
            case 0:
                return "Clear";
            case 1:
            case 2:
            case 3:
                return "Partly cloudy";
            case 45:
            case 48:
                return "Fog";
            case 51:
            case 53:
            case 55:
                return "Drizzle";
            case 56:
            case 57:
                return "Freezing drizzle";
            case 61:
            case 63:
            case 65:
                return "Rain";
            case 66:
            case 67:
                return "Freezing rain";
            case 71:
            case 73:
            case 75:
                return "Snow";
            case 77:
                return "Snow grains";
            case 80:
            case 81:
            case 82:
                return "Rain showers";
            case 85:
            case 86:
                return "Snow showers";
            case 95:
                return "Thunderstorm";
            case 96:
            case 99:
                return "Thunderstorm with hail";
            default:
                return "Weather code " + code;
        }
    }

    private static MailQueryResult QueryOutlookMail(int timeoutMilliseconds, CancellationToken token)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + EncodePowerShellCommand(QueryOutlookMailScript),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using (Process process = new Process())
        {
            process.StartInfo = startInfo;

            try
            {
                if (!process.Start())
                {
                    return MailQueryResult.Failed("Unable to start PowerShell.");
                }
            }
            catch (Exception ex)
            {
                return MailQueryResult.Failed(ex.Message);
            }

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMilliseconds) || token.IsCancellationRequested)
            {
                TryKill(process);
                return MailQueryResult.Failed("Timed out while querying Outlook mail.");
            }

            string output = ReadCompletedTask(outputTask);
            string error = ReadCompletedTask(errorTask);
            MailQueryResult result = ParseMailOutput(output);

            if (process.ExitCode != 0)
            {
                return MailQueryResult.Failed(string.IsNullOrWhiteSpace(error) ? "PowerShell mail query failed." : error.Trim());
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                return result;
            }

            return result;
        }
    }

    private static MailQueryResult ParseMailOutput(string output)
    {
        MailQueryResult result = new MailQueryResult();
        if (string.IsNullOrWhiteSpace(output))
        {
            result.ErrorMessage = "No Outlook mail data was returned. Confirm Outlook is installed and signed in.";
            return result;
        }

        string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split('\t');
            if (fields.Length == 0)
            {
                continue;
            }

            string recordType = DecodeField(fields[0]);
            if (string.Equals(recordType, "SUMMARY", StringComparison.OrdinalIgnoreCase))
            {
                if (fields.Length > 1)
                {
                    int.TryParse(DecodeField(fields[1]), NumberStyles.Integer, CultureInfo.InvariantCulture, out result.UnreadCount);
                }

                if (fields.Length > 2)
                {
                    result.AccountName = DecodeField(fields[2]);
                }
            }
            else if (string.Equals(recordType, "MAIL", StringComparison.OrdinalIgnoreCase))
            {
                if (fields.Length < 4 || result.Previews.Count >= MaxMailPreviewCount)
                {
                    continue;
                }

                result.Previews.Add(new MailPreview(
                    DecodeField(fields[1]),
                    DecodeField(fields[2]),
                    DecodeField(fields[3])));
            }
            else if (string.Equals(recordType, "ERROR", StringComparison.OrdinalIgnoreCase))
            {
                result.ErrorMessage = fields.Length > 1 ? DecodeField(fields[1]) : "Outlook mail query failed.";
            }
        }

        return result;
    }

    private static string EncodePowerShellCommand(string command)
    {
        return Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
    }

    private static string DecodeField(string encodedValue)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(encodedValue);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadCompletedTask(Task<string> task)
    {
        try
        {
            return task.Wait(1000) ? task.Result : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch
        {
            // Process may already be gone.
        }
    }

    private static bool IsWindowsRuntime()
    {
        return Application.platform == RuntimePlatform.WindowsPlayer
            || Application.platform == RuntimePlatform.WindowsEditor;
    }

#pragma warning disable 0649
    [Serializable]
    private sealed class IpLocationResponse
    {
        public double latitude;
        public double longitude;
        public string city;
        public string region;
        public string country;
        public bool success;
    }

    [Serializable]
    private sealed class OpenMeteoResponse
    {
        public OpenMeteoCurrent current;
    }

    [Serializable]
    private sealed class OpenMeteoCurrent
    {
        public float temperature_2m;
        public float relative_humidity_2m;
        public float apparent_temperature;
        public float precipitation;
        public int weather_code;
        public float wind_speed_10m;
        public float wind_direction_10m;
    }
#pragma warning restore 0649

    private sealed class WeatherLocation
    {
        public readonly double Latitude;
        public readonly double Longitude;
        public readonly string Label;

        public WeatherLocation(double latitude, double longitude, string label)
        {
            Latitude = latitude;
            Longitude = longitude;
            Label = string.IsNullOrWhiteSpace(label) ? "Weather Location" : label;
        }
    }

    private sealed class WeatherSnapshot
    {
        public readonly string LocationLabel;
        public readonly float TemperatureCelsius;
        public readonly float ApparentTemperatureCelsius;
        public readonly float RelativeHumidity;
        public readonly float PrecipitationMm;
        public readonly float WindSpeedKmh;
        public readonly float WindDirectionDegrees;
        public readonly string Condition;

        public WeatherSnapshot(
            string locationLabel,
            float temperatureCelsius,
            float apparentTemperatureCelsius,
            float relativeHumidity,
            float precipitationMm,
            float windSpeedKmh,
            float windDirectionDegrees,
            string condition)
        {
            LocationLabel = string.IsNullOrWhiteSpace(locationLabel) ? "Weather Location" : locationLabel;
            TemperatureCelsius = temperatureCelsius;
            ApparentTemperatureCelsius = apparentTemperatureCelsius;
            RelativeHumidity = relativeHumidity;
            PrecipitationMm = precipitationMm;
            WindSpeedKmh = windSpeedKmh;
            WindDirectionDegrees = windDirectionDegrees;
            Condition = string.IsNullOrWhiteSpace(condition) ? "Unknown" : condition;
        }
    }

    private sealed class MailQueryResult
    {
        public readonly List<MailPreview> Previews = new List<MailPreview>();
        public int UnreadCount;
        public string AccountName = string.Empty;
        public string ErrorMessage;

        public static MailQueryResult Failed(string errorMessage)
        {
            return new MailQueryResult { ErrorMessage = errorMessage };
        }
    }

    private sealed class MailPreview
    {
        public readonly string Sender;
        public readonly string Subject;
        public readonly string ReceivedAtLabel;

        public MailPreview(string sender, string subject, string receivedAtLabel)
        {
            Sender = string.IsNullOrWhiteSpace(sender) ? "Unknown Sender" : sender;
            Subject = string.IsNullOrWhiteSpace(subject) ? "(No subject)" : subject;
            ReceivedAtLabel = string.IsNullOrWhiteSpace(receivedAtLabel) ? "Unknown time" : receivedAtLabel;
        }
    }
}
