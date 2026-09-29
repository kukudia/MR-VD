using UnityEditor;

public static class StageVFXGraphCompleter
{
    [MenuItem("Tools/Stage/Complete Stage VFX Graphs")]
    public static void CompleteStageVFXGraphs()
    {
        AudioVisualEffectsInstaller.InstallActiveScene();
    }
}
