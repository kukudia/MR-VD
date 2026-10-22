using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

/// <summary>
/// Creates an independent native mist graph and two low-rate side emitters in the desktop
/// showcase. Existing graph assets and production scene activation remain authored separately.
/// Graph-model reflection is scoped to the installed VFX 17.3 editor API.
/// </summary>
public static class AudioShowcaseAtmosphereTools
{
    public const string GraphPath = AudioVisualFieldTools.Root + "/Graphs/ShowcaseMist.vfx";
    private const string RootName = "Desktop Showcase - Side Mist Detail";
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [MenuItem("Tools/MR-VD/Spatial Audio/Add Missing Showcase Side Mist")]
    public static void BuildMissing()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Build outside Play Mode.");
        }
        BuildGraph();
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(AudioVisualShowcaseTools.ScenePath);
        bool opened = !scene.isLoaded;
        if (opened)
        {
            scene = EditorSceneManager.OpenScene(AudioVisualShowcaseTools.ScenePath, OpenSceneMode.Additive);
        }
        try
        {
            if (scene.GetRootGameObjects().Any(x => x.name == RootName))
            {
                return;
            }
            AudioVisualField field = scene.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<AudioVisualField>()).Single();
            Camera camera = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Camera>()).Single();
            GameObject root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            for (int side = 0; side < 2; side++)
            {
                GameObject host = new GameObject(side == 0 ? "Left Mist" : "Right Mist");
                host.transform.SetParent(root.transform, false);
                host.transform.localPosition = new Vector3(side == 0 ? -2.9f : 2.9f, -0.75f, 3.4f);
                VisualEffect effect = host.AddComponent<VisualEffect>();
                effect.visualEffectAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath);
                effect.startSeed = (uint)(8457 + side * 137);
                effect.resetSeedOnPlay = false;
                AudioVisualEffectsController controller = host.AddComponent<AudioVisualEffectsController>();
                SerializedObject data = new SerializedObject(controller);
                data.FindProperty("spatialField").objectReferenceValue = field;
                data.FindProperty("targetCamera").objectReferenceValue = camera;
                data.FindProperty("smokeEffect").objectReferenceValue = effect;
                data.FindProperty("smokeDensity").floatValue = 0.14f;
                data.FindProperty("smokeParticlesPerSecond").floatValue = 80f;
                data.FindProperty("masterIntensity").floatValue = 0.5f;
                data.FindProperty("graphPaletteAmount").floatValue = 1f;
                data.FindProperty("graphSpectralAmount").floatValue = 0.3f;
                data.FindProperty("vfx").FindPropertyRelative("triggerBeatEvents").boolValue = false;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            SceneManager.SetActiveScene(previous);
        }
    }

    private static void BuildGraph()
    {
        if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) != null)
        {
            return;
        }
        if (!AssetDatabase.IsValidFolder(AudioVisualFieldTools.Root + "/Graphs"))
        {
            AssetDatabase.CreateFolder(AudioVisualFieldTools.Root, "Graphs");
        }
        if (!AssetDatabase.CopyAsset("Assets/VFX/SmokeEffect.vfx", GraphPath))
        {
            throw new InvalidOperationException("Cannot copy source mist graph.");
        }
        AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport);
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == "Unity.VisualEffectGraph.Editor");
        Type resourceType = Type.GetType("UnityEditor.VFX.VisualEffectResource, UnityEditor.VFXModule", true);
        object resource = resourceType.GetMethod("GetResourceAtPath", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { GraphPath });
        Type extensions = assembly.GetType("UnityEditor.VFX.VisualEffectResourceExtensions", true);
        object graph = extensions.GetMethod("GetOrCreateGraph").Invoke(null, new[] { resource });
        int changed = 0;
        foreach (object node in (IEnumerable)Get(graph, "children"))
        {
            if (!node.GetType().Name.Contains("Output"))
            {
                continue;
            }
            foreach (object block in (IEnumerable)Get(node, "children"))
            {
                if (Get(block, "inputSlots") is not IEnumerable slots)
                {
                    continue;
                }
                foreach (object slot in slots)
                {
                    if ((string)Get(slot, "name") == "_Size")
                    {
                        Property(slot, "value").SetValue(slot, 0.65f);
                        EditorUtility.SetDirty((Object)slot);
                        changed++;
                    }
                }
            }
        }
        if (changed != 1)
        {
            throw new InvalidOperationException("Expected exactly one output particle-size block, got " + changed);
        }
        extensions.GetMethod("WriteAssetWithSubAssets").Invoke(null, new[] { resource });
        AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
    }

    private static object Get(object value, string name) => Property(value, name)?.GetValue(value);

    private static PropertyInfo Property(object value, string name)
    {
        // Some VFX model subclasses hide an inherited property; GetProperty alone is ambiguous.
        return value.GetType().GetProperties(Instance)
            .FirstOrDefault(p => p.Name == name && p.GetIndexParameters().Length == 0);
    }
}
