using System.Collections.Generic;
using UnityEngine;

/// <summary>Emits world-space comets around a captured head pose; never moves the camera.</summary>
[DisallowMultipleComponent]
public sealed class XrBeatMeteorEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem meteors;
    [SerializeField] private ParticleSystem sparks;
    [Header("Emission pose (metres)")]
    [SerializeField, Min(0.5f)] private float sideOffset = 2.4f;
    [SerializeField, Min(1f)] private float forwardDistance = 3.8f;
    [SerializeField, Min(0f)] private float verticalSpread = 0.8f;
    [Tooltip("The trajectories stay on their own side of the central reading area.")]
    [SerializeField, Min(0.6f)] private float passingOffset = 1.15f;
    [SerializeField, Range(0.65f, 2f)] private float flightTime = 1.1f;
    [SerializeField, Range(1, 24)] private int meteorsPerSide = 9;
    [SerializeField, Range(0.1f, 1f)] private float minimumCountFraction = 0.5f;
    [SerializeField] private int seed = 917;
    [Header("Trajectory")]
    [SerializeField] private bool useBezierTrajectory;
    [SerializeField, Range(0f, 1.5f)] private float outwardCurve = 0.65f;
    [SerializeField, Range(-1f, 1f)] private float verticalCurve = 0.2f;
    [Header("Beat response")]
    [SerializeField, Range(60f, 200f)] private float fallbackBpm = 120f;
    [SerializeField, Range(2f, 15f)] private float launchMultiplier = 10f;

    private const float recoveryExcess = 0.1f;
    private readonly Dictionary<uint, MeteorState> activeMeteors = new Dictionary<uint, MeteorState>(128);
    private readonly HashSet<uint> liveSeeds = new HashSet<uint>();
    private readonly List<uint> expiredSeeds = new List<uint>();
    private ParticleSystem.Particle[] particles;
    private ParticleSystemRenderer meteorRenderer;
    private Material sourceMaterial;
    private Material instanceMaterial;
    private System.Random random;

    private struct MeteorState
    {
        public Vector3 start, control, end;
        public float lifetime, halfBeat, decayRate, integral;
        public float launch;
        public bool curved;
    }

    public int LiveParticleCount => (meteors != null ? meteors.particleCount : 0) + (sparks != null ? sparks.particleCount : 0);

    private void OnEnable() => InitializeMeteorSystem();

    private void InitializeMeteorSystem()
    {
        if (meteors == null) return;
        if (particles == null || particles.Length < meteors.main.maxParticles)
            particles = new ParticleSystem.Particle[Mathf.Max(128, meteors.main.maxParticles)];
        var velocity = meteors.velocityOverLifetime;
        velocity.enabled = false;
        if (meteorRenderer == null) meteorRenderer = meteors.GetComponent<ParticleSystemRenderer>();
        if (sourceMaterial == null && meteorRenderer != null) sourceMaterial = meteorRenderer.sharedMaterial;
    }

    private void LateUpdate() => RefreshMeteors();

    public void Emit(Transform view, float intensity, float bpm = 0f)
    {
        if (view == null || meteors == null || intensity <= 0f) return;
        InitializeMeteorSystem();
        if (random == null) random = new System.Random(seed);
        if (!meteors.isPlaying) meteors.Play(false);
        if (sparks != null && !sparks.isPlaying) sparks.Play(false);
        Vector3 origin = view.position;
        // Capture yaw rather than roll/pitch: head tilts cannot swing a volley into the face.
        Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float halfBeat = 30f / Mathf.Clamp(bpm > 0f ? bpm : fallbackBpm, 60f, 200f);
        float launch = Mathf.Max(2f, launchMultiplier);
        if (meteorRenderer == null) meteorRenderer = meteors.GetComponent<ParticleSystemRenderer>();
        if (sourceMaterial == null && meteorRenderer != null) sourceMaterial = meteorRenderer.sharedMaterial;
        if (sourceMaterial != null)
        {
            if (instanceMaterial == null)
            {
                instanceMaterial = new Material(sourceMaterial);
                meteorRenderer.sharedMaterial = instanceMaterial;
            }
            instanceMaterial.SetFloat("_Emission", sourceMaterial.GetFloat("_Emission") * launch);
        }
        float decayRate = Mathf.Log((launch - 1f) / recoveryExcess);
        int count = Mathf.Max(1, Mathf.RoundToInt(meteorsPerSide * Mathf.Lerp(minimumCountFraction, 1f, Mathf.Clamp01(intensity))));
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 start = origin + right * side * (sideOffset + Range(-0.25f, 0.3f))
                    + forward * (forwardDistance + Range(-0.3f, 0.35f))
                    + Vector3.up * Range(-verticalSpread, verticalSpread);
                Vector3 end = origin + right * side * (passingOffset + Range(0f, 0.65f))
                    + forward * 0.65f + Vector3.up * Range(-verticalSpread * 1.3f, verticalSpread * 1.3f);
                Vector3 control = (start + end) * 0.5f + right * side * outwardCurve + Vector3.up * verticalCurve;
                float duration = flightTime * Range(0.85f, 1.15f);
                float integral = IntegratedBoost(duration, halfBeat, launch, decayRate);
                var state = new MeteorState
                {
                    start = start, control = control, end = end, lifetime = duration,
                    halfBeat = halfBeat, decayRate = decayRate, integral = integral,
                    launch = launch, curved = useBezierTrajectory
                };
                uint particleSeed = (uint)random.Next(1, int.MaxValue);
                while (activeMeteors.ContainsKey(particleSeed)) particleSeed = (uint)random.Next(1, int.MaxValue);
                activeMeteors.Add(particleSeed, state);
                var emission = new ParticleSystem.EmitParams
                {
                    position = start, velocity = Vector3.zero,
                    startLifetime = duration, startSize = Range(0.035f, 0.075f),
                    randomSeed = particleSeed
                };
                meteors.Emit(emission, 1);
                if (sparks == null) continue;
                emission.startSize *= 0.4f;
                emission.startLifetime = Range(0.22f, 0.4f);
                emission.velocity = right * side * Range(-0.8f, 1.6f)
                    + Vector3.up * Range(-1.5f, 1.5f) - forward * Range(0.2f, 1f);
                sparks.Emit(emission, 2);
            }
        }
    }

    // Called by LateUpdate and by the Editor preview after ParticleSystem.Simulate.
    public void RefreshMeteors()
    {
        if (meteors == null || particles == null || activeMeteors.Count == 0) return;
        int count = meteors.GetParticles(particles);
        liveSeeds.Clear();
        for (int i = 0; i < count; i++)
        {
            uint particleSeed = particles[i].randomSeed;
            if (!activeMeteors.TryGetValue(particleSeed, out MeteorState state)) continue;
            liveSeeds.Add(particleSeed);
            float age = Mathf.Clamp(state.lifetime - particles[i].remainingLifetime, 0f, state.lifetime);
            float boost = Boost(age, state.halfBeat, state.launch, state.decayRate);
            float progress = IntegratedBoost(age, state.halfBeat, state.launch, state.decayRate) / state.integral;
            if (state.curved)
            {
                float inverse = 1f - progress;
                particles[i].position = inverse * inverse * state.start
                    + 2f * inverse * progress * state.control + progress * progress * state.end;
            }
            else
            {
                particles[i].position = Vector3.LerpUnclamped(state.start, state.end, progress);
            }
            particles[i].velocity = Vector3.zero;
            particles[i].startColor = new Color(1f, 1f, 1f, boost / state.launch);
        }
        meteors.SetParticles(particles, count);
        expiredSeeds.Clear();
        foreach (uint particleSeed in activeMeteors.Keys)
            if (!liveSeeds.Contains(particleSeed)) expiredSeeds.Add(particleSeed);
        foreach (uint particleSeed in expiredSeeds) activeMeteors.Remove(particleSeed);
    }

    private static float Boost(float age, float halfBeat, float launch, float rate)
        => 1f + (launch - 1f) * Mathf.Exp(-rate * age / halfBeat);

    private static float IntegratedBoost(float age, float halfBeat, float launch, float rate)
        => age + (launch - 1f) * halfBeat * (1f - Mathf.Exp(-rate * age / halfBeat)) / rate;

    public void Clear()
    {
        if (meteors != null) meteors.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (sparks != null) sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        activeMeteors.Clear();
    }

    private void OnDisable()
    {
        Clear();
        if (meteorRenderer != null && sourceMaterial != null) meteorRenderer.sharedMaterial = sourceMaterial;
        if (instanceMaterial != null)
        {
            if (Application.isPlaying) Destroy(instanceMaterial);
            else DestroyImmediate(instanceMaterial);
            instanceMaterial = null;
        }
    }

    private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
}
