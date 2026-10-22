using UnityEngine;

/// <summary>
/// Builds bilateral flowing ribbons in Screen-local metres. RenderFrame deforms cached strips;
/// independent phase and depth keep a readable central opening rather than a full-screen flash.
/// </summary>
public sealed class AudioAuroraRibbons : AudioVisualMesh
{
    [SerializeField, Range(1, 5)] private int ribbonsPerSide = 3;
    [SerializeField, Range(24, 192)] private int segments = 96;
    [SerializeField, Min(0.05f)] private float width = 0.32f;
    [SerializeField, Min(0.5f)] private float innerClearance = 1.05f;
    [SerializeField, Min(1f)] private float reach = 4.8f;
    [SerializeField, Min(0f)] private float depth = 3.2f;
    [SerializeField, Range(0f, 2f)] private float waveHeight = 0.85f;
    [SerializeField] private int seed = 4107;
    private int builtSegments;
    private int builtRibbonCount;

    public void RenderFrame(AudioVisualField.Frame frame, AudioVisualTheme theme)
    {
        int count = Mathf.Clamp(ribbonsPerSide, 1, 5) * 2;
        int steps = Mathf.Clamp(segments, 24, 192);
        if (mesh == null || builtSegments != steps || builtRibbonCount != count)
        {
            Initialize(count * (steps + 1) * 2, StripIndices(count, steps), MeshTopology.Triangles);
            builtSegments = steps;
            builtRibbonCount = count;
        }

        for (int ribbon = 0; ribbon < count; ribbon++)
        {
            float side = ribbon < count / 2 ? -1f : 1f;
            int layer = ribbon % (count / 2);
            float phase = seed * 0.013f + layer * 0.58f + side * 0.35f;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float flow = frame.time * (0.18f + theme.motion * 0.35f);
                float wave = Mathf.Sin(t * 4.2f - flow + phase)
                    + Mathf.Sin(t * 9f + flow * 0.6f + phase) * 0.12f;
                float envelope = Mathf.Sin(t * Mathf.PI);
                Vector3 center = new Vector3(
                    side * (innerClearance + t * reach + layer * 0.15f),
                    0.25f + layer * 0.32f + envelope * 0.4f + wave * waveHeight * (0.35f + frame.bass * 0.65f),
                    0.55f + t * depth + layer * 0.65f);
                float halfWidth = width * (0.45f + envelope * 0.55f) * (0.5f + frame.energy * 0.7f);
                int index = (ribbon * (steps + 1) + i) * 2;
                vertices[index] = center + Vector3.up * halfWidth;
                vertices[index + 1] = center - Vector3.up * halfWidth;
                coordinates[index] = new Vector2(t, 1f);
                coordinates[index + 1] = new Vector2(t, 0f);
                float alpha = envelope * frame.ribbonWeight * (0.25f + frame.energy * 0.85f);
                Color color = Palette(theme, Mathf.Repeat(t * 0.65f + layer * 0.2f, 1f), alpha);
                colors[index] = color;
                colors[index + 1] = color;
            }
        }
        Upload(frame, theme, 0);
    }
}
