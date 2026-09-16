using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a twelve-segment chroma wheel and emphasizes the detected pitch class.
/// </summary>
public sealed class AudioChromaWheelGraphic : MaskableGraphic
{
    [Range(-1, 11)]
    public int highlightedSegment = -1;

    [Range(0f, 1f)]
    public float beatPulse;

    private static readonly Color[] SegmentColors =
    {
        new Color(0.25f, 0.85f, 0.95f), new Color(0.18f, 0.9f, 0.68f), new Color(0.32f, 0.92f, 0.38f),
        new Color(0.72f, 0.95f, 0.24f), new Color(1f, 0.78f, 0.2f), new Color(1f, 0.5f, 0.3f),
        new Color(1f, 0.38f, 0.5f), new Color(0.95f, 0.3f, 0.7f), new Color(0.72f, 0.35f, 0.95f),
        new Color(0.45f, 0.43f, 0.95f), new Color(0.35f, 0.62f, 1f), new Color(0.35f, 0.78f, 1f)
    };

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
        float outerRadius = radius * 0.94f;
        float innerRadius = radius * 0.42f;
        float gap = Mathf.Deg2Rad * 2.2f;

        for (int i = 0; i < 12; i++)
        {
            float start = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / 12f + gap;
            float end = -Mathf.PI * 0.5f + (i + 1) * Mathf.PI * 2f / 12f - gap;
            float currentOuterRadius = i == highlightedSegment
                ? outerRadius * (1f + 0.04f * (1f + beatPulse))
                : outerRadius;
            Color segmentColor = SegmentColors[i];
            if (i == highlightedSegment)
            {
                segmentColor = Color.Lerp(segmentColor, Color.white, 0.38f + beatPulse * 0.18f);
            }
            else
            {
                segmentColor.a = 0.78f;
            }

            AddSegment(vh, innerRadius, currentOuterRadius, start, end, segmentColor);
        }
    }

    private static void AddSegment(VertexHelper vh, float innerRadius, float outerRadius, float start, float end, Color color)
    {
        const int arcSteps = 4;
        int vertexStart = vh.currentVertCount;
        for (int step = 0; step <= arcSteps; step++)
        {
            float angle = Mathf.Lerp(start, end, step / (float)arcSteps);
            vh.AddVert(ToPosition(innerRadius, angle), color, Vector2.zero);
            vh.AddVert(ToPosition(outerRadius, angle), color, Vector2.zero);
        }

        for (int step = 0; step < arcSteps; step++)
        {
            int innerStart = vertexStart + step * 2;
            vh.AddTriangle(innerStart, innerStart + 1, innerStart + 3);
            vh.AddTriangle(innerStart, innerStart + 3, innerStart + 2);
        }
    }

    private static Vector3 ToPosition(float radius, float angle)
    {
        return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
    }
}
