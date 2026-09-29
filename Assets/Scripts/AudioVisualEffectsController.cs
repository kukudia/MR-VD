using System;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>Owns audio VFX. Meteors consume detected beats, never the stage's synthetic beat clock.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class AudioVisualEffectsController : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private AudioVisualizer audioVisualizer;
    [SerializeField] private Camera targetCamera;
    [Tooltip("Optional palette/cue source for the existing stage graphs. Meteors work without it.")]
    [SerializeField] private StageManager stage;
    [Header("XR Beat Meteors")]
    [SerializeField] private XrBeatMeteorEffect[] meteorVariants = new XrBeatMeteorEffect[0];
    [SerializeField, Min(0.1f)] private float minimumBurstInterval = 0.28f;
    [SerializeField, Min(1)] private int beatsPerVariant = 4;
    [SerializeField, Range(0f, 1f)] private float meteorIntensity = 0.85f;
    [Header("Stage Graphs")]
    [SerializeField] private VisualEffect backgroundParticles;
    [SerializeField] private VisualEffect smokeEffect;
    [SerializeField] private VisualEffect laserBeamEffect;
    [SerializeField] private VisualEffect groundRingEffect;
    [SerializeField] private VisualEffect[] additionalGraphs = new VisualEffect[0];
    [SerializeField, Min(0f)] private float particleSpawnRate = 100f;
    [SerializeField, Range(0f, 1f)] private float smokeDensity = 0.3f;
    [SerializeField, Min(0f)] private float laserIntensity = 5f;
    [SerializeField, Range(0f, 4f)] private float masterIntensity = 1f;
    [SerializeField] private GraphSettings vfx = new GraphSettings();

    private float lastObservedBeat;
    private float lastBurstTime = float.NegativeInfinity;
    private int beatCount;
    public int EmittedBeatCount { get; private set; }
    public int VariantCount => meteorVariants.Length;
    public StageManager Stage => stage;
    public void SetSmokeDensity(float value) => smokeDensity = Mathf.Clamp01(value);

    private void OnEnable()
    {
        if (audioVisualizer == null) audioVisualizer = FindFirstObjectByType<AudioVisualizer>();
        if (targetCamera == null) targetCamera = Camera.main;
        // Enabling the component must not replay a stale beat.
        lastObservedBeat = audioVisualizer != null ? audioVisualizer.lastBeatTime : 0f;
        lastBurstTime = float.NegativeInfinity;
    }

    private void LateUpdate()
    {
        if (targetCamera == null || !targetCamera.isActiveAndEnabled) targetCamera = Camera.main;
        if (audioVisualizer == null || !audioVisualizer.isActiveAndEnabled) return;

        float timestamp = audioVisualizer.lastBeatTime;
        bool freshBeat = timestamp > lastObservedBeat;
        lastObservedBeat = timestamp;
        if (vfx.enableVFX && vfx.triggerBeatEvents && !audioVisualizer.wasSilent && freshBeat)
        {
            EmitBeat(Mathf.Clamp01(0.35f + audioVisualizer.smoothedKickEnergy * 12f));
        }
        if (!vfx.enableVFX) return;
        UpdateGraphs();
    }

    public void EmitBeat(float strength)
    {
        if (!isActiveAndEnabled || targetCamera == null || !targetCamera.isActiveAndEnabled
            || meteorVariants.Length == 0 || Time.time - lastBurstTime < minimumBurstInterval) return;
        int index = (beatCount / Mathf.Max(1, beatsPerVariant)) % meteorVariants.Length;
        XrBeatMeteorEffect effect = meteorVariants[index];
        if (effect == null || !effect.isActiveAndEnabled) return;
        effect.Emit(targetCamera.transform, Mathf.Clamp01(strength) * meteorIntensity * masterIntensity);
        lastBurstTime = Time.time;
        beatCount++;
        EmittedBeatCount++;
    }

    [ContextMenu("Preview Beat (Play Mode)")]
    private void PreviewBeat()
    {
        if (Application.isPlaying) EmitBeat(1f);
    }

    private void OnDisable()
    {
        foreach (XrBeatMeteorEffect effect in meteorVariants)
            if (effect != null) effect.Clear();
    }

    private void UpdateGraphs()
    {
        StageManager.PresentationFrame f;
        if (stage != null && stage.isActiveAndEnabled && stage.runtime.enableSystem) f = stage.Presentation;
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
        float master = masterIntensity * f.visibility;
        Vector4 color = f.color;
        SetFloat(backgroundParticles, "SpawnRate", particleSpawnRate * (0.15f + f.energy * vfx.maxSpawnMultiplier) * vfx.backgroundGain * master);
        SetColor(backgroundParticles, "ParticleColor", color);
        float smoke = smokeDensity * (0.3f + f.bass * 0.7f) * f.smoke * vfx.smokeGain * master;
        SetFloat(smokeEffect, "Density", smoke);
        SetFloat(smokeEffect, "SmokeDensity", smoke);
        SetColor(smokeEffect, "SmokeColor", color);
        if (smokeEffect != null && smokeEffect.HasVector3("TransformPosition")) smokeEffect.SetVector3("TransformPosition", smokeEffect.transform.position);
        SetFloat(laserBeamEffect, "BeamIntensity", f.laser * laserIntensity * vfx.laserBeamGain * master);
        SetColor(laserBeamEffect, "BeamColor", color);
        float ring = Mathf.Clamp(f.bass * vfx.maxRingExpansion, 0f, vfx.maxRingExpansion) * vfx.groundRingGain * master;
        SetFloat(groundRingEffect, "RingExpansion", ring);
        SetFloat(groundRingEffect, "BeamIntensity", ring);
        SetColor(groundRingEffect, "RingColor", color);
        SetColor(groundRingEffect, "BeamColor", color);
        if (!vfx.sendCommonParameters) return;
        SendCommon(backgroundParticles, f); SendCommon(smokeEffect, f);
        SendCommon(laserBeamEffect, f); SendCommon(groundRingEffect, f);
        foreach (VisualEffect effect in additionalGraphs) SendCommon(effect, f);
    }

    private static void SendCommon(VisualEffect effect, StageManager.PresentationFrame f)
    {
        SetFloat(effect, "Kick", f.kick); SetFloat(effect, "Bass", f.bass);
        SetFloat(effect, "Synth", f.synth); SetFloat(effect, "Energy", f.energy);
        SetFloat(effect, "BPM", f.bpm); SetFloat(effect, "BeatPhase", f.beatPhase);
        SetFloat(effect, "BarPhase", f.barPhase); SetColor(effect, "StageColor", f.color);
    }

    private static void SetFloat(VisualEffect effect, string property, float value)
    {
        if (effect != null && effect.HasFloat(property)) effect.SetFloat(property, value);
    }

    private static void SetColor(VisualEffect effect, string property, Vector4 value)
    {
        if (effect == null) return;
        if (effect.HasVector4(property)) effect.SetVector4(property, value);
        else if (effect.HasVector3(property)) effect.SetVector3(property, new Vector3(value.x, value.y, value.z));
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
