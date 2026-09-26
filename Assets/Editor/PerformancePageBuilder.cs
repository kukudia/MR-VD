using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LibreHardwareMonitor.Hardware;

public static class PerformancePageBuilder
{
    private const string ScenePath = "Assets/Scenes/v203.0.0.unity";

    [MenuItem("Tools/MR-VD/Configure Performance Plugins")]
    public static void ConfigurePlugins()
    {
        string[] names = { "BlackSharp.Core", "DiskInfoToolkit", "HidSharp", "LibreHardwareMonitorLib", "RAMSPDToolkit-NDD" };
        foreach (string name in names)
        {
            string path = "Assets/Plugins/LibreHardwareMonitor/" + name + ".dll";
            PluginImporter importer = AssetImporter.GetAtPath(path) as PluginImporter;
            if (importer == null)
            {
                throw new MissingReferenceException("Plugin importer missing: " + path);
            }

            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(true);
            importer.SetEditorData("OS", "Windows");
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
            importer.SaveAndReimport();
        }

        Debug.Log("[PerformancePageBuilder] Windows plugin platforms configured.");
    }

    [MenuItem("Tools/MR-VD/Verify Performance Page")]
    public static void Verify()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RectTransform dashboard = GameObject.Find("Screen/Canvas/InfoPanel/RuntimeDashboard")?.GetComponent<RectTransform>();
        Transform page = dashboard?.Find("PerformancePage");
        Toggle toggle = dashboard?.Find("SettingsModule/SettingsBody/SettingsRowFour/PerformancePageToggle")?.GetComponent<Toggle>();
        if (page == null || toggle == null || page.GetComponentsInChildren<PerformanceHistoryGraphic>(true).Length != 4)
        {
            throw new MissingReferenceException("Performance page hierarchy is incomplete.");
        }

