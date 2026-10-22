using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Renders isolated production surface shaders and reads real pixels to check reading clearance.
/// Covers front/back geometry, rotated reading planes, per-view offsets and near fade in mono;
/// these GPU image probes do not certify headset stereo or Player performance.
/// </summary>
public static class AudioVisualPixelValidation
{
    private const int Size = 128;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ProbeParticle
    {
        public Vector3 position;
        public float size;
        public Vector3 velocity;
        public float seed;
        public Vector4 color;
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Validate GPU Particle Reading Pixels")]
    public static void RunGpuParticles()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Run isolated pixel validation outside Play Mode.");
        }
        PreviewRenderUtility preview = new PreviewRenderUtility();
        Material material = new Material(Shader.Find("MR-VD/Audio Reactive GPU Stardust"));
        Texture2D pixels = new Texture2D(Size, Size, TextureFormat.RGB24, false);
        ComputeBuffer buffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf<ProbeParticle>());
        try
        {
            preview.camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            preview.camera.fieldOfView = 60f;
            preview.camera.nearClipPlane = 0.05f;
            preview.camera.farClipPlane = 20f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Color.black;
            ProbeParticle[] particle = { new ProbeParticle { position = Vector3.forward * 3f, size = 1f, color = Vector4.one } };
            buffer.SetData(particle);
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetBuffer("_Particles", buffer);
            properties.SetFloat("_ParticleSize", 0.4f);
            properties.SetFloat("_Emission", 1f);
            properties.SetFloat("_Opacity", 1f);
            properties.SetFloat("_Saturation", 1f);
            properties.SetMatrix("_OcclusionWorldToLocal", Matrix4x4.Translate(Vector3.back * 2f));
            properties.SetVector("_OcclusionRect", new Vector4(-0.5f, -0.5f, 0.5f, 0.5f));
            Action<Camera> draw = camera =>
            {
                RenderParams parameters = new RenderParams(material)
                {
                    camera = camera,
                    worldBounds = new Bounds(Vector3.forward * 2f, Vector3.one * 10f),
                    matProps = properties
                };
                Graphics.RenderPrimitives(parameters, MeshTopology.Triangles, 6, 1);
            };
            Require(Read(preview, pixels, draw).x > 0.03f, "Missing GPU particle baseline pixels.");
            particle[0].velocity = Vector3.right;
            buffer.SetData(particle);
            properties.SetFloat("_MotionStretch", 4f);
            Read(preview, pixels, draw);
            Require(pixels.GetPixel(Size * 3 / 4, Size / 2).maxColorComponent > 0.01f
                && pixels.GetPixel(Size / 2, Size * 3 / 4).maxColorComponent < 0.01f,
                "GPU velocity glint did not align with projected motion.");
            properties.SetFloat("_MotionStretch", 0f);
            Read(preview, pixels, draw);
            Require(pixels.GetPixel(Size * 3 / 4, Size / 2).maxColorComponent < 0.01f,
                "Zero motion stretch did not restore round dust.");
            properties.SetFloat("_OcclusionEnabled", 1f);
            Require(Read(preview, pixels, draw).x < 0.01f, "GPU particle behind screen was not masked.");
            particle[0].position = Vector3.forward * 1.6f;
            buffer.SetData(particle);
            Require(Read(preview, pixels, draw).x < 0.01f, "GPU foreground particle obstructs reading ray.");
            properties.SetVector("_OcclusionRect", new Vector4(0.4f, -0.5f, 1.4f, 0.5f));
            Require(Read(preview, pixels, draw).x > 0.03f, "Offset-pivot screen incorrectly masks center particle.");
            properties.SetVector("_OcclusionRect", new Vector4(-0.5f, -0.5f, 0.5f, 0.5f));
            properties.SetMatrix("_OcclusionWorldToLocal", Matrix4x4.TRS(Vector3.forward * 2f,
                Quaternion.Euler(0f, 25f, 0f), Vector3.one).inverse);
            foreach (float offset in new[] { -0.032f, 0.032f })
            {
                preview.camera.transform.position = Vector3.right * offset;
                Require(Read(preview, pixels, draw).x < 0.01f, "GPU offset view reading clearance failed.");
            }
            Directory.CreateDirectory(AudioVisualFieldTools.PreviewDirectory);
            File.WriteAllText(AudioVisualFieldTools.PreviewDirectory + "/gpu-reading-pixels.txt",
                DateTimeOffset.Now.ToString("o") + "\nPASS: production GPU particle shader pixels; "
                + "background/foreground; non-centered rect; rotated plane; two mono view offsets.\n"
                + "Not a headset stereo or Player performance test.\n");
            Debug.Log("[AudioVisualPixels] PASS: GPU particle front/back, offset rect and rotated view reading clearance.");
        }
        finally
        {
            preview.Cleanup();
            buffer.Release();
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(pixels);
        }
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Validate Reading Clearance Pixels")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Run isolated pixel validation outside Play Mode.");
        }
        PreviewRenderUtility preview = new PreviewRenderUtility();
        Mesh mesh = new Mesh { name = "Reading clearance test quad" };
        Material material = new Material(Shader.Find("MR-VD/Spatial Audio Surface"));
        Texture2D pixels = new Texture2D(Size, Size, TextureFormat.RGB24, false);
        try
        {
            mesh.vertices = new[] { new Vector3(-8f, -8f, 0f), new Vector3(8f, -8f, 0f),
                new Vector3(-8f, 8f, 0f), new Vector3(8f, 8f, 0f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.uv = new[] { Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            GameObject host = new GameObject("Isolated surface pixel probe", typeof(MeshFilter), typeof(MeshRenderer));
            host.GetComponent<MeshFilter>().sharedMesh = mesh;
            host.GetComponent<MeshRenderer>().sharedMaterial = material;
            preview.AddSingleGO(host);
            preview.camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            preview.camera.fieldOfView = 60f;
            preview.camera.nearClipPlane = 0.05f;
            preview.camera.farClipPlane = 20f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Color.black;
            material.SetFloat("_Emission", 1f);
            material.SetFloat("_Opacity", 1f);
            material.SetFloat("_NearFadeStart", 0.8f);
            material.SetFloat("_NearFadeEnd", 1.4f);
            material.SetVector("_ScreenRect", new Vector4(-0.5f, -0.5f, 0.5f, 0.5f));
            for (int style = 0; style < 6; style++)
            {
                material.SetFloat("_Style", style);
                host.transform.position = Vector3.forward * 3f;
                material.SetFloat("_ScreenEnabled", 0f);
                Vector2 baseline = Read(preview, pixels);
                Require(baseline.x > 0.03f && baseline.y > 0.03f, "Missing baseline pixels for style " + style);
                material.SetFloat("_ScreenEnabled", 1f);
                material.SetMatrix("_ScreenWorldToLocal", Matrix4x4.TRS(Vector3.forward * 2f,
                    Quaternion.Euler(0f, 25f, 0f), Vector3.one).inverse);
                Vector2 behind = Read(preview, pixels);
                Require(behind.x < 0.01f && behind.y > baseline.y * 0.8f,
                    "Rotated screen failed to clear background while retaining exterior: " + style);
                host.transform.position = Vector3.forward * 1.6f;
                Vector2 foreground = Read(preview, pixels);
                Require(foreground.x < 0.01f && foreground.y > 0.03f,
                    "Reading ray failed to clear foreground: " + style);
                material.SetMatrix("_ScreenWorldToLocal", Matrix4x4.Translate(Vector3.forward * 2f));
                Require(Read(preview, pixels).x > 0.03f, "Screen behind camera incorrectly cleared pixels: " + style);
                material.SetFloat("_ScreenEnabled", 0f);
                host.transform.position = Vector3.forward * 0.5f;
                Require(Read(preview, pixels).x < 0.01f, "Near-eye surface did not fade: " + style);
                host.transform.position = Vector3.forward * 3f;
                // Separate mono cameras approximate eye offsets; a device is still required for stereo certification.
                foreach (float offset in new[] { -0.032f, 0.032f })
                {
                    preview.camera.transform.position = Vector3.right * offset;
                    material.SetFloat("_ScreenEnabled", 1f);
                    material.SetMatrix("_ScreenWorldToLocal", Matrix4x4.Translate(Vector3.back * 2f));
                    Require(Read(preview, pixels).x < 0.01f, "Offset view did not preserve reading clearance: " + style);
                }
                preview.camera.transform.position = Vector3.zero;
            }
            Directory.CreateDirectory(AudioVisualFieldTools.PreviewDirectory);
            File.WriteAllText(AudioVisualFieldTools.PreviewDirectory + "/reading-pixels.txt",
                DateTimeOffset.Now.ToString("o") + "\nPASS: six production surface styles; background/foreground; "
                + "rotated screen; behind-camera screen; near fade; two mono view offsets.\n"
                + "Actual GPU pixel readback, isolated camera; not headset stereo or Player performance.\n");
            Debug.Log("[AudioVisualPixels] PASS: six surface styles preserve reading clearance and near fade.");
        }
        finally
        {
            preview.Cleanup();
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(pixels);
        }
    }

    private static Vector2 Read(PreviewRenderUtility preview, Texture2D pixels, Action<Camera> beforeRender = null)
    {
        preview.BeginPreview(new Rect(0f, 0f, Size, Size), GUIStyle.none);
        beforeRender?.Invoke(preview.camera);
        preview.Render(true);
        RenderTexture rendered = (RenderTexture)preview.EndPreview();
        RenderTexture scaled = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Graphics.Blit(rendered, scaled);
            RenderTexture.active = scaled;
            pixels.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
            pixels.Apply();
            return new Vector2(pixels.GetPixel(Size / 2, Size / 2).maxColorComponent,
                pixels.GetPixel(Size * 7 / 8, Size / 2).maxColorComponent);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(scaled);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
