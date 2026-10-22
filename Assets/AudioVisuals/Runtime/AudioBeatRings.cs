using UnityEngine;

/// <summary>
/// Keeps a fixed pool of floor-plane annuli. RenderFrame consumes one accepted onset and advances
/// each ring's analytic age; expired rings become transparent without creating GameObjects.
/// </summary>
public sealed class AudioBeatRings : AudioVisualMesh
{
    [SerializeField, Range(2, 12)] private int capacity = 6;
    [SerializeField, Range(32, 160)] private int segments = 96;
    [SerializeField, Min(0.1f)] private float duration = 2.4f;
    [SerializeField, Min(0.1f)] private float initialRadius = 0.55f;
    [SerializeField, Min(0.1f)] private float finalRadius = 5.5f;
    [SerializeField, Min(0.01f)] private float thickness = 0.045f;
    [SerializeField] private Vector3 center = new Vector3(0f, -0.85f, 1.6f);
    private Ring[] rings;
    private int cursor;
    private int builtSegments;
    public int EmittedCount { get; private set; }
    public int LiveCount { get; private set; }

    private struct Ring
    {
        public float birth;
        public float strength;
        public bool active;
    }

    public void Clear()
    {
        if (rings != null)
        {
            System.Array.Clear(rings, 0, rings.Length);
        }
        LiveCount = 0;
    }

    public void RenderFrame(AudioVisualField.Frame frame, AudioVisualTheme theme)
    {
        int count = Mathf.Clamp(capacity, 2, 12);
        int steps = Mathf.Clamp(segments, 32, 160);
        if (mesh == null || rings == null || rings.Length != count || builtSegments != steps)
        {
            Initialize(count * (steps + 1) * 2, StripIndices(count, steps), MeshTopology.Triangles);
            rings = new Ring[count];
            builtSegments = steps;
            cursor = 0;
        }

        if (frame.onset && frame.ringWeight > 0.01f)
        {
            rings[cursor] = new Ring { birth = frame.time, strength = frame.onsetStrength, active = true };
            cursor = (cursor + 1) % rings.Length;
            EmittedCount++;
        }

        LiveCount = 0;
        for (int ring = 0; ring < count; ring++)
        {
            Ring state = rings[ring];
            float age = Mathf.Clamp01((frame.time - state.birth) / Mathf.Max(0.1f, duration));
            if (age >= 1f)
            {
                state.active = false;
                rings[ring] = state;
            }
            if (state.active)
            {
                LiveCount++;
            }
            float radius = Mathf.Lerp(initialRadius, finalRadius, 1f - Mathf.Pow(1f - age, 1.4f));
            float halfWidth = thickness * (0.6f + state.strength * 0.7f);
            float alpha = state.active
                ? Mathf.Pow(1f - age, 2f) * state.strength * frame.ringWeight
                : 0f;
            Color color = Color.Lerp(theme.accent, theme.secondary, age);
            color.a = alpha;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float angle = t * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                int index = (ring * (steps + 1) + i) * 2;
                vertices[index] = center + direction * (radius + halfWidth);
                vertices[index + 1] = center + direction * Mathf.Max(0f, radius - halfWidth);
                coordinates[index] = new Vector2(t, 1f);
                coordinates[index + 1] = new Vector2(t, 0f);
                colors[index] = color;
                colors[index + 1] = color;
            }
        }
        Upload(frame, theme, 1);
    }

    private void OnDisable()
    {
        Clear();
    }
}
