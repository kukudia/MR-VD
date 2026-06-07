using System;
using UnityEngine;

/// <summary>
/// Defines reusable lighting values for stage presets and music styles.
/// </summary>
[CreateAssetMenu(fileName = "LightingPreset", menuName = "Stage/Lighting Preset")]
public class StageLightingPreset : ScriptableObject
{
    [Header("Preset Info")]
    public string presetName = "Default";

    [TextArea(3, 5)]
    public string description = "Default lighting configuration";

    public MusicGenre genre = MusicGenre.Electronic;

    [Header("Primary Lighting")]
    [Range(0f, 10f)]
    public float baseLightIntensity = 2f;

    [Range(0f, 20f)]
    public float beatLightIntensity = 8f;

    [Range(0f, 5f)]
    public float colorChangeSpeed = 1f;

    [Header("Color Scheme")]
    public ColorScheme colorScheme;

    [Header("Animation")]
    [Range(0f, 2f)]
    public float chaseLightSpeed = 0.5f;

    [Range(0f, 5f)]
    public float spotlightRotationSpeed = 0f;

    public bool enableAutoColor = true;

    [Header("Effects")]
    [Range(0f, 1f)]
    public float strobeFrequency = 0.7f;

    [Range(0f, 1f)]
    public float laserDensity = 0.5f;

    [Range(0f, 1f)]
    public float smokeAmount = 0.3f;

    [Header("Beat Response")]
    public BeatResponse kickResponse;
    public BeatResponse snareResponse;
    public BeatResponse bassResponse;

    /// <summary>
    /// Applies this preset's core values to a stage manager instance.
    /// </summary>
    public void ApplyToStage(StageManager stage)
    {
        if (stage == null)
        {
            Debug.LogError("[LightingPreset] StageManager reference is missing.");
            return;
        }

        stage.baseLightIntensity = baseLightIntensity;
        stage.beatLightIntensity = beatLightIntensity;
        stage.colorChangeSpeed = colorChangeSpeed;
        stage.chaseLightSpeed = chaseLightSpeed;
        stage.strobeThreshold = 1f - strobeFrequency;
        stage.laserThreshold = 1f - laserDensity;
        stage.smokeDensity = smokeAmount;

        Debug.Log($"[LightingPreset] Applied preset: {presetName}");
    }
}

/// <summary>
/// Supported musical style categories for lighting presets.
/// </summary>
public enum MusicGenre
{
    Electronic,
    Rock,
    HipHop,
    Jazz,
    Pop,
    Classical,
    Metal,
    Ambient
}

/// <summary>
/// Color gradients and palette colors used by a stage lighting preset.
/// </summary>
[Serializable]
public class ColorScheme
{
    [Header("Primary Colors")]
    public Color primaryColor = Color.red;
    public Color secondaryColor = Color.blue;
    public Color accentColor = Color.white;

    [Header("Mode Mapping")]
    public Gradient majorMoodGradient;
    public Gradient minorMoodGradient;

    [Header("Energy Mapping")]
    public Gradient lowEnergyGradient;
    public Gradient highEnergyGradient;
}

/// <summary>
/// Configures how a lighting preset responds to beat events.
/// </summary>
[Serializable]
public class BeatResponse
{
    [Header("Response Type")]
    public bool flashLights = true;
    public bool triggerParticles = true;
    public bool pulseIntensity = true;

    [Header("Response Strength")]
    [Range(0f, 2f)]
    public float intensityMultiplier = 1f;

    [Range(0f, 1f)]
    public float smoothing = 0.5f;

    [Header("Color Change")]
    public bool changeColor = false;
    public Color responseColor = Color.white;
}
