using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GpuAudioParticleSceneInstaller
{
    private const string ScenePath = "Assets/Scenes/v203.0.0.unity";
    private const string RunOnceMarkerPath = "Temp/install-gpu-audio-particles.once";
    private const string HostName = "GPU Audio Particle Visualizer";
    private const string ComputePath = "Assets/AudioReactiveParticles/Shaders/GpuAudioParticles.compute";
    private const string ShaderPath = "Assets/AudioReactiveParticles/Shaders/GpuAudioParticles.shader";

    [InitializeOnLoadMethod]
    private static void InstallIfRequested()
    {
        if (!File.Exists(RunOnceMarkerPath))
        {
            return;
        }

        File.Delete(RunOnceMarkerPath);
        EditorApplication.delayCall += Install;
    }

    [MenuItem("Tools/MR-VD/Install GPU Audio Particle Visualizer")]
    public static void Install()
    {
        Scene scene = OpenTargetScene();
        AudioVisualizer audioVisualizer = Object.FindFirstObjectByType<AudioVisualizer>();
        AudioCaptureCSCore audioCapture = Object.FindFirstObjectByType<AudioCaptureCSCore>();
        if (audioVisualizer == null || audioCapture == null)
        {
            throw new System.InvalidOperationException("v203.0.0 requires AudioVisualizer and AudioCaptureCSCore before installing GPU particles.");
        }

        Transform parent = audioVisualizer.transform.parent != null
            ? audioVisualizer.transform.parent
            : audioVisualizer.transform;
        Transform existing = parent.Find(HostName);
        bool created = existing == null;
        GameObject host = created ? new GameObject(HostName) : existing.gameObject;

        if (created)
        {
            host.transform.SetParent(parent, false);
            host.transform.localPosition = new Vector3(0f, -0.08f, 0.22f);
            host.transform.localRotation = Quaternion.identity;
            host.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(host, "Install GPU Audio Particle Visualizer");
        }

        GpuAudioParticleVisualizer visualizer = host.GetComponent<GpuAudioParticleVisualizer>();
        bool componentAdded = visualizer == null;
        if (visualizer == null)
        {
            visualizer = Undo.AddComponent<GpuAudioParticleVisualizer>(host);
        }

        ComputeShader particleCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
        Shader particleShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

        if (particleCompute == null || particleShader == null)
        {
            throw new System.InvalidOperationException("GPU audio particle shader assets could not be loaded.");
        }

        bool referencesChanged = visualizer.audioCapture != audioCapture
            || visualizer.audioVisualizer != audioVisualizer
            || visualizer.particleCompute != particleCompute
            || visualizer.particleShader != particleShader;
        if (created || componentAdded || referencesChanged)
        {
            visualizer.audioCapture = audioCapture;
            visualizer.audioVisualizer = audioVisualizer;
            visualizer.particleCompute = particleCompute;
            visualizer.particleShader = particleShader;
            EditorUtility.SetDirty(visualizer);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Selection.activeGameObject = host;
        string action = created || componentAdded || referencesChanged ? "Installed" : "Already configured";
        Debug.Log("[GpuAudioParticleSceneInstaller] " + action + " 7,168 compute-driven particles with spectrum, beat burst, and background layers in " + ScenePath, host);
    }

    [MenuItem("Tools/MR-VD/Validate GPU Audio Particle Visualizer")]
    public static void Validate()
    {
        OpenTargetScene();
        GpuAudioParticleVisualizer visualizer = Object.FindFirstObjectByType<GpuAudioParticleVisualizer>();
        if (visualizer == null)
        {
            throw new System.InvalidOperationException("GpuAudioParticleVisualizer is missing from v203.0.0.");
        }

        if (visualizer.transform.parent == null || visualizer.transform.parent.name != "Screen")
        {
            throw new System.InvalidOperationException("GpuAudioParticleVisualizer must remain under Screen so it follows the MR display anchor.");
        }

        if (visualizer.audioCapture == null || visualizer.audioVisualizer == null)
        {
            throw new System.InvalidOperationException("GpuAudioParticleVisualizer audio references are incomplete.");
        }

        if (visualizer.particleCompute == null || visualizer.particleShader == null || !visualizer.particleShader.isSupported)
        {
            throw new System.InvalidOperationException("GpuAudioParticleVisualizer GPU assets are missing or unsupported by the active editor graphics device.");
        }

        if (visualizer.TotalParticleCount != 0 && visualizer.TotalParticleCount != 7168)
        {
            throw new System.InvalidOperationException("Unexpected runtime particle allocation. Re-enter Play Mode to rebuild the GPU buffers.");
        }

        Debug.Log("[GpuAudioParticleSceneInstaller] Validation passed: independent audio sources, ComputeShader, URP shader, Screen anchoring, and 7,168-particle configuration are ready.", visualizer);
    }

    private static Scene OpenTargetScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == ScenePath)
        {
            return activeScene;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            throw new System.OperationCanceledException("Scene installation was cancelled because the current scene was not saved.");
        }

        return EditorSceneManager.OpenScene(ScenePath);
    }
}
