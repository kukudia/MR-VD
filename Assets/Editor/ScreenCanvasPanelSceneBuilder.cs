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

        ClearChildren(audioPanel, "AudioCaptureCSCore");
        ClearChildren(infoPanel);

        BuildAudioPanel(audioPanel, font);
        BuildInfoPanel(infoPanel, font);
        WireSceneComponents(audioPanel, infoPanel);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[ScreenCanvasPanelSceneBuilder] Rebuilt AudioPanel and InfoPanel under Screen/Canvas in " + ScenePath);
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
            ScreenCanvasArtTheme.AudioPrimaryAccent,
            ScreenCanvasArtTheme.AudioSecondaryAccent,
            true);

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
            ScreenCanvasArtTheme.AudioPrimaryAccent,
            ScreenCanvasArtTheme.AudioSecondaryAccent,
            false);

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
        RectTransform content = CreateRect("RuntimeInfoCanvasContent", panel, new Vector2(270f, 480f));
        content.localScale = Vector3.one * 0.1f;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ScreenCanvasArtTheme.ApplyPanelArt(
            content,
            ScreenCanvasArtTheme.InfoPanelBase,
            ScreenCanvasArtTheme.InfoPrimaryAccent,
            ScreenCanvasArtTheme.InfoSecondaryAccent,
            true);

        CreateText("LocalTimeText", content, "Local Time\nHH:mm:ss\nyyyy-MM-dd", font, 15, FontStyle.Bold, TextAnchor.UpperLeft, 70f);

        RectTransform weatherHeader = CreateHeaderRow("WeatherHeader", content, "Weather", font);
        CreateButton("WeatherRefreshButton", weatherHeader, "Refresh", font, 62f, 28f, 9);
        CreateToggle("WeatherAutoToggle", weatherHeader, "Auto", font, true);
        CreateText("WeatherText", content, "No weather data yet.", font, 10, FontStyle.Normal, TextAnchor.UpperLeft, 110f);

        RectTransform mailHeader = CreateHeaderRow("MailHeader", content, "Mail", font);
        CreateButton("MailRefreshButton", mailHeader, "Refresh", font, 62f, 28f, 9);
        CreateToggle("MailAutoToggle", mailHeader, "Auto", font, true);
        CreateText("MailText", content, "Unread: 0\nNo unread mail previews from the local Outlook inbox.", font, 9, FontStyle.Normal, TextAnchor.UpperLeft, 150f);

        CreateText("SystemText", content, "System", font, 9, FontStyle.Normal, TextAnchor.UpperLeft, 60f);
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
        RectTransform row = CreateRect(name, parent, new Vector2(AudioChildWidth, height));
        AddLayoutElement(row.gameObject, height, height, 0f, AudioChildWidth, AudioChildWidth);
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
        GameObject obj = CreateObject(name, parent, typeof(Toggle));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(68f, 28f);
        AddLayoutElement(obj, 28f, 28f, 0f, 68f, 68f);

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

        Text text = CreateText("Label", rect, label, font, 9, FontStyle.Normal, TextAnchor.MiddleLeft, 28f);
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
        GameObject obj = CreateObject(name, parent, typeof(CanvasRenderer), typeof(Text));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(AudioChildWidth, height);
        AddLayoutElement(obj, height, height, 0f, AudioChildWidth, AudioChildWidth);

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
