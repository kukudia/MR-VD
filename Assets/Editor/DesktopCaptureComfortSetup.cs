using System;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Applies the capture/reading comfort settings to the authored MR-VD scene through Unity serialization.</summary>
public static class DesktopCaptureComfortSetup
{
    [MenuItem("Tools/MR-VD/Apply Desktop Capture and Reading Comfort")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/v203.0.0.unity")
            throw new InvalidOperationException("Open v203.0.0 in Edit Mode.");
        var rays = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RayInteractor>(true))
            .Where(ray => ray.GetComponent<HandRef>() != null).ToArray();
        if (rays.Length != 2) throw new InvalidOperationException("Expected two hand rays; configuration was not applied.");
        foreach (RayInteractor ray in rays)
        {
            var visibility = ray.GetComponent<HandRayPointerVisibility>();
            if (visibility == null) visibility = Undo.AddComponent<HandRayPointerVisibility>(ray.gameObject);
            Undo.RecordObject(visibility, "Configure intentional hand pointers");
            var settings = new SerializedObject(visibility);
            var renderers = ray.GetComponentsInChildren<Renderer>(true);
            var array = settings.FindProperty("pointerRenderers");
            array.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            settings.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(visibility);
        }
        foreach (var particles in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GpuAudioParticleVisualizer>(true)))
        {
            Undo.RecordObject(particles, "Soften background stardust");
            particles.emission = 1.2f;
            particles.opacity = 0.55f;
            particles.saturation = 0.5f;
            EditorUtility.SetDirty(particles);
            PrefabUtility.RecordPrefabInstancePropertyModifications(particles);
        }
        Material meteor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/AudioVisualEffects/MeteorGlow.mat");
        if (meteor == null) throw new MissingReferenceException("MeteorGlow material is missing.");
        Undo.RecordObject(meteor, "Soften meteor glow");
        meteor.SetFloat("_Emission", 2f);
        meteor.SetFloat("_Saturation", 0.45f);
        EditorUtility.SetDirty(meteor);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[DesktopCaptureComfort] Saved pinch-only hand pointers, softer meteor glow and background stardust.");
    }
}
