using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Repeatable Edit Mode checks for mesh lifetime, bounded history, transient consumption and GPU
/// formations. Run uses isolated objects/buffers; reflection only injects controlled audio events
/// through the existing private publisher and never touches the user's scene audio component.
/// </summary>
public static class AudioVisualValidation
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Particle
    {
        public Vector3 position;
        public float size;
        public Vector3 velocity;
        public float seed;
        public Vector4 color;
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Validate Meshes and GPU Formations")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Run outside Play Mode.");
        }
        CheckSurfaceShader();
        CheckSpectralShape();
        CheckSpectralLayers();
        CheckAggregateEnergy();
        int meshesBefore = Resources.FindObjectsOfTypeAll<Mesh>().Length;
        CheckMeshesAndOnsets();
        CheckLatticePropagation();
        CheckTerrainScroll();
        CheckRingSpectrumSnapshot();
        Require(Resources.FindObjectsOfTypeAll<Mesh>().Length == meshesBefore,
            "Isolated preview objects leaked meshes after teardown.");
        CheckGpuFormations();
        Directory.CreateDirectory(AudioVisualFieldTools.PreviewDirectory);
        string report = DateTimeOffset.Now.ToString("o") + "\n"
            + "PASS: surface shader; finite bounded geometry; mesh reuse; ring expiration; history; "
            + "fresh transient once; stale transient ignored on enable; silence; private theme blending; spectral shape; aggregate energy bounds/fallback/zero allocation; "
            + "lattice propagation/clear; terrain scroll continuity; ring spectral snapshot/closed seam; iris onset response/global zero intensity.\n"
            + "PASS: GPU Stardust, SpiralGalaxy, Torus, FlowKnot, CurlNebula dispatch/readback; finite values, speed and volume/near-eye bounds.\n"
            + "Edit Mode and GPU checks only; not real audio listening, XR, Player performance or Play Mode.\n";
        File.WriteAllText(AudioVisualFieldTools.PreviewDirectory + "/validation.txt", report);
        Debug.Log("[SpatialAudioValidation] " + report);
    }

    private static void CheckSurfaceShader()
    {
        Shader shader = Shader.Find("MR-VD/Spatial Audio Surface");
        Require(shader != null && shader.isSupported, "Surface shader unavailable.");
        Require(!ShaderUtil.GetShaderMessages(shader).Any(m => m.severity.ToString() == "Error"),
            "Surface shader compilation failed.");
    }

    private static void CheckAggregateEnergy()
    {
        float[] fft = new float[4096];
        fft[10] = 0.2f;
        float narrow = AudioSpectralEnergy.Blend(0f, fft, 48000f, 6f, 1f);
        fft[10] = 0f;
        for (int i = 100; i < 116; i++)
        {
            fft[i] = 0.05f;
        }
        float distributed = AudioSpectralEnergy.Blend(0f, fft, 48000f, 6f, 1f);
        Require(Mathf.Abs(narrow - distributed) < 0.00001f,
            "Equal spectral power changed with bin distribution.");
        Require(AudioSpectralEnergy.Blend(0.37f, fft, 48000f, 6f, 0f) == 0.37f
            && AudioSpectralEnergy.Blend(0.37f, null, 48000f, 6f, 1f) == 0.37f
            && AudioSpectralEnergy.Blend(0.37f, fft, float.NaN, 6f, 1f) == 0.37f,
            "Disabled/missing/invalid aggregate input changed legacy energy.");
        Array.Clear(fft, 0, fft.Length);
        fft[10] = float.NaN;
        fft[11] = float.PositiveInfinity;
        fft[12] = -1f;
        Require(AudioSpectralEnergy.Blend(0f, fft, 48000f, 6f, 1f) == 0f,
            "Invalid FFT bins produced aggregate energy.");
        fft[13] = float.MaxValue;
        Require(AudioSpectralEnergy.Blend(0f, fft, 48000f, 6f, 1f) == 1f,
            "Large finite FFT did not remain bounded.");
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            AudioSpectralEnergy.Blend(0.2f, fft, 48000f, 6f, 0.5f);
        }
        Require(GC.GetAllocatedBytesForCurrentThread() == before, "Aggregate energy allocated in steady state.");
    }

    private static void CheckRingSpectrumSnapshot()
    {
        GameObject host = new GameObject("Isolated ring spectral snapshot");
        AudioBeatRings rings = host.AddComponent<AudioBeatRings>();
        AudioVisualField source = host.AddComponent<AudioVisualField>();
        AudioVisualTheme theme = ScriptableObject.CreateInstance<AudioVisualTheme>();
        try
        {
            float[] bands = (float[])typeof(AudioVisualField).GetField("spectrum",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
            for (int i = 0; i < bands.Length; i++)
            {
                bands[i] = i < 24 ? 1f : 0f;
            }
            theme.ringSpectralRelief = 0.4f;
            AudioVisualField.Frame frame = new AudioVisualField.Frame
            {
                time = 10f, deltaTime = 0.02f, onset = true, onsetStrength = 1f, ringWeight = 1f
            };
            rings.RenderFrame(frame, theme, source);
            frame.onset = false;
            frame.time = 10.6f;
            rings.RenderFrame(frame, theme, source);
            Mesh mesh = host.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] captured = mesh.vertices;
            Array.Clear(bands, 0, bands.Length);
            rings.RenderFrame(frame, theme, source);
            Vector3[] afterSourceChange = mesh.vertices;
            Require(captured.SequenceEqual(afterSourceChange), "Old ring followed later source spectrum.");
            Require(Vector3.Distance(captured[0], captured[192]) < 0.00001f
                && Vector3.Distance(captured[1], captured[193]) < 0.00001f, "Spectral ring seam is open.");
            theme.ringSpectralRelief = 0f;
            rings.RenderFrame(frame, theme, source);
            Vector3[] circular = mesh.vertices;
            Require(captured.Zip(circular, Vector3.Distance).Max() > 0.05f,
                "Accepted onset spectrum did not create local ring relief.");
            Require(rings.EmittedCount == 1 && rings.LiveCount == 1
                && host.GetComponent<MeshFilter>().sharedMesh == mesh, "Relief recreated mesh or emitted extra rings.");
            rings.Clear();
            frame.onset = true;
            rings.RenderFrame(frame, theme, source);
            theme.ringSpectralRelief = 0.4f;
            frame.onset = false;
            rings.RenderFrame(frame, theme, source);
            Vector3[] silentCapture = mesh.vertices;
            theme.ringSpectralRelief = 0f;
            rings.RenderFrame(frame, theme, source);
            Require(silentCapture.SequenceEqual(mesh.vertices), "Cleared ring retained stale spectral relief.");
        }
        finally
        {
            rings.ReleaseMesh();
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(theme);
        }
    }

    private static void CheckTerrainScroll()
    {
        GameObject host = new GameObject("Isolated terrain sample continuity");
        AudioSpectrumTerrain terrain = host.AddComponent<AudioSpectrumTerrain>();
        AudioVisualTheme theme = ScriptableObject.CreateInstance<AudioVisualTheme>();
        try
        {
            float[] bands = Enumerable.Repeat(0.2f, 48).ToArray();
            AudioVisualField.Frame frame = new AudioVisualField.Frame { deltaTime = 0.05f, terrainWeight = 1f };
            terrain.RenderFrame(frame, theme, bands);
            for (int i = 0; i < bands.Length; i++)
            {
                bands[i] = 0.8f;
            }
            frame.deltaTime = 0.0499f;
            terrain.RenderFrame(frame, theme, bands);
            Mesh mesh = host.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] before = mesh.vertices;
            Color[] colors = mesh.colors;
            frame.deltaTime = 0.0002f;
            terrain.RenderFrame(frame, theme, bands);
            Vector3[] after = mesh.vertices;
            Color[] afterColors = mesh.colors;
            for (int i = 0; i < before.Length; i++)
            {
                Require(Vector3.Distance(before[i], after[i]) < 0.003f, "Terrain jumped across sample boundary.");
                Require(Mathf.Abs(colors[i].a - afterColors[i].a) < 0.003f, "Terrain alpha jumped across sample boundary.");
            }
        }
        finally
        {
            terrain.ReleaseMesh();
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(theme);
        }
    }

    private static void CheckLatticePropagation()
    {
        GameObject host = new GameObject("Isolated constellation propagation validation");
        AudioVisualTheme theme = ScriptableObject.CreateInstance<AudioVisualTheme>();
        AudioSpectralLattice lattice = host.AddComponent<AudioSpectralLattice>();
        try
        {
            AudioVisualField.Frame frame = new AudioVisualField.Frame
            {
                time = 10f, deltaTime = 1f / 60f, ribbonWeight = 1f,
                onset = true, onsetStrength = 1f
            };
            lattice.RenderFrame(frame, theme, null);
            Mesh mesh = host.GetComponent<MeshFilter>().sharedMesh;
            Require(lattice.NodeCount == 60 && mesh.vertexCount == 2619, "Unexpected constellation geometry budget.");
            float initial = mesh.colors[0].a;
            frame.onset = false;
            frame.time = 10.75f;
            lattice.RenderFrame(frame, theme, null);
            Color[] late = mesh.colors;
            Require(late[0].a < initial * 0.7f, "Constellation pulse did not leave first node.");
            // Last vertex of the first arch arrives 0.75 seconds after its first node.
            int lastVertex = (19 * 9 - 1) * 3;
            Require(late[lastVertex].a > late[0].a * 1.5f, "Constellation pulse did not propagate to far nodes.");
            lattice.Clear();
            lattice.RenderFrame(frame, theme, null);
            Require(mesh.colors[lastVertex].a < late[lastVertex].a * 0.7f, "Clear replayed a constellation pulse.");
            Require(host.GetComponent<MeshFilter>().sharedMesh == mesh, "Constellation mesh was not reused.");
            foreach (Vector3 v in mesh.vertices)
            {
                Require(Finite(v) && v.magnitude < 30f, "Constellation geometry escaped bounds.");
            }
        }
        finally
        {
            lattice.ReleaseMesh();
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(theme);
        }
    }

    private static void CheckSpectralShape()
    {
        AudioSpectralShape analyzer = new AudioSpectralShape(48);
        float[] bands = new float[48];
        bands[3] = 1f;
        Vector4 low = analyzer.Analyze(bands, false);
        Require(low.x < 0.1f && low.y < 0.001f && low.z < 0.01f && low.w == 0f,
            "Low narrowband descriptors or initial flux are incorrect.");
        Require(analyzer.Analyze(bands, false).w == 0f, "Unchanged spectrum must have zero positive change.");
        bands[3] = 0f;
        bands[42] = 1f;
        Vector4 high = analyzer.Analyze(bands, false);
        Require(high.x > 0.8f && high.w > 0.9f, "High frequency movement must change centroid and flux.");
        for (int i = 0; i < bands.Length; i++)
        {
            bands[i] = 0.5f;
        }
        Vector4 flat = analyzer.Analyze(bands, false);
        Require(Mathf.Abs(flat.x - 0.5f) < 0.001f && flat.z > 0.99f && flat.y > 0.5f,
            "Uniform bands must be broad and flat.");
        Require(analyzer.Analyze(bands, true) == Vector4.zero, "Silent spectrum must clear descriptors.");
        Require(analyzer.Analyze(bands, false).w == 0f, "Silence recovery must not create a synthetic change pulse.");
        bands[0] = float.NaN;
        bands[1] = float.PositiveInfinity;
        Vector4 finite = analyzer.Analyze(bands, false);
        Require(float.IsFinite(finite.x) && float.IsFinite(finite.y) && float.IsFinite(finite.z)
            && float.IsFinite(finite.w), "Invalid FFT input propagated non-finite descriptors.");
    }

    private static void CheckSpectralLayers()
    {
        AudioSpectralLayers layers = new AudioSpectralLayers(48);
        float[] bands = new float[48];
        bands[20] = 0.8f;
        Vector4 sustained = Vector4.zero;
        for (int i = 0; i < 12; i++)
        {
            sustained = layers.Analyze(bands, 0.05f, false);
        }
        Require(sustained.x > 0.99f && sustained.y < 0.01f && sustained.z > 0.99f,
            "Persistent narrowband spectrum was not distinguished from broadband structure.");
        layers.Reset();
        for (int i = 0; i < bands.Length; i++)
        {
            bands[i] = 0.8f;
        }
        Vector4 impulse = layers.Analyze(bands, 0.05f, false);
        Require(impulse.y > 0.99f && impulse.x < 0.01f && impulse.w < 0.2f,
            "First broadband frame did not remain a low-coverage transient.");
        for (int i = 0; i < 12; i++)
        {
            impulse = layers.Analyze(bands, 0.05f, false);
        }
        Require(Mathf.Abs(impulse.x - 0.5f) < 0.001f && impulse.z < 0.001f,
            "Sustained broadband ambiguity was incorrectly labelled confident.");
        Require(layers.Analyze(bands, 0.05f, true) == Vector4.zero, "Silent masks were not cleared.");
        bands[2] = float.NaN;
        bands[3] = float.PositiveInfinity;
        Vector4 sanitized = layers.Analyze(bands, 0.05f, false);
        Require(float.IsFinite(sanitized.x) && float.IsFinite(sanitized.z), "Invalid input escaped median masks.");
        foreach (int fps in new[] { 30, 60, 120 })
        {
            layers.Reset();
            for (int i = 0; i < bands.Length; i++)
            {
                bands[i] = i == 20 ? 0.8f : 0f;
            }
            for (int i = 0; i < fps * 2 / 5; i++)
            {
                layers.Analyze(bands, 1f / fps, false);
            }
            Require(Mathf.Abs(layers.Current.w - 8f / 9f) < 0.001f,
                "Median feature cadence changed with frame rate: " + fps);
        }
        // Warm framework sort helpers before measuring steady-state allocation.
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            layers.Analyze(bands, 0.05f, false);
        }
        Require(GC.GetAllocatedBytesForCurrentThread() == before, "Median feature allocated in steady state.");
    }

    private static void CheckMeshesAndOnsets()
    {
        GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AudioVisualFieldTools.PrefabPath));
        GameObject sourceObject = new GameObject("Validation Audio - isolated");
        AudioVisualTheme theme = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AudioVisualTheme>(AudioVisualFieldTools.ThemePath(0)));
        try
        {
            AudioVisualField field = root.GetComponent<AudioVisualField>();
            AudioVisualizer audio = sourceObject.AddComponent<AudioVisualizer>();
            Require(root.GetComponentInChildren<AudioSpectralIris>().GetComponent<MeshFilter>().sharedMesh == null,
                "Disabled optional iris allocated a mesh.");
            theme.iris = 1f;
            audio.wasSilent = false;
            audio.smoothedBassEnergy = 0.1f;
            audio.smoothedSynthEnergy = 0.1f;
            field.Configure(audio, null, theme, root.GetComponentInChildren<AudioAuroraRibbons>(),
                root.GetComponentInChildren<AudioBeatRings>(), root.GetComponentInChildren<AudioSpectrumTerrain>(), false);
            MethodInfo read = typeof(AudioVisualField).GetMethod("ReadAudio", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo publish = typeof(AudioVisualizer).GetMethod("PublishVisualOnset", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 90; i++)
            {
                field.RenderFrame((AudioVisualField.Frame)read.Invoke(field, new object[] { 1f / 60f }));
            }
            int emitted = field.Rings.EmittedCount;
            publish.Invoke(audio, new object[] { Time.time, 1f, 1f });
            field.RenderFrame((AudioVisualField.Frame)read.Invoke(field, new object[] { 1f / 60f }));
            field.RenderFrame((AudioVisualField.Frame)read.Invoke(field, new object[] { 1f / 60f }));
            Require(field.Rings.EmittedCount == emitted + 1, "A fresh transient must be consumed once.");
            field.enabled = false;
            typeof(AudioVisualField).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(field, null);
            field.enabled = true;
            typeof(AudioVisualField).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(field, null);
            field.RenderFrame((AudioVisualField.Frame)read.Invoke(field, new object[] { 1f / 60f }));
            Require(field.Rings.EmittedCount == emitted + 1, "Re-enable replayed a stale transient.");
            audio.wasSilent = true;
            for (int i = 0; i < 300; i++)
            {
                field.RenderFrame((AudioVisualField.Frame)read.Invoke(field, new object[] { 1f / 60f }));
            }
            Require(field.CurrentFrame.energy < 0.001f, "Silence did not release energy.");

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
            Mesh[] meshes = filters.Select(f => f.sharedMesh).ToArray();
            int meshCount = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            float[] spectrum = Enumerable.Repeat(0.5f, 48).ToArray();
            for (int i = 0; i < 600; i++)
            {
                AudioVisualField.Frame frame = new AudioVisualField.Frame
                {
                    time = i / 60f, deltaTime = 1f / 60f, energy = 0.6f, bass = 0.5f, detail = 0.5f, visibility = 1f,
                    ribbonWeight = 0.7f, ringWeight = 0.7f, terrainWeight = 0.7f, sculptureWeight = 0.7f,
                    onset = i < 360 && i % 30 == 0, onsetStrength = 1f
                };
                field.RenderFrame(frame);
                field.Terrain.RenderFrame(frame, theme, spectrum);
            }
            Require(field.Rings.LiveCount == 0, "Rings did not expire.");
            Require(field.Terrain.SampleCount > 100, "History did not advance.");
            Require(meshCount == Resources.FindObjectsOfTypeAll<Mesh>().Length, "Steady-state mesh allocation.");
            for (int i = 0; i < filters.Length; i++)
            {
                Require(filters[i].sharedMesh == meshes[i], "Mesh was rebuilt without configuration changes.");
                foreach (Vector3 vertex in meshes[i].vertices)
                {
                    Require(Finite(vertex) && vertex.magnitude < 30f,
                        "Non-finite or unbounded mesh: " + filters[i].name + " vertex=" + vertex);
                }
            }
            AudioSpectralIris iris = root.GetComponentInChildren<AudioSpectralIris>();
            Mesh irisMesh = iris.GetComponent<MeshFilter>().sharedMesh;
            Vector3 beforeOnset = irisMesh.vertices[irisMesh.vertexCount / 4];
            field.RenderFrame(new AudioVisualField.Frame
            {
                time = 10f,
                deltaTime = 1f / 60f,
                energy = 0.6f,
                visibility = 1f,
                sculptureWeight = 0.7f,
                onset = true,
                onsetStrength = 1f
            });
            Vector3 afterOnset = irisMesh.vertices[irisMesh.vertexCount / 4];
            Require(Vector3.Distance(beforeOnset, afterOnset) > 0.005f, "Accepted transient did not expand the iris petals.");
            field.SetIntensity(0f);
            for (int i = 0; i < 300; i++)
            {
                field.RenderFrame((AudioVisualField.Frame)read.Invoke(field, new object[] { 1f / 60f }));
            }
            Require(irisMesh.colors.All(c => c.a < 0.001f), "Global zero intensity left the iris illuminated.");
            Color original = theme.primary;
            theme.primary = Color.red;
            field.RenderFrame(new AudioVisualField.Frame { deltaTime = 0.01f });
            Require(field.DisplayedTheme != theme && field.DisplayedTheme.primary != Color.red,
                "Theme transition jumped or mutated the shared theme.");
            theme.primary = original;
        }
        finally
        {
            root.GetComponent<AudioVisualField>().ReleasePreviewResources();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(theme);
        }
    }

    private static void CheckGpuFormations()
    {
        Require(SystemInfo.supportsComputeShaders, "GPU compute unsupported; GPU checks not run.");
        ComputeShader source = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/AudioReactiveParticles/Shaders/GpuAudioParticles.compute");
        ComputeShader compute = Object.Instantiate(source);
        const int count = 128;
        GraphicsBuffer buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, Marshal.SizeOf<Particle>());
        try
        {
            int initialize = compute.FindKernel("InitializeParticles");
            int update = compute.FindKernel("UpdateParticles");
            compute.SetBuffer(initialize, "_Particles", buffer);
            compute.SetBuffer(update, "_Particles", buffer);
            compute.SetInt("_ParticleCount", count);
            compute.SetFloat("_VolumeRadius", 8f);
            compute.SetFloat("_MinimumDistance", 0.8f);
            compute.SetFloat("_BackgroundSpeed", 0.1f);
            compute.SetFloat("_MotionSmoothing", 2f);
            compute.SetFloat("_FormationRadius", 3f);
            compute.SetFloat("_FormationThickness", 0.22f);
            compute.SetFloat("_FormationAttraction", 1.2f);
            compute.SetFloat("_FormationRotationSpeed", 0.06f);
            compute.SetFloat("_FormationDetail", 0.75f);
            compute.SetFloat("_KnotLobes", 3f);
            compute.SetFloat("_KnotDepth", 0.28f);
            compute.SetFloat("_CurlScale", 3.2f);
            compute.SetFloat("_CurlSpeed", 0.5f);
            compute.SetFloat("_CurlConfinement", 0.8f);
            compute.SetVector("_SpectralShape", new Vector4(0.7f, 0.4f, 0.2f, 0.3f));
            compute.SetVector("_FormationCenter", new Vector3(0f, 0f, 2f));
            compute.SetVector("_FormationRight", Vector3.right);
            compute.SetVector("_FormationUp", Vector3.up);
            compute.SetVector("_FormationForward", Vector3.forward);
            compute.SetVector("_CameraDelta", Vector3.zero);
            compute.SetTexture(update, "_Gradient", Texture2D.whiteTexture);
            Particle[] particles = new Particle[count];
            for (int formation = 0; formation < 5; formation++)
            {
                compute.SetFloat("_FormationBlend", formation == 0 ? 0f : 1f);
                compute.SetFloat("_TorusBlend", formation == 2 ? 1f : 0f);
                compute.SetFloat("_KnotBlend", formation == 3 ? 1f : 0f);
                compute.SetFloat("_CurlBlend", formation == 4 ? 1f : 0f);
                compute.Dispatch(initialize, 2, 1, 1);
                for (int step = 0; step < (formation == 4 ? 3600 : 360); step++)
                {
                    float delta = formation == 4 ? 1f / 30f : 1f / 60f;
                    compute.SetFloat("_DeltaTime", delta);
                    compute.SetFloat("_SimulationTime", step * delta);
                    compute.Dispatch(update, 2, 1, 1);
                }
                buffer.GetData(particles);
                foreach (Particle particle in particles)
                {
                    float radius = particle.position.magnitude;
                    Require(Finite(particle.position) && Finite(particle.velocity), "Non-finite GPU simulation.");
                    Require(particle.velocity.magnitude <= 2.01f, "GPU formation exceeded bounded speed.");
                    Require(radius >= 0.79f && radius <= 8.01f, "GPU volume or near-eye bound failed.");
                }
                if (formation > 0 && formation < 4)
                {
                    Require(particles.Average(p => Mathf.Abs(p.position.y)) < (formation == 3 ? 1.1f : 0.7f),
                        "Particles did not converge towards the formation plane.");
                }
                if (formation == 4)
                {
                    Vector3 anchor = new Vector3(0f, 0f, 2f);
                    Require(particles.Average(p => Vector3.Distance(p.position, anchor)) < 3.5f,
                        "Curl nebula did not converge into its authored volume.");
                    Require(particles.Average(p => p.velocity.magnitude) > 0.08f,
                        "Curl nebula stopped flowing after convergence.");
                    Vector3 mean = particles.Aggregate(Vector3.zero, (sum, p) => sum + p.position) / particles.Length;
                    Require(particles.Average(p => (p.position - mean).sqrMagnitude) > 0.15f,
                        "Curl nebula collapsed into a single point.");
                    Debug.Log("[AudioFlow] 120 simulated seconds: mean anchor radius="
                        + particles.Average(p => Vector3.Distance(p.position, anchor)).ToString("F3")
                        + ", mean speed=" + particles.Average(p => p.velocity.magnitude).ToString("F3"));
                }
            }
        }
        finally
        {
            buffer.Release();
            Object.DestroyImmediate(compute);
        }
    }

    private static bool Finite(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
