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
    [SerializeField] private int seed = 917;
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
        int count = Mathf.Max(1, Mathf.RoundToInt(meteorsPerSide * Mathf.Lerp(0.5f, 1f, intensity)));
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 start = origin + right * side * (sideOffset + Range(-0.25f, 0.3f))
                    + forward * (forwardDistance + Range(-0.3f, 0.35f))
                    + Vector3.up * Range(-verticalSpread, verticalSpread);
                Vector3 end = origin + right * side * (passingOffset + Range(0f, 0.65f))
                    + forward * 0.65f + Vector3.up * Range(-verticalSpread * 1.3f, verticalSpread * 1.3f);
                float duration = flightTime * Range(0.85f, 1.15f);
                var emission = new ParticleSystem.EmitParams
                {
                    position = start, velocity = (end - start) / duration,
                    startLifetime = duration, startSize = Range(0.035f, 0.075f),
                    randomSeed = (uint)random.Next(1, int.MaxValue)
                };
                meteors.Emit(emission, 1);
                if (sparks != null)
                {
                    emission.startSize *= 0.4f;
                    emission.startLifetime = Range(0.22f, 0.4f);
                    emission.velocity = right * side * Range(-0.8f, 1.6f)
                        + Vector3.up * Range(-1.5f, 1.5f) - forward * Range(0.2f, 1f);
                    sparks.Emit(emission, 2);
                }
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
