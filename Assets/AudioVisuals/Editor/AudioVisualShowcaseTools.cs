using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Creates missing cinematic showcase assets only. Existing looks, post profile and scene are
/// preserved, so artists can edit each native asset without regeneration overwriting their work.
/// </summary>
public static class AudioVisualShowcaseTools
{
    public const string ScenePath = "Assets/Scenes/AudioVisualShowcase.unity";
    public const string LookRoot = AudioVisualFieldTools.Root + "/Looks";
    public const string ProfilePath = LookRoot + "/ShowcasePost.asset";
    public const string PipelinePath = LookRoot + "/ShowcaseHDRPipeline.asset";
    private static readonly string[] Names = { "Aurora Cathedral", "Solar Percussion", "Jade Orbit", "Violet Knot", "Opal Iris", "Amber Flow" };

    [MenuItem("Tools/MR-VD/Spatial Audio/Build Missing Cinematic Showcase")]
    public static void BuildMissing()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Build outside Play Mode.");
        }
        AudioVisualFieldTools.BuildAssets();
        if (!AssetDatabase.IsValidFolder(LookRoot))
        {
            AssetDatabase.CreateFolder(AudioVisualFieldTools.Root, "Looks");
        }
        for (int i = 0; i < Names.Length; i++)
        {
            BuildLook(i);
        }
        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            UniversalRenderPipelineAsset source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (source == null)
            {
                throw new InvalidOperationException("Showcase requires the project's existing URP pipeline.");
            }
            pipeline = Object.Instantiate(source);
            pipeline.name = "Showcase HDR Pipeline";
            pipeline.supportsHDR = true;
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
        }
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 0.25f;
            bloom.intensity.value = 0.65f;
            bloom.scatter.value = 0.6f;
            bloom.highQualityFiltering.value = true;
            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.ACES;
            ColorAdjustments grading = profile.Add<ColorAdjustments>(true);
            grading.postExposure.value = 1.1f;
            foreach (VolumeComponent component in profile.components)
            {
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            BuildScene(profile, pipeline);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[AudioShowcase] Editable looks, post profile and synthetic showcase saved; existing assets preserved.");
    }

    private static void BuildLook(int index)
    {
        string themePath = LookRoot + "/" + Names[index] + " Theme.asset";
        AudioVisualTheme theme = AssetDatabase.LoadAssetAtPath<AudioVisualTheme>(themePath);
        if (theme == null)
        {
            theme = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AudioVisualTheme>(AudioVisualFieldTools.ThemePath(index == 1 ? 2 : index == 2 ? 3 : index == 3 ? 1 : 0)));
            theme.name = Names[index] + " Theme";
            theme.emission = index == 1 ? 2.5f : index == 4 ? 1.25f : 2.2f;
            theme.ribbons = index == 0 ? 0.85f : 0.45f;
            theme.canopy = index == 0 ? 0.85f : 0.55f;
            theme.lattice = index == 2 ? 0.85f : 0.6f;
            theme.terrain = index == 1 ? 0.65f : 0.25f;
            theme.rings = index == 1 ? 0.95f : 0.35f;
            theme.ringSpectralRelief = index == 1 ? 0.35f : 0f;
            theme.motion = index == 0 ? 0.35f : 0.8f;
            theme.sparkle = index == 1 ? 0.7f : 0.45f;
            if (index == 4)
            {
                theme.primary = new Color(0.12f, 0.65f, 0.72f);
                theme.secondary = new Color(0.8f, 0.3f, 0.55f);
                theme.accent = new Color(0.85f, 0.95f, 0.75f);
                theme.iris = 1f;
                theme.irisIridescence = 0.65f;
                theme.irisFilmCycles = 2.4f;
                theme.irisEdgeLight = 0.3f;
                theme.canopy = 0.15f;
                theme.lattice = 0.25f;
                theme.ribbons = 0.3f;
                theme.terrain = 0.2f;
                theme.rings = 0.15f;
                theme.motion = 0.6f;
            }
            if (index == 5)
            {
                theme.primary = new Color(0.9f, 0.4f, 0.12f);
                theme.secondary = new Color(0.16f, 0.5f, 0.72f);
                theme.emission = 1.65f;
                theme.ribbons = 0.22f;
                theme.canopy = 0.2f;
                theme.lattice = 0.65f;
                theme.terrain = 0.18f;
                theme.rings = 0.12f;
                theme.motion = 0.4f;
            }
            AssetDatabase.CreateAsset(theme, themePath);
        }
        string path = LookRoot + "/" + Names[index] + ".asset";
        if (AssetDatabase.LoadAssetAtPath<AudioVisualLook>(path) != null)
        {
            return;
        }
        AudioVisualLook look = ScriptableObject.CreateInstance<AudioVisualLook>();
        SerializedObject data = new SerializedObject(look);
        data.FindProperty("theme").objectReferenceValue = theme;
        data.FindProperty("composition").enumValueIndex = index == 4 ? (int)AudioVisualField.Composition.Sculpture
            : index == 1 ? (int)AudioVisualField.Composition.Pulse : (int)AudioVisualField.Composition.Balanced;
        data.FindProperty("formation").enumValueIndex = index == 5 ? (int)GpuAudioParticleVisualizer.ParticleFormation.CurlNebula
            : index == 3 ? (int)GpuAudioParticleVisualizer.ParticleFormation.FlowKnot
            : index == 2 ? (int)GpuAudioParticleVisualizer.ParticleFormation.Torus : (int)GpuAudioParticleVisualizer.ParticleFormation.SpiralGalaxy;
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(look, path);
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Validate Authored Showcase Looks")]
    public static void ValidateLooks()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Validate outside Play Mode.");
        }
        GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AudioVisualFieldTools.PrefabPath));
        GameObject particleHost = new GameObject("Isolated look validation");
        particleHost.SetActive(false);
        try
        {
            AudioVisualField field = root.GetComponent<AudioVisualField>();
            GpuAudioParticleVisualizer particles = particleHost.AddComponent<GpuAudioParticleVisualizer>();
            for (int i = 0; i < Names.Length; i++)
            {
                AudioVisualLook look = AssetDatabase.LoadAssetAtPath<AudioVisualLook>(LookRoot + "/" + Names[i] + ".asset");
                if (look == null)
                {
                    throw new InvalidOperationException("Missing look " + Names[i]);
                }
                look.Apply(field, particles);
                if (field.Theme == null || field.Theme.name != Names[i] + " Theme"
                    || particles.Formation != (i == 5 ? GpuAudioParticleVisualizer.ParticleFormation.CurlNebula
                    : i == 3 ? GpuAudioParticleVisualizer.ParticleFormation.FlowKnot
                    : i == 2 ? GpuAudioParticleVisualizer.ParticleFormation.Torus
                    : GpuAudioParticleVisualizer.ParticleFormation.SpiralGalaxy))
                {
                    throw new InvalidOperationException("Look wiring failed " + Names[i]);
                }
                if (i == 4 && (field.Theme.iris <= 0f
                    || ReadComposition(field) != AudioVisualField.Composition.Sculpture))
                {
                    throw new InvalidOperationException("Opal Iris is missing its sculptural composition tuning.");
                }
                string before = JsonUtility.ToJson(field.Theme);
                field.RenderFrame(new AudioVisualField.Frame { deltaTime = 0.05f });
                if (JsonUtility.ToJson(field.Theme) != before)
                {
                    throw new InvalidOperationException("Look mutated theme asset " + Names[i]);
                }
            }
            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (pipeline == null || !pipeline.supportsHDR || profile == null
                || !profile.TryGet(out Bloom bloom) || bloom.intensity.value <= 0f
                || !profile.TryGet(out Tonemapping tone) || tone.mode.value != TonemappingMode.ACES
                || !profile.TryGet(out ColorAdjustments grading) || grading.postExposure.value <= 0f)
            {
                throw new InvalidOperationException("Showcase HDR/post assets missing.");
            }
            Debug.Log("[AudioShowcase] PASS: six independent look bindings, private theme rendering, HDR/post assets.");
        }
        finally
        {
            root.GetComponent<AudioVisualField>().ReleasePreviewResources();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(particleHost);
        }
    }

    private static void BuildScene(VolumeProfile profile, UniversalRenderPipelineAsset pipeline)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            GameObject cameraHost = new GameObject("Cinematic Showcase Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            Camera camera = cameraHost.GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 0.2f, -2.8f);
            camera.fieldOfView = 72f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.002f, 0.003f, 0.007f);
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            GameObject volumeHost = new GameObject("Editable Showcase Post", typeof(Volume));
            SceneManager.MoveGameObjectToScene(volumeHost, scene);
            Volume volume = volumeHost.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            AudioShowcasePipelineScope scope = volumeHost.AddComponent<AudioShowcasePipelineScope>();
            SerializedObject scopeData = new SerializedObject(scope);
            scopeData.FindProperty("pipeline").objectReferenceValue = pipeline;
            scopeData.ApplyModifiedPropertiesWithoutUndo();
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AudioVisualFieldTools.PrefabPath), scene);
            AudioVisualField field = root.GetComponent<AudioVisualField>();
            SerializedObject fieldData = new SerializedObject(field);
            fieldData.FindProperty("demonstration").boolValue = true;
            fieldData.ApplyModifiedPropertiesWithoutUndo();
            AudioVisualFieldTools.AddLaboratoryParticles(scene, field, camera);
            GpuAudioParticleVisualizer particles = scene.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<GpuAudioParticleVisualizer>()).First();
            particles.motionStretch = 1.2f;
            SerializedObject particleData = new SerializedObject(particles);
            particleData.FindProperty("spectralBandResponse").floatValue = 0.6f;
            particleData.ApplyModifiedPropertiesWithoutUndo();
            GameObject playerHost = new GameObject("SYNTHETIC SHOWCASE - Authored Look", typeof(AudioVisualLookPlayer));
            SceneManager.MoveGameObjectToScene(playerHost, scene);
            SerializedObject player = new SerializedObject(playerHost.GetComponent<AudioVisualLookPlayer>());
            player.FindProperty("look").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioVisualLook>(LookRoot + "/" + Names[0] + ".asset");
            player.FindProperty("field").objectReferenceValue = field;
            player.FindProperty("particles").objectReferenceValue = particles;
            player.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(previous);
        }
    }

    private static AudioVisualField.Composition ReadComposition(AudioVisualField field)
    {
        SerializedObject data = new SerializedObject(field);
        return (AudioVisualField.Composition)data.FindProperty("composition").enumValueIndex;
    }
}
