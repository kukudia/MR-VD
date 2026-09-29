using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

/// <summary>Creates editable assets and migrates serialized stage VFX through Unity APIs.</summary>
public static class AudioVisualEffectsInstaller
{
    public const string ArtRoot = "Assets/Art/AudioVisualEffects";
    private static readonly string[] Variants = { "AuroraCyan", "NebulaViolet", "SolarAmber", "JadeEmerald" };
    private static readonly Color[] Starts = {
        new Color(0.3f, 0.95f, 1f), new Color(1f, 0.36f, 0.8f),
        new Color(1f, 0.85f, 0.3f), new Color(0.35f, 1f, 0.65f) };
    private static readonly Color[] Ends = {
        new Color(0.15f, 0.25f, 1f), new Color(0.32f, 0.1f, 1f),
        new Color(1f, 0.12f, 0.025f), new Color(0.02f, 0.45f, 0.8f) };

    [MenuItem("Tools/MR-VD/Audio Effects/Install or Repair Active Scene")]
    public static void InstallActiveScene()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Install in Edit Mode.");
        BuildMissingAssets();
        Install(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/MR-VD/Audio Effects/Boost Installed Impact Assets")]
    public static void BoostInstalledImpactAssets()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Update impact assets in Edit Mode.");
        foreach (string variant in Variants)
        {
            string path = ArtRoot + "/" + variant + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var systems = root.GetComponentsInChildren<ParticleSystem>(true);
                if (systems.Length != 2) throw new InvalidOperationException("Incomplete meteor prefab: " + path);
                var heads = systems.First(x => x.name == "Meteors").main;
                heads.maxParticles = Mathf.Max(heads.maxParticles, 144);
                var sparks = systems.First(x => x.name == "Burst Sparks").main;
                sparks.maxParticles = Mathf.Max(sparks.maxParticles, 256);
                foreach (ParticleSystem system in systems) ConfigureImpactEnvelope(system);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/MeteorGlow.mat");
        if (material != null)
        {
            Undo.RecordObject(material, "Brighten XR meteor glow");
            material.SetFloat("_Emission", 4.5f);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[AudioEffects] Strengthened four meteor variants and raised particle capacity for per-beat bursts.");
    }

    private static void ConfigureImpactEnvelope(ParticleSystem ps)
    {
        var color = ps.colorOverLifetime;
        // Keep each authored palette; replace only the slow fade-in and soft attack envelope.
        Gradient gradient = color.color.gradient;
        gradient.SetKeys(gradient.colorKeys, new[] {
            new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(1f, 0.025f),
            new GradientAlphaKey(0.7f, 0.28f), new GradientAlphaKey(0.18f, 0.7f), new GradientAlphaKey(0f, 1f)
        });
        color.color = gradient;
        var size = ps.sizeOverLifetime;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.12f, 0.9f), new Keyframe(1f, 0.15f)));
    }

    [MenuItem("Tools/MR-VD/Audio Effects/Migrate Project Scenes")]
    public static void MigrateProjectScenes()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Migrate in Edit Mode.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before project migration.");
        BuildMissingAssets();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string path in new[] { "Assets/Scenes/v203.0.0.unity" })
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Install(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        AssetDatabase.SaveAssets();
        Debug.Log("[AudioEffects] Migrated the stage scene and saved four editable meteor prefabs.");
    }

    private static void Install(Scene scene)
    {
        var stages = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<StageManager>(true)).ToArray();
        foreach (StageManager stage in stages)
        {
            var existing = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<AudioVisualEffectsController>(true))
                .FirstOrDefault(x => new SerializedObject(x).FindProperty("stage").objectReferenceValue == stage);
            bool created = existing == null;
            AudioVisualEffectsController controller = existing;
            if (created)
            {
                var host = new GameObject("Audio Visual Effects");
                SceneManager.MoveGameObjectToScene(host, scene);
                Undo.RegisterCreatedObjectUndo(host, "Install audio effects");
                controller = Undo.AddComponent<AudioVisualEffectsController>(host);
            }
            var destination = new SerializedObject(controller);
            var source = new SerializedObject(stage);
            destination.FindProperty("stage").objectReferenceValue = stage;
            if (created)
            {
                foreach (string name in new[] { "audioVisualizer", "backgroundParticles", "smokeEffect", "laserBeamEffect", "groundRingEffect" })
                {
                    SerializedProperty property = source.FindProperty(name);
                    if (property != null) destination.FindProperty(name).objectReferenceValue = property.objectReferenceValue;
                    else if (name != "audioVisualizer") destination.FindProperty(name).objectReferenceValue = stage.GetComponentsInChildren<VisualEffect>(true)
                        .FirstOrDefault(x => string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase));
                }
                foreach (string name in new[] { "particleSpawnRate", "smokeDensity", "laserIntensity" })
                {
                    SerializedProperty property = source.FindProperty(name);
                    if (property != null) destination.FindProperty(name).floatValue = property.floatValue;
                }
                var oldSettings = source.FindProperty("vfx");
                if (oldSettings != null)
                {
                    foreach (string name in new[] { "enableVFX", "sendCommonParameters", "triggerBeatEvents" })
                        destination.FindProperty("vfx." + name).boolValue = oldSettings.FindPropertyRelative(name).boolValue;
                    foreach (string name in new[] { "backgroundGain", "smokeGain", "laserBeamGain", "groundRingGain", "maxSpawnMultiplier", "maxRingExpansion" })
                        destination.FindProperty("vfx." + name).floatValue = oldSettings.FindPropertyRelative(name).floatValue;
                }
                var oldMaster = source.FindProperty("runtime.vfxMaster");
                if (oldMaster != null) destination.FindProperty("masterIntensity").floatValue = oldMaster.floatValue;
                var graphs = stage.GetComponentsInChildren<VisualEffect>(true).Where(x => x.name != "BeatBurstEffect").ToArray();
                var extras = destination.FindProperty("additionalGraphs");
                var known = new[] { "backgroundParticles", "smokeEffect", "laserBeamEffect", "groundRingEffect" }
                    .Select(x => destination.FindProperty(x).objectReferenceValue).ToArray();
                graphs = graphs.Where(x => !known.Contains(x)).ToArray();
                extras.arraySize = graphs.Length;
                for (int i = 0; i < graphs.Length; i++) extras.GetArrayElementAtIndex(i).objectReferenceValue = graphs[i];
            }
            AudioVisualizer audio = stage.audioVisualizer != null ? stage.audioVisualizer
                : scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<AudioVisualizer>(true)).FirstOrDefault();
            destination.FindProperty("audioVisualizer").objectReferenceValue = audio;
            Camera camera = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(x => x.isActiveAndEnabled && x.CompareTag("MainCamera"));
            if (camera == null) camera = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Camera>(true)).FirstOrDefault(x => x.isActiveAndEnabled);
            destination.FindProperty("targetCamera").objectReferenceValue = camera;
            var variants = destination.FindProperty("meteorVariants");
            if (created || variants.arraySize == 0)
            {
                variants.arraySize = Variants.Length;
                for (int i = 0; i < Variants.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/" + Variants[i] + ".prefab");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, controller.transform);
                    Undo.RegisterCreatedObjectUndo(instance, "Add meteor variant");
                    variants.GetArrayElementAtIndex(i).objectReferenceValue = instance.GetComponent<XrBeatMeteorEffect>();
                }
            }
            destination.ApplyModifiedProperties();
            // Remove every reference to the retired graph, including inactive instances outside Stage.
            foreach (VisualEffect effect in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<VisualEffect>(true)))
            {
                if (effect.visualEffectAsset == null || AssetDatabase.GetAssetPath(effect.visualEffectAsset) != "Assets/VFX/BeatBurstEffect.vfx") continue;
                Undo.DestroyObjectImmediate(effect);
            }
            foreach (Transform oldHost in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)))
            {
                if (oldHost.name == "BeatBurstEffect" && oldHost.childCount == 0 && oldHost.GetComponents<Component>().Length == 1)
                    Undo.DestroyObjectImmediate(oldHost.gameObject);
            }
            // During one-time migration the old fields still exist; prevent their runtime auto-creation.
            source.Update();
            var complete = source.FindProperty("vfx.autoCompleteGraphBindings");
            if (complete != null) complete.boolValue = false;
            var enabled = source.FindProperty("vfx.enableVFX");
            if (enabled != null) enabled.boolValue = false;
            source.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }
        foreach (var gpu in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<GpuAudioParticleVisualizer>(true)))
        {
            // Upgrade only the old neutral default, preserving subsequent authored gradients and brightness.
            var colors = gpu.particleGradient.colorKeys;
            if (colors.All(x => x.color == Color.white))
            {
                Undo.RecordObject(gpu, "Upgrade stardust gradient");
                gpu.particleGradient = MakeGradient(new Color(0.12f, 0.45f, 1f), new Color(1f, 0.25f, 0.58f), false);
                gpu.emission = 3.2f;
                gpu.particleSize = 0.018f;
                EditorUtility.SetDirty(gpu);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    public static void BuildMissingAssets()
    {
        EnsureFolder("Assets/Art"); EnsureFolder(ArtRoot);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/AudioReactiveParticles/Shaders/XrMeteor.shader");
        if (shader == null) throw new InvalidOperationException("Meteor shader has not imported.");
        string materialPath = ArtRoot + "/MeteorGlow.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MeteorGlow", enableInstancing = true };
            material.SetFloat("_Emission", 4.5f);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        for (int i = 0; i < Variants.Length; i++)
        {
            string path = ArtRoot + "/" + Variants[i] + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
            var root = new GameObject(Variants[i]);
            try
            {
                var emitter = root.AddComponent<XrBeatMeteorEffect>();
                ParticleSystem heads = CreateParticles(root.transform, "Meteors", material, Starts[i], Ends[i], true);
                ParticleSystem sparks = CreateParticles(root.transform, "Burst Sparks", material, Starts[i], Ends[i], false);
                var data = new SerializedObject(emitter);
                data.FindProperty("meteors").objectReferenceValue = heads;
                data.FindProperty("sparks").objectReferenceValue = sparks;
                data.FindProperty("seed").intValue = 917 + i * 101;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        AssetDatabase.SaveAssets();
    }

    private static ParticleSystem CreateParticles(Transform parent, string name, Material material, Color start, Color end, bool trails)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.duration = 2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = trails ? 144 : 256;
        main.startSpeed = 0f; main.startLifetime = 1.1f; main.startSize = 0.06f;
        main.startColor = Color.white;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        var color = ps.colorOverLifetime; color.enabled = true;
        color.color = MakeGradient(start, end, true);
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.65f), new Keyframe(0.12f, 1f), new Keyframe(1f, 0.2f)));
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        if (trails)
        {
            var trail = ps.trails;
            trail.enabled = true; trail.ratio = 1f; trail.lifetime = 0.3f;
            trail.worldSpace = true; trail.dieWithParticles = true;
            trail.minVertexDistance = 0.065f; trail.sizeAffectsWidth = true;
            trail.sizeAffectsLifetime = false;
            trail.widthOverTrail = new ParticleSystem.MinMaxCurve(0.75f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            trail.colorOverTrail = MakeGradient(start, end, true);
            trail.colorOverLifetime = MakeGradient(Color.white, Color.white, true);
            trail.inheritParticleColor = false;
            renderer.trailMaterial = material;
        }
        ConfigureImpactEnvelope(ps);
        return ps;
    }

    public static Gradient MakeGradient(Color start, Color end, bool fade)
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] {
            new GradientColorKey(start, 0f),
            new GradientColorKey(fade ? Color.Lerp(start, Color.white, 0.5f) : new Color(0.12f, 0.95f, 1f), 0.2f),
            new GradientColorKey(fade ? start : new Color(0.55f, 0.2f, 1f), 0.58f),
            new GradientColorKey(end, 1f)
        }, fade ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.8f, 0.65f), new GradientAlphaKey(0f, 1f) }
            : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    [MenuItem("Tools/MR-VD/Audio Effects/Validate Active Scene")]
    public static void Validate()
    {
        var controller = Object.FindFirstObjectByType<AudioVisualEffectsController>();
        if (controller == null || controller.VariantCount != 4) throw new InvalidOperationException("Four meteor variants are required.");
        var data = new SerializedObject(controller);
        if (data.FindProperty("audioVisualizer").objectReferenceValue == null || data.FindProperty("targetCamera").objectReferenceValue == null)
            throw new InvalidOperationException("Audio/camera binding missing.");
        foreach (string variant in Variants)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/" + variant + ".prefab");
            if (prefab == null || prefab.GetComponentsInChildren<ParticleSystem>().Length != 2) throw new InvalidOperationException("Incomplete prefab: " + variant);
        }
        foreach (var effect in Object.FindObjectsByType<VisualEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (effect.visualEffectAsset != null && AssetDatabase.GetAssetPath(effect.visualEffectAsset) == "Assets/VFX/BeatBurstEffect.vfx")
                throw new InvalidOperationException("Retired graph still referenced.");
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/AudioReactiveParticles/Shaders/XrMeteor.shader");
        if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Meteor shader compile error.");
        Debug.Log("[AudioEffects] Asset validation passed: four prefabs, audio/camera references, retired graph removed, shader compiled.");
    }
}
