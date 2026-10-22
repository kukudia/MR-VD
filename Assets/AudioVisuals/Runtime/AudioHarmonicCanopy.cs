using UnityEngine;

/// <summary>
/// Renders stable, band-driven braided arcs above and around the reading aperture. RenderFrame
/// reuses one strip mesh; a seeded analytic weave keeps authored shape reproducible at any time.
/// The name describes harmonic-looking geometry, not detected musical harmonics.
/// </summary>
public sealed class AudioHarmonicCanopy : AudioVisualMesh
{
    [SerializeField, Range(6, 24)] private int strands = 15;
    [SerializeField, Range(48, 256)] private int segments = 160;
    [SerializeField, Min(0.001f)] private float strandWidth = 0.021f;
    [SerializeField, Min(1f)] private float radius = 3.0f;
    [SerializeField, Range(0.3f, 1.5f)] private float heightRatio = 0.75f;
    [SerializeField, Min(0f)] private float layerSpacing = 0.085f;
    [SerializeField, Range(0f, 0.5f)] private float braidWidth = 0.22f;
    [SerializeField, Range(2, 24)] private int braidTurns = 9;
    [SerializeField, Range(0f, 1f)] private float spectrumDisplacement = 0.5f;
    [SerializeField] private Vector3 center = new Vector3(0f, -0.25f, 3.4f);
    [SerializeField] private int seed = 2079;
    private int builtStrands;
    private int builtSegments;

    public void RenderFrame(AudioVisualField.Frame frame, AudioVisualTheme theme, AudioVisualField source)
    {
        int count = Mathf.Clamp(strands, 6, 24);
        int steps = Mathf.Clamp(segments, 48, 256);
        if (mesh == null || builtStrands != count || builtSegments != steps)
        {
            Initialize(count * (steps + 1) * 2, StripIndices(count, steps), MeshTopology.Triangles);
            builtStrands = count;
            builtSegments = steps;
        }
        float motion = frame.time * (0.12f + theme.motion * 0.3f);
        for (int strand = 0; strand < count; strand++)
        {
            float identity = strand / (float)(count - 1);
            float phase = strand * 2.39996f + seed * 0.017f;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float angle = Mathf.Lerp(-0.12f, Mathf.PI + 0.12f, t);
                float band = source != null ? source.GetSpectrumBand(Mathf.RoundToInt(t * 47f)) : 0f;
                float weave = angle * braidTurns + phase - motion;
                float radial = radius + (strand - (count - 1) * 0.5f) * layerSpacing
                    + Mathf.Sin(weave) * braidWidth + band * spectrumDisplacement;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * heightRatio, 0f);
                Vector3 position = center + direction * radial;
                position.z += Mathf.Cos(weave) * braidWidth * 1.8f + identity * 0.45f
                    + Mathf.Sin(angle * 3f + phase * 0.4f + motion * 0.3f) * 0.14f;
                Vector3 across = direction.normalized * strandWidth * (0.7f + band * 0.6f);
                int index = (strand * (steps + 1) + i) * 2;
                vertices[index] = position + across;
                vertices[index + 1] = position - across;
                coordinates[index] = new Vector2(t, 1f);
                coordinates[index + 1] = new Vector2(t, 0f);
                float fade = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.4f);
                float alpha = fade * theme.canopy * frame.ribbonWeight * (0.3f + band * 0.7f);
                Color color = Palette(theme, Mathf.Repeat(identity * 0.65f + t * 0.3f, 1f), alpha);
                colors[index] = color;
                colors[index + 1] = color;
            }
        }
        Upload(frame, theme, 3);
    }
}
