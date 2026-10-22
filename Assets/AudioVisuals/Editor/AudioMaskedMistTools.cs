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
/// Creates an independent native VFX17.3 research graph through package model APIs. Source graph
/// remains untouched; existing masked assets are never regenerated. Reflection stays Editor-only.
/// </summary>
public static class AudioMaskedMistTools
{
    public const string GraphPath = AudioVisualFieldTools.Root + "/Graphs/MaskedMistResearch.vfx";
    public const string ScenePath = "Assets/Scenes/AudioMaskedMistResearch.unity";
    private const string ShaderPath = AudioVisualFieldTools.Root + "/Shaders/AudioMistClearance.hlsl";
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [MenuItem("Tools/MR-VD/Spatial Audio/Build Missing Masked Mist Research Graph")]
    public static void BuildMissing()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Create graph outside Play Mode.");
        }
        if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath) != null)
        {
            return;
        }
        if (!AssetDatabase.CopyAsset(AudioShowcaseAtmosphereTools.GraphPath, GraphPath))
        {
            throw new InvalidOperationException("Build the independent showcase mist graph first.");
        }
        try
        {
            ConfigureNewGraph();
        }
        catch
        {
            // This path was absent at entry and belongs to this failed creation only.
            AssetDatabase.DeleteAsset(GraphPath);
            throw;
        }
    }

    private static void ConfigureNewGraph()
    {
        AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport);
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == "Unity.VisualEffectGraph.Editor");
        Type resourceType = Type.GetType("UnityEditor.VFX.VisualEffectResource, UnityEditor.VFXModule", true);
        object resource = resourceType.GetMethod("GetResourceAtPath", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { GraphPath });
        Type extensions = assembly.GetType("UnityEditor.VFX.VisualEffectResourceExtensions", true);
        object graph = extensions.GetMethod("GetOrCreateGraph").Invoke(null, new[] { resource });
        object output = ((IEnumerable)Get(graph, "children")).Cast<object>()
            .Single(x => x.GetType().Name.Contains("Output"));
        Object block = ScriptableObject.CreateInstance(assembly.GetType("UnityEditor.VFX.Block.CustomHLSL", true));
        Set(block, "m_BlockName", "Desktop Reading Clearance (Research)");
        Set(block, "m_ShaderFile", AssetDatabase.LoadMainAssetAtPath(ShaderPath));
        Method(output, "AddChild", 3).Invoke(output, new object[] { block, -1, true });
        foreach (object slot in (IEnumerable)Get(block, "inputSlots"))
        {
            // CustomHLSL prefixes its generated input slots; expose a clean runtime contract.
            string name = ((string)Get(slot, "name")).TrimStart('_');
            object property = Get(slot, "property");
            Type type = (Type)Get(property, "type");
            Object parameter = ScriptableObject.CreateInstance(assembly.GetType("UnityEditor.VFX.VFXParameter", true));
            parameter.GetType().GetMethod("Init", Instance).Invoke(parameter, new object[] { type });
            Set(parameter, "m_ExposedName", name);
            Set(parameter, "m_Exposed", true);
            object value = name == "ClearanceVisibility" ? 0f
                : name == "ClearanceSettings" ? new Vector4(0f, 0.8f, 1.4f, 0.6f)
                : type == typeof(Matrix4x4) ? Matrix4x4.identity : Activator.CreateInstance(type);
            Property(parameter, "value").SetValue(parameter, value);
            Method(graph, "AddChild", 3).Invoke(graph, new object[] { parameter, -1, true });
            object source = ((IEnumerable)Get(parameter, "outputSlots")).Cast<object>().Single();
            bool linked = (bool)slot.GetType().GetMethod("Link", Instance).Invoke(slot, new[] { source, (object)true });
            if (!linked)
            {
                throw new InvalidOperationException("Cannot link clearance property " + name);
            }
        }
        extensions.GetMethod("WriteAssetWithSubAssets").Invoke(null, new[] { resource });
        AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        Debug.Log("[MaskedMist] Saved independent native graph with conservative desktop clearance. XR unverified.");
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Build Missing Masked Mist Research Scene")]
    public static void BuildScene()
    {
        BuildMissing();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            return;
        }
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            GameObject cameraHost = new GameObject("Desktop Research Camera", typeof(Camera));
            cameraHost.tag = "MainCamera";
            Camera camera = cameraHost.GetComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            GameObject screenHost = new GameObject("Invisible Reading Rectangle", typeof(RectTransform));
            RectTransform screen = screenHost.GetComponent<RectTransform>();
            screen.sizeDelta = new Vector2(1.2f, 0.8f);
            screen.position = Vector3.forward * 2f;
            for (int side = 0; side < 2; side++)
            {
                GameObject host = new GameObject(side == 0 ? "Center Mist Probe" : "Exterior Mist Probe");
                host.transform.position = new Vector3(side == 0 ? 0f : 2f, 0f, 3f);
                VisualEffect effect = host.AddComponent<VisualEffect>();
                effect.visualEffectAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath);
                effect.startSeed = (uint)(8457 + side);
                effect.resetSeedOnPlay = false;
                effect.SetFloat("Density", 40f);
                effect.SetVector4("SmokeColor", Vector4.one);
                effect.SetVector3("TransformPosition", host.transform.position);
                AudioMistClearanceBinding binding = host.AddComponent<AudioMistClearanceBinding>();
                SerializedObject data = new SerializedObject(binding);
                data.FindProperty("effect").objectReferenceValue = effect;
                data.FindProperty("eye").objectReferenceValue = camera;
                data.FindProperty("readingScreen").objectReferenceValue = screen;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(previous);
        }
    }

    private static void Set(object target, string name, object value)
    {
        Method(target, "SetSettingValue", 2).Invoke(target, new[] { name, value });
    }

    private static object Get(object value, string name) => Property(value, name)?.GetValue(value);

    private static MethodInfo Method(object value, string name, int count) => value.GetType().GetMethods(Instance)
        .First(m => m.Name == name && !m.IsGenericMethod && m.GetParameters().Length == count);

    private static PropertyInfo Property(object value, string name) => value.GetType().GetProperties(Instance)
        .FirstOrDefault(p => p.Name == name && p.GetIndexParameters().Length == 0);
}
