using UnityEngine;

/// <summary>
/// Builds a bounded spiral of folded luminous petals around an open center. Each petal samples
/// a stable frequency identity; the analytic shape is reproducible and reuses one dynamic mesh.
/// </summary>
public sealed class AudioSpectralIris : AudioVisualMesh
{
    [SerializeField, Range(12, 48)] private int petals = 32;
    [SerializeField, Range(24, 128)] private int segments = 72;
    [SerializeField, Min(0.2f)] private float aperture = 1.05f;
    [SerializeField, Min(0.1f)] private float reach = 1.7f;
    [SerializeField, Range(-2f, 2f)] private float spiral = 1.15f;
    [SerializeField, Range(0.02f, 0.45f)] private float petalWidth = 0.22f;
    [SerializeField, Range(0f, 1f)] private float foldDepth = 0.55f;
    [SerializeField, Range(0f, 1f)] private float spectrumReach = 0.65f;
    [SerializeField, Range(0f, 0.3f)] private float rotationSpeed = 0.12f;
    [SerializeField, Range(0f, 0.6f)] private float onsetExpansion = 0.24f;
    [SerializeField, Range(0f, 1f)] private float onsetFold = 0.35f;
    [SerializeField, Min(0.1f)] private float onsetRelax = 4f;
    [SerializeField] private Vector3 center = new Vector3(0f, 0.25f, 3.5f);
    [SerializeField] private int seed = 8457;
    private int builtPetals;
    private int builtSegments;
    private float onsetPulse;

    public void RenderFrame(AudioVisualField.Frame frame, AudioVisualTheme theme, AudioVisualField source)
    {
        onsetPulse = Mathf.Max(onsetPulse * Mathf.Exp(-Mathf.Max(0f, frame.deltaTime) * onsetRelax),
            frame.onset ? frame.onsetStrength : 0f);
        // Existing scenes keep this optional layer at zero and pay no mesh-generation cost.
        if (theme.iris <= 0.0001f)
        {
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
            return;
        }
        GetComponent<MeshRenderer>().enabled = true;
        int count = Mathf.Clamp(petals, 12, 48);
        int steps = Mathf.Clamp(segments, 24, 128);
        if (mesh == null || builtPetals != count || builtSegments != steps)
        {
            Initialize(count * (steps + 1) * 2, StripIndices(count, steps), MeshTopology.Triangles);
            builtPetals = count;
            builtSegments = steps;
        }
        float clock = frame.time * rotationSpeed * theme.motion;
        float visibility = theme.iris * frame.sculptureWeight;
        for (int petal = 0; petal < count; petal++)
        {
            float identity = petal / (float)count;
            float band = source != null ? source.GetSpectrumBand(Mathf.RoundToInt(identity * 47f)) : 0f;
            float phase = petal * 2.39996f + seed * 0.013f;
            float breathing = Mathf.Sin(frame.time * 0.55f + phase) * 0.025f * theme.motion;
            for (int step = 0; step <= steps; step++)
            {
                float u = step / (float)steps;
            float envelope = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));
                float radius = aperture * (1f - onsetPulse * onsetExpansion * 0.22f)
                    + u * (reach + band * spectrumReach + onsetPulse * onsetExpansion) + breathing * envelope;
                float angle = identity * Mathf.PI * 2f + spiral * u + clock;
                float halfAngle = petalWidth / count * Mathf.PI * Mathf.Pow(envelope, 0.65f);
                float depth = foldDepth * (1f + onsetPulse * onsetFold) * envelope
                    * Mathf.Sin(phase + u * 5f - frame.time * 0.35f * theme.motion);
                int index = (petal * (steps + 1) + step) * 2;
                for (int side = 0; side < 2; side++)
                {
                    float theta = angle + (side == 0 ? -halfAngle : halfAngle);
                    vertices[index + side] = center + new Vector3(Mathf.Cos(theta) * radius,
                        Mathf.Sin(theta) * radius, depth + (side == 0 ? -1f : 1f) * envelope * foldDepth * 0.12f);
                    coordinates[index + side] = new Vector2(u, side);
                    float alpha = visibility * Mathf.Pow(envelope, 0.45f) * (0.55f + band * 0.45f);
                    Color color = Palette(theme, Mathf.Repeat(identity + u * 0.3f, 1f), alpha);
                    colors[index + side] = color;
                }
            }
        }
        Upload(frame, theme, 5);
    }

    private void OnDisable()
    {
        Clear();
    }

    public void Clear()
    {
        onsetPulse = 0f;
    }
}
