using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Runs an independent GPU star-dust volume around the active camera. Audio energy
/// changes the drift and twinkle, while the Screen plane can occlude the particles.
/// UpdateSimulation optionally attracts particles to Screen-local galaxy/torus targets;
/// Stardust remains the serialized default. OnDisable/OnDestroy release all owned GPU resources.
/// A bound spatial field coordinates palette/visibility after its frame update; unbound use is independent.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(130)]
public sealed class GpuAudioParticleVisualizer : MonoBehaviour
{
    public enum ParticleFormation { Stardust, SpiralGalaxy, Torus, FlowKnot, CurlNebula }

    private const int ThreadGroupSize = 64;
    private const int ParticleStride = 12 * sizeof(float);

    [Header("Audio Source")]
    [Tooltip("Audio energy source. Automatically resolved when empty.")]
    public AudioVisualizer audioVisualizer;

    [Header("Camera Volume")]
    [Tooltip("Camera that anchors the star-dust volume. Automatically resolves to MainCamera.")]
    public Camera targetCamera;

    [Tooltip("World-space Screen/Canvas plane used for explicit occlusion.")]
    public RectTransform occlusionScreen;

    [Min(64)] public int particleCount = 3072;
    [Min(0.5f)] public float volumeRadius = 6f;
    [Range(0f, 2f)] public float minimumDistance = 0.35f;

    [Header("Motion")]
    [Min(0f)] public float backgroundSpeed = 0.12f;
    [Min(0.01f)] public float motionSmoothing = 0.7f;
    [Min(0f)] public float bassGain = 10f;
    [Min(0f)] public float synthGain = 9f;
    [Range(0f, 0.25f)] public float silentBackgroundLevel = 0.035f;

    [Header("Color")]
    [ColorUsage(true, true)] public Color backgroundColor = new Color(0.14f, 0.3f, 0.62f, 1f);
    [ColorUsage(true, true)] public Color accentColor = new Color(0.65f, 0.95f, 1f, 1f);
    [GradientUsage(true)] public Gradient particleGradient = new Gradient();
    [Range(0f, 0.2f)] public float gradientSpeed = 0.025f;

    [Header("Optional Spatial Formation")]
    [SerializeField] private ParticleFormation formation;
    [Tooltip("World-space anchor; defaults to this object. Head motion never rotates the formation.")]
    [SerializeField] private Transform formationAnchor;
    [SerializeField, Min(0.5f)] private float formationRadius = 3.5f;
    [SerializeField, Range(0.05f, 1f)] private float formationThickness = 0.22f;
    [SerializeField, Range(0.1f, 5f)] private float formationAttraction = 1.2f;
    [SerializeField, Range(0f, 0.5f)] private float formationRotationSpeed = 0.06f;
    [SerializeField, Min(0.1f)] private float formationTransitionSeconds = 3f;
    [SerializeField] private AudioVisualField spatialField;
    [Header("Formation Microstructure")]
    [Tooltip("Filament separation, halo particles and braided orbit lanes. Zero restores simple targets.")]
    [SerializeField, Range(0f, 1f)] private float formationDetail = 0.75f;
    [SerializeField, Range(0f, 1f)] private float spectralResponse = 0.65f;
    [Tooltip("Odd lobe counts keep the two-turn flow knot as one closed path.")]
    [SerializeField, Range(3, 7)] private int knotLobes = 3;
    [SerializeField, Range(0.1f, 0.4f)] private float knotDepth = 0.28f;
    private float formationBlend;
    private float torusBlend;
    private float knotBlend;
    private float curlBlend;
    [Header("Curl Nebula Flow")]
    [SerializeField, Range(0.5f, 8f)] private float curlScale = 3.2f;
    [SerializeField, Range(0.05f, 1.2f)] private float curlSpeed = 0.5f;
    [SerializeField, Range(0.1f, 3f)] private float curlConfinement = 0.8f;

    public ParticleFormation Formation => formation;

    public void SetFormation(ParticleFormation value)
    {
        formation = value;
    }

    [Header("Rendering")]
    [Min(0.001f)] public float particleSize = 0.014f;
    [Min(0f)] public float emission = 1.2f;
    [Range(0f, 1f)] public float opacity = 0.55f;
    [Range(0f, 1f)] public float saturation = 0.5f;
    [Tooltip("Velocity-aligned short glints, capped at three times the base radius. Zero keeps round dust.")]
    [Range(0f, 4f)] public float motionStretch;
    [Min(0f)] public float occlusionPadding = 0.02f;

    [Header("GPU Assets")]
    public ComputeShader particleCompute;
    public Shader particleShader;

