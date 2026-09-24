using UnityEngine;
using UnityEngine.UI;

public sealed class PerformanceHistoryGraphic : Graphic
{
    [SerializeField, Range(16, 120)] private int capacity = 60;
    [SerializeField] private float maximum = 100f;
    [SerializeField] private bool autoScale;

    private float[] samples;
    private int count;
    private int next;

    public void Configure(float maximumValue, Color lineColor)
    {
        autoScale = maximumValue <= 0f;
        maximum = Mathf.Max(0.01f, maximumValue);
        color = lineColor;
        raycastTarget = false;
        SetVerticesDirty();
    }

    public void AddSample(float value)
    {
        if (samples == null || samples.Length != capacity)
        {
            samples = new float[capacity];
            count = 0;
            next = 0;
        }

        samples[next] = value;
        next = (next + 1) % capacity;
        count = Mathf.Min(count + 1, capacity);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (samples == null || count < 2)
        {
            return;
        }

        Rect rect = GetPixelAdjustedRect();
        int first = (next - count + capacity) % capacity;
        float scale = maximum;
        if (autoScale)
        {
            scale = 1f;
            for (int i = 0; i < count; i++)
            {
                float value = samples[(first + i) % capacity];
                if (!float.IsNaN(value))
                {
                    scale = Mathf.Max(scale, value * 1.2f);
                }
            }
        }

        for (int i = 1; i < count; i++)
        {
            float a = samples[(first + i - 1) % capacity];
            float b = samples[(first + i) % capacity];
            if (float.IsNaN(a) || float.IsNaN(b))
            {
                continue;
            }

            Vector2 from = new Vector2(rect.xMin + rect.width * (i - 1) / (capacity - 1), rect.yMin + rect.height * Mathf.Clamp01(a / scale));
            Vector2 to = new Vector2(rect.xMin + rect.width * i / (capacity - 1), rect.yMin + rect.height * Mathf.Clamp01(b / scale));
            Vector2 normal = new Vector2(-(to.y - from.y), to.x - from.x).normalized * 0.7f;
            int index = vh.currentVertCount;
            AddVertex(vh, from - normal);
            AddVertex(vh, from + normal);
            AddVertex(vh, to + normal);
            AddVertex(vh, to - normal);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }

    private void AddVertex(VertexHelper vh, Vector2 position)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = position;
        vh.AddVert(vertex);
    }
}
