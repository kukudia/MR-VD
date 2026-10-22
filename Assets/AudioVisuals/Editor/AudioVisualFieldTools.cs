using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Authoring entrypoints for native visual assets, an isolated demonstration scene and target-scene
/// installation. Existing themes/prefabs are preserved; mutations use Undo and Unity serialization.
/// RenderContactSheet uses a private preview scene and never modifies the user's loaded scene.
/// </summary>
public static class AudioVisualFieldTools
{
    public const string Root = "Assets/AudioVisuals";
    public const string PrefabPath = Root + "/Prefabs/SpatialAudioField.prefab";
    public const string LaboratoryPath = "Assets/Scenes/AudioVisualLaboratory.unity";
    public const string PreviewDirectory = "Temp/AudioVisualResearch";
    public static readonly string[] ThemeNames = { "Aurora", "Nebula", "Solar", "Jade" };

    [MenuItem("Tools/MR-VD/Spatial Audio/Build Missing Assets and Laboratory")]
    public static void BuildAssets()
    {
        EnsureFolder(Root + "/Themes");
        EnsureFolder(Root + "/Materials");
        EnsureFolder(Root + "/Prefabs");
        Color[] primary = { Hex("1688B8"), Hex("7041C7"), Hex("CD7325"), Hex("21997B") };
        Color[] secondary = { Hex("6845AF"), Hex("BB4D94"), Hex("BC414E"), Hex("276EAD") };
        Color[] accent = { Hex("9BE7F2"), Hex("D9B5F1"), Hex("F5D294"), Hex("A0E3C4") };
        for (int i = 0; i < ThemeNames.Length; i++)
        {
            string path = ThemePath(i);
            if (AssetDatabase.LoadAssetAtPath<AudioVisualTheme>(path) != null)
            {
                continue;
            }
            AudioVisualTheme theme = ScriptableObject.CreateInstance<AudioVisualTheme>();
            theme.primary = primary[i];
            theme.secondary = secondary[i];
            theme.accent = accent[i];
            AssetDatabase.CreateAsset(theme, path);
        }
        Material material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/SpatialAudio.mat");
        if (material == null)
        {
            Shader shader = Shader.Find("MR-VD/Spatial Audio Surface");
            if (shader == null)
            {
                throw new InvalidOperationException("Spatial audio Shader has not imported.");
            }
            material = new Material(shader) { name = "SpatialAudio" };
            AssetDatabase.CreateAsset(material, Root + "/Materials/SpatialAudio.mat");
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            GameObject root = new GameObject("Spatial Audio Field");
            try
            {
                AudioVisualField field = root.AddComponent<AudioVisualField>();
                AudioAuroraRibbons ribbons = CreateEffect<AudioAuroraRibbons>("Aurora Ribbons", root.transform, material);
                AudioBeatRings rings = CreateEffect<AudioBeatRings>("Beat Rings", root.transform, material);
                AudioSpectrumTerrain terrain = CreateEffect<AudioSpectrumTerrain>("Spectrum History", root.transform, material);
                field.Configure(null, null, AssetDatabase.LoadAssetAtPath<AudioVisualTheme>(ThemePath(0)),
                    ribbons, rings, terrain, false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
        AssetDatabase.SaveAssets();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LaboratoryPath) == null)
        {
            BuildLaboratory();
        }
        Debug.Log("[SpatialAudio] Editable themes, material, prefab and laboratory are ready; existing assets preserved.");
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Install or Repair Active Scene")]
    public static void InstallActiveScene()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Install outside Play Mode.");
        }
        Scene scene = SceneManager.GetActiveScene();
        AudioVisualizer audio = scene.GetRootGameObjects()
            .SelectMany(x => x.GetComponentsInChildren<AudioVisualizer>(true)).FirstOrDefault();
        if (audio == null)
        {
            throw new InvalidOperationException("Active scene requires its own AudioVisualizer.");
        }
        Transform screen = audio.transform.parent;
        RectTransform reading = screen.GetComponentsInChildren<Canvas>(true)
            .Where(x => x.renderMode == RenderMode.WorldSpace)
            .Select(x => x.transform as RectTransform).FirstOrDefault(x => x != null && x.rect.width > 0.5f);
        if (reading == null)
        {
            throw new InvalidOperationException("Could not resolve the authored world-space reading screen.");
        }
        BuildAssets();
        AudioVisualField existing = screen.GetComponentInChildren<AudioVisualField>(true);
        GameObject instance = existing != null ? existing.gameObject
            : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(instance, "Install Spatial Audio Field");
            instance.transform.SetParent(screen, false);
        }
        AudioVisualField field = instance.GetComponent<AudioVisualField>();
        Undo.RecordObject(field, "Bind Spatial Audio Sources");
        SerializedObject data = new SerializedObject(field);
        data.FindProperty("audioVisualizer").objectReferenceValue = audio;
        data.FindProperty("readingScreen").objectReferenceValue = reading;
        data.FindProperty("stage").objectReferenceValue = scene.GetRootGameObjects()
            .SelectMany(x => x.GetComponentsInChildren<StageManager>(true)).FirstOrDefault();
        data.FindProperty("demonstration").boolValue = false;
        data.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(field);
        GpuAudioParticleVisualizer particles = screen.GetComponentInChildren<GpuAudioParticleVisualizer>(true);
        if (particles != null)
        {
            Undo.RecordObject(particles, "Bind Spatial Particle Composition");
            SerializedObject particleData = new SerializedObject(particles);
            particleData.FindProperty("spatialField").objectReferenceValue = field;
            particleData.FindProperty("formationAnchor").objectReferenceValue = screen;
            particleData.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(particles);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = instance;
        Debug.Log("[SpatialAudio] Installed in " + scene.path + ". Save explicitly after reviewing the scene.", instance);
    }

