using UnityEngine;

/// <summary>
/// A sparse proscenium of triangular tubes. Stable seeded nodes follow display bands, while
/// accepted transients travel along the arches instead of flashing the complete network.
/// RenderFrame reuses geometry; this is an authored spectral mapping, not a music similarity graph.
/// </summary>
public sealed class AudioSpectralLattice : AudioVisualMesh
{
    [SerializeField, Range(12, 36)] private int nodesPerArch = 20;
    [SerializeField, Range(2, 5)] private int layers = 3;
    [SerializeField, Range(4, 16)] private int linkSegments = 8;
    [SerializeField, Min(0.001f)] private float filamentRadius = 0.008f;
    [SerializeField, Min(1f)] private float radius = 3.8f;
    [SerializeField, Range(0.3f, 1.2f)] private float heightRatio = 0.67f;
    [SerializeField, Min(0.05f)] private float depthSpacing = 0.42f;
    [SerializeField, Range(0f, 0.7f)] private float spectrumDisplacement = 0.24f;
    [SerializeField, Range(0.1f, 2f)] private float propagationSeconds = 0.75f;
    [SerializeField] private Vector3 center = new Vector3(0f, -0.45f, 4.2f);
    [SerializeField] private int seed = 4319;
    private Vector3[] nodes;
    private float[] bands;
    private int builtNodes, builtLayers, builtSteps;
    private readonly float[] hitTimes = new float[8];
    private readonly float[] hitStrengths = new float[8];
    private int hitCursor;
    public int NodeCount => nodes != null ? nodes.Length : 0;

    public void Clear()
    {
        System.Array.Clear(hitStrengths, 0, hitStrengths.Length);
        hitCursor = 0;
    }

    public void RenderFrame(AudioVisualField.Frame frame, AudioVisualTheme theme, AudioVisualField source)
    {
        int count = Mathf.Clamp(nodesPerArch, 12, 36);
        int rows = Mathf.Clamp(layers, 2, 5);
        int steps = Mathf.Clamp(linkSegments, 4, 16);
        int links = rows * (count - 1) + (rows - 1) * count;
        if (mesh == null || count != builtNodes || rows != builtLayers || steps != builtSteps)
        {
            // Three-sided tubes remain volumetric in stereo; no camera-facing basis is needed.
            int[] indices = new int[links * steps * 18];
            int cursor = 0;
            for (int link = 0; link < links; link++)
            {
                for (int step = 0; step < steps; step++)
                {
                    for (int side = 0; side < 3; side++)
                    {
                        int a = (link * (steps + 1) + step) * 3 + side;
                        int b = (link * (steps + 1) + step) * 3 + (side + 1) % 3;
                        indices[cursor++] = a;
                        indices[cursor++] = a + 3;
                        indices[cursor++] = b;
                        indices[cursor++] = b;
                        indices[cursor++] = a + 3;
                        indices[cursor++] = b + 3;
                    }
                }
            }
            Initialize(links * (steps + 1) * 3, indices, UnityEngine.MeshTopology.Triangles);
            nodes = new Vector3[count * rows];
            bands = new float[nodes.Length];
            builtNodes = count;
            builtLayers = rows;
            builtSteps = steps;
        }
        if (frame.onset)
        {
            hitTimes[hitCursor] = frame.time;
            hitStrengths[hitCursor] = Mathf.Clamp01(frame.onsetStrength);
            hitCursor = (hitCursor + 1) % hitTimes.Length;
        }
        for (int row = 0; row < rows; row++)
        {
            for (int n = 0; n < count; n++)
            {
                int id = row * count + n;
                float t = n / (float)(count - 1);
                float phase = id * 2.399963f + seed * 0.019f;
                float band = source != null ? source.GetSpectrumBand(Mathf.RoundToInt(t * 47f)) : 0f;
                bands[id] = band;
                float angle = Mathf.Lerp(-0.18f, Mathf.PI + 0.18f, t);
                float r = radius + row * 0.13f + band * spectrumDisplacement;
                nodes[id] = center + new Vector3(Mathf.Cos(angle) * r,
                    Mathf.Sin(angle) * r * heightRatio,
                    row * depthSpacing + Mathf.Sin(phase + frame.time * theme.motion * 0.22f) * 0.12f);
            }
        }
        int linkIndex = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int n = 0; n < count - 1; n++)
            {
                WriteLink(linkIndex++, row * count + n, row * count + n + 1, steps, frame, theme);
            }
        }
        for (int row = 0; row < rows - 1; row++)
        {
            for (int n = 0; n < count; n++)
            {
                WriteLink(linkIndex++, row * count + n, (row + 1) * count + n, steps, frame, theme);
            }
        }
        Upload(frame, theme, 4);
    }

    private void WriteLink(int link, int first, int last, int steps,
        AudioVisualField.Frame frame, AudioVisualTheme theme)
    {
        Vector3 from = nodes[first];
        Vector3 to = nodes[last];
        Vector3 direction = (to - from).normalized;
        Vector3 normal = Vector3.Cross(direction, Vector3.up);
        if (normal.sqrMagnitude < 0.01f)
        {
            normal = Vector3.Cross(direction, Vector3.forward);
        }
        normal.Normalize();
        Vector3 binormal = Vector3.Cross(direction, normal);
        for (int step = 0; step <= steps; step++)
        {
            float t = step / (float)steps;
            float band = Mathf.Lerp(bands[first], bands[last], t);
            float position = Mathf.Lerp(first % builtNodes, last % builtNodes, t) / (builtNodes - 1f);
            float pulse = 0f;
            for (int hit = 0; hit < hitTimes.Length; hit++)
            {
                float age = frame.time - hitTimes[hit] - position * propagationSeconds;
                if (age >= 0f && age < 0.65f)
                {
                    pulse = Mathf.Max(pulse, hitStrengths[hit] * Mathf.Exp(-age * 9f));
                }
            }
            float endpoint = Mathf.Pow(Mathf.Abs(t * 2f - 1f), 16f);
            float width = filamentRadius * (1f + endpoint * 2.5f + band * 0.5f + pulse * 0.5f);
            Vector3 p = Vector3.Lerp(from, to, t) + normal * Mathf.Sin(t * Mathf.PI)
                * Mathf.Sin(link * 1.7f + frame.time * 0.3f) * 0.045f;
            Color color = Color.Lerp(Palette(theme, position, 1f), theme.accent, pulse * 0.8f);
            color.a = theme.lattice * frame.ribbonWeight * (0.16f + band * 0.25f + pulse * 0.55f + endpoint * 0.15f);
            for (int side = 0; side < 3; side++)
            {
                float angle = side * Mathf.PI * 2f / 3f;
                int index = (link * (steps + 1) + step) * 3 + side;
                vertices[index] = p + (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * width;
                coordinates[index] = new Vector2(t, side / 2f);
                colors[index] = color;
            }
        }
    }
}
