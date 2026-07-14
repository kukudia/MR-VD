using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Presents local time, weather, and system status inside Screen/Canvas.
/// </summary>
public sealed class RuntimeInformationPanel : MonoBehaviour
{
    private const float MinimumWeatherRefreshIntervalSeconds = 60f;
    private const int DefaultRequestTimeoutSeconds = 12;
    private const string IpLocationEndpoint = "https://ipwho.is/";
    private const string OpenMeteoEndpointFormat = "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m,wind_direction_10m,is_day&timezone=auto";

    [Header("Panel")]
    public bool showPanel = true;
    public KeyCode togglePanelKey = KeyCode.F9;

    [Header("Screen Canvas Panel")]
    public bool useScreenCanvasPanel = true;
    public RectTransform screenCanvasPanelRoot;

    [Range(0.05f, 1f)]
    public float screenCanvasRefreshInterval = 0.2f;

    [Range(0.05f, 0.6f)]
    public float transitionDuration = 0.22f;

    [Header("Modules")]
    public bool showAudioModule = true;
    public bool showWeatherModule = true;
    public bool showSystemModule = true;
    public bool weatherModuleExpanded = true;
    public bool systemModuleExpanded = true;

    [Header("Weather")]
    public bool autoRefreshWeather = true;
    public bool useIpLocationForWeather = true;
    public bool useManualWeatherLocation;
    public double manualLatitude;
    public double manualLongitude;
    public string manualWeatherLocationLabel = "Manual Location";
    public float weatherRefreshIntervalSeconds = 900f;

    [Tooltip("Weather artwork 01-21 from Assets/GIF, in numeric filename order.")]
    public Texture2D[] weatherVisuals = new Texture2D[21];

    private static RuntimeInformationPanel instance;

    private RectTransform dashboard;
    private Text timeText;
    private Text dateText;
    private Text timeZoneText;
    private Text weatherTemperatureText;
    private Text weatherConditionText;
    private Text weatherDetailsText;
    private Text weatherStatusText;
    private Text systemText;
    private RawImage weatherArtwork;
    private Button settingsButton;
    private Button weatherRefreshButton;
    private Button weatherExpandButton;
    private Button systemExpandButton;
    private Toggle audioModuleToggle;
    private Toggle weatherModuleToggle;
    private Toggle systemModuleToggle;
    private Toggle weatherAutoToggle;
    private ScreenCanvasModuleAnimator settingsAnimator;
    private ScreenCanvasModuleAnimator weatherAnimator;
    private ScreenCanvasModuleAnimator systemAnimator;
    private ScreenCanvasPanelAnimator dashboardAnimator;
    private ScreenCanvasPanelAnimator audioPanelAnimator;

