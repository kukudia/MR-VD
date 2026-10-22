using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// Opt-in eight-minute legacy or twelve-minute full synthetic rotation. Editor update observes actual Play frames,
/// checkpoints one CSV row per second, and restores the initial look/formation on stop.
/// Script reload or Play exit ends the run; never starts itself or saves scene modifications.
/// </summary>
public static class AudioVisualSoak
{
    private static readonly string[] Names = { "Aurora Cathedral", "Solar Percussion", "Jade Orbit", "Violet Knot" };
    private static readonly string[] FullNames = { "Aurora Cathedral", "Solar Percussion", "Jade Orbit", "Violet Knot", "Opal Iris", "Amber Flow" };
    private static readonly List<float> frameTimes = new List<float>(65536);
    private static string[] runNames;
    private static double durationSeconds, meshWarmupSeconds;
    private static AudioVisualField field;
    private static GpuAudioParticleVisualizer particles;
    private static AudioVisualLook[] looks;
    private static AudioVisualTheme originalTheme;
    private static GpuAudioParticleVisualizer.ParticleFormation originalFormation;
    private static AudioVisualField.Composition originalComposition;
    private static float originalIntensity;
    private static double started, nextSample;
    private static int lastFrame, lastLook, firstMeshes, lastMeshes;
    private static string csvPath;
    public static bool IsRunning { get; private set; }

    [MenuItem("Tools/MR-VD/Spatial Audio/Start Eight Minute Look Soak")]
    public static void Start()
    {
        StartRun(Names, 480d, 20d);
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Start Twelve Minute Six Look Soak")]
    public static void StartFullRotation()
    {
        // First pass warms every optional mesh. Compare the second pass to that baseline,
        // rather than misclassifying first-time iris allocation as a resource leak.
        StartRun(FullNames, 720d, 360d);
    }

    private static void StartRun(string[] names, double duration, double meshWarmup)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Soak is already running.");
        }
        field = UnityEngine.Object.FindFirstObjectByType<AudioVisualField>();
        particles = UnityEngine.Object.FindFirstObjectByType<GpuAudioParticleVisualizer>();
        if (!EditorApplication.isPlaying || field == null || !field.UsesDemonstration || particles == null)
        {
            throw new InvalidOperationException("Start only in a running explicit synthetic showcase.");
        }
        if (UnityEngine.Object.FindObjectsByType<AudioVisualSequencePlayer>(FindObjectsSortMode.None).Any(x => x.IsRunning))
        {
            throw new InvalidOperationException("Stop the authored sequence before starting an isolated look soak.");
        }
        runNames = names;
        durationSeconds = duration;
        meshWarmupSeconds = meshWarmup;
        looks = runNames.Select(n => AssetDatabase.LoadAssetAtPath<AudioVisualLook>(AudioVisualShowcaseTools.LookRoot + "/" + n + ".asset")).ToArray();
        if (looks.Any(x => x == null))
        {
            throw new InvalidOperationException("Build missing showcase looks before soak.");
        }
        SerializedObject data = new SerializedObject(field);
        originalTheme = field.Theme;
        originalComposition = (AudioVisualField.Composition)data.FindProperty("composition").enumValueIndex;
        originalIntensity = data.FindProperty("intensity").floatValue;
        originalFormation = particles.Formation;
        frameTimes.Clear();
        started = EditorApplication.timeSinceStartup;
        nextSample = started;
        lastFrame = -1;
        lastLook = -1;
        firstMeshes = lastMeshes = -1;
        Directory.CreateDirectory(AudioVisualFieldTools.PreviewDirectory);
        csvPath = AudioVisualFieldTools.PreviewDirectory + "/soak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv";
        File.WriteAllText(csvPath, "elapsed_s,look,frame,dt_ms,managed_mb,unity_allocated_mb,unity_reserved_mb,generated_meshes\n");
        IsRunning = true;
        EditorApplication.update += Update;
        AssemblyReloadEvents.beforeAssemblyReload += StopBeforeReload;
        Debug.Log("[AudioSoak] Started explicit synthetic rotation: " + csvPath);
    }

    private static void Update()
    {
        if (!EditorApplication.isPlaying || field == null || particles == null)
        {
            Stop("interrupted: Play ended or source removed");
            return;
        }
        double now = EditorApplication.timeSinceStartup;
        double elapsed = now - started;
        if (elapsed >= durationSeconds)
        {
            Stop("completed " + durationSeconds.ToString("F0", CultureInfo.InvariantCulture) + " second rotation");
            return;
        }
        int look = (int)(elapsed / 60d) % looks.Length;
        if (look != lastLook)
        {
            looks[look].Apply(field, particles);
            lastLook = look;
        }
        if (Time.frameCount != lastFrame)
        {
            lastFrame = Time.frameCount;
            if (elapsed >= 20d)
            {
                frameTimes.Add(Time.unscaledDeltaTime * 1000f);
            }
        }
        if (now < nextSample)
        {
            return;
        }
        nextSample = now + 1d;
        lastMeshes = Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.EndsWith(" (Generated)"));
        if (elapsed >= meshWarmupSeconds && firstMeshes < 0)
        {
            firstMeshes = lastMeshes;
        }
        File.AppendAllText(csvPath, string.Format(CultureInfo.InvariantCulture,
            "{0:F2},{1},{2},{3:F3},{4:F2},{5:F2},{6:F2},{7}\n", elapsed, runNames[look], Time.frameCount,
            Time.unscaledDeltaTime * 1000f, GC.GetTotalMemory(false) / 1048576d,
            Profiler.GetTotalAllocatedMemoryLong() / 1048576d, Profiler.GetTotalReservedMemoryLong() / 1048576d, lastMeshes));
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Stop Look Soak and Save Report")]
    public static void StopManually()
    {
        Stop("stopped explicitly");
    }

    private static void StopBeforeReload()
    {
        Stop("interrupted: script reload");
    }

    private static void Stop(string reason)
    {
        if (!IsRunning)
        {
            return;
        }
        IsRunning = false;
        EditorApplication.update -= Update;
        AssemblyReloadEvents.beforeAssemblyReload -= StopBeforeReload;
        if (field != null)
        {
            field.SetTheme(originalTheme);
            field.SetComposition(originalComposition);
            field.SetIntensity(originalIntensity);
        }
        if (particles != null)
        {
            particles.SetFormation(originalFormation);
        }
        frameTimes.Sort();
        float p95 = frameTimes.Count > 0 ? frameTimes[Mathf.Clamp(Mathf.FloorToInt(frameTimes.Count * 0.95f), 0, frameTimes.Count - 1)] : 0f;
        string report = DateTimeOffset.Now.ToString("o") + "\n" + reason + "\n"
            + "Elapsed seconds: " + (EditorApplication.timeSinceStartup - started).ToString("F2", CultureInfo.InvariantCulture)
            + "\nObserved frames after warmup: " + frameTimes.Count
            + "\nEditor frame interval p95 ms: " + p95.ToString("F3", CultureInfo.InvariantCulture)
            + "\nGenerated mesh count warmup/end: " + firstMeshes + "/" + lastMeshes
            + "\nMesh baseline begins at seconds: " + meshWarmupSeconds.ToString("F0", CultureInfo.InvariantCulture)
            + "\nLook count: " + runNames.Length
            + "\nCSV: " + csvPath + "\nNot a Player GPU timing, real audio or XR performance result.\n";
        File.WriteAllText(Path.ChangeExtension(csvPath, ".txt"), report);
        Debug.Log("[AudioSoak] " + report);
    }
}
