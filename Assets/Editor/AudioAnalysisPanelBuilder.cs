using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AudioAnalysisPanelBuilder
{
    private const string ScenePath = "Assets/Scenes/v203.0.0.unity";

    [InitializeOnLoadMethod]
    private static void ScheduleSceneUpgrade()
    {
        EditorApplication.delayCall += TryUpgradeOpenScene;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += TryUpgradeOpenScene;
        }
    }

    private static void TryUpgradeOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            return;
        }

        GameObject externalButton = GameObject.Find("Screen/Canvas/AudioPanel/AudioPanelControls/AnalysisPageButton");
        GameObject settingsToggle = GameObject.Find("Screen/Canvas/InfoPanel/RuntimeDashboard/SettingsModule/SettingsBody/SettingsRowThree/AudioAnalysisPageToggle");
        GameObject chromaSegments = GameObject.Find("Screen/Canvas/AudioPanel/AudioCaptureCanvasContent/AudioAnalysisPage/ChromaWheel/Segments");
        bool hasChromaCanvasRenderer = chromaSegments != null && chromaSegments.GetComponent<CanvasRenderer>() != null;
        bool navigationUpgradeComplete = externalButton == null && settingsToggle != null;
        if (navigationUpgradeComplete && hasChromaCanvasRenderer)
        {
            return;
        }

        if (scene.isDirty)
        {
            Debug.LogWarning("[AudioAnalysisPanelBuilder] Scene upgrade skipped because v203.0.0 has unsaved changes. Save it, then use Tools/MR-VD/Rebuild Audio Analysis Page.");
            return;
        }

        if (navigationUpgradeComplete && chromaSegments != null)
        {
            Undo.AddComponent<CanvasRenderer>(chromaSegments);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AudioAnalysisPanelBuilder] Added the missing chroma CanvasRenderer and saved v203.0.0.");
            return;
        }

        ApplyToScene(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AudioAnalysisPanelBuilder] Moved audio page switching into Settings and saved v203.0.0.");
    }

    [MenuItem("Tools/MR-VD/Rebuild Audio Analysis Page")]
    public static void BuildAndSave()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyToScene(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AudioAnalysisPanelBuilder] Rebuilt and saved the editable audio analysis page.");
    }

    private static void ApplyToScene(Scene scene)
    {
        AudioCaptureCSCore audioCapture = Object.FindFirstObjectByType<AudioCaptureCSCore>();
        if (audioCapture == null)
        {
            throw new MissingReferenceException("AudioCaptureCSCore was not found in v203.0.0.");
        }

        Undo.RegisterFullObjectHierarchyUndo(audioCapture.gameObject, "Rebuild Audio Analysis Page");
        audioCapture.RebuildScreenAnalysisPageInEditor();
        RebuildSettingsPageToggle();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void RebuildSettingsPageToggle()
    {
        GameObject settingsBodyObject = GameObject.Find("Screen/Canvas/InfoPanel/RuntimeDashboard/SettingsModule/SettingsBody");
        if (settingsBodyObject == null)
        {
            throw new MissingReferenceException("RuntimeDashboard SettingsBody was not found.");
        }

        RectTransform settingsBody = settingsBodyObject.GetComponent<RectTransform>();
        Transform existingRow = settingsBody.Find("SettingsRowThree");
        if (existingRow != null)
        {
            Object.DestroyImmediate(existingRow.gameObject);
        }

        Transform sourceRow = settingsBody.Find("SettingsRowOne");
        if (sourceRow == null)
        {
            throw new MissingReferenceException("SettingsRowOne was not found.");
        }

        GameObject rowObject = Object.Instantiate(sourceRow.gameObject, settingsBody);
        rowObject.name = "SettingsRowThree";
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.SetSiblingIndex(settingsBody.childCount - 1);
        row.sizeDelta = new Vector2(244f, 25f);

        for (int i = row.childCount - 1; i >= 1; i--)
        {
            Object.DestroyImmediate(row.GetChild(i).gameObject);
        }

        if (row.childCount == 0)
        {
            throw new MissingReferenceException("SettingsRowOne has no toggle template.");
        }

        GameObject toggleObject = row.GetChild(0).gameObject;
        toggleObject.name = "AudioAnalysisPageToggle";
        LayoutElement toggleLayout = toggleObject.GetComponent<LayoutElement>();
        if (toggleLayout != null)
        {
            toggleLayout.minWidth = 244f;
            toggleLayout.preferredWidth = 244f;
        }

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.SetIsOnWithoutNotify(false);
        Text label = toggleObject.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = "AUDIO ANALYSIS";
        }

        LayoutElement bodyLayout = settingsBody.GetComponent<LayoutElement>();
        bodyLayout.minHeight = 91f;
        bodyLayout.preferredHeight = 91f;

        ScreenCanvasModuleAnimator settingsAnimator = settingsBody.GetComponentInParent<ScreenCanvasModuleAnimator>();
        SerializedObject animatorObject = new SerializedObject(settingsAnimator);
        animatorObject.FindProperty("expandedHeight").floatValue = 99f;
        animatorObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
