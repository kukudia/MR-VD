using UnityEngine;

/// <summary>Explicit scene wiring for a look. Apply on enable or from the Inspector context menu.</summary>
public sealed class AudioVisualLookPlayer : MonoBehaviour
{
    [SerializeField] private AudioVisualLook look;
    [SerializeField] private AudioVisualField field;
    [SerializeField] private GpuAudioParticleVisualizer particles;
    public AudioVisualLook Look => look;

    public void SetLook(AudioVisualLook value)
    {
        look = value;
        ApplyLook();
    }

    private void OnEnable()
    {
        ApplyLook();
    }

    [ContextMenu("Apply Authored Look")]
    public void ApplyLook()
    {
        if (look != null)
        {
            look.Apply(field, particles);
        }
    }
}
