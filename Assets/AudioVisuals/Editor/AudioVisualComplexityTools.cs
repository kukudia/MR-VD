using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit Undo-enabled complexity authoring; presets describe counts, not measured device performance.</summary>
public static class AudioVisualComplexityTools
{
    public const string Root = AudioVisualFieldTools.Root + "/Complexity";

    [MenuItem("Tools/MR-VD/Spatial Audio/Build Missing Complexity Presets")]
    public static void BuildMissing()
    {
        if (!AssetDatabase.IsValidFolder(Root))
        {
            AssetDatabase.CreateFolder(AudioVisualFieldTools.Root, "Complexity");
        }
        Create("Desktop Detail", false);
        Create("Reduced Geometry", true);
        AssetDatabase.SaveAssets();
    }

    private static void Create(string name, bool reduced)
    {
        string path = Root + "/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<AudioVisualComplexity>(path) != null)
        {
            return;
        }
        AudioVisualComplexity profile = ScriptableObject.CreateInstance<AudioVisualComplexity>();
        if (reduced)
        {
            profile.ribbonsPerSide = 2;
            profile.ribbonSegments = 64;
            profile.ringCapacity = 4;
            profile.ringSegments = 64;
            profile.terrainBands = 32;
            profile.terrainRows = 32;
            profile.canopyStrands = 9;
            profile.canopySegments = 96;
            profile.latticeNodes = 16;
            profile.latticeLayers = 2;
            profile.latticeSegments = 5;
            profile.irisPetals = 24;
            profile.irisSegments = 48;
            profile.particleCount = 2048;
        }
        AssetDatabase.CreateAsset(profile, path);
    }

    public static void Apply(AudioVisualComplexity profile, AudioVisualField field, GpuAudioParticleVisualizer particles)
    {
        if (EditorApplication.isPlaying || profile == null || field == null)
        {
            throw new InvalidOperationException("Apply an authored complexity asset to a field outside Play Mode.");
        }
        Undo.SetCurrentGroupName("Apply Audio Visual Complexity");
        foreach (AudioVisualMesh effect in field.GetComponentsInChildren<AudioVisualMesh>(true))
        {
            Undo.RecordObject(effect, "Apply Audio Visual Complexity");
            SerializedObject data = new SerializedObject(effect);
            if (effect is AudioAuroraRibbons)
            {
                Set(data, "ribbonsPerSide", profile.ribbonsPerSide);
                Set(data, "segments", profile.ribbonSegments);
            }
            else if (effect is AudioBeatRings)
            {
                Set(data, "capacity", profile.ringCapacity);
                Set(data, "segments", profile.ringSegments);
            }
            else if (effect is AudioSpectrumTerrain)
            {
                Set(data, "bands", profile.terrainBands);
                Set(data, "historyRows", profile.terrainRows);
            }
            else if (effect is AudioHarmonicCanopy)
            {
                Set(data, "strands", profile.canopyStrands);
                Set(data, "segments", profile.canopySegments);
            }
            else if (effect is AudioSpectralLattice)
            {
                Set(data, "nodesPerArch", profile.latticeNodes);
                Set(data, "layers", profile.latticeLayers);
                Set(data, "linkSegments", profile.latticeSegments);
            }
            else if (effect is AudioSpectralIris)
            {
                Set(data, "petals", profile.irisPetals);
                Set(data, "segments", profile.irisSegments);
            }
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
            EditorUtility.SetDirty(effect);
        }
        if (particles != null)
        {
            Undo.RecordObject(particles, "Apply Audio Visual Complexity");
            particles.particleCount = Mathf.Clamp(profile.particleCount, 256, 16384);
            EditorUtility.SetDirty(particles);
            PrefabUtility.RecordPrefabInstancePropertyModifications(particles);
        }
    }

    private static void Set(SerializedObject data, string name, int value)
    {
        data.FindProperty(name).intValue = value;
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Validate Complexity Presets")]
    public static void Validate()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Validate outside Play Mode.");
        }
        GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AudioVisualFieldTools.PrefabPath));
        AudioVisualTheme theme = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AudioVisualTheme>(AudioVisualFieldTools.ThemePath(0)));
        try
        {
            AudioVisualField field = root.GetComponent<AudioVisualField>();
            theme.iris = 1f;
            field.SetTheme(theme);
            Vector3 originalPosition = field.transform.position;
            foreach (string name in new[] { "Reduced Geometry", "Desktop Detail" })
            {
                AudioVisualComplexity profile = AssetDatabase.LoadAssetAtPath<AudioVisualComplexity>(Root + "/" + name + ".asset");
                Apply(profile, field, null);
                field.RenderFrame(new AudioVisualField.Frame
                {
                    time = 3f, deltaTime = 0.05f, energy = 0.6f, ribbonWeight = 0.7f,
                    terrainWeight = 0.7f, ringWeight = 0.7f, sculptureWeight = 0.7f, visibility = 1f
                });
                MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
                int actual = filters.Sum(x => x.sharedMesh != null ? x.sharedMesh.vertexCount : 0);
                if (actual != profile.MaximumMeshVertices || originalPosition != field.transform.position)
                {
                    throw new InvalidOperationException("Complexity budget or transform preservation failed: " + name + ": " + actual);
                }
                foreach (MeshFilter filter in filters)
                {
                    if (filter.sharedMesh.vertices.Any(v => float.IsNaN(v.x) || float.IsInfinity(v.x)
                        || float.IsNaN(v.y) || float.IsInfinity(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.z)))
                    {
                        throw new InvalidOperationException("Non-finite reduced geometry.");
                    }
                }
            }
            Debug.Log("[AudioComplexity] PASS: both authored budgets match finite generated meshes; root transform retained.");
        }
        finally
        {
            root.GetComponent<AudioVisualField>().ReleasePreviewResources();
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(theme);
        }
    }
}
