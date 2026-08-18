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
    private const int ExpectedParticleCount = 3072;

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
        Camera targetCamera = FindActiveCamera();
        if (audioVisualizer == null || targetCamera == null)
        {
            throw new System.InvalidOperationException("v203.0.0 requires AudioVisualizer and an active Camera before installing GPU stardust.");
        }

        Transform parent = audioVisualizer.transform.parent != null
            ? audioVisualizer.transform.parent
            : audioVisualizer.transform;
        Canvas screenCanvas = parent.GetComponentInChildren<Canvas>(true);
        RectTransform occlusionScreen = screenCanvas != null ? screenCanvas.transform as RectTransform : null;
        if (occlusionScreen == null)
        {
            throw new System.InvalidOperationException("v203.0.0 requires a world-space Screen/Canvas for stardust occlusion.");
        }

        Transform existing = parent.Find(HostName);
        bool created = existing == null;
        GameObject host = created ? new GameObject(HostName) : existing.gameObject;
        if (created)
        {
            host.transform.SetParent(parent, false);
            host.transform.localPosition = Vector3.zero;
            host.transform.localRotation = Quaternion.identity;
            host.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(host, "Install GPU Camera Stardust");
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
            throw new System.InvalidOperationException("GPU stardust shader assets could not be loaded.");
        }

        bool referencesChanged = visualizer.audioVisualizer != audioVisualizer
            || visualizer.targetCamera != targetCamera
            || visualizer.occlusionScreen != occlusionScreen
            || visualizer.particleCompute != particleCompute
            || visualizer.particleShader != particleShader
            || visualizer.particleCount != ExpectedParticleCount;
        if (created || componentAdded || referencesChanged)
        {
            visualizer.audioVisualizer = audioVisualizer;
            visualizer.targetCamera = targetCamera;
            visualizer.occlusionScreen = occlusionScreen;
            visualizer.particleCompute = particleCompute;
            visualizer.particleShader = particleShader;
            visualizer.particleCount = ExpectedParticleCount;
            EditorUtility.SetDirty(visualizer);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Selection.activeGameObject = host;
        string action = created || componentAdded || referencesChanged ? "Installed" : "Already configured";
        Debug.Log("[GpuAudioParticleSceneInstaller] " + action
            + " 3,072 camera-wrapping, Screen-occluded GPU stardust particles in " + ScenePath, host);
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
            throw new System.InvalidOperationException("GpuAudioParticleVisualizer must remain under Screen.");
        }

        if (visualizer.audioVisualizer == null || visualizer.targetCamera == null)
        {
            throw new System.InvalidOperationException("GpuAudioParticleVisualizer audio or camera reference is incomplete.");
        }

        if (visualizer.occlusionScreen == null
            || !visualizer.occlusionScreen.IsChildOf(visualizer.transform.parent))
        {
            throw new System.InvalidOperationException("GpuAudioParticleVisualizer Screen occlusion plane is missing or invalid.");
        }

        if (visualizer.particleCompute == null || visualizer.particleShader == null || !visualizer.particleShader.isSupported)
        {
            throw new System.InvalidOperationException("GpuAudioParticleVisualizer GPU assets are missing or unsupported by the active editor graphics device.");
        }

        if (visualizer.ConfiguredParticleCount != ExpectedParticleCount
            || (visualizer.TotalParticleCount != 0 && visualizer.TotalParticleCount != ExpectedParticleCount))
        {
            throw new System.InvalidOperationException("GPU stardust must contain exactly 3,072 background particles.");
        }

        Debug.Log("[GpuAudioParticleSceneInstaller] Validation passed: camera-centered spherical volume,"
            + " Screen occlusion, ComputeShader, URP shader, and 3,072 background particles are ready.", visualizer);
    }

    private static Camera FindActiveCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            return mainCamera;
        }

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i].isActiveAndEnabled)
            {
                return cameras[i];
            }
        }

        return null;
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