    private static void BuildLaboratory()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene lab = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            GameObject cameraObject = new GameObject("Laboratory Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, lab);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.012f, 0.028f);
            camera.fieldOfView = 78f;
            camera.farClipPlane = 40f;
            camera.transform.position = new Vector3(0f, 0.1f, -1.5f);
            camera.allowHDR = true;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), lab);
            AudioVisualField field = instance.GetComponent<AudioVisualField>();
            SerializedObject data = new SerializedObject(field);
            data.FindProperty("demonstration").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            AddLaboratoryParticles(lab, field, camera);
            GameObject notice = new GameObject("DEMONSTRATION INPUT - synthetic audio, not live analysis");
            SceneManager.MoveGameObjectToScene(notice, lab);
            EditorSceneManager.SaveScene(lab, LaboratoryPath);
        }
        finally
        {
            EditorSceneManager.CloseScene(lab, true);
            SceneManager.SetActiveScene(previous);
        }
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Add Missing Laboratory Particles")]
    public static void UpgradeLaboratory()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Upgrade outside Play Mode.");
        }
        Scene previous = SceneManager.GetActiveScene();
        Scene lab = SceneManager.GetSceneByPath(LaboratoryPath);
        bool opened = !lab.isLoaded;
        if (opened)
        {
            lab = EditorSceneManager.OpenScene(LaboratoryPath, OpenSceneMode.Additive);
        }
        try
        {
            AudioVisualField field = lab.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<AudioVisualField>(true)).First();
            Camera camera = lab.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<Camera>(true)).First();
            if (!lab.GetRootGameObjects().Any(x => x.GetComponent<GpuAudioParticleVisualizer>() != null))
            {
                AddLaboratoryParticles(lab, field, camera);
                EditorSceneManager.SaveScene(lab);
            }
        }
        finally
        {
            if (opened)
            {
                EditorSceneManager.CloseScene(lab, true);
            }
            SceneManager.SetActiveScene(previous);
        }
    }

    private static void AddLaboratoryParticles(Scene scene, AudioVisualField field, Camera camera)
    {
        GameObject host = new GameObject("Spatial Formation - synthetic input");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.transform.position = new Vector3(0f, 0.1f, 3.2f);
        host.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        GpuAudioParticleVisualizer particles = host.AddComponent<GpuAudioParticleVisualizer>();
        particles.targetCamera = camera;
        particles.particleCount = 4096;
        particles.volumeRadius = 8f;
        particles.minimumDistance = 1f;
        particles.particleSize = 0.012f;
        particles.opacity = 0.65f;
        particles.particleCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/AudioReactiveParticles/Shaders/GpuAudioParticles.compute");
        particles.particleShader = Shader.Find("MR-VD/Audio Reactive GPU Stardust");
        particles.SetFormation(GpuAudioParticleVisualizer.ParticleFormation.SpiralGalaxy);
        SerializedObject data = new SerializedObject(particles);
        data.FindProperty("spatialField").objectReferenceValue = field;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Render Theme Contact Sheet")]
    public static void RenderContactSheet()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Render theme contact sheets outside Play Mode.");
        }
        BuildAssets();
        Directory.CreateDirectory(PreviewDirectory);
        const int width = 960;
        const int height = 600;
        Texture2D sheet = new Texture2D(width * 2, height * 2, TextureFormat.RGB24, false);
        try
        {
            for (int variant = 0; variant < ThemeNames.Length; variant++)
            {
                PreviewRenderUtility preview = new PreviewRenderUtility();
                AudioVisualField previewField = null;
                try
                {
                    preview.camera.fieldOfView = 78f;
                    preview.camera.transform.position = new Vector3(0f, 0.1f, -1.5f);
                    preview.camera.transform.rotation = Quaternion.identity;
                    preview.camera.clearFlags = CameraClearFlags.SolidColor;
                    preview.camera.backgroundColor = new Color(0.008f, 0.012f, 0.028f);
                    preview.camera.farClipPlane = 40f;
                    GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                    preview.AddSingleGO(root);
                    AudioVisualField field = root.GetComponent<AudioVisualField>();
                    previewField = field;
                    SerializedObject data = new SerializedObject(field);
                    data.FindProperty("demonstration").boolValue = true;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    field.SetTheme(AssetDatabase.LoadAssetAtPath<AudioVisualTheme>(ThemePath(variant)));
                    for (int frame = 0; frame < 150; frame++)
                    {
                        field.PreviewDemonstration(frame / 30f, 1f / 30f);
                    }
                    preview.BeginPreview(new Rect(0, 0, width, height), GUIStyle.none);
                    preview.Render(true);
                    RenderTexture rendered = (RenderTexture)preview.EndPreview();
                    RenderTexture scaled = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                    RenderTexture previous = RenderTexture.active;
                    try
                    {
                        // Preview rendering follows Editor DPI; normalize pixels before building the sheet.
                        Graphics.Blit(rendered, scaled);
                        RenderTexture.active = scaled;
                        sheet.ReadPixels(new Rect(0, 0, width, height), variant % 2 * width, (1 - variant / 2) * height);
                    }
                    finally
                    {
                        RenderTexture.active = previous;
                        RenderTexture.ReleaseTemporary(scaled);
                    }
                }
                finally
                {
                    if (previewField != null)
                    {
                        previewField.ReleasePreviewResources();
                    }
                    preview.Cleanup();
                }
            }
            sheet.Apply();
            File.WriteAllBytes(PreviewDirectory + "/theme-contact-sheet.png", sheet.EncodeToPNG());
            Debug.Log("[SpatialAudio] Rendered synthetic-input themes to " + PreviewDirectory + "/theme-contact-sheet.png");
        }
        finally
        {
            Object.DestroyImmediate(sheet);
        }
    }

    public static string ThemePath(int index)
    {
        return Root + "/Themes/" + ThemeNames[index] + ".asset";
    }

    private static T CreateEffect<T>(string name, Transform parent, Material material) where T : AudioVisualMesh
    {
        GameObject host = new GameObject(name);
        host.transform.SetParent(parent, false);
        T effect = host.AddComponent<T>();
        host.GetComponent<MeshRenderer>().sharedMaterial = material;
        return effect;
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out Color color);
        return color;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
