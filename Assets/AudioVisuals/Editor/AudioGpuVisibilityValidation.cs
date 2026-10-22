using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Exercises the real render submission state of a running laboratory GPU component. Temporarily
/// changes only its bound field and frame, restores them in finally, and never saves scene data.
/// </summary>
public static class AudioGpuVisibilityValidation
{
    [MenuItem("Tools/MR-VD/Spatial Audio/Validate Running GPU Visibility")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Enter Play Mode in the synthetic laboratory first.");
        }
        GpuAudioParticleVisualizer particles = UnityEngine.Object.FindFirstObjectByType<GpuAudioParticleVisualizer>();
        AudioVisualField field = UnityEngine.Object.FindFirstObjectByType<AudioVisualField>();
        if (particles == null || field == null || !field.UsesDemonstration || !field.enabled)
        {
            throw new InvalidOperationException("An active explicit synthetic field and GPU component are required.");
        }
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(GpuAudioParticleVisualizer);
        FieldInfo binding = type.GetField("spatialField", flags);
        FieldInfo blockField = type.GetField("materialProperties", flags);
        MethodInfo render = type.GetMethod("RenderParticles", flags);
        object previous = binding.GetValue(particles);
        AudioVisualField.Frame original = field.CurrentFrame;
        if (!(bool)type.GetField("resourcesReady", flags).GetValue(particles) || particles.targetCamera == null)
        {
            throw new InvalidOperationException("Wait for GPU resources and camera before validation.");
        }
        try
        {
            binding.SetValue(particles, field);
            Action<float> expect = expected =>
            {
                render.Invoke(particles, new object[] { particles.targetCamera.transform.position });
                MaterialPropertyBlock block = (MaterialPropertyBlock)blockField.GetValue(particles);
                if (Mathf.Abs(block.GetFloat("_Opacity") - expected) > 0.0001f)
                {
                    throw new InvalidOperationException("GPU visibility submission did not match " + expected);
                }
            };
            AudioVisualField.Frame frame = original;
            frame.visibility = 0f;
            field.RenderFrame(frame);
            expect(0f);
            frame.visibility = 0.5f;
            field.RenderFrame(frame);
            expect(particles.opacity * 0.5f);
            frame.visibility = 1f;
            field.RenderFrame(frame);
            expect(particles.opacity);
            field.enabled = false;
            expect(0f);
            field.enabled = true;
            field.RenderFrame(frame);
            expect(particles.opacity);
            binding.SetValue(particles, null);
            expect(particles.opacity);
            Debug.Log("[AudioGpuVisibility] PASS: blackout, half fade, reveal, field disable/re-enable and independent use.");
        }
        finally
        {
            binding.SetValue(particles, previous);
            field.enabled = true;
            field.RenderFrame(original);
        }
    }
}