    private bool settingsExpanded;
    private bool uiWired;
    private bool weatherRefreshInProgress;
    private DateTime lastWeatherRefreshUtc;
    private WeatherSnapshot weatherSnapshot;
    private string weatherError;
    private float nextScreenCanvasRefreshTime;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        EnsureScreenCanvasPanel();
        RequestWeatherRefresh(true);
    }

    private void Update()
    {
        if (togglePanelKey != KeyCode.None && Input.GetKeyDown(togglePanelKey))
        {
            showPanel = !showPanel;
            ApplyDashboardVisibility(false);
        }

        if (autoRefreshWeather
            && (DateTime.UtcNow - lastWeatherRefreshUtc).TotalSeconds >= Mathf.Max(MinimumWeatherRefreshIntervalSeconds, weatherRefreshIntervalSeconds))
        {
            RequestWeatherRefresh(false);
        }

        UpdateScreenCanvasPanel(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
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

    private IEnumerator RefreshWeather()
    {
        weatherRefreshInProgress = true;
        weatherError = null;
        UpdateWeatherPresentation();

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
                    weatherError = "Location unavailable: " + request.error;
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
                ? "Enable IP location or provide manual coordinates."
                : weatherError;
            FinishWeatherRefresh();
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
                }
                else
                {
                    weatherError = "Weather response could not be parsed.";
                }
            }
            else
            {
                weatherError = "Weather unavailable: " + request.error;
            }
        }

        FinishWeatherRefresh();
    }

    private void FinishWeatherRefresh()
    {
        lastWeatherRefreshUtc = DateTime.UtcNow;
        weatherRefreshInProgress = false;
        UpdateWeatherPresentation();
    }

    private void OnGUI()
    {
        if (useScreenCanvasPanel || !showPanel)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(16f, 16f, 420f, 300f), GUI.skin.box);
        DateTime now = DateTime.Now;
        GUILayout.Label(now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        GUILayout.Label(now.ToString("yyyy-MM-dd dddd", CultureInfo.CurrentCulture));
        GUILayout.Space(8f);
        GUILayout.Label(BuildWeatherFallbackText());
        GUILayout.Space(8f);
        GUILayout.Label(BuildSystemText());
        GUILayout.EndArea();
    }

    private void UpdateScreenCanvasPanel(bool force)
    {
        if (!useScreenCanvasPanel || !EnsureScreenCanvasPanel())
        {
            return;
        }

        if (!force && Time.unscaledTime < nextScreenCanvasRefreshTime)
        {
            return;
        }

        nextScreenCanvasRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, screenCanvasRefreshInterval);
        DateTime now = DateTime.Now;
        timeText.text = now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        dateText.text = now.ToString("dddd, dd MMMM yyyy", CultureInfo.CurrentCulture);
        timeZoneText.text = TimeZoneInfo.Local.StandardName;
        UpdateWeatherPresentation();
        systemText.text = BuildSystemText();
    }

    private bool EnsureScreenCanvasPanel()
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

        if (dashboard == null)
        {
            dashboard = FindRect(screenCanvasPanelRoot, "RuntimeDashboard");
        }

        if (dashboard == null)
        {
            return false;
        }

        if (!uiWired)
        {
            uiWired = BindDashboard();
        }

        return uiWired;
    }

    private bool BindDashboard()
    {
        timeText = Find<Text>(dashboard, "TimeCard/TimeText");
        dateText = Find<Text>(dashboard, "TimeCard/DateText");
        timeZoneText = Find<Text>(dashboard, "TimeCard/TimeZoneText");
        weatherTemperatureText = Find<Text>(dashboard, "WeatherModule/WeatherBody/WeatherSummary/WeatherCopy/WeatherTemperatureText");
        weatherConditionText = Find<Text>(dashboard, "WeatherModule/WeatherBody/WeatherSummary/WeatherCopy/WeatherConditionText");
        weatherDetailsText = Find<Text>(dashboard, "WeatherModule/WeatherBody/WeatherDetailsText");
        weatherStatusText = Find<Text>(dashboard, "WeatherModule/WeatherBody/WeatherStatusText");
        weatherArtwork = Find<RawImage>(dashboard, "WeatherModule/WeatherBody/WeatherSummary/WeatherArtwork");
        systemText = Find<Text>(dashboard, "SystemModule/SystemBody/SystemText");
        settingsButton = Find<Button>(dashboard, "DashboardHeader/SettingsButton");
        weatherRefreshButton = Find<Button>(dashboard, "WeatherModule/WeatherHeader/WeatherRefreshButton");
        weatherExpandButton = Find<Button>(dashboard, "WeatherModule/WeatherHeader/WeatherExpandButton");
        systemExpandButton = Find<Button>(dashboard, "SystemModule/SystemHeader/SystemExpandButton");
        audioModuleToggle = Find<Toggle>(dashboard, "SettingsModule/SettingsBody/SettingsRowOne/AudioModuleToggle");
        weatherModuleToggle = Find<Toggle>(dashboard, "SettingsModule/SettingsBody/SettingsRowOne/WeatherModuleToggle");
        systemModuleToggle = Find<Toggle>(dashboard, "SettingsModule/SettingsBody/SettingsRowTwo/SystemModuleToggle");
        weatherAutoToggle = Find<Toggle>(dashboard, "SettingsModule/SettingsBody/SettingsRowTwo/WeatherAutoToggle");
        settingsAnimator = Find<ScreenCanvasModuleAnimator>(dashboard, "SettingsModule");
        weatherAnimator = Find<ScreenCanvasModuleAnimator>(dashboard, "WeatherModule");
        systemAnimator = Find<ScreenCanvasModuleAnimator>(dashboard, "SystemModule");
        dashboardAnimator = dashboard.GetComponent<ScreenCanvasPanelAnimator>();

        RectTransform audioPanel = FindSiblingPanel("AudioPanel/AudioCaptureCanvasContent");
        if (audioPanel != null)
        {
            audioPanelAnimator = audioPanel.GetComponent<ScreenCanvasPanelAnimator>();
        }

        bool complete = timeText != null
            && dateText != null
            && timeZoneText != null
            && weatherTemperatureText != null
            && weatherConditionText != null
            && weatherDetailsText != null
            && weatherStatusText != null
            && weatherArtwork != null
            && systemText != null
            && settingsButton != null
            && weatherRefreshButton != null
            && weatherExpandButton != null
            && systemExpandButton != null
            && audioModuleToggle != null
            && weatherModuleToggle != null
            && systemModuleToggle != null
            && weatherAutoToggle != null
            && settingsAnimator != null
            && weatherAnimator != null
            && systemAnimator != null;

        if (!complete)
        {
            Debug.LogError("[RuntimeInformationPanel] RuntimeDashboard is incomplete. Rebuild it from Tools/MR-VD/Rebuild Screen Canvas Panels.");
            return false;
        }

        settingsButton.onClick.RemoveAllListeners();
        settingsButton.onClick.AddListener(() => SetSettingsExpanded(!settingsExpanded, false));
        weatherRefreshButton.onClick.RemoveAllListeners();
        weatherRefreshButton.onClick.AddListener(() => RequestWeatherRefresh(true));
        weatherExpandButton.onClick.RemoveAllListeners();
        weatherExpandButton.onClick.AddListener(() => SetWeatherExpanded(!weatherModuleExpanded, false));
        systemExpandButton.onClick.RemoveAllListeners();
        systemExpandButton.onClick.AddListener(() => SetSystemExpanded(!systemModuleExpanded, false));

        ConfigureToggle(audioModuleToggle, showAudioModule, SetAudioModuleVisible);
        ConfigureToggle(weatherModuleToggle, showWeatherModule, SetWeatherModuleVisible);
        ConfigureToggle(systemModuleToggle, showSystemModule, SetSystemModuleVisible);
        ConfigureToggle(weatherAutoToggle, autoRefreshWeather, value => autoRefreshWeather = value);

        SetSettingsExpanded(false, true);
        SetWeatherModuleVisible(showWeatherModule, true);
        SetSystemModuleVisible(showSystemModule, true);
        SetAudioModuleVisible(showAudioModule, true);
        ApplyDashboardVisibility(true);
        return true;
    }

    private void ConfigureToggle(Toggle toggle, bool value, UnityEngine.Events.UnityAction<bool> listener)
    {
        toggle.onValueChanged.RemoveAllListeners();
        toggle.SetIsOnWithoutNotify(value);
        toggle.onValueChanged.AddListener(listener);
    }

    private void SetSettingsExpanded(bool expanded, bool immediate)
    {
        settingsExpanded = expanded;
        settingsAnimator.SetState(true, settingsExpanded, immediate, transitionDuration);
        SetButtonLabel(settingsButton, settingsExpanded ? "DONE" : "SETTINGS");
    }

    private void SetAudioModuleVisible(bool visible)
    {
        SetAudioModuleVisible(visible, false);
    }

    private void SetAudioModuleVisible(bool visible, bool immediate)
    {
        showAudioModule = visible;
        if (audioPanelAnimator != null)
        {
            audioPanelAnimator.SetVisible(visible, immediate, transitionDuration);
        }
    }

    private void SetWeatherModuleVisible(bool visible)
    {
        SetWeatherModuleVisible(visible, false);
    }

    private void SetWeatherModuleVisible(bool visible, bool immediate)
    {
        showWeatherModule = visible;
        weatherAnimator.SetState(visible, weatherModuleExpanded, immediate, transitionDuration);
    }

    private void SetSystemModuleVisible(bool visible)
    {
        SetSystemModuleVisible(visible, false);
    }

    private void SetSystemModuleVisible(bool visible, bool immediate)
    {
        showSystemModule = visible;
        systemAnimator.SetState(visible, systemModuleExpanded, immediate, transitionDuration);
    }

    private void SetWeatherExpanded(bool expanded, bool immediate)
    {
        weatherModuleExpanded = expanded;
        weatherAnimator.SetState(showWeatherModule, expanded, immediate, transitionDuration);
        SetButtonLabel(weatherExpandButton, expanded ? "HIDE" : "SHOW");
    }

    private void SetSystemExpanded(bool expanded, bool immediate)
    {
        systemModuleExpanded = expanded;
        systemAnimator.SetState(showSystemModule, expanded, immediate, transitionDuration);
        SetButtonLabel(systemExpandButton, expanded ? "HIDE" : "SHOW");
    }

    private void ApplyDashboardVisibility(bool immediate)
    {
        if (dashboardAnimator != null)
        {
            dashboardAnimator.SetVisible(showPanel, immediate, transitionDuration);
        }
        else if (dashboard != null)
        {
            dashboard.gameObject.SetActive(showPanel);
        }
    }

    private void UpdateWeatherPresentation()
    {
        if (weatherTemperatureText == null)
        {
            return;
        }

        weatherRefreshButton.interactable = !weatherRefreshInProgress;
        if (weatherSnapshot == null)
        {
            weatherTemperatureText.text = "-- C";
            weatherConditionText.text = "Weather unavailable";
            weatherDetailsText.text = "Waiting for location and current conditions.";
            weatherArtwork.texture = null;
            weatherArtwork.enabled = false;
        }
        else
        {
            weatherTemperatureText.text = string.Format(CultureInfo.CurrentCulture, "{0:0.#} C", weatherSnapshot.TemperatureCelsius);
            weatherConditionText.text = weatherSnapshot.Condition + "  |  " + weatherSnapshot.LocationLabel;
            weatherDetailsText.text = string.Format(
                CultureInfo.CurrentCulture,
                "Feels {0:0.#} C    Humidity {1:0}%\nWind {2:0.#} km/h    Rain {3:0.#} mm",
                weatherSnapshot.ApparentTemperatureCelsius,
                weatherSnapshot.RelativeHumidity,
                weatherSnapshot.WindSpeedKmh,
                weatherSnapshot.PrecipitationMm);

            Texture2D texture = ResolveWeatherVisual(weatherSnapshot);
            weatherArtwork.texture = texture;
            weatherArtwork.enabled = texture != null;
        }

        if (weatherRefreshInProgress)
        {
            weatherStatusText.text = "UPDATING CURRENT CONDITIONS";
        }
        else if (!string.IsNullOrWhiteSpace(weatherError))
        {
            weatherStatusText.text = weatherError;
        }
        else if (lastWeatherRefreshUtc != default(DateTime))
        {
            weatherStatusText.text = "UPDATED " + lastWeatherRefreshUtc.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);
        }
        else
        {
            weatherStatusText.text = "NOT YET UPDATED";
        }
    }

    private Texture2D ResolveWeatherVisual(WeatherSnapshot snapshot)
    {
        int visualNumber = WeatherCodeToVisualNumber(snapshot.WeatherCode, snapshot.IsDay, snapshot.WindSpeedKmh);
        int index = visualNumber - 1;
        return weatherVisuals != null && index >= 0 && index < weatherVisuals.Length
            ? weatherVisuals[index]
            : null;
    }

    private static int WeatherCodeToVisualNumber(int code, bool isDay, float windSpeedKmh)
    {
        if (windSpeedKmh >= 45f && code < 51)
        {
            return 18;
        }

        if (code == 0)
        {
            return isDay ? 1 : 11;
        }

        if (code == 1)
        {
            return isDay ? 3 : 12;
        }

        if (code == 2)
        {
            return isDay ? 2 : 12;
        }

        if (code == 3)
        {
            return isDay ? 4 : 13;
        }

        if (code == 45 || code == 48)
        {
            return 21;
        }

        if (code >= 51 && code <= 57)
        {
            return isDay ? 5 : 14;
        }

        if ((code >= 61 && code <= 67) || (code >= 80 && code <= 82))
        {
            return isDay ? 6 : 15;
        }

        if ((code >= 71 && code <= 77) || (code >= 85 && code <= 86))
        {
            return isDay ? 7 : 16;
        }

        if (code >= 95)
        {
            return 17;
        }

        return isDay ? 2 : 12;
    }

    private string BuildWeatherFallbackText()
    {
        if (weatherSnapshot == null)
        {
            return string.IsNullOrWhiteSpace(weatherError) ? "Weather unavailable." : weatherError;
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            "{0}: {1:0.#} C, {2}",
            weatherSnapshot.LocationLabel,
            weatherSnapshot.TemperatureCelsius,
            weatherSnapshot.Condition);
    }

    private static string BuildSystemText()
    {
        string reachability = Application.internetReachability == NetworkReachability.NotReachable
            ? "Offline"
            : "Online";
        return "DEVICE  " + Environment.MachineName
            + "\nPLATFORM  " + Application.platform
            + "\nNETWORK  " + reachability;
    }

    private RectTransform FindSiblingPanel(string relativePath)
    {
        if (screenCanvasPanelRoot == null || screenCanvasPanelRoot.parent == null)
        {
            return null;
        }

        Transform target = screenCanvasPanelRoot.parent.Find(relativePath);
        return target as RectTransform;
    }

    private static RectTransform FindRect(RectTransform root, string path)
    {
        Transform target = root.Find(path);
        return target as RectTransform;
    }

    private static T Find<T>(RectTransform root, string path) where T : Component
    {
        Transform target = root.Find(path);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = label;
        }
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

            return new WeatherLocation(
                response.latitude,
                response.longitude,
                BuildLocationLabel(response.city, response.region, response.country));
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
                current.weather_code,
                current.is_day != 0,
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

        return parts.Count == 0 ? "Current location" : string.Join(", ", parts);
    }

    private static string WeatherCodeToText(int code)
    {
        switch (code)
        {
            case 0:
                return "Clear";
            case 1:
                return "Mostly clear";
            case 2:
                return "Partly cloudy";
            case 3:
                return "Overcast";
            case 45:
            case 48:
                return "Fog";
            case 51:
            case 53:
            case 55:
            case 56:
            case 57:
                return "Drizzle";
            case 61:
            case 63:
            case 65:
            case 66:
            case 67:
                return "Rain";
            case 71:
            case 73:
            case 75:
            case 77:
                return "Snow";
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
                return "Current conditions";
        }
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
        public int is_day;
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
            Label = string.IsNullOrWhiteSpace(label) ? "Weather location" : label;
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
        public readonly int WeatherCode;
        public readonly bool IsDay;
        public readonly string Condition;

        public WeatherSnapshot(
            string locationLabel,
            float temperatureCelsius,
            float apparentTemperatureCelsius,
            float relativeHumidity,
            float precipitationMm,
            float windSpeedKmh,
            float windDirectionDegrees,
            int weatherCode,
            bool isDay,
            string condition)
        {
            LocationLabel = string.IsNullOrWhiteSpace(locationLabel) ? "Weather location" : locationLabel;
            TemperatureCelsius = temperatureCelsius;
            ApparentTemperatureCelsius = apparentTemperatureCelsius;
            RelativeHumidity = relativeHumidity;
            PrecipitationMm = precipitationMm;
            WindSpeedKmh = windSpeedKmh;
            WindDirectionDegrees = windDirectionDegrees;
            WeatherCode = weatherCode;
            IsDay = isDay;
            Condition = string.IsNullOrWhiteSpace(condition) ? "Unknown" : condition;
        }
    }
}
