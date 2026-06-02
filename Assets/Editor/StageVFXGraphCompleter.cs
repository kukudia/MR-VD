using UnityEditor;
using UnityEngine;

public static class StageVFXGraphCompleter
{
    [MenuItem("Tools/Stage/Complete Stage VFX Graphs")]
    public static void CompleteStageVFXGraphs()
    {
        StageManager[] managers = Object.FindObjectsByType<StageManager>(FindObjectsSortMode.None);
        if (managers == null || managers.Length == 0)
        {
            Debug.LogWarning("[StageVFXGraphCompleter] No StageManager found in the open scenes.");
            return;
        }

        foreach (StageManager manager in managers)
        {
            if (manager == null)
            {
                continue;
            }

            Undo.RegisterFullObjectHierarchyUndo(manager.gameObject, "Complete Stage VFX Graphs");
            manager.CompleteStageVfxGraphs();
            EditorUtility.SetDirty(manager);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[StageVFXGraphCompleter] Completed VFX Graph bindings for {managers.Length} StageManager instance(s).");
    }
}
