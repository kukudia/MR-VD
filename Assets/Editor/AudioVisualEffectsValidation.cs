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
        var instance = Object.Instantiate(prefab);
        var head = new GameObject("Temporary trajectory test head");
        try
        {
            head.transform.SetPositionAndRotation(new Vector3(4f, 1.6f, -2f), Quaternion.Euler(25f, 70f, 30f));
            var effect = instance.GetComponent<XrBeatMeteorEffect>();
            effect.Emit(head.transform, 1f);
            var ps = instance.transform.Find("Meteors").GetComponent<ParticleSystem>();
            var particles = new ParticleSystem.Particle[128];
            int count = ps.GetParticles(particles);
            if (count != 18) throw new Exception("Expected nine meteors per side on a maximum impact, got " + count);
            Vector3 forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            int left = 0, rightCount = 0;
            for (int i = 0; i < count; i++)
            {
                var p = particles[i];
                float startSide = Vector3.Dot(p.position - head.transform.position, right);
                if (startSide < 0f) left++; else rightCount++;
                for (int step = 0; step <= 16; step++)
                {
                    float lifeFraction = step / 16f;
                    float travelled = 0.12f * lifeFraction
                        + 0.88f * (1f - Mathf.Exp(-3.4f * lifeFraction)) / 3.4f;
                    Vector3 relative = p.position + p.velocity * p.startLifetime * travelled - head.transform.position;
                    float side = Vector3.Dot(relative, right);
                    if (side * startSide <= 0f || relative.magnitude < 1.1f) throw new Exception("Trajectory crossed the central region or head clearance.");
                }
            }
            if (left != 9 || rightCount != 9) throw new Exception("Asymmetric volley.");
            var modifier = ps.velocityOverLifetime.speedModifier;
            float startSpeed = modifier.Evaluate(0f);
            float middleSpeed = modifier.Evaluate(0.5f);
            float endSpeed = modifier.Evaluate(1f);
            if (!(startSpeed > middleSpeed && middleSpeed > endSpeed && startSpeed > endSpeed * 5f))
                throw new Exception("Meteor speed did not decay strongly over its lifetime.");
            var alpha = ps.colorOverLifetime.color.gradient;
            if (!(alpha.Evaluate(0f).a > alpha.Evaluate(0.5f).a
                && alpha.Evaluate(0.5f).a > alpha.Evaluate(1f).a))
                throw new Exception("Meteor brightness did not decay over its lifetime.");
            uint probeSeed = particles[0].randomSeed;
            Vector3 previousPosition = particles[0].position;
            float previousStep = float.PositiveInfinity;
            for (int step = 0; step < 5; step++)
            {
                ps.Simulate(0.1f, false, false, false);
                count = ps.GetParticles(particles);
                int probe = Array.FindIndex(particles, 0, count, p => p.randomSeed == probeSeed);
                if (probe < 0) throw new Exception("Meteor expired before the decay probe finished.");
                float displacement = Vector3.Distance(previousPosition, particles[probe].position);
                if (displacement >= previousStep) throw new Exception("Simulated meteor did not slow down.");
                previousStep = displacement;
                previousPosition = particles[probe].position;
            }
            effect.Clear();
            if (effect.LiveParticleCount != 0) throw new Exception("Particle cleanup failed.");
            Debug.Log("[AudioEffects] Trajectory validation passed: 18 meteors, symmetric sides, decaying speed, tilted head, >1.1m clearance, cleanup.");
        }
        finally { Object.DestroyImmediate(instance); Object.DestroyImmediate(head); }
    }
}
