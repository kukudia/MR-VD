using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Runs an independent GPU star-dust volume around the active camera. Audio energy
/// changes the drift and twinkle, while the Screen plane can occlude the particles.
/// </summary>
[DisallowMultipleComponent]
public sealed class GpuAudioParticleVisualizer : MonoBehaviour
{
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

    [Header("Rendering")]
    [Min(0.001f)] public float particleSize = 0.014f;
    [Min(0f)] public float emission = 1.8f;
    [Range(0f, 1f)] public float opacity = 0.82f;
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
    private static readonly int CameraPositionWSId = Shader.PropertyToID("_CameraPositionWS");
    private static readonly int OcclusionWorldToLocalId = Shader.PropertyToID("_OcclusionWorldToLocal");
    private static readonly int OcclusionHalfSizeId = Shader.PropertyToID("_OcclusionHalfSize");
    private static readonly int OcclusionEnabledId = Shader.PropertyToID("_OcclusionEnabled");
    private static readonly int ParticleSizeId = Shader.PropertyToID("_ParticleSize");
    private static readonly int EmissionId = Shader.PropertyToID("_Emission");
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
        runtimeCompute.SetInt(ParticleCountId, totalParticleCount);
        runtimeCompute.SetFloat(VolumeRadiusId, volumeRadius);
        runtimeCompute.SetFloat(MinimumDistanceId, minimumDistance);
        runtimeCompute.SetFloat(BackgroundSpeedId, backgroundSpeed);
        runtimeCompute.SetFloat(MotionSmoothingId, motionSmoothing);
        runtimeCompute.SetVector(BackgroundColorId, backgroundColor);
        runtimeCompute.SetVector(AccentColorId, accentColor);
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

        synth = Mathf.Max(synth, silent ? silentBackgroundLevel : 0f);
        float energy = Mathf.Clamp01(bass * 0.55f + synth * 0.45f);

        SetStaticSimulationParameters();
        runtimeCompute.SetFloat(DeltaTimeId, deltaTime);
        runtimeCompute.SetFloat(SimulationTimeId, Time.time);
        runtimeCompute.SetVector(CameraDeltaId, cameraDelta);
        runtimeCompute.SetFloat(BassId, bass);
        runtimeCompute.SetFloat(SynthId, synth);
        runtimeCompute.SetFloat(EnergyId, energy);
        runtimeCompute.Dispatch(updateKernel, GetThreadGroupCount(totalParticleCount), 1, 1);
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
        materialProperties.SetVector(CameraPositionWSId, cameraPosition);
        materialProperties.SetFloat(ParticleSizeId, particleSize);
        materialProperties.SetFloat(EmissionId, emission);
        materialProperties.SetFloat(OpacityId, opacity);

        if (occlusionScreen != null)
        {
            Rect rect = occlusionScreen.rect;
            materialProperties.SetMatrix(OcclusionWorldToLocalId, occlusionScreen.worldToLocalMatrix);
            materialProperties.SetVector(OcclusionHalfSizeId, new Vector4(
                rect.width * 0.5f + occlusionPadding,
                rect.height * 0.5f + occlusionPadding,
                0f,
                0f));
            materialProperties.SetFloat(OcclusionEnabledId, 1f);
        }
        else
        {
            materialProperties.SetFloat(OcclusionEnabledId, 0f);
        }

        Bounds worldBounds = new Bounds(
            cameraPosition,
            Vector3.one * (volumeRadius * 2f + particleSize * 2f));
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
