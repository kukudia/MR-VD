using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Owns one dynamic visual mesh and its renderer. Initialize/Upload are called by an effect;
/// OnDestroy releases only the mesh this component created. Shared materials remain assets.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public abstract class AudioVisualMesh : MonoBehaviour
{
    protected Mesh mesh;
    protected Vector3[] vertices;
    protected Vector2[] coordinates;
    protected Color[] colors;
    protected MaterialPropertyBlock properties;
    private MeshRenderer meshRenderer;

    protected void Initialize(int vertexCount, int[] indices, MeshTopology topology)
    {
        if (mesh != null)
        {
            ReleaseMesh();
        }

        vertices = new Vector3[vertexCount];
        coordinates = new Vector2[vertexCount];
        colors = new Color[vertexCount];
        mesh = new Mesh { name = name + " (Generated)", hideFlags = HideFlags.DontSave };
        if (vertexCount > 65535)
        {
            mesh.indexFormat = IndexFormat.UInt32;
        }
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.SetIndices(indices, topology, 0);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        properties = new MaterialPropertyBlock();
    }

    protected void Upload(AudioVisualField.Frame frame, AudioVisualTheme theme, int style)
    {
        mesh.vertices = vertices;
        mesh.uv = coordinates;
        mesh.colors = colors;
        mesh.RecalculateBounds();
        meshRenderer.GetPropertyBlock(properties);
        properties.SetFloat("_Emission", theme.emission);
        properties.SetFloat("_Opacity", theme.opacity);
        properties.SetFloat("_Style", style);
        properties.SetFloat("_VisualTime", frame.time);
        properties.SetFloat("_Detail", frame.detail);
        properties.SetFloat("_FiberDensity", theme.fiberDensity);
        properties.SetFloat("_Turbulence", theme.turbulence);
        properties.SetFloat("_FlowSpeed", theme.flowSpeed);
        properties.SetFloat("_Sparkle", theme.sparkle);
        properties.SetFloat("_NearFadeStart", theme.fadeStartDistance);
        properties.SetFloat("_NearFadeEnd", Mathf.Max(theme.fadeStartDistance + 0.1f, theme.fullVisibilityDistance));
        properties.SetFloat("_ScreenEnabled", frame.hasScreen ? 1f : 0f);
        properties.SetMatrix("_ScreenWorldToLocal", frame.screenWorldToLocal);
        properties.SetVector("_ScreenRect", frame.screenRect);
        meshRenderer.SetPropertyBlock(properties);
    }

    protected static Color Palette(AudioVisualTheme theme, float position, float alpha)
    {
        Color color = Color.Lerp(theme.primary, theme.secondary, Mathf.Clamp01(position));
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    protected static int[] StripIndices(int strips, int segments)
    {
        int[] result = new int[strips * segments * 6];
        int cursor = 0;
        for (int strip = 0; strip < strips; strip++)
        {
            for (int i = 0; i < segments; i++)
            {
                int v = strip * (segments + 1) * 2 + i * 2;
                result[cursor++] = v;
                result[cursor++] = v + 2;
                result[cursor++] = v + 1;
                result[cursor++] = v + 1;
                result[cursor++] = v + 2;
                result[cursor++] = v + 3;
            }
        }
        return result;
    }

    protected virtual void OnDestroy()
    {
        ReleaseMesh();
    }

    // Explicit disposal is also required for never-enabled objects in Editor preview scenes:
    // Unity does not guarantee their MonoBehaviour.OnDestroy callback.
    public void ReleaseMesh()
    {
        if (mesh == null)
        {
            return;
        }
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh == mesh)
        {
            filter.sharedMesh = null;
        }
        if (Application.isPlaying)
        {
            Destroy(mesh);
        }
        else
        {
            DestroyImmediate(mesh);
        }
        mesh = null;
    }
}
