using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AudioVisualEffectsValidation
{
    [MenuItem("Tools/MR-VD/Audio Effects/Validate Play Mode Response")]
    public static void ValidatePlayModeResponse()
    {
        if (!Application.isPlaying) throw new Exception("Run this check in Play Mode.");
        var controller = Object.FindFirstObjectByType<AudioVisualEffectsController>();
        var data = new SerializedObject(controller);
        var audio = (AudioVisualizer)data.FindProperty("audioVisualizer").objectReferenceValue;
        var stage = controller.Stage;
        bool silent = audio.wasSilent;
        bool stageEnabled = stage != null && stage.enabled;
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var publish = typeof(AudioVisualizer).GetMethod("PublishVisualOnset", flags);
        var tick = typeof(AudioVisualEffectsController).GetMethod("LateUpdate", flags);
        var cooldown = typeof(AudioVisualEffectsController).GetField("lastBurstTime", flags);
        int before = controller.EmittedBeatCount;
        float tempoBeat = audio.lastBeatTime;
        try
        {
            if (stage != null) stage.enabled = false;
            audio.wasSilent = false;
            cooldown.SetValue(controller, float.NegativeInfinity);
            publish.Invoke(audio, new object[] { Time.time, 1f, 1f });
            tick.Invoke(controller, null);
            if (controller.EmittedBeatCount != before + 1) throw new Exception("Fresh onset failed.");
            tick.Invoke(controller, null);
            publish.Invoke(audio, new object[] { Time.time, 1f, 1f });
            tick.Invoke(controller, null);
            if (controller.EmittedBeatCount != before + 1) throw new Exception("Duplicate/cooldown suppression failed.");
            cooldown.SetValue(controller, Time.time - 0.2f);
            publish.Invoke(audio, new object[] { Time.time, 1f, 0.6f });
            tick.Invoke(controller, null);
            if (controller.EmittedBeatCount != before + 2 || audio.lastBeatTime != tempoBeat)
                throw new Exception("Fast onset must fire independently of the tempo clock.");
            cooldown.SetValue(controller, float.NegativeInfinity);
            audio.wasSilent = true;
            publish.Invoke(audio, new object[] { Time.time, 1f, 1f });
            tick.Invoke(controller, null);
            audio.wasSilent = false;
            publish.Invoke(audio, new object[] { Time.time - 1f, 1f, 1f });
            tick.Invoke(controller, null);
            publish.Invoke(audio, new object[] { Time.time, 0.1f, 1f });
            tick.Invoke(controller, null);
            if (controller.EmittedBeatCount != before + 2) throw new Exception("Silent, stale or weak onset emitted.");
            controller.enabled = false;
            foreach (var effect in controller.GetComponentsInChildren<XrBeatMeteorEffect>())
                if (effect.LiveParticleCount != 0) throw new Exception("Disabled controller left live particles.");
            controller.enabled = true;
            tick.Invoke(controller, null);
            if (controller.EmittedBeatCount != before + 2) throw new Exception("Re-enable replayed a stale onset.");
            Debug.Log("[AudioEffects] Play Mode response passed: fast accents independent of BPM/stage, deduplication, cooldown, silence/stale/weak rejection, re-enable and cleanup.");
        }
        finally
        {
            controller.enabled = true;
            audio.wasSilent = silent;
            if (stage != null) stage.enabled = stageEnabled;
        }
    }

    [MenuItem("Tools/MR-VD/Audio Effects/Validate Onset Response")]
    public static void ValidateOnsetResponse()
    {
        var host = new GameObject("Temporary onset test") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var audio = host.AddComponent<AudioVisualizer>();
            audio.enabled = false;
            audio.wasSilent = false;
            audio.beatCooldown = 0.45f;
            audio.dynamicKickThresholdSpeed = 30f;
            audio.dynamicSnareThresholdSpeed = 10f;
            audio.onsetSensitivity = 1.2f;
            audio.bpmUpdateInterval = float.MaxValue;
            var detect = typeof(AudioVisualizer).GetMethod("DetectBeatImproved", BindingFlags.NonPublic | BindingFlags.Instance);
            var fft = new float[4096];
            var hits = new List<float>();
            uint previous = 0;
            for (int frame = 0; frame < 240; frame++)
            {
                float t = frame * 0.02f;
                // Twelve 250-BPM accents: tempo gating should still reject alternate hits,
                // but each real transient must be available to visual consumers.
                bool kick = frame >= 60 && frame < 204 && (frame - 60) % 12 == 0;
                float energy = kick ? 0.06f : 0.002f;
                audio.kickEnergy = audio.bassEnergy = energy;
                for (int bin = 0; bin < 32; bin++) fft[bin] = energy;
                detect.Invoke(audio, new object[] { fft, t, 0.02f });
                if (audio.VisualOnsetSequence != previous && audio.VisualOnsetConfidence >= 0.22f) hits.Add(t);
                previous = audio.VisualOnsetSequence;
            }
            if (hits.Count != 12 || audio.beatTimestamps.Count >= hits.Count)
                throw new Exception($"Onset/tempo separation failed: {hits.Count} accents, {audio.beatTimestamps.Count} tempo beats.");
            audio.wasSilent = true;
            audio.kickEnergy = audio.bassEnergy = 1f;
            for (int bin = 0; bin < 32; bin++) fft[bin] = 1f;
            detect.Invoke(audio, new object[] { fft, 5f, 0.02f });
            if (audio.VisualOnsetSequence != previous) throw new Exception("Silence published an onset.");
            Debug.Log($"[AudioEffects] Onset validation passed: 12/12 fast accents, {audio.beatTimestamps.Count} tempo beats, no steady-tone retriggers, silence suppressed.");
        }
        finally { Object.DestroyImmediate(host); }
    }

    [MenuItem("Tools/MR-VD/Audio Effects/Render Variant Contact Sheet")]
    public static void RenderVariants()
    {
        string[] names = { "AuroraCyan", "NebulaViolet", "SolarAmber", "JadeEmerald" };
        const int width = 800, height = 500;
        var sheet = new Texture2D(width * 2, height * 2, TextureFormat.RGB24, false);
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                var preview = new PreviewRenderUtility();
                try
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioVisualEffectsInstaller.ArtRoot + "/" + names[i] + ".prefab");
                    var instance = Object.Instantiate(prefab);
                    preview.AddSingleGO(instance);
                    Camera camera = preview.camera;
                    camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    camera.fieldOfView = 85f;
                    camera.nearClipPlane = 0.05f;
                    camera.farClipPlane = 20f;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.004f, 0.008f, 0.025f);
                    var effect = instance.GetComponent<XrBeatMeteorEffect>();
                    effect.Emit(camera.transform, 1f);
                    foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>())
                    {
                        for (int step = 0; step < 24; step++) ps.Simulate(1f / 60f, false, false, false);
                        if (ps.main.simulationSpace != ParticleSystemSimulationSpace.World) throw new Exception("Particles must remain world-space.");
                    }
                    effect.RefreshMeteors();
                    if (effect.LiveParticleCount == 0) throw new Exception("Meteor emission failed: " + names[i]);
                    preview.BeginPreview(new Rect(0, 0, width, height), GUIStyle.none);
                    preview.Render(true);
                    var rendered = (RenderTexture)preview.EndPreview();
                    var scaled = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                    var previous = RenderTexture.active;
                    try
                    {
                        Graphics.Blit(rendered, scaled);
                        RenderTexture.active = scaled;
                        sheet.ReadPixels(new Rect(0, 0, width, height), (i % 2) * width, (1 - i / 2) * height);
                    }
                    finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(scaled); }
                }
                finally { preview.Cleanup(); }
            }
            sheet.Apply();
            Directory.CreateDirectory("Temp/AudioEffectsValidation");
            File.WriteAllBytes("Temp/AudioEffectsValidation/meteor-variants.png", sheet.EncodeToPNG());
            Debug.Log("[AudioEffects] Four variants rendered to Temp/AudioEffectsValidation/meteor-variants.png");
        }
        finally { Object.DestroyImmediate(sheet); }
    }

    [MenuItem("Tools/MR-VD/Audio Effects/Validate Trajectories")]
    public static void ValidateTrajectories()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioVisualEffectsInstaller.ArtRoot + "/AuroraCyan.prefab");
        if (prefab == null) throw new Exception("AuroraCyan prefab missing.");
        ValidateFlight(prefab, 60f, false, 18);
        ValidateFlight(prefab, 120f, false, 18);
        ValidateFlight(prefab, 180f, false, 18);
        ValidateFlight(prefab, 120f, true, 18);
        ValidateFlight(prefab, 120f, true, 12, 0.3f);
        Debug.Log("[AudioEffects] 10x launch and 30/BPM recovery passed at 60/120/180 BPM; intensity count, straight/Bezier clearance and cleanup passed.");
    }

    private static void ValidateFlight(GameObject prefab, float bpm, bool curved, int expectedCount, float intensity = 1f)
    {
        var instance = Object.Instantiate(prefab);
        var head = new GameObject("Temporary trajectory test head");
        try
        {
            head.transform.SetPositionAndRotation(new Vector3(4f, 1.6f, -2f), Quaternion.Euler(25f, 70f, 30f));
            var effect = instance.GetComponent<XrBeatMeteorEffect>();
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("useBezierTrajectory").boolValue = curved;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            effect.Emit(head.transform, intensity, bpm);
            var ps = instance.transform.Find("Meteors").GetComponent<ParticleSystem>();
            var particles = new ParticleSystem.Particle[128];
            int count = ps.GetParticles(particles);
            if (count != expectedCount) throw new Exception($"Expected {expectedCount} heads, got {count}.");
            if (ps.velocityOverLifetime.enabled) throw new Exception("Old lifetime velocity curve is still enabled.");
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            float normalEmission = prefab.transform.Find("Meteors").GetComponent<ParticleSystemRenderer>().sharedMaterial.GetFloat("_Emission");
            float launchEmission = renderer.sharedMaterial.GetFloat("_Emission");
            if (Mathf.Abs(launchEmission / normalEmission - 10f) > 0.01f)
                throw new Exception($"Initial meteor emission is not 10x: runtime={launchEmission}, source={normalEmission}, renderer={renderer.sharedMaterial.name}, active={effect.isActiveAndEnabled}.");
            uint probeSeed = particles[0].randomSeed;
            Vector3 previousProbePosition = particles[0].position;
            float firstStep = 0f, lastStep = 0f;
            Vector3 right = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(head.transform.forward, Vector3.up).normalized);
            int left = 0, rightCount = 0;
            for (int i = 0; i < count; i++)
            {
                float side = Vector3.Dot(particles[i].position - head.transform.position, right);
                if (side < 0f) left++; else rightCount++;
            }
            if (left != expectedCount / 2 || rightCount != expectedCount / 2) throw new Exception("Asymmetric volley.");
            float halfBeat = 30f / bpm;
            for (int step = 1; step <= 20; step++)
            {
                ps.Simulate(halfBeat / 20f, false, false, false);
                effect.RefreshMeteors();
                count = ps.GetParticles(particles);
                int currentProbe = Array.FindIndex(particles, 0, count, p => p.randomSeed == probeSeed);
                if (currentProbe < 0) throw new Exception("Probe meteor disappeared early.");
                float distanceTraveled = Vector3.Distance(previousProbePosition, particles[currentProbe].position);
                if (step == 1) firstStep = distanceTraveled;
                if (step == 20) lastStep = distanceTraveled;
                previousProbePosition = particles[currentProbe].position;
                for (int i = 0; i < count; i++)
                {
                    Vector3 relative = particles[i].position - head.transform.position;
                    float side = Vector3.Dot(relative, right);
                    if (Mathf.Abs(side) < 1.1f || relative.magnitude < 1.1f)
                        throw new Exception($"Meteor entered clearance: bpm={bpm}, curved={curved}, step={step}, side={side:F3}, distance={relative.magnitude:F3}.");
                }
            }
            int probe = Array.FindIndex(particles, 0, count, p => p.randomSeed == probeSeed);
            if (probe < 0) throw new Exception("Meteor expired before half a beat.");
            float recoveredBrightness = launchEmission * (particles[probe].startColor.a / 255f) / normalEmission;
            if (Mathf.Abs(recoveredBrightness - 1.1f) > 0.15f)
                throw new Exception($"Brightness did not recover at 30/BPM: {recoveredBrightness:F2}x.");
            var lifetimeColor = ps.colorOverLifetime.color.gradient;
            if (lifetimeColor.Evaluate(halfBeat / particles[probe].startLifetime).a < 0.99f)
                throw new Exception("Lifetime alpha hides the BPM brightness recovery.");
            if (!curved && Mathf.Abs(firstStep / lastStep - 10f / 1.1f) > 1.5f)
                throw new Exception($"Speed did not recover at 30/BPM: first={firstStep:F3}, last={lastStep:F3}.");
            effect.Clear();
            if (effect.LiveParticleCount != 0) throw new Exception("Particle cleanup failed.");
        }
        finally { Object.DestroyImmediate(instance); Object.DestroyImmediate(head); }
    }
}