        Computer probe = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
        try
        {
            probe.Open();
            foreach (IHardware hardware in probe.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu || hardware.HardwareType == HardwareType.GpuNvidia
                    || hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuIntel)
                {
                    hardware.Update();
                    Debug.Log("[PerformancePageBuilder] Sensor probe: " + hardware.Name + " / " + hardware.HardwareType + " / " + hardware.Sensors.Length + " sensors");
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Load && (sensor.Name == "CPU Total" || sensor.Name == "GPU Core"))
                        {
                            Debug.Log("[PerformancePageBuilder] Load sensor: " + sensor.Name + " = " + (sensor.Value.HasValue ? sensor.Value.Value.ToString("0.0") : "N/A"));
                        }
                    }
                }
            }
        }
        finally
        {
            probe.Close();
        }

        Debug.Log("[PerformancePageBuilder] Performance scene hierarchy verified.");
    }

    [MenuItem("Tools/MR-VD/Build Performance Page")]
    public static void BuildAndSave()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RectTransform dashboard = GameObject.Find("Screen/Canvas/InfoPanel/RuntimeDashboard")?.GetComponent<RectTransform>();
        if (dashboard == null)
        {
            throw new MissingReferenceException("RuntimeDashboard was not found.");
        }

        RectTransform settingsBody = dashboard.Find("SettingsModule/SettingsBody") as RectTransform;
        Transform sourceRow = settingsBody?.Find("SettingsRowThree");
        if (sourceRow == null)
        {
            throw new MissingReferenceException("SettingsRowThree was not found.");
        }

        Transform oldRow = settingsBody.Find("SettingsRowFour");
        if (oldRow != null)
        {
            Object.DestroyImmediate(oldRow.gameObject);
        }

        GameObject rowObject = Object.Instantiate(sourceRow.gameObject, settingsBody);
        rowObject.name = "SettingsRowFour";
        Transform toggleTransform = rowObject.transform.GetChild(0);
        toggleTransform.name = "PerformancePageToggle";
        Toggle toggle = toggleTransform.GetComponent<Toggle>();
        toggle.SetIsOnWithoutNotify(false);
        toggleTransform.GetComponentInChildren<Text>(true).text = "PERFORMANCE";

        LayoutElement bodyLayout = settingsBody.GetComponent<LayoutElement>();
        bodyLayout.minHeight = 132f;
        bodyLayout.preferredHeight = 132f;
        ((RectTransform)settingsBody).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 132f);
        ScreenCanvasModuleAnimator animator = settingsBody.GetComponentInParent<ScreenCanvasModuleAnimator>();
        SerializedObject animatorObject = new SerializedObject(animator);
        animatorObject.FindProperty("expandedHeight").floatValue = 172f;
        animatorObject.ApplyModifiedPropertiesWithoutUndo();

        Transform oldPage = dashboard.Find("PerformancePage");
        if (oldPage != null)
        {
            Object.DestroyImmediate(oldPage.gameObject);
        }

        GameObject pageObject = new GameObject("PerformancePage", typeof(RectTransform), typeof(CanvasRenderer), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(PerformanceMonitorPanel));
        pageObject.layer = dashboard.gameObject.layer;
        RectTransform page = pageObject.GetComponent<RectTransform>();
        page.SetParent(dashboard, false);
        Transform systemModule = dashboard.Find("SystemModule");
        page.SetSiblingIndex(systemModule.GetSiblingIndex() + 1);
        LayoutElement pageLayout = pageObject.GetComponent<LayoutElement>();
        pageLayout.minHeight = 234f;
        pageLayout.preferredHeight = 234f;
        VerticalLayoutGroup pageGroup = pageObject.GetComponent<VerticalLayoutGroup>();
        pageGroup.spacing = 4f;
        pageGroup.childControlWidth = true;
        pageGroup.childControlHeight = true;
        pageGroup.childForceExpandWidth = true;
        pageGroup.childForceExpandHeight = false;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        AddText(page, "PerformanceTitle", "PERFORMANCE  /  LAST 60 SECONDS", font, 10, FontStyle.Bold, 20f, new Color(0.82f, 0.85f, 0.88f));
        PerformanceMonitorPanel monitor = pageObject.GetComponent<PerformanceMonitorPanel>();
        SerializedObject monitorObject = new SerializedObject(monitor);
        AddMetric(page, monitorObject, "CPU", "cpuValue", "cpuGraph", font, new Color(0.33f, 0.76f, 0.91f));
        AddMetric(page, monitorObject, "GPU", "gpuValue", "gpuGraph", font, new Color(0.91f, 0.69f, 0.39f));
        AddMetric(page, monitorObject, "MEMORY", "memoryValue", "memoryGraph", font, new Color(0.84f, 0.51f, 0.58f));
        AddMetric(page, monitorObject, "NETWORK", "networkValue", "networkGraph", font, new Color(0.49f, 0.83f, 0.66f));
        monitorObject.ApplyModifiedPropertiesWithoutUndo();
        pageObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PerformancePageBuilder] Saved editable performance page and Settings toggle.");
    }

    private static void AddMetric(RectTransform parent, SerializedObject monitor, string title, string valueProperty, string graphProperty, Font font, Color color)
    {
        GameObject rowObject = new GameObject(title + "Metric", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        rowObject.layer = parent.gameObject.layer;
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.SetParent(parent, false);
        rowObject.GetComponent<LayoutElement>().preferredHeight = 48f;
        VerticalLayoutGroup layout = rowObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 1f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        GameObject headingObject = new GameObject("Heading", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        headingObject.layer = parent.gameObject.layer;
        RectTransform heading = headingObject.GetComponent<RectTransform>();
        heading.SetParent(row, false);
        headingObject.GetComponent<LayoutElement>().preferredHeight = 15f;
        HorizontalLayoutGroup headingLayout = headingObject.GetComponent<HorizontalLayoutGroup>();
        headingLayout.childControlWidth = true;
        headingLayout.childControlHeight = true;
        headingLayout.childForceExpandWidth = true;
        Text name = AddText(heading, "Label", title, font, 9, FontStyle.Bold, 15f, color);
        name.alignment = TextAnchor.MiddleLeft;
        Text value = AddText(heading, "Value", "N/A", font, 9, FontStyle.Normal, 15f, Color.white);
        value.alignment = TextAnchor.MiddleRight;
        monitor.FindProperty(valueProperty).objectReferenceValue = value;

        GameObject graphObject = new GameObject("Graph", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        graphObject.layer = parent.gameObject.layer;
        graphObject.transform.SetParent(row, false);
        graphObject.GetComponent<LayoutElement>().preferredHeight = 31f;
        Image background = graphObject.GetComponent<Image>();
        background.color = new Color(0.13f, 0.16f, 0.18f, 0.8f);
        background.raycastTarget = false;
        GameObject lineObject = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(PerformanceHistoryGraphic));
        lineObject.layer = parent.gameObject.layer;
        RectTransform lineRect = lineObject.GetComponent<RectTransform>();
        lineRect.SetParent(graphObject.transform, false);
        lineRect.anchorMin = Vector2.zero;
        lineRect.anchorMax = Vector2.one;
        lineRect.offsetMin = Vector2.zero;
        lineRect.offsetMax = Vector2.zero;
        PerformanceHistoryGraphic graph = lineObject.GetComponent<PerformanceHistoryGraphic>();
        graph.Configure(title == "NETWORK" ? 0f : 100f, color);
        monitor.FindProperty(graphProperty).objectReferenceValue = graph;
    }

    private static Text AddText(RectTransform parent, string name, string content, Font font, int size, FontStyle style, float height, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);
        textObject.GetComponent<LayoutElement>().preferredHeight = height;
        Text label = textObject.GetComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.fontStyle = style;
        label.text = content;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }
}
