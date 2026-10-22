using UnityEngine;

/// <summary>Editable performance look combining an existing theme, composition and GPU formation.</summary>
[CreateAssetMenu(menuName = "MR-VD/Audio Visual Look", fileName = "AudioVisualLook")]
public sealed class AudioVisualLook : ScriptableObject
{
    [SerializeField] private AudioVisualTheme theme;
    [SerializeField] private AudioVisualField.Composition composition;
    [SerializeField, Range(0f, 1f)] private float intensity = 0.85f;
    [SerializeField] private GpuAudioParticleVisualizer.ParticleFormation formation;

    public void Apply(AudioVisualField field, GpuAudioParticleVisualizer particles)
    {
        if (field != null)
        {
            field.SetTheme(theme);
            field.SetComposition(composition);
            field.SetIntensity(intensity);
        }
        if (particles != null)
        {
            particles.SetFormation(formation);
        }
    }
}
