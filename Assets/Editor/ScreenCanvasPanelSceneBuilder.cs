using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public static class ScreenCanvasPanelSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/v203.0.0.unity";
    private const string RunOnceMarkerPath = "Temp/rebuild-screen-canvas-panels.once";
    private const string AudioPanelPath = "Screen/Canvas/AudioPanel";
    private const string InfoPanelPath = "Screen/Canvas/InfoPanel";
    private const float AudioContentWidth = 270f;
    private const float AudioContentHeight = 560f;
    private const float AudioChildWidth = 250f;

    [InitializeOnLoadMethod]
    private static void BuildIfRequested()
    {
        if (!File.Exists(RunOnceMarkerPath))
        {
            return;
        }

        File.Delete(RunOnceMarkerPath);
        EditorApplication.delayCall += Build;
    }

    [MenuItem("Tools/MR-VD/Rebuild Screen Canvas Panels")]
    public static void Build()
    {
        EditorSceneManager.OpenScene(ScenePath);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform audioPanel = RequireRectTransform(AudioPanelPath);
        RectTransform infoPanel = RequireRectTransform(InfoPanelPath);

        MakePanelTransparent(audioPanel);
        MakePanelTransparent(infoPanel);

        ClearChildren(audioPanel, "AudioCaptureCSCore");
        ClearChildren(infoPanel);

        BuildAudioPanel(audioPanel, font);
        BuildInfoPanel(infoPanel, font);
        WireSceneComponents(audioPanel, infoPanel);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[ScreenCanvasPanelSceneBuilder] Rebuilt minimalist black-gray AudioPanel and InfoPanel under Screen/Canvas in " + ScenePath);
    }

    [MenuItem("Tools/MR-VD/Validate Screen Canvas Dashboard")]
    public static void Validate()
    {
        EditorSceneManager.OpenScene(ScenePath);

        RectTransform audioPanel = RequireRectTransform(AudioPanelPath);
        RectTransform infoPanel = RequireRectTransform(InfoPanelPath);
        RectTransform dashboard = RequireChild(infoPanel, "RuntimeDashboard");
        RectTransform audioContent = RequireChild(audioPanel, "AudioCaptureCanvasContent");
        string[] requiredPaths =
        {
            "DashboardHeader/SettingsButton",
            "TimeCard/TimeText",
            "TimeCard/DateText",
            "SettingsModule/SettingsBody/SettingsRowOne/AudioModuleToggle",
            "SettingsModule/SettingsBody/SettingsRowOne/WeatherModuleToggle",
            "SettingsModule/SettingsBody/SettingsRowTwo/SystemModuleToggle",
            "SettingsModule/SettingsBody/SettingsRowTwo/WeatherAutoToggle",
            "WeatherModule/WeatherHeader/WeatherRefreshButton",
            "WeatherModule/WeatherBody/WeatherSummary/WeatherArtwork",
            "SystemModule/SystemBody/SystemText"
        };

        for (int i = 0; i < requiredPaths.Length; i++)
        {
            RequireChild(dashboard, requiredPaths[i]);
        }

        RuntimeInformationPanel runtimeInformation = infoPanel.GetComponent<RuntimeInformationPanel>();
        if (runtimeInformation == null)
        {
            throw new System.InvalidOperationException("RuntimeInformationPanel is missing from " + InfoPanelPath);
        }

        if (runtimeInformation.weatherVisuals == null || runtimeInformation.weatherVisuals.Length != 21)
        {
            throw new System.InvalidOperationException("RuntimeInformationPanel must reference all 21 weather visuals.");
        }

        for (int i = 0; i < runtimeInformation.weatherVisuals.Length; i++)
        {
            if (runtimeInformation.weatherVisuals[i] == null)
            {
                throw new System.InvalidOperationException("Weather visual " + (i + 1) + " is missing.");
            }
        }

        foreach (Transform child in infoPanel.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.IndexOf("Mail", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new System.InvalidOperationException("Mail UI remains under InfoPanel: " + child.name);
            }
        }

        const float maximumLayoutHeight = 512f;
        if (maximumLayoutHeight > dashboard.rect.height)
        {
            throw new System.InvalidOperationException("Expanded dashboard layout exceeds its height.");
        }

        ValidateTransparentPanel(audioPanel);
        ValidateTransparentPanel(infoPanel);
        ValidateThemedPanel(audioContent);
        ValidateThemedPanel(dashboard);
        ValidateThemedPanel(RequireChild(audioContent, "DeviceList"));
        ValidateThemedPanel(RequireChild(dashboard, "TimeCard"));
        ValidateThemedPanel(RequireChild(dashboard, "SettingsModule"));
        ValidateThemedPanel(RequireChild(dashboard, "WeatherModule"));
        ValidateThemedPanel(RequireChild(dashboard, "SystemModule"));

        foreach (Transform child in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (child.name == "ArtScanline")
            {
                throw new System.InvalidOperationException("ArtScanline remains in the scene under " + child.parent.name + ".");
            }
        }

        ScreenCanvasPanelAnimator audioAnimator = audioContent.GetComponent<ScreenCanvasPanelAnimator>();
        if (audioAnimator == null || (audioAnimator.VisibleScale - audioContent.localScale).sqrMagnitude > 0.000001f)
        {
            throw new System.InvalidOperationException("Audio panel animator does not preserve its authored scale.");
        }

        Debug.Log("[ScreenCanvasPanelSceneBuilder] Validation passed: stable audio scale, black-gray panels, no scanline, 21 weather visuals, no mail UI, and layout budget " + maximumLayoutHeight + "/" + dashboard.rect.height + ".");
    }

    private static void WireSceneComponents(RectTransform audioPanel, RectTransform infoPanel)
    {
        AudioCaptureCSCore audioCapture = Object.FindFirstObjectByType<AudioCaptureCSCore>();
        if (audioCapture == null)
        {
            Transform audioHost = audioPanel.Find("AudioCaptureCSCore");
            if (audioHost == null)
            {
                audioHost = new GameObject("AudioCaptureCSCore").transform;
                audioHost.SetParent(audioPanel, false);
            }

            audioCapture = audioHost.gameObject.AddComponent<AudioCaptureCSCore>();
        }
        else
        {
            audioCapture.transform.SetParent(audioPanel, false);
            audioCapture.gameObject.name = "AudioCaptureCSCore";
        }

        audioCapture.useScreenCanvasPanel = true;
        audioCapture.screenCanvasPanelRoot = audioPanel;
        audioCapture.showManualControlPanel = true;

        RuntimeInformationPanel runtimeInformation = infoPanel.GetComponent<RuntimeInformationPanel>();
        if (runtimeInformation == null)
        {
            runtimeInformation = infoPanel.gameObject.AddComponent<RuntimeInformationPanel>();
        }

        runtimeInformation.useScreenCanvasPanel = true;
        runtimeInformation.screenCanvasPanelRoot = infoPanel;
        runtimeInformation.showPanel = true;
        runtimeInformation.weatherVisuals = LoadWeatherVisuals();
    }

    private static void BuildAudioPanel(RectTransform panel, Font font)
    {
        RectTransform content = CreateRect("AudioCaptureCanvasContent", panel, new Vector2(AudioContentWidth, AudioContentHeight));
        content.localScale = Vector3.one * 0.1f;
        AddLayoutElement(content.gameObject, AudioContentHeight, AudioContentHeight, 0f, AudioContentWidth, AudioContentWidth);
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ScreenCanvasArtTheme.ApplyPanelArt(
            content,
            ScreenCanvasArtTheme.AudioPanelBase,
            ScreenCanvasArtTheme.AudioPrimaryAccent);

        CanvasGroup audioGroup = content.gameObject.AddComponent<CanvasGroup>();
        ScreenCanvasPanelAnimator audioAnimator = content.gameObject.AddComponent<ScreenCanvasPanelAnimator>();
        audioAnimator.Configure(content, audioGroup);

        CreateText("ModeText", content, "Mode: Loopback", font, 13, FontStyle.Bold, TextAnchor.MiddleLeft, 24f);
        CreateText("DeviceText", content, "Device: None", font, 11, FontStyle.Normal, TextAnchor.UpperLeft, 42f);

        RectTransform modeButtons = CreateRow("ModeButtons", content, 30f);
        CreateButton("InputButton", modeButtons, "Input", font, 80f, 28f, 10);
        CreateButton("LoopbackButton", modeButtons, "Loopback", font, 80f, 28f, 10);

        RectTransform actionButtons = CreateRow("ActionButtons", content, 30f);
        CreateButton("RefreshButton", actionButtons, "Refresh", font, 80f, 28f, 10);
        CreateButton("PreviousButton", actionButtons, "Prev", font, 80f, 28f, 10);
        CreateButton("NextButton", actionButtons, "Next", font, 80f, 28f, 10);

        CreateText("DeviceHeaderText", content, "Loopback Devices", font, 12, FontStyle.Bold, TextAnchor.MiddleLeft, 22f);

        CreateDeviceList(content, font);

        CreateText("VisualizerText", content, "Audio Visualizer\nAudioVisualizer not found", font, 10, FontStyle.Normal, TextAnchor.UpperLeft, 185f);
    }

    private static RectTransform CreateDeviceList(RectTransform parent, Font font)
    {
        RectTransform deviceList = CreateRect("DeviceList", parent, new Vector2(AudioChildWidth, 150f));

        AddLayoutElement(deviceList.gameObject, 120f, 150f, 0f, AudioChildWidth, AudioChildWidth);

        Image background = deviceList.gameObject.AddComponent<Image>();
        background.color = new Color(0.05f, 0.07f, 0.08f, 0.45f);
        background.raycastTarget = true;

        ScreenCanvasArtTheme.ApplyPanelArt(
            deviceList,
            ScreenCanvasArtTheme.DeviceListBase,
            ScreenCanvasArtTheme.AudioSecondaryAccent);

        VerticalLayoutGroup deviceLayout = deviceList.gameObject.AddComponent<VerticalLayoutGroup>();
        deviceLayout.spacing = 3f;
        deviceLayout.childAlignment = TextAnchor.UpperLeft;
        deviceLayout.childControlWidth = true;
        deviceLayout.childControlHeight = true;
        deviceLayout.childForceExpandWidth = true;
        deviceLayout.childForceExpandHeight = false;

        CreateText("NoDevicesText", deviceList, "No active devices", font, 10, FontStyle.Italic, TextAnchor.MiddleLeft, 24f);
        return deviceList;
    }

    private static void BuildInfoPanel(RectTransform panel, Font font)
    {
        const float dashboardWidth = 286f;
        const float contentWidth = 268f;

        RectTransform dashboard = CreateRect("RuntimeDashboard", panel, new Vector2(dashboardWidth, 520f));
        dashboard.localScale = Vector3.one * 0.1f;
        VerticalLayoutGroup layout = dashboard.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(9, 9, 9, 9);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ScreenCanvasArtTheme.ApplyPanelArt(
            dashboard,
            ScreenCanvasArtTheme.AudioPanelBase,
            ScreenCanvasArtTheme.AudioPrimaryAccent);

        CanvasGroup dashboardGroup = dashboard.gameObject.AddComponent<CanvasGroup>();
        ScreenCanvasPanelAnimator dashboardAnimator = dashboard.gameObject.AddComponent<ScreenCanvasPanelAnimator>();
        dashboardAnimator.Configure(dashboard, dashboardGroup);

        RectTransform header = CreateRow("DashboardHeader", dashboard, 28f, contentWidth);
        Text headerText = CreateText("HeaderText", header, "RUNTIME OVERVIEW", font, 11, FontStyle.Bold, TextAnchor.MiddleLeft, 28f, 174f);
        headerText.color = ScreenCanvasArtTheme.MutedText;
        CreateButton("SettingsButton", header, "SETTINGS", font, 86f, 26f, 8);

        RectTransform timeCard = CreateCard("TimeCard", dashboard, contentWidth, 108f, ScreenCanvasArtTheme.CardRaised);
        VerticalLayoutGroup timeLayout = timeCard.gameObject.AddComponent<VerticalLayoutGroup>();
        timeLayout.padding = new RectOffset(10, 10, 8, 8);
        timeLayout.spacing = 0f;
        timeLayout.childAlignment = TextAnchor.UpperLeft;
        timeLayout.childControlWidth = true;
        timeLayout.childControlHeight = true;
        timeLayout.childForceExpandWidth = true;
        timeLayout.childForceExpandHeight = false;

        Text timeEyebrow = CreateText("TimeEyebrowText", timeCard, "LOCAL TIME", font, 8, FontStyle.Bold, TextAnchor.MiddleLeft, 14f, contentWidth - 20f);
        timeEyebrow.color = ScreenCanvasArtTheme.AudioPrimaryAccent;
        Text timeText = CreateText("TimeText", timeCard, "00:00:00", font, 32, FontStyle.Bold, TextAnchor.MiddleLeft, 42f, contentWidth - 20f);
        timeText.color = Color.white;
        Text dateText = CreateText("DateText", timeCard, "Monday, 01 January 2026", font, 11, FontStyle.Normal, TextAnchor.MiddleLeft, 20f, contentWidth - 20f);
        dateText.color = new Color(0.93f, 0.95f, 0.96f, 1f);
        Text zoneText = CreateText("TimeZoneText", timeCard, "Local time zone", font, 8, FontStyle.Normal, TextAnchor.MiddleLeft, 14f, contentWidth - 20f);
        zoneText.color = ScreenCanvasArtTheme.MutedText;

        RectTransform settingsModule = CreateCard("SettingsModule", dashboard, contentWidth, 0f, ScreenCanvasArtTheme.CardBase);
        CanvasGroup settingsGroup = settingsModule.gameObject.AddComponent<CanvasGroup>();
        RectTransform settingsBody = CreateVerticalBody("SettingsBody", settingsModule, contentWidth - 12f, 62f, 4f, new RectOffset(6, 6, 4, 4));
        CanvasGroup settingsBodyGroup = settingsBody.gameObject.AddComponent<CanvasGroup>();
        RectTransform settingsRowOne = CreateRow("SettingsRowOne", settingsBody, 25f, contentWidth - 24f);
        CreateToggle("AudioModuleToggle", settingsRowOne, "Audio", font, true, 116f);
        CreateToggle("WeatherModuleToggle", settingsRowOne, "Weather", font, true, 116f);
        RectTransform settingsRowTwo = CreateRow("SettingsRowTwo", settingsBody, 25f, contentWidth - 24f);
        CreateToggle("SystemModuleToggle", settingsRowTwo, "System", font, true, 116f);
        CreateToggle("WeatherAutoToggle", settingsRowTwo, "Auto update", font, true, 116f);
        ScreenCanvasModuleAnimator settingsAnimator = settingsModule.gameObject.AddComponent<ScreenCanvasModuleAnimator>();
        settingsAnimator.Configure(settingsModule.GetComponent<LayoutElement>(), settingsGroup, settingsBodyGroup, 0f, 70f, true, false);

        RectTransform weatherModule = CreateCard("WeatherModule", dashboard, contentWidth, 176f, ScreenCanvasArtTheme.CardBase);
        VerticalLayoutGroup weatherLayout = weatherModule.gameObject.AddComponent<VerticalLayoutGroup>();
        weatherLayout.padding = new RectOffset(6, 6, 5, 5);
        weatherLayout.spacing = 4f;
        weatherLayout.childAlignment = TextAnchor.UpperLeft;
        weatherLayout.childControlWidth = true;
        weatherLayout.childControlHeight = true;
        weatherLayout.childForceExpandWidth = true;
        weatherLayout.childForceExpandHeight = false;
        CanvasGroup weatherGroup = weatherModule.gameObject.AddComponent<CanvasGroup>();

        RectTransform weatherHeader = CreateRow("WeatherHeader", weatherModule, 30f, contentWidth - 12f);
        Text weatherLabel = CreateText("WeatherLabel", weatherHeader, "WEATHER", font, 11, FontStyle.Bold, TextAnchor.MiddleLeft, 28f, 108f);
        weatherLabel.color = ScreenCanvasArtTheme.AudioSecondaryAccent;
        CreateButton("WeatherRefreshButton", weatherHeader, "REFRESH", font, 70f, 26f, 8);
        CreateButton("WeatherExpandButton", weatherHeader, "HIDE", font, 58f, 26f, 8);

        RectTransform weatherBody = CreateVerticalBody("WeatherBody", weatherModule, contentWidth - 12f, 128f, 3f, new RectOffset(2, 2, 0, 0));
        CanvasGroup weatherBodyGroup = weatherBody.gameObject.AddComponent<CanvasGroup>();
        RectTransform weatherSummary = CreateRow("WeatherSummary", weatherBody, 64f, contentWidth - 16f);
        RawImage artwork = CreateRawImage("WeatherArtwork", weatherSummary, 64f, 64f);
        CanvasGroup artworkGroup = artwork.gameObject.AddComponent<CanvasGroup>();
        ScreenCanvasWeatherAnimator weatherArtworkAnimator = artwork.gameObject.AddComponent<ScreenCanvasWeatherAnimator>();
        weatherArtworkAnimator.Configure(artwork.rectTransform, artworkGroup);

        RectTransform weatherCopy = CreateVerticalBody("WeatherCopy", weatherSummary, 176f, 64f, 0f, new RectOffset(4, 0, 0, 0));
        Text temperature = CreateText("WeatherTemperatureText", weatherCopy, "-- C", font, 22, FontStyle.Bold, TextAnchor.MiddleLeft, 30f, 172f);
        temperature.color = Color.white;
        Text condition = CreateText("WeatherConditionText", weatherCopy, "Weather unavailable", font, 9, FontStyle.Normal, TextAnchor.UpperLeft, 30f, 172f);
        condition.color = ScreenCanvasArtTheme.MutedText;
        CreateText("WeatherDetailsText", weatherBody, "Waiting for current conditions.", font, 9, FontStyle.Normal, TextAnchor.UpperLeft, 35f, contentWidth - 16f);
        Text weatherStatus = CreateText("WeatherStatusText", weatherBody, "NOT YET UPDATED", font, 8, FontStyle.Bold, TextAnchor.MiddleLeft, 15f, contentWidth - 16f);
        weatherStatus.color = ScreenCanvasArtTheme.AudioPrimaryAccent;
        ScreenCanvasModuleAnimator weatherAnimator = weatherModule.gameObject.AddComponent<ScreenCanvasModuleAnimator>();
        weatherAnimator.Configure(weatherModule.GetComponent<LayoutElement>(), weatherGroup, weatherBodyGroup, 40f, 176f, true, true);

        RectTransform systemModule = CreateCard("SystemModule", dashboard, contentWidth, 88f, ScreenCanvasArtTheme.CardBase);
        VerticalLayoutGroup systemLayout = systemModule.gameObject.AddComponent<VerticalLayoutGroup>();
        systemLayout.padding = new RectOffset(6, 6, 5, 5);
        systemLayout.spacing = 4f;
        systemLayout.childAlignment = TextAnchor.UpperLeft;
        systemLayout.childControlWidth = true;
        systemLayout.childControlHeight = true;
        systemLayout.childForceExpandWidth = true;
        systemLayout.childForceExpandHeight = false;
        CanvasGroup systemGroup = systemModule.gameObject.AddComponent<CanvasGroup>();

        RectTransform systemHeader = CreateRow("SystemHeader", systemModule, 30f, contentWidth - 12f);
        Text systemLabel = CreateText("SystemLabel", systemHeader, "SYSTEM", font, 11, FontStyle.Bold, TextAnchor.MiddleLeft, 28f, 182f);
        systemLabel.color = ScreenCanvasArtTheme.AudioSecondaryAccent;
        CreateButton("SystemExpandButton", systemHeader, "HIDE", font, 58f, 26f, 8);
        RectTransform systemBody = CreateVerticalBody("SystemBody", systemModule, contentWidth - 12f, 42f, 0f, new RectOffset(2, 2, 0, 0));
        CanvasGroup systemBodyGroup = systemBody.gameObject.AddComponent<CanvasGroup>();
        Text systemText = CreateText("SystemText", systemBody, "DEVICE\nPLATFORM\nNETWORK", font, 8, FontStyle.Normal, TextAnchor.UpperLeft, 42f, contentWidth - 16f);
        systemText.color = ScreenCanvasArtTheme.MutedText;
        ScreenCanvasModuleAnimator systemAnimator = systemModule.gameObject.AddComponent<ScreenCanvasModuleAnimator>();
        systemAnimator.Configure(systemModule.GetComponent<LayoutElement>(), systemGroup, systemBodyGroup, 40f, 88f, true, true);
    }

    private static RectTransform CreateHeaderRow(string name, RectTransform parent, string title, Font font)
    {
        RectTransform row = CreateRow(name, parent, 30f);
        Text label = CreateText(title + "Label", row, title, font, 12, FontStyle.Bold, TextAnchor.MiddleLeft, 28f);
        LayoutElement layout = label.gameObject.GetComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.preferredWidth = 90f;
        ScreenCanvasArtTheme.StyleText(label, title + "Label", 12, FontStyle.Bold);
        return row;
    }

    private static RectTransform CreateRow(string name, RectTransform parent, float height)
    {
        return CreateRow(name, parent, height, AudioChildWidth);
    }

    private static RectTransform CreateRow(string name, RectTransform parent, float height, float width)
    {
        RectTransform row = CreateRect(name, parent, new Vector2(width, height));
        AddLayoutElement(row.gameObject, height, height, 0f, width, width);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return row;
    }

    private static Button CreateButton(string name, RectTransform parent, string label, Font font, float width, float height, int fontSize)
    {
        GameObject obj = CreateObject(name, parent, typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        AddLayoutElement(obj, height, height, 0f, width, width);

        Image image = obj.GetComponent<Image>();
        image.color = new Color(0.18f, 0.24f, 0.28f, 0.88f);
        image.raycastTarget = true;

        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        button.interactable = true;
        ScreenCanvasArtTheme.StyleSelectable(button, image, true);

        Text text = CreateText("Label", rect, label, font, fontSize, FontStyle.Normal, TextAnchor.MiddleCenter, height);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 2f);
        textRect.offsetMax = new Vector2(-4f, -2f);
        return button;
    }

    private static Toggle CreateToggle(string name, RectTransform parent, string label, Font font, bool isOn)
    {
        return CreateToggle(name, parent, label, font, isOn, 68f);
    }

    private static Toggle CreateToggle(string name, RectTransform parent, string label, Font font, bool isOn, float width)
    {
        GameObject obj = CreateObject(name, parent, typeof(Toggle));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, 24f);
        AddLayoutElement(obj, 24f, 24f, 0f, width, width);

        GameObject background = CreateObject("Background", rect, typeof(CanvasRenderer), typeof(Image));
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(12f, 12f);
        backgroundRect.anchoredPosition = new Vector2(8f, 0f);
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.color = new Color(0.18f, 0.24f, 0.28f, 0.88f);

        GameObject checkmark = CreateObject("Checkmark", backgroundRect, typeof(CanvasRenderer), typeof(Image));
        RectTransform checkRect = checkmark.GetComponent<RectTransform>();
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.offsetMin = new Vector2(2f, 2f);
        checkRect.offsetMax = new Vector2(-2f, -2f);
        Image checkImage = checkmark.GetComponent<Image>();
        checkImage.color = new Color(0.35f, 0.82f, 0.62f, 1f);

        Text text = CreateText("Label", rect, label, font, 9, FontStyle.Normal, TextAnchor.MiddleLeft, 24f, width);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(22f, 0f);
        textRect.offsetMax = Vector2.zero;

        Toggle toggle = obj.GetComponent<Toggle>();
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkImage;
        toggle.isOn = isOn;
        ScreenCanvasArtTheme.StyleSelectable(toggle, backgroundImage, false);
        ScreenCanvasArtTheme.StyleText(text, name, 9, FontStyle.Normal);
        return toggle;
    }

    private static Text CreateText(string name, RectTransform parent, string value, Font font, int fontSize, FontStyle style, TextAnchor alignment, float height)
    {
        return CreateText(name, parent, value, font, fontSize, style, alignment, height, AudioChildWidth);
    }

    private static Text CreateText(string name, RectTransform parent, string value, Font font, int fontSize, FontStyle style, TextAnchor alignment, float height, float width)
    {
        GameObject obj = CreateObject(name, parent, typeof(CanvasRenderer), typeof(Text));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        AddLayoutElement(obj, height, height, 0f, width, width);

        Text text = obj.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.color = Color.white;
        text.text = value;
        ScreenCanvasArtTheme.StyleText(text, name, fontSize, style);
        return text;
    }

    private static RectTransform CreateCard(string name, RectTransform parent, float width, float height, Color color)
    {
        RectTransform card = CreateRect(name, parent, new Vector2(width, height));
        AddLayoutElement(card.gameObject, height, height, 0f, width, width);
        Image image = card.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        Shadow shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        shadow.useGraphicAlpha = true;
        return card;
    }

    private static void MakePanelTransparent(RectTransform panel)
    {
        Image image = panel.GetComponent<Image>();
        if (image == null)
        {
            image = panel.gameObject.AddComponent<Image>();
        }

        Color color = image.color;
        color.a = 0f;
        image.color = color;
        image.raycastTarget = false;
    }

    private static void ValidateTransparentPanel(RectTransform panel)
    {
        Image image = panel.GetComponent<Image>();
        if (image == null || image.color.a > 0.0001f)
        {
            throw new System.InvalidOperationException(panel.name + " must have a transparent background.");
        }
    }

    private static void ValidateThemedPanel(RectTransform panel)
    {
        Image image = panel.GetComponent<Image>();
        if (image == null || image.color.a < 0.85f)
        {
            throw new System.InvalidOperationException(panel.name + " must have a visible black-gray background.");
        }

        float channelSpread = Mathf.Max(image.color.r, Mathf.Max(image.color.g, image.color.b))
            - Mathf.Min(image.color.r, Mathf.Min(image.color.g, image.color.b));
        if (channelSpread > 0.025f)
        {
            throw new System.InvalidOperationException(panel.name + " background must remain neutral gray.");
        }
    }

    private static RectTransform CreateVerticalBody(string name, RectTransform parent, float width, float height, float spacing, RectOffset padding)
    {
        RectTransform body = CreateRect(name, parent, new Vector2(width, height));
        AddLayoutElement(body.gameObject, height, height, 0f, width, width);
        VerticalLayoutGroup layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = padding;
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return body;
    }

    private static RawImage CreateRawImage(string name, RectTransform parent, float width, float height)
    {
        GameObject obj = CreateObject(name, parent, typeof(CanvasRenderer), typeof(RawImage));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        AddLayoutElement(obj, height, height, 0f, width, width);

        RawImage image = obj.GetComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static Texture2D[] LoadWeatherVisuals()
    {
        string[] names =
        {
            "01 Sunny COLOR.gif",
            "02 Partly Cloudy COLOR.gif",
            "03 Partly Sunny COLOR.gif",
            "04 Cloudy COLOR.gif",
            "05 Drizzle COLOR.gif",
            "06 Rain COLOR.gif",
            "07 Snowy COLOR.gif",
            "08 Drizzle Sunny COLOR.gif",
            "09 Rain Sunny COLOR.gif",
            "10 Snowy Sunny COLOR.gif",
            "11 Clear Night COLOR.gif",
            "12 Partly Cloudy Night COLOR.gif",
            "13 Mostly Cloudy Night COLOR.gif",
            "14 Drizzle Night COLOR.gif",
            "15 Rain Night COLOR.gif",
            "16 Snowy Night COLOR.gif",
            "17 Storm COLOR.gif",
            "18 Windy COLOR.gif",
            "19 Hurricane COLOR.gif",
            "20 Tornado COLOR.gif",
            "21 Mist COLOR.gif"
        };

        Texture2D[] textures = new Texture2D[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GIF/" + names[i]);
            if (textures[i] == null)
            {
                Debug.LogWarning("[ScreenCanvasPanelSceneBuilder] Weather artwork could not be loaded: " + names[i]);
            }
        }

        return textures;
    }

    private static RectTransform CreateRect(string name, RectTransform parent, Vector2 size)
    {
        GameObject obj = CreateObject(name, parent);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static GameObject CreateObject(string name, RectTransform parent, params System.Type[] components)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        foreach (System.Type component in components)
        {
            obj.AddComponent(component);
        }

        obj.layer = parent.gameObject.layer;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return obj;
    }

    private static void ClearChildren(RectTransform parent, params string[] preservedNames)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (System.Array.IndexOf(preservedNames, child.name) >= 0)
            {
                continue;
            }

            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static RectTransform RequireRectTransform(string path)
    {
        GameObject obj = GameObject.Find(path);
        if (obj == null)
        {
            throw new System.InvalidOperationException("Could not find " + path);
        }

        return obj.GetComponent<RectTransform>();
    }

    private static RectTransform RequireChild(RectTransform parent, string path)
    {
        Transform child = parent.Find(path);
        if (child == null)
        {
            throw new System.InvalidOperationException("Could not find " + path + " under " + parent.name);
        }

        RectTransform rect = child as RectTransform;
        if (rect == null)
        {
            throw new System.InvalidOperationException(path + " is not a RectTransform.");
        }

        return rect;
    }

    private static LayoutElement AddLayoutElement(GameObject obj, float minHeight, float preferredHeight, float flexibleHeight, float minWidth = -1f, float preferredWidth = -1f)
    {
        LayoutElement layoutElement = obj.AddComponent<LayoutElement>();
        layoutElement.minHeight = minHeight;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleHeight = flexibleHeight;
        layoutElement.minWidth = minWidth;
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.flexibleWidth = minWidth > 0f ? 0f : -1f;
        return layoutElement;
    }
}
