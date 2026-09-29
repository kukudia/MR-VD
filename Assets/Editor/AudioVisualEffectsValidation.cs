using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AudioVisualEffectsValidation
{
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
            if (count != 18) throw new Exception("Expected nine meteors per side, got " + count);
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
                    Vector3 relative = p.position + p.velocity * p.startLifetime * step / 16f - head.transform.position;
                    float side = Vector3.Dot(relative, right);
                    if (side * startSide <= 0f || relative.magnitude < 1.1f) throw new Exception("Trajectory crossed the central region or head clearance.");
                }
            }
            if (left != 9 || rightCount != 9) throw new Exception("Asymmetric volley.");
            effect.Clear();
            if (effect.LiveParticleCount != 0) throw new Exception("Particle cleanup failed.");
            Debug.Log("[AudioEffects] Trajectory validation passed: 18 meteors, symmetric sides, tilted head, >1.1m clearance, cleanup.");
        }
        finally { Object.DestroyImmediate(instance); Object.DestroyImmediate(head); }
    }
}
