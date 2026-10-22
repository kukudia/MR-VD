using System;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Owns meteor onset consumption and graph property writes. LateUpdate follows StageManager and
/// AudioVisualField, applying optional spectral/palette modulation through existing exposed graph
/// properties. Synthetic graph input is explicit on the field and never emits meteor onsets.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(140)]
public sealed class AudioVisualEffectsController : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private AudioVisualizer audioVisualizer;
    [SerializeField] private Camera targetCamera;
    [Tooltip("Optional palette/cue source for the existing stage graphs. Meteors work without it.")]
    [SerializeField] private StageManager stage;
    [Tooltip("Optional continuous spectral shape/palette source; does not replace real meteor onset events.")]
    [SerializeField] private AudioVisualField spatialField;
    [SerializeField, Range(0f, 1f)] private float graphSpectralAmount = 0.55f;
    [SerializeField, Range(0f, 1f)] private float graphPaletteAmount = 0.6f;
    [SerializeField, Range(0f, 1f)] private float graphOnsetAccent = 0.35f;
    [Header("XR Beat Meteors")]
    [SerializeField] private XrBeatMeteorEffect[] meteorVariants = new XrBeatMeteorEffect[0];
    [Tooltip("Deduplicates a drum attack without blocking faster musical accents like the BPM cooldown does.")]
    [SerializeField, Min(0.1f)] private float minimumBurstInterval = 0.18f;
    [SerializeField, Range(0f, 1f)] private float minimumOnsetConfidence = 0.22f;
    [Tooltip("Minimum presence of an accepted hit; stronger attacks emit more meteors.")]
    [SerializeField, Range(0f, 1f)] private float minimumImpact = 0.55f;
    [SerializeField, Min(1)] private int beatsPerVariant = 4;
    [SerializeField, Range(0f, 1f)] private float meteorIntensity = 1f;
    [Header("Stage Graphs")]
    [SerializeField] private VisualEffect backgroundParticles;
    [SerializeField] private VisualEffect smokeEffect;
    [SerializeField] private VisualEffect laserBeamEffect;
    [SerializeField] private VisualEffect groundRingEffect;
    [SerializeField] private VisualEffect[] additionalGraphs = new VisualEffect[0];
    [SerializeField, Min(0f)] private float particleSpawnRate = 100f;
    [SerializeField, Range(0f, 1f)] private float smokeDensity = 0.3f;
    [Tooltip("The existing smoke graph connects Density directly to spawn Rate (particles/second).")]
    [SerializeField, Min(0f)] private float smokeParticlesPerSecond = 120f;
    [SerializeField, Min(0f)] private float laserIntensity = 5f;
    [SerializeField, Range(0f, 4f)] private float masterIntensity = 1f;
    [SerializeField] private GraphSettings vfx = new GraphSettings();

    private uint lastObservedOnset;
    private float lastBurstTime = float.NegativeInfinity;
    private int beatCount;
    private float graphAccent;
    public int EmittedBeatCount { get; private set; }
    public int VariantCount => meteorVariants.Length;
    public StageManager Stage => stage;
    public void SetSmokeDensity(float value) => smokeDensity = Mathf.Clamp01(value);

    private void OnEnable()
    {
        if (audioVisualizer == null)
        {
            audioVisualizer = FindFirstObjectByType<AudioVisualizer>();
        }
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
        // Enabling the component must not replay a stale beat.
        lastObservedOnset = audioVisualizer != null ? audioVisualizer.VisualOnsetSequence : 0;
        lastBurstTime = float.NegativeInfinity;
    }

    private void LateUpdate()
    {
        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            targetCamera = Camera.main;
        }
        bool hasAudio = audioVisualizer != null && audioVisualizer.isActiveAndEnabled;
        bool demo = spatialField != null && spatialField.isActiveAndEnabled && spatialField.UsesDemonstration;
        if (hasAudio)
        {
            uint sequence = audioVisualizer.VisualOnsetSequence;
            bool freshOnset = sequence != lastObservedOnset && Time.time - audioVisualizer.VisualOnsetTime < 0.12f;
            lastObservedOnset = sequence;
            if (!demo && vfx.enableVFX && vfx.triggerBeatEvents && !audioVisualizer.wasSilent && freshOnset
                && audioVisualizer.VisualOnsetConfidence >= minimumOnsetConfidence)
            {
                EmitBeat(Mathf.Lerp(minimumImpact, 1f, audioVisualizer.VisualOnsetStrength));
            }
        }
        if ((!hasAudio && !demo) || !vfx.enableVFX)
        {
            ClearGraphIntensities();
            return;
        }
        UpdateGraphs();
    }

    public void EmitBeat(float strength)
    {
        if (!isActiveAndEnabled || targetCamera == null || !targetCamera.isActiveAndEnabled
            || meteorVariants.Length == 0 || strength <= 0f || meteorIntensity * masterIntensity <= 0f
            || Time.time - lastBurstTime < minimumBurstInterval)
        {
            return;
        }
        int index = (beatCount / Mathf.Max(1, beatsPerVariant)) % meteorVariants.Length;
        XrBeatMeteorEffect effect = meteorVariants[index];
        if (effect == null || !effect.isActiveAndEnabled)
        {
            return;
        }
        effect.Emit(targetCamera.transform, Mathf.Clamp01(strength) * meteorIntensity * masterIntensity,
            audioVisualizer != null ? audioVisualizer.limitedBPM : 0f);
        lastBurstTime = Time.time;
        beatCount++;
        EmittedBeatCount++;
    }

    [ContextMenu("Preview Beat (Play Mode)")]
    private void PreviewBeat()
    {
        if (Application.isPlaying)
        {
            EmitBeat(1f);
        }
    }

    private void OnDisable()
    {
        graphAccent = 0f;
        ClearGraphIntensities();
        foreach (XrBeatMeteorEffect effect in meteorVariants)
        {
            if (effect != null)
            {
                effect.Clear();
            }
        }
    }

    private void UpdateGraphs()
    {
        StageManager.PresentationFrame f;
        if (spatialField != null && spatialField.isActiveAndEnabled && spatialField.UsesDemonstration)
        {
            AudioVisualField.Frame spatial = spatialField.CurrentFrame;
            f = new StageManager.PresentationFrame
            {
                bass = spatial.bass, synth = spatial.detail, kick = spatial.onsetStrength,
                energy = spatial.energy, bpm = spatial.bpm, beatPhase = spatial.beatPhase,
                color = spatialField.DisplayedTheme.primary, visibility = 1f, smoke = 1f, laser = spatial.bass
            };
        }
        else if (stage != null && stage.isActiveAndEnabled && stage.runtime.enableSystem)
        {
            f = stage.Presentation;
        }
        else
        {
            float bass = 1f - Mathf.Exp(-Mathf.Max(0f, audioVisualizer.smoothedBassEnergy) * 10f);
            float synth = 1f - Mathf.Exp(-Mathf.Max(0f, audioVisualizer.smoothedSynthEnergy) * 9f);
            f = new StageManager.PresentationFrame
            {
                bass = bass, synth = synth, kick = Mathf.Clamp01(audioVisualizer.smoothedKickEnergy * 12f),
                energy = (bass + synth) * 0.5f, bpm = audioVisualizer.limitedBPM,
                color = Color.cyan, visibility = 1f, smoke = 1f, laser = bass
            };
        }
        WriteGraphFrame(f);
    }

    private void ClearGraphIntensities()
    {
        SetFloat(backgroundParticles, "SpawnRate", 0f);
        SetFloat(smokeEffect, "Density", 0f);
        SetFloat(smokeEffect, "SmokeDensity", 0f);
        SetFloat(laserBeamEffect, "BeamIntensity", 0f);
        SetFloat(groundRingEffect, "RingExpansion", 0f);
        SetFloat(groundRingEffect, "BeamIntensity", 0f);
    }

    private void WriteGraphFrame(StageManager.PresentationFrame f)
    {
        bool coordinated = spatialField != null && spatialField.isActiveAndEnabled;
        AudioVisualField.Frame spatial = coordinated ? spatialField.CurrentFrame : default;
        Vector4 shape = spatial.spectralShape * graphSpectralAmount;
        graphAccent *= Mathf.Exp(-Mathf.Min(Time.deltaTime, 0.1f) * 6f);
        if (coordinated && spatial.onset)
        {
            graphAccent = Mathf.Max(graphAccent, spatial.onsetStrength * graphOnsetAccent);
        }
        float master = masterIntensity * Mathf.Clamp01(f.visibility);
        Color graphColor = f.color;
        if (coordinated && spatialField.DisplayedTheme != null)
        {
            graphColor = Color.Lerp(graphColor, spatialField.DisplayedTheme.primary, graphPaletteAmount);
        }
        Vector4 color = graphColor;
        float dust = 1f + shape.x * 0.45f + graphAccent * 0.4f;
        SetFloat(backgroundParticles, "SpawnRate", particleSpawnRate * (0.15f + f.energy * vfx.maxSpawnMultiplier)
            * vfx.backgroundGain * master * dust);
        SetColor(backgroundParticles, "ParticleColor", color);
        float smoke = smokeDensity * (0.3f + f.bass * 0.7f) * f.smoke * vfx.smokeGain * master
            * (1f + shape.z * 0.35f);
        SetFloat(smokeEffect, "Density", smoke * smokeParticlesPerSecond);
        SetFloat(smokeEffect, "SmokeDensity", smoke);
        SetColor(smokeEffect, "SmokeColor", color);
        if (smokeEffect != null && smokeEffect.HasVector3("TransformPosition"))
        {
            smokeEffect.SetVector3("TransformPosition", smokeEffect.transform.position);
        }
        SetFloat(laserBeamEffect, "BeamIntensity", (f.laser * (1f + shape.w * 0.75f) + graphAccent * 0.3f)
            * laserIntensity * vfx.laserBeamGain * master);
        SetColor(laserBeamEffect, "BeamColor", color);
        float ring = Mathf.Clamp((f.bass + graphAccent * 0.2f) * vfx.maxRingExpansion, 0f, vfx.maxRingExpansion)
            * vfx.groundRingGain * master;
        SetFloat(groundRingEffect, "RingExpansion", ring);
        SetFloat(groundRingEffect, "BeamIntensity", ring);
        SetColor(groundRingEffect, "RingColor", color);
        SetColor(groundRingEffect, "BeamColor", color);
        if (!vfx.sendCommonParameters)
        {
            return;
        }
        SendCommon(backgroundParticles, f);
        SendCommon(smokeEffect, f);
        SendCommon(laserBeamEffect, f);
        SendCommon(groundRingEffect, f);
        foreach (VisualEffect effect in additionalGraphs)
        {
            SendCommon(effect, f);
        }
    }

    private static void SendCommon(VisualEffect effect, StageManager.PresentationFrame f)
    {
        SetFloat(effect, "Kick", f.kick);
        SetFloat(effect, "Bass", f.bass);
        SetFloat(effect, "Synth", f.synth);
        SetFloat(effect, "Energy", f.energy);
        SetFloat(effect, "BPM", f.bpm);
        SetFloat(effect, "BeatPhase", f.beatPhase);
        SetFloat(effect, "BarPhase", f.barPhase);
        SetColor(effect, "StageColor", f.color);
    }

    private static void SetFloat(VisualEffect effect, string property, float value)
    {
        if (effect != null && effect.HasFloat(property))
        {
            effect.SetFloat(property, value);
        }
    }

    private static void SetColor(VisualEffect effect, string property, Vector4 value)
    {
        if (effect == null)
        {
            return;
        }
        if (effect.HasVector4(property))
        {
            effect.SetVector4(property, value);
        }
        else if (effect.HasVector3(property))
        {
            effect.SetVector3(property, new Vector3(value.x, value.y, value.z));
        }
    }

    [Serializable]
    private sealed class GraphSettings
    {
        public bool enableVFX = true;
        public bool sendCommonParameters = true;
        public bool triggerBeatEvents = true;
        [Range(0f, 4f)] public float backgroundGain = 1f;
        [Range(0f, 4f)] public float smokeGain = 1f;
        [Range(0f, 4f)] public float laserBeamGain = 1f;
        [Range(0f, 4f)] public float groundRingGain = 1f;
        [Range(0f, 10f)] public float maxSpawnMultiplier = 4f;
        [Range(0f, 10f)] public float maxRingExpansion = 8f;
    }
}