    private GraphicsBuffer particleBuffer;
    private ComputeShader runtimeCompute;
    private Material runtimeMaterial;
    private MaterialPropertyBlock materialProperties;
    private int initializeKernel;
    private int updateKernel;
    private int totalParticleCount;
    private Vector3 previousCameraPosition;
    private bool hasPreviousCameraPosition;
    private bool resourcesReady;
    private bool warnedAboutSupport;
    private Texture2D gradientTexture;
    private bool gradientDirty = true;
    private readonly Color[] gradientPixels = new Color[128];

    private static readonly int ParticlesId = Shader.PropertyToID("_Particles");
    private static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");
    private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
    private static readonly int SimulationTimeId = Shader.PropertyToID("_SimulationTime");
    private static readonly int CameraDeltaId = Shader.PropertyToID("_CameraDelta");
    private static readonly int VolumeRadiusId = Shader.PropertyToID("_VolumeRadius");
    private static readonly int MinimumDistanceId = Shader.PropertyToID("_MinimumDistance");
    private static readonly int BackgroundSpeedId = Shader.PropertyToID("_BackgroundSpeed");
    private static readonly int MotionSmoothingId = Shader.PropertyToID("_MotionSmoothing");
    private static readonly int BassId = Shader.PropertyToID("_Bass");
    private static readonly int SynthId = Shader.PropertyToID("_Synth");
    private static readonly int EnergyId = Shader.PropertyToID("_Energy");
    private static readonly int BackgroundColorId = Shader.PropertyToID("_BackgroundColor");
    private static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");
    private static readonly int ParticleCenterWSId = Shader.PropertyToID("_ParticleCenterWS");
    private static readonly int OcclusionWorldToLocalId = Shader.PropertyToID("_OcclusionWorldToLocal");
    private static readonly int OcclusionRectId = Shader.PropertyToID("_OcclusionRect");
    private static readonly int OcclusionEnabledId = Shader.PropertyToID("_OcclusionEnabled");
    private static readonly int ParticleSizeId = Shader.PropertyToID("_ParticleSize");
    private static readonly int EmissionId = Shader.PropertyToID("_Emission");
    private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    public int TotalParticleCount => totalParticleCount;
    public int ConfiguredParticleCount => particleCount;
    public bool IsReady => resourcesReady;

    private void OnEnable()
    {
        ResolveReferences();
        TryCreateResources();
    }

