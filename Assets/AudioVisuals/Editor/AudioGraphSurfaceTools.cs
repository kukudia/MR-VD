using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Reproducibly produces linear RGBA particle masks and binds them to existing graph output slots.
/// Unity 17.3 keeps its graph model API internal, so version-scoped reflection is confined here;
/// graph serialization/compilation still uses the package's own API, never YAML replacement.
/// Existing particle blocks, exposed properties, graph identity and scene activation are preserved.
/// </summary>
public static class AudioGraphSurfaceTools
{
    public const string TextureRoot = "Assets/AudioVisuals/Textures";
    private static AudioGraphTextureSettings settings;
    private static int Size => Mathf.ClosestPowerOfTwo(Mathf.Clamp(settings.resolution, 64, 512));
    private static int Seed => settings.seed;
    private static readonly string[] Graphs = { "BackgroundParticles", "SmokeEffect", "LaserBeamEffect", "GroundRingEffect" };
    private static readonly string[] Textures = { "SoftStar", "FractalMist", "FilamentBeam", "ContourTrail" };
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [MenuItem("Tools/MR-VD/Spatial Audio/Generate and Apply Detailed VFX Surfaces")]
    public static void GenerateAndApply()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Generate graph surfaces outside Play Mode.");
        }
        if (!AssetDatabase.IsValidFolder(TextureRoot))
        {
            AssetDatabase.CreateFolder(AudioVisualFieldTools.Root, "Textures");
        }
        string settingsPath = TextureRoot + "/GraphTextureSettings.asset";
        settings = AssetDatabase.LoadAssetAtPath<AudioGraphTextureSettings>(settingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<AudioGraphTextureSettings>();
            AssetDatabase.CreateAsset(settings, settingsPath);
        }
        for (int kind = 0; kind < Textures.Length; kind++)
        {
            GenerateTexture(kind);
        }
        for (int kind = 0; kind < Textures.Length; kind++)
        {
            ApplyOutputTextures(kind);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[AudioGraphSurfaces] Generated seeded " + Size + "px linear RGBA masks and saved graph output bindings.");
    }

    private static void GenerateTexture(int kind)
    {
        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        try
        {
            Color[] pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size;
                    float v = (y + 0.5f) / Size;
                    float px = u * 2f - 1f;
                    float py = v * 2f - 1f;
                    float r2 = px * px + py * py;
                    float edge = Mathf.SmoothStep(0f, 0.2f, Mathf.Min(u, Mathf.Min(v, Mathf.Min(1f - u, 1f - v))));
                    float alpha;
                    if (kind == 0)
                    {
                        float core = Mathf.Exp(-r2 * 24f);
                        float halo = Mathf.Exp(-r2 * 4.5f) * settings.starHalo;
                        float rays = (Mathf.Exp(-px * px * 450f) + Mathf.Exp(-py * py * 450f))
                            * Mathf.Exp(-r2 * 7f) * 0.22f;
                        alpha = (core + halo + rays) * edge;
                    }
                    else if (kind == 1)
                    {
                        float noise = Fractal(u * settings.mistScale, v * settings.mistScale);
                        float warped = Fractal(u * 8f + noise, v * 8f - noise);
                        alpha = Mathf.Pow(Mathf.Clamp01(1f - r2), 1.5f)
                            * Mathf.SmoothStep(0.22f, 0.86f, noise * 0.7f + warped * 0.3f) * edge;
                    }
                    else
                    {
                        float transverse = Mathf.Exp(-py * py * (kind == 2 ? 35f : 20f));
                        float thin = Mathf.Exp(-py * py * 250f);
                        float strands = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(py * settings.filamentFrequency + Mathf.Sin(u * 18f) * 0.4f)), 5f);
                        float along = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.4f);
                        float pulses = 0.7f + Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * 42f)), 8f) * 0.3f;
                        alpha = (transverse * (0.2f + strands * 0.35f) + thin * 0.5f) * along * pulses * edge;
                    }
                    // Neutral RGB preserves the graph's color contract; alpha carries the authored detail.
                    pixels[y * Size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            string path = TextureRoot + "/" + Textures[kind] + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    private static float Fractal(float x, float y)
    {
        float value = 0f;
        float weight = 0.5f;
        for (int octave = 0; octave < 4; octave++)
        {
            value += Mathf.PerlinNoise(x + Seed * 0.013f, y + Seed * 0.021f) * weight;
            x = x * 2.07f + 13.1f;
            y = y * 2.03f - 5.7f;
            weight *= 0.5f;
        }
        return value / 0.9375f;
    }

    private static void ApplyOutputTextures(int kind)
    {
        Assembly graphAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == "Unity.VisualEffectGraph.Editor");
        Type resourceType = Type.GetType("UnityEditor.VFX.VisualEffectResource, UnityEditor.VFXModule", true);
        object resource = resourceType.GetMethod("GetResourceAtPath", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { "Assets/VFX/" + Graphs[kind] + ".vfx" });
        Type extensions = graphAssembly.GetType("UnityEditor.VFX.VisualEffectResourceExtensions", true);
        object graph = extensions.GetMethod("GetOrCreateGraph").Invoke(null, new[] { resource });
        IEnumerable children = (IEnumerable)graph.GetType().GetProperty("children").GetValue(graph);
        int changed = 0;
        foreach (object node in children)
        {
            if (!node.GetType().Name.Contains("Output"))
            {
                continue;
            }
            IEnumerable slots = node.GetType().GetProperty("inputSlots", Instance)?.GetValue(node) as IEnumerable;
            if (slots == null)
            {
                continue;
            }
            foreach (object slot in slots)
            {
                string slotName = (string)slot.GetType().GetProperty("name").GetValue(slot);
                if (slotName != "mainTexture")
                {
                    continue;
                }
                string textureName = node.GetType().Name.Contains("Strip") ? Textures[kind] : Textures[kind == 1 ? 1 : 0];
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureRoot + "/" + textureName + ".png");
                if (texture == null)
                {
                    throw new InvalidOperationException("Generate mask before binding: " + textureName);
                }
                Undo.RecordObject((Object)slot, "Assign Detailed Audio Graph Surface");
                slot.GetType().GetProperty("value").SetValue(slot, texture);
                EditorUtility.SetDirty((Object)slot);
                changed++;
            }
        }
        if (changed == 0)
        {
            throw new InvalidOperationException("No mainTexture output slot found for " + Graphs[kind]);
        }
        extensions.GetMethod("WriteAssetWithSubAssets").Invoke(null, new[] { resource });
        AssetDatabase.ImportAsset("Assets/VFX/" + Graphs[kind] + ".vfx", ImportAssetOptions.ForceUpdate);
    }
}
