using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

/// <summary>
/// Creates an isolated native scene with the project's four existing VFX graphs. Parameters and
/// transforms remain editable; creation never enables the disabled production graph objects.
/// ValidatePropertyWrites checks the actual exposed graph contract using isolated components.
/// </summary>
public static class AudioGraphLaboratoryTools
{
    public const string ScenePath = "Assets/Scenes/AudioGraphLaboratory.unity";
    private static readonly string[] GraphNames = { "BackgroundParticles", "SmokeEffect", "LaserBeamEffect", "GroundRingEffect" };
    private static readonly string[] Fields = { "backgroundParticles", "smokeEffect", "laserBeamEffect", "groundRingEffect" };
    private static readonly string[] FloatProperties = { "SpawnRate", "Density", "BeamIntensity", "RingExpansion" };

    [MenuItem("Tools/MR-VD/Spatial Audio/Build Missing VFX Graph Laboratory")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Build outside Play Mode.");
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            Debug.Log("[AudioGraphLaboratory] Existing scene preserved: " + ScenePath);
            return;
        }
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            GameObject cameraHost = new GameObject("Graph Laboratory Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraHost, scene);
            Camera camera = cameraHost.GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 1.2f, -7f);
            camera.transform.rotation = Quaternion.Euler(5f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.012f, 0.028f);
            camera.fieldOfView = 70f;
            camera.farClipPlane = 80f;
            GameObject fieldObject = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(AudioVisualFieldTools.PrefabPath), scene);
            AudioVisualField field = fieldObject.GetComponent<AudioVisualField>();
            SerializedObject fieldData = new SerializedObject(field);
            fieldData.FindProperty("demonstration").boolValue = true;
            fieldData.FindProperty("intensity").floatValue = 0.35f;
            fieldData.ApplyModifiedPropertiesWithoutUndo();
            GameObject controllerHost = new GameObject("Graph Composition - SYNTHETIC INPUT", typeof(AudioVisualEffectsController));
            SceneManager.MoveGameObjectToScene(controllerHost, scene);
            SerializedObject data = new SerializedObject(controllerHost.GetComponent<AudioVisualEffectsController>());
            data.FindProperty("spatialField").objectReferenceValue = field;
            data.FindProperty("targetCamera").objectReferenceValue = camera;
            data.FindProperty("particleSpawnRate").floatValue = 45f;
            data.FindProperty("smokeDensity").floatValue = 0.06f;
            data.FindProperty("laserIntensity").floatValue = 0.4f;
            data.FindProperty("masterIntensity").floatValue = 0.7f;
            data.FindProperty("vfx").FindPropertyRelative("triggerBeatEvents").boolValue = false;
            for (int i = 0; i < GraphNames.Length; i++)
            {
                GameObject graphHost = new GameObject(GraphNames[i], typeof(VisualEffect));
                graphHost.transform.SetParent(controllerHost.transform, false);
                VisualEffect effect = graphHost.GetComponent<VisualEffect>();
                effect.visualEffectAsset = LoadGraph(i);
                effect.startSeed = (uint)(1409 + i * 127);
                effect.resetSeedOnPlay = false;
                graphHost.transform.position = new Vector3((i - 1.5f) * 3.3f, i == 1 ? 0.4f : -0.5f, 2f + i * 0.4f);
                if (i == 1)
                {
                    graphHost.transform.localScale = new Vector3(0.8f, 0.12f, 0.4f);
                }
                data.FindProperty(Fields[i]).objectReferenceValue = effect;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(previous);
        }
        Debug.Log("[AudioGraphLaboratory] Saved explicit synthetic-input scene: " + ScenePath);
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Validate Existing VFX Property Writes")]
    public static void ValidatePropertyWrites()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Run outside Play Mode.");
        }
        GameObject host = new GameObject("Graph property validation - isolated");
        GameObject fieldHost = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AudioVisualFieldTools.PrefabPath));
        try
        {
            AudioVisualEffectsController controller = host.AddComponent<AudioVisualEffectsController>();
            AudioVisualField field = fieldHost.GetComponent<AudioVisualField>();
            SerializedObject data = new SerializedObject(controller);
            data.FindProperty("spatialField").objectReferenceValue = field;
            data.FindProperty("graphPaletteAmount").floatValue = 1f;
            VisualEffect[] effects = new VisualEffect[4];
            for (int i = 0; i < effects.Length; i++)
            {
                GameObject graph = new GameObject(GraphNames[i], typeof(VisualEffect));
                graph.transform.SetParent(host.transform, false);
                effects[i] = graph.GetComponent<VisualEffect>();
                effects[i].visualEffectAsset = LoadGraph(i);
                Require(effects[i].HasFloat(FloatProperties[i]), GraphNames[i] + " missing " + FloatProperties[i]);
                data.FindProperty(Fields[i]).objectReferenceValue = effects[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            var write = typeof(AudioVisualEffectsController).GetMethod("WriteGraphFrame",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            StageManager.PresentationFrame presentation = new StageManager.PresentationFrame
            {
                bass = 0.6f, synth = 0.5f, energy = 0.55f, visibility = 1f,
                smoke = 1f, laser = 0.6f, color = Color.white, bpm = 120f
            };
            field.RenderFrame(new AudioVisualField.Frame { deltaTime = 1f / 60f, spectralShape = Vector4.zero });
            write.Invoke(controller, new object[] { presentation });
            float spawn = effects[0].GetFloat("SpawnRate");
            float density = effects[1].GetFloat("Density");
            Require(density > 1f && density <= 120f, "Smoke intensity must be converted to bounded particles per second.");
            float beam = effects[2].GetFloat("BeamIntensity");
            field.RenderFrame(new AudioVisualField.Frame
            {
                deltaTime = 1f / 60f, spectralShape = Vector4.one, onset = true, onsetStrength = 1f
            });
            write.Invoke(controller, new object[] { presentation });
            Require(effects[0].GetFloat("SpawnRate") > spawn, "Spectral dust response missing.");
            Require(effects[1].GetFloat("Density") > density, "Flatness smoke response missing.");
            Require(effects[2].GetFloat("BeamIntensity") > beam, "Positive-change beam response missing.");
            Vector4 color = effects[0].GetVector4("ParticleColor");
            Require(Vector4.Distance(color, field.DisplayedTheme.primary) < 0.001f, "Graph palette not coordinated.");
            presentation.visibility = 0f;
            write.Invoke(controller, new object[] { presentation });
            for (int i = 0; i < effects.Length; i++)
            {
                Require(effects[i].GetFloat(FloatProperties[i]) == 0f, "Blackout did not zero " + GraphNames[i]);
            }
            Debug.Log("[AudioGraphLaboratory] PASS: four exposed graph contracts, spectral response, palette and blackout writes. "
                + "Property validation only; not an image or XR performance test.");
        }
        finally
        {
            fieldHost.GetComponent<AudioVisualField>().ReleasePreviewResources();
            Object.DestroyImmediate(fieldHost);
            Object.DestroyImmediate(host);
        }
    }

    private static VisualEffectAsset LoadGraph(int index)
    {
        VisualEffectAsset asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/VFX/" + GraphNames[index] + ".vfx");
        if (asset == null)
        {
            throw new InvalidOperationException("Missing project graph: " + GraphNames[index]);
        }
        return asset;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
