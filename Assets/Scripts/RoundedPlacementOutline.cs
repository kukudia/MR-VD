using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class RoundedPlacementOutline : MaskableGraphic
{
    [SerializeField, Min(0.001f)] private float lineWidth = 0.006f;
    [SerializeField, Min(0.001f)] private float cornerRadius = 0.025f;
    [SerializeField, Range(2, 16)] private int cornerSegments = 8;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        float halfWidth = Mathf.Min(lineWidth * 0.5f, Mathf.Min(rect.width, rect.height) * 0.25f);
        float radius = Mathf.Clamp(cornerRadius, halfWidth, Mathf.Min(rect.width, rect.height) * 0.5f - halfWidth);
        float centerX = rect.width * 0.5f - radius;
        float centerY = rect.height * 0.5f - radius;
        Vector2 rectCenter = rect.center;
        Color32 tint = color;

        for (int corner = 0; corner < 4; corner++)
        {
            float startAngle = corner * 90f;
            Vector2 center = rectCenter + new Vector2(
                corner == 0 || corner == 3 ? centerX : -centerX,
                corner < 2 ? centerY : -centerY);
            for (int segment = 0; segment <= cornerSegments; segment++)
            {
                float angle = (startAngle + segment * 90f / cornerSegments) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddVertex(mesh, center + direction * (radius + halfWidth), tint);
                AddVertex(mesh, center + direction * (radius - halfWidth), tint);
            }
        }

        int pairCount = 4 * (cornerSegments + 1);
        for (int pair = 0; pair < pairCount; pair++)
        {
            int next = (pair + 1) % pairCount;
            int outer = pair * 2;
            int nextOuter = next * 2;
            mesh.AddTriangle(outer, nextOuter, outer + 1);
            mesh.AddTriangle(outer + 1, nextOuter, nextOuter + 1);
        }
    }

    private static void AddVertex(VertexHelper mesh, Vector2 position, Color32 tint)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = tint;
        mesh.AddVert(vertex);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        lineWidth = Mathf.Max(0.001f, lineWidth);
        cornerRadius = Mathf.Max(0.001f, cornerRadius);
    }
#endif
}
