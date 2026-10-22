using UnityEngine;

/// <summary>
/// Editable palette, audio response and composition budget for the spatial audio field.
/// Created by AudioVisualFieldTools; consumed by AudioVisualField without changing shared assets.
/// </summary>
[CreateAssetMenu(menuName = "MR-VD/Audio Visual Theme", fileName = "AudioVisualTheme")]
public sealed class AudioVisualTheme : ScriptableObject
{
    [Header("Palette")]
    [ColorUsage(false, true)] public Color primary = new Color(0.08f, 0.55f, 0.85f);
    [ColorUsage(false, true)] public Color secondary = new Color(0.33f, 0.17f, 0.74f);
    [ColorUsage(false, true)] public Color accent = new Color(0.55f, 0.95f, 1f);
    [Range(0f, 3f)] public float emission = 1.8f;
    [Range(0f, 1f)] public float opacity = 0.8f;

    [Header("Surface Detail")]
    [Tooltip("Fine luminous filaments across each ribbon; reduce for distant/mobile rendering.")]
    [Range(4f, 48f)] public float fiberDensity = 22f;
    [Range(0f, 2f)] public float turbulence = 0.65f;
    [Range(0f, 2f)] public float flowSpeed = 0.7f;
    [Range(0f, 1f)] public float sparkle = 0.4f;
    [Tooltip("Spectral shape changes filament flow, surface roughness and fine highlights.")]
    [Range(0f, 1f)] public float spectralInfluence = 0.7f;
    [Tooltip("Experimental causal median masks; 0 disables their surface modulation. Not source separation.")]
    [Range(0f, 1f)] public float layerResponse = 0.5f;

    [Header("Terrain Relief")]
    [Range(2f, 24f)] public float contourDensity = 9f;
    [Range(0f, 1f)] public float contourStrength = 0.4f;
    [Range(0f, 1f)] public float ridgeGlow = 0.45f;
    [Range(0f, 1f)] public float gridStrength = 0.7f;

    [Header("Audio Response")]
    [Min(0f)] public float bassGain = 10f;
    [Min(0f)] public float detailGain = 9f;
    [Min(0.1f)] public float attack = 12f;
    [Min(0.1f)] public float release = 3f;
    [Range(0f, 0.3f)] public float idleLevel = 0.06f;

    [Header("Composition")]
    [Range(0f, 1f)] public float ribbons = 0.75f;
    [Range(0f, 1f)] public float rings = 0.5f;
    [Range(0f, 1f)] public float terrain = 0.45f;
    [Range(0f, 1f)] public float canopy = 0.78f;
    [Range(0f, 1f)] public float lattice = 0.65f;
    [Tooltip("Optional spiral petal sculpture; existing looks leave this layer off.")]
    [Range(0f, 1f)] public float iris = 0f;
    [Range(0f, 1f)] public float motion = 0.6f;
    [Tooltip("A slow visual phrase, not automatic song-section recognition.")]
    [Range(4, 64)] public int phraseBeats = 16;

    [Header("Reading and Near-Eye Clearance")]
    [Min(0f)] public float screenPadding = 0.12f;
    [Min(0f)] public float fadeStartDistance = 0.8f;
    [Min(0.1f)] public float fullVisibilityDistance = 1.4f;
}