    private void OnDisable()
    {
        ReleaseResources();
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    private void OnValidate()
    {
        particleCount = Mathf.Max(ThreadGroupSize, particleCount);
        volumeRadius = Mathf.Max(0.5f, volumeRadius);
        minimumDistance = Mathf.Clamp(minimumDistance, 0f, volumeRadius - 0.05f);
        backgroundSpeed = Mathf.Max(0f, backgroundSpeed);
        motionSmoothing = Mathf.Max(0.01f, motionSmoothing);
        gradientDirty = true;
    }

    private void Update()
    {
        if (!resourcesReady)
        {
            TryCreateResources();
            return;
        }

        ResolveReferences();
        if (targetCamera == null)
        {
            return;
        }

        float deltaTime = Mathf.Min(Time.deltaTime, 0.05f);
        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 cameraDelta = hasPreviousCameraPosition
            ? cameraPosition - previousCameraPosition
            : Vector3.zero;
        previousCameraPosition = cameraPosition;
        hasPreviousCameraPosition = true;

        if (cameraDelta.sqrMagnitude > volumeRadius * volumeRadius)
        {
            runtimeCompute.Dispatch(initializeKernel, GetThreadGroupCount(totalParticleCount), 1, 1);
            cameraDelta = Vector3.zero;
        }

        UpdateSimulation(deltaTime, cameraDelta);
        RenderParticles(cameraPosition);
    }

    private void ResolveReferences()
    {
        if (audioVisualizer == null)
        {
            audioVisualizer = FindFirstObjectByType<AudioVisualizer>();
        }

        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.isActiveAndEnabled)
            {
                targetCamera = mainCamera;
            }
            else
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i].isActiveAndEnabled)
                    {
                        targetCamera = cameras[i];
                        break;
                    }
                }
            }
        }

        if (occlusionScreen == null && transform.parent != null)
        {
            Canvas canvas = transform.parent.GetComponentInChildren<Canvas>(true);
            occlusionScreen = canvas != null ? canvas.transform as RectTransform : null;
        }
    }

    private void TryCreateResources()
    {
        if (resourcesReady || !isActiveAndEnabled)
        {
            return;
        }

        if (!SystemInfo.supportsComputeShaders)
        {
            if (!warnedAboutSupport)
            {
                Debug.LogWarning("[GpuAudioParticleVisualizer] Compute shaders are not supported on this graphics device.", this);
                warnedAboutSupport = true;
            }
            return;
        }

        if (particleCompute == null || particleShader == null)
        {
            return;
        }

        ReleaseResources();
        totalParticleCount = particleCount;
        particleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalParticleCount, ParticleStride);
        runtimeCompute = Instantiate(particleCompute);
        runtimeMaterial = new Material(particleShader)
        {
            name = "GPU Camera Stardust (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        materialProperties = new MaterialPropertyBlock();

        initializeKernel = runtimeCompute.FindKernel("InitializeParticles");
        updateKernel = runtimeCompute.FindKernel("UpdateParticles");
        runtimeCompute.SetBuffer(initializeKernel, ParticlesId, particleBuffer);
        runtimeCompute.SetBuffer(updateKernel, ParticlesId, particleBuffer);
        SetStaticSimulationParameters();
        runtimeCompute.Dispatch(initializeKernel, GetThreadGroupCount(totalParticleCount), 1, 1);
        resourcesReady = true;
    }

    private void SetStaticSimulationParameters()
    {
        UpdateGradientTexture();
        runtimeCompute.SetInt(ParticleCountId, totalParticleCount);
        runtimeCompute.SetFloat(VolumeRadiusId, volumeRadius);
        runtimeCompute.SetFloat(MinimumDistanceId, minimumDistance);
        runtimeCompute.SetFloat(BackgroundSpeedId, backgroundSpeed);
        runtimeCompute.SetFloat(MotionSmoothingId, motionSmoothing);
        runtimeCompute.SetVector(BackgroundColorId, backgroundColor);
        runtimeCompute.SetVector(AccentColorId, accentColor);
        runtimeCompute.SetTexture(updateKernel, "_Gradient", gradientTexture);
        runtimeCompute.SetFloat("_GradientSpeed", gradientSpeed);
    }

    private void UpdateGradientTexture()
    {
        if (gradientTexture == null)
        {
            gradientTexture = new Texture2D(128, 1, TextureFormat.RGBAHalf, false, true)
            {
                name = "Stardust Gradient (Runtime)", wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
            };
            gradientDirty = true;
        }
        if (!gradientDirty)
        {
            return;
        }
        for (int i = 0; i < gradientPixels.Length; i++)
        {
            gradientPixels[i] = particleGradient.Evaluate(i / (float)(gradientPixels.Length - 1)).linear;
        }
        gradientTexture.SetPixels(gradientPixels);
        gradientTexture.Apply(false, false);
        gradientDirty = false;
    }

    private void UpdateSimulation(float deltaTime, Vector3 cameraDelta)
    {
        float bass = 0f;
        float synth = 0f;
        bool silent = true;
        if (audioVisualizer != null)
        {
            bass = CompressEnergy(audioVisualizer.smoothedBassEnergy, bassGain);
            synth = CompressEnergy(audioVisualizer.smoothedSynthEnergy, synthGain);
            silent = audioVisualizer.wasSilent;
        }
        if (spatialField != null && spatialField.isActiveAndEnabled && spatialField.UsesDemonstration)
        {
            bass = spatialField.CurrentFrame.bass;
            synth = spatialField.CurrentFrame.detail;
            silent = false;
        }

        synth = Mathf.Max(synth, silent ? silentBackgroundLevel : 0f);
        float energy = Mathf.Clamp01(bass * 0.55f + synth * 0.45f);

        SetStaticSimulationParameters();
        runtimeCompute.SetFloat(DeltaTimeId, deltaTime);
        runtimeCompute.SetFloat(SimulationTimeId, Time.time);
        runtimeCompute.SetVector(CameraDeltaId, cameraDelta);
        runtimeCompute.SetFloat(BassId, bass);
        runtimeCompute.SetFloat(SynthId, synth);
        runtimeCompute.SetFloat(EnergyId, energy);
        UpdateFormation(deltaTime);
        runtimeCompute.Dispatch(updateKernel, GetThreadGroupCount(totalParticleCount), 1, 1);
    }

    private void UpdateFormation(float deltaTime)
    {
        float response = 1f - Mathf.Exp(-deltaTime * 3f / Mathf.Max(0.1f, formationTransitionSeconds));
        formationBlend = Mathf.Lerp(formationBlend, formation == ParticleFormation.Stardust ? 0f : 1f, response);
        torusBlend = Mathf.Lerp(torusBlend, formation == ParticleFormation.Torus ? 1f : 0f, response);
        knotBlend = Mathf.Lerp(knotBlend, formation == ParticleFormation.FlowKnot ? 1f : 0f, response);
        curlBlend = Mathf.Lerp(curlBlend, formation == ParticleFormation.CurlNebula ? 1f : 0f, response);
        Transform anchor = formationAnchor != null ? formationAnchor : transform;
        runtimeCompute.SetFloat("_FormationBlend", formationBlend);
        runtimeCompute.SetFloat("_TorusBlend", torusBlend);
        runtimeCompute.SetFloat("_KnotBlend", knotBlend);
        runtimeCompute.SetFloat("_CurlBlend", curlBlend);
        runtimeCompute.SetFloat("_CurlScale", curlScale);
        runtimeCompute.SetFloat("_CurlSpeed", curlSpeed);
        runtimeCompute.SetFloat("_CurlConfinement", curlConfinement);
        runtimeCompute.SetFloat("_KnotLobes", Mathf.Clamp(knotLobes | 1, 3, 7));
        runtimeCompute.SetFloat("_KnotDepth", knotDepth);
        runtimeCompute.SetFloat("_FormationRadius", Mathf.Min(formationRadius, volumeRadius * 0.65f));
        runtimeCompute.SetFloat("_FormationThickness", formationThickness);
        runtimeCompute.SetFloat("_FormationAttraction", formationAttraction);
        runtimeCompute.SetFloat("_FormationRotationSpeed", formationRotationSpeed);
        runtimeCompute.SetFloat("_FormationDetail", formationDetail);
        Vector4 shape = spatialField != null && spatialField.isActiveAndEnabled
            ? spatialField.CurrentFrame.spectralShape * spectralResponse : Vector4.zero;
        runtimeCompute.SetVector("_SpectralShape", shape);
        runtimeCompute.SetVector("_FormationCenter", anchor.position - targetCamera.transform.position);
        runtimeCompute.SetVector("_FormationRight", anchor.right);
        runtimeCompute.SetVector("_FormationUp", anchor.up);
        runtimeCompute.SetVector("_FormationForward", anchor.forward);
        bool coordinated = spatialField != null && spatialField.isActiveAndEnabled && spatialField.Theme != null;
        runtimeCompute.SetFloat("_UseSpatialPalette", coordinated ? 1f : 0f);
        if (coordinated)
        {
            runtimeCompute.SetVector("_SpatialPrimary", spatialField.DisplayedTheme.primary.linear);
            runtimeCompute.SetVector("_SpatialSecondary", spatialField.DisplayedTheme.secondary.linear);
        }
    }

    private void RenderParticles(Vector3 cameraPosition)
    {
        if (runtimeMaterial == null || particleBuffer == null)
        {
            return;
        }

        materialProperties.Clear();
        materialProperties.SetBuffer(ParticlesId, particleBuffer);
        materialProperties.SetVector(ParticleCenterWSId, cameraPosition);
        materialProperties.SetFloat(ParticleSizeId, particleSize);
        materialProperties.SetFloat(EmissionId, emission);
        float visibility = spatialField == null ? 1f
            : spatialField.isActiveAndEnabled && spatialField.Theme != null
                ? Mathf.Clamp01(spatialField.CurrentFrame.visibility) : 0f;
        // Keep simulation alive during blackout for a continuous reveal, but submit zero light.
        materialProperties.SetFloat(OpacityId, opacity * visibility);
        materialProperties.SetFloat(SaturationId, saturation);
        materialProperties.SetFloat("_MotionStretch", motionStretch);

        if (occlusionScreen != null)
        {
            Rect rect = occlusionScreen.rect;
            materialProperties.SetMatrix(OcclusionWorldToLocalId, occlusionScreen.worldToLocalMatrix);
            // Rect bounds preserve an authored non-centered pivot as well as the default center.
            materialProperties.SetVector(OcclusionRectId, new Vector4(
                rect.xMin - occlusionPadding, rect.yMin - occlusionPadding,
                rect.xMax + occlusionPadding, rect.yMax + occlusionPadding));
            materialProperties.SetFloat(OcclusionEnabledId, 1f);
        }
        else
        {
            materialProperties.SetFloat(OcclusionEnabledId, 0f);
        }

        Bounds worldBounds = new Bounds(
            cameraPosition,
            Vector3.one * (volumeRadius * 2f + particleSize * 12f));
        RenderParams renderParams = new RenderParams(runtimeMaterial)
        {
            worldBounds = worldBounds,
            matProps = materialProperties,
            layer = gameObject.layer,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false
        };

        Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, 6, totalParticleCount);
    }

    private static float CompressEnergy(float value, float gain)
    {
        return Mathf.Clamp01(1f - Mathf.Exp(-Mathf.Max(0f, value) * gain));
    }

    private static int GetThreadGroupCount(int count)
    {
        return Mathf.CeilToInt(count / (float)ThreadGroupSize);
    }

    private void ReleaseResources()
    {
        resourcesReady = false;
        hasPreviousCameraPosition = false;
        particleBuffer?.Release();
        particleBuffer = null;
        DestroyRuntimeObject(runtimeMaterial);
        runtimeMaterial = null;
        DestroyRuntimeObject(runtimeCompute);
        runtimeCompute = null;
        DestroyRuntimeObject(gradientTexture);
        gradientTexture = null;
        materialProperties = null;
    }

    private static void DestroyRuntimeObject(Object target)
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
