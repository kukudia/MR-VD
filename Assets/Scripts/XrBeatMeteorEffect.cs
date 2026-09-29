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
    [SerializeField, Range(0.45f, 2f)] private float flightTime = 1.1f;
    [SerializeField, Range(1, 24)] private int meteorsPerSide = 9;
    [SerializeField] private int seed = 917;
    [Header("Impact response")]
    [SerializeField, Range(1f, 2f)] private float strongBeatCountMultiplier = 1.4f;
    [SerializeField, Range(1f, 2f)] private float strongBeatSpeedMultiplier = 1.28f;
    [SerializeField, Range(1f, 2f)] private float strongBeatSizeMultiplier = 1.5f;
    [SerializeField, Range(0, 20)] private int burstSparksPerSide = 12;
    [Tooltip("Small, short-lived glow at each launch point, in metres; never a full-screen flash.")]
    [SerializeField, Range(0f, 0.8f)] private float impactFlashSize = 0.45f;
    private System.Random random;

    public int LiveParticleCount => (meteors != null ? meteors.particleCount : 0) + (sparks != null ? sparks.particleCount : 0);

    public void Emit(Transform view, float intensity)
    {
        if (view == null || meteors == null || intensity <= 0f) return;
        if (random == null) random = new System.Random(seed);
        if (!meteors.isPlaying) meteors.Play(false);
        if (sparks != null && !sparks.isPlaying) sparks.Play(false);
        Vector3 origin = view.position;
        // Capture yaw rather than roll/pitch: head tilts cannot swing a volley into the face.
        Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float impact = Mathf.Clamp01(intensity);
        int count = Mathf.Max(1, Mathf.RoundToInt(meteorsPerSide * Mathf.Lerp(0.65f, strongBeatCountMultiplier, impact)));
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 burstOrigin = origin + right * side * sideOffset + forward * forwardDistance;
            for (int i = 0; i < count; i++)
            {
                Vector3 start = burstOrigin + right * Range(-0.18f, 0.18f)
                    + forward * Range(-0.15f, 0.15f)
                    + Vector3.up * Range(-verticalSpread * 0.35f, verticalSpread * 0.35f);
                Vector3 end = origin + right * side * (passingOffset + Range(0f, 0.65f))
                    + forward * 0.65f + Vector3.up * Range(-verticalSpread * 1.3f, verticalSpread * 1.3f);
                float duration = flightTime / Mathf.Lerp(0.9f, strongBeatSpeedMultiplier, impact) * Range(0.85f, 1.15f);
                var emission = new ParticleSystem.EmitParams
                {
                    position = start, velocity = (end - start) / duration,
                    startLifetime = duration, startSize = Range(0.045f, 0.09f) * Mathf.Lerp(0.85f, strongBeatSizeMultiplier, impact),
                    randomSeed = (uint)random.Next(1, int.MaxValue)
                };
                meteors.Emit(emission, 1);
                if (sparks != null)
                {
                    emission.startSize *= 0.38f;
                    emission.startLifetime = Range(0.22f, 0.4f);
                    emission.velocity = right * side * Range(-0.8f, 1.6f)
                        + Vector3.up * Range(-1.5f, 1.5f) - forward * Range(0.2f, 1f);
                    sparks.Emit(emission, 2);
                }
            }

            if (sparks == null) continue;
            if (impactFlashSize > 0f)
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = burstOrigin, velocity = Vector3.zero,
                    startLifetime = 0.1f, startSize = impactFlashSize * Mathf.Lerp(0.5f, 1f, impact),
                    randomSeed = (uint)random.Next(1, int.MaxValue)
                }, 1);
            int burstCount = Mathf.RoundToInt(burstSparksPerSide * Mathf.Lerp(0.4f, 1f, impact));
            for (int i = 0; i < burstCount; i++)
            {
                Vector3 direction = (right * side * Range(0.25f, 1f)
                    + Vector3.up * Range(-0.8f, 0.8f)
                    + forward * Range(-0.7f, 0.25f)).normalized;
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = burstOrigin + Vector3.up * Range(-0.2f, 0.2f),
                    velocity = direction * Range(2.2f, 4.8f) * Mathf.Lerp(0.8f, 1.45f, impact),
                    startLifetime = Range(0.2f, 0.42f),
                    startSize = Range(0.045f, 0.085f) * Mathf.Lerp(0.9f, 1.55f, impact),
                    randomSeed = (uint)random.Next(1, int.MaxValue)
                }, 1);
            }
        }
    }

    public void Clear()
    {
        if (meteors != null) meteors.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (sparks != null) sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable() => Clear();
    private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
}
