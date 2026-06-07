using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds a lightweight visible cone to a Unity spot light so URP stage lights read as beams in air.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public class StageVisibleLightBeam : MonoBehaviour
{
    private const string BeamObjectName = "__VisibleLightBeam";
    private const string BeamShaderName = "Stage/Visible Light Beam";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    [Tooltip("Master switch for the visible light beam mesh.")]
    public bool beamEnabled = true;

    [Tooltip("Hide the beam whenever the source Light is disabled.")]
    public bool hideWhenLightDisabled = true;

    [Tooltip("Scales the beam length from the source Light range.")]
    [Range(0.05f, 2f)] public float lengthScale = 1f;

    [Tooltip("Scales the beam width from the source Light spot angle.")]
    [Range(0.05f, 2f)] public float radiusScale = 1f;

    [Tooltip("Maximum beam opacity before intensity scaling.")]
    [Range(0f, 1f)] public float opacity = 0.24f;

    [Tooltip("Light intensity that maps to full beam opacity.")]
    [Range(0.1f, 50f)] public float intensityForFullOpacity = 8f;

    [Tooltip("Do not draw the beam below this light intensity.")]
    [Range(0f, 2f)] public float minVisibleIntensity = 0.01f;

    [Tooltip("Segments around the cone. Higher values are smoother.")]
    [Range(8, 64)] public int sideSegments = 24;

    [Tooltip("Segments along the cone. Higher values create a softer alpha falloff.")]
    [Range(2, 12)] public int lengthSegments = 5;

    [Tooltip("Tint the beam with the source Light color.")]
    public bool useLightColor = true;

    [Tooltip("Fade beam opacity based on source Light intensity.")]
    public bool scaleOpacityByIntensity = true;

    [Tooltip("Optional material override. Leave empty to use the built-in Stage beam material.")]
    public Material materialOverride;

    private Light sourceLight;
    private GameObject beamObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh beamMesh;
    private Material runtimeMaterial;
    private float cachedRange = -1f;
    private float cachedSpotAngle = -1f;
    private float cachedLengthScale = -1f;
    private float cachedRadiusScale = -1f;
    private int cachedSideSegments = -1;
    private int cachedLengthSegments = -1;

    public Light SourceLight
    {
        get
        {
            if (sourceLight == null)
            {
                sourceLight = GetComponent<Light>();
            }

            return sourceLight;
        }
    }

    public void Configure(float beamOpacity, float beamLengthScale, float beamRadiusScale, float fullOpacityIntensity)
    {
        opacity = Mathf.Clamp01(beamOpacity);
        lengthScale = Mathf.Clamp(beamLengthScale, 0.05f, 2f);
        radiusScale = Mathf.Clamp(beamRadiusScale, 0.05f, 2f);
        intensityForFullOpacity = Mathf.Max(0.1f, fullOpacityIntensity);
        ForceRefresh();
    }

    public void ForceRefresh()
    {
        cachedRange = -1f;
        cachedSpotAngle = -1f;
        UpdateBeam();
    }

    private void OnEnable()
    {
        sourceLight = GetComponent<Light>();
        EnsureBeamObject();
        ForceRefresh();
    }

    private void LateUpdate()
    {
        UpdateBeam();
    }

    private void OnValidate()
    {
        lengthScale = Mathf.Clamp(lengthScale, 0.05f, 2f);
        radiusScale = Mathf.Clamp(radiusScale, 0.05f, 2f);
        opacity = Mathf.Clamp01(opacity);
        intensityForFullOpacity = Mathf.Max(0.1f, intensityForFullOpacity);
        sideSegments = Mathf.Clamp(sideSegments, 8, 64);
        lengthSegments = Mathf.Clamp(lengthSegments, 2, 12);
        minVisibleIntensity = Mathf.Max(0f, minVisibleIntensity);
        ForceRefresh();
    }

    private void OnDisable()
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        DestroyGeneratedObjects();
    }

    private void UpdateBeam()
    {
        Light light = SourceLight;
        EnsureBeamObject();

        if (meshRenderer == null || light == null)
        {
            return;
        }

        bool visible = beamEnabled
            && light.type == LightType.Spot
            && (!hideWhenLightDisabled || light.enabled)
            && light.intensity > minVisibleIntensity
            && gameObject.activeInHierarchy;

        meshRenderer.enabled = visible;
        if (!visible)
        {
            return;
        }

        bool needsRebuild = !Mathf.Approximately(cachedRange, light.range)
            || !Mathf.Approximately(cachedSpotAngle, light.spotAngle)
            || !Mathf.Approximately(cachedLengthScale, lengthScale)
            || !Mathf.Approximately(cachedRadiusScale, radiusScale)
            || cachedSideSegments != sideSegments
            || cachedLengthSegments != lengthSegments;

        if (needsRebuild)
        {
            RebuildMesh(light);
        }

        Material beamMaterial = materialOverride != null ? materialOverride : runtimeMaterial;
        if (beamMaterial != null)
        {
            Color beamColor = useLightColor ? light.color : Color.white;
            beamColor.a = 1f;

            float intensityOpacity = scaleOpacityByIntensity
                ? Mathf.Clamp01(light.intensity / Mathf.Max(0.1f, intensityForFullOpacity))
                : 1f;

            if (beamMaterial.HasProperty(BaseColorId))
            {
                beamMaterial.SetColor(BaseColorId, beamColor);
            }

            if (beamMaterial.HasProperty(ColorId))
            {
                beamMaterial.SetColor(ColorId, beamColor);
            }

            if (beamMaterial.HasProperty(OpacityId))
            {
                beamMaterial.SetFloat(OpacityId, opacity * intensityOpacity);
            }
        }

        beamObject.layer = gameObject.layer;
        beamObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        beamObject.transform.localScale = Vector3.one;
    }

    private void EnsureBeamObject()
    {
        if (beamObject == null)
        {
            Transform existing = transform.Find(BeamObjectName);
            beamObject = existing != null ? existing.gameObject : new GameObject(BeamObjectName);
            beamObject.hideFlags = HideFlags.HideAndDontSave;
            beamObject.transform.SetParent(transform, false);
        }

        if (meshFilter == null)
        {
            meshFilter = beamObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = beamObject.AddComponent<MeshFilter>();
            }
        }

        if (meshRenderer == null)
        {
            meshRenderer = beamObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = beamObject.AddComponent<MeshRenderer>();
            }

            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            meshRenderer.allowOcclusionWhenDynamic = false;
        }

        if (beamMesh == null)
        {
            beamMesh = new Mesh
            {
                name = "Stage Visible Light Beam",
                hideFlags = HideFlags.HideAndDontSave
            };
            meshFilter.sharedMesh = beamMesh;
        }

        if (runtimeMaterial == null)
        {
            runtimeMaterial = CreateBeamMaterial();
        }

        meshRenderer.sharedMaterial = materialOverride != null ? materialOverride : runtimeMaterial;
    }

    private void RebuildMesh(Light light)
    {
        if (beamMesh == null)
        {
            return;
        }

        int sides = Mathf.Clamp(sideSegments, 8, 64);
        int rings = Mathf.Clamp(lengthSegments, 2, 12) + 1;
        int vertexCount = rings * sides;
        Vector3[] vertices = new Vector3[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] triangles = new int[(rings - 1) * sides * 6];

        float length = Mathf.Max(0.05f, light.range * lengthScale);
        float angle = Mathf.Clamp(light.spotAngle, 1f, 179f) * Mathf.Deg2Rad;
        float maxRadius = Mathf.Tan(angle * 0.5f) * length * radiusScale;

        for (int ring = 0; ring < rings; ring++)
        {
            float t = (float)ring / (rings - 1);
            float z = length * t;
            float radius = Mathf.Lerp(0.025f, maxRadius, t);
            float alpha = Mathf.Pow(1f - t, 1.7f);

            for (int side = 0; side < sides; side++)
            {
                float a = side / (float)sides * Mathf.PI * 2f;
                int index = ring * sides + side;
                vertices[index] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z);
                colors[index] = new Color(1f, 1f, 1f, alpha);
            }
        }

        int tri = 0;
        for (int ring = 0; ring < rings - 1; ring++)
        {
            int current = ring * sides;
            int next = (ring + 1) * sides;

            for (int side = 0; side < sides; side++)
            {
                int sideNext = (side + 1) % sides;
                triangles[tri++] = current + side;
                triangles[tri++] = next + side;
                triangles[tri++] = next + sideNext;
                triangles[tri++] = current + side;
                triangles[tri++] = next + sideNext;
                triangles[tri++] = current + sideNext;
            }
        }

        beamMesh.Clear();
        beamMesh.vertices = vertices;
        beamMesh.colors = colors;
        beamMesh.triangles = triangles;
        beamMesh.RecalculateBounds();
        beamMesh.UploadMeshData(false);

        cachedRange = light.range;
        cachedSpotAngle = light.spotAngle;
        cachedLengthScale = lengthScale;
        cachedRadiusScale = radiusScale;
        cachedSideSegments = sides;
        cachedLengthSegments = lengthSegments;
    }

    private Material CreateBeamMaterial()
    {
        Shader shader = Shader.Find(BeamShaderName);
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material material = new Material(shader)
        {
            name = "Stage Visible Light Beam Material",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent
        };

        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 2f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.One);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        return material;
    }

    private void DestroyGeneratedObjects()
    {
        DestroyGeneratedObject(beamMesh);
        DestroyGeneratedObject(runtimeMaterial);

        if (beamObject != null)
        {
            DestroyGeneratedObject(beamObject);
        }

        beamMesh = null;
        runtimeMaterial = null;
        beamObject = null;
        meshFilter = null;
        meshRenderer = null;
    }

    private static void DestroyGeneratedObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}

/// <summary>
/// Runtime bootstrap that makes every spot light in loaded scenes receive a visible beam.
/// </summary>
public static class StageVisibleLightBeamBootstrap
{
    private static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyToLoadedScenes();
    }

    public static int ApplyToLoadedScenes()
    {
        int count = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            count += ApplyToScene(SceneManager.GetSceneAt(i));
        }

        return count;
    }

    public static int ApplyToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return 0;
        }

        int count = 0;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Light[] lights = roots[i].GetComponentsInChildren<Light>(true);
            for (int j = 0; j < lights.Length; j++)
            {
                if (EnsureForLight(lights[j]) != null)
                {
                    count++;
                }
            }
        }

        return count;
    }

    public static StageVisibleLightBeam EnsureForLight(Light light)
    {
        if (light == null || light.type != LightType.Spot)
        {
            return null;
        }

        StageVisibleLightBeam beam = light.GetComponent<StageVisibleLightBeam>();
        if (beam == null)
        {
            beam = light.gameObject.AddComponent<StageVisibleLightBeam>();
        }

        return beam;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToScene(scene);
    }
}
