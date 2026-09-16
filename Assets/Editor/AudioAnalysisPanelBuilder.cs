using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AudioAnalysisPanelBuilder
{
    private const string ScenePath = "Assets/Scenes/v203.0.0.unity";

    [MenuItem("Tools/MR-VD/Rebuild Audio Analysis Page")]
    public static void BuildAndSave()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AudioCaptureCSCore audioCapture = Object.FindFirstObjectByType<AudioCaptureCSCore>();
        if (audioCapture == null)
        {
            throw new MissingReferenceException("AudioCaptureCSCore was not found in v203.0.0.");
        }

        Undo.RegisterFullObjectHierarchyUndo(audioCapture.gameObject, "Rebuild Audio Analysis Page");
        audioCapture.RebuildScreenAnalysisPageInEditor();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AudioAnalysisPanelBuilder] Rebuilt and saved the editable audio analysis page.");
    }
}
