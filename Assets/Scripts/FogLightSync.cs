using UnityEngine;

/// <summary>
/// Synchronizes a particle system's start color with a target light.
/// </summary>
public class FogLightSync : MonoBehaviour
{
    [Tooltip("Light source used to drive the particle color.")]
    public Light targetLight;

    private ParticleSystem fogParticleSystem;

    private void Start()
    {
        fogParticleSystem = GetComponent<ParticleSystem>();
    }

    private void Update()
    {
        if (targetLight == null || fogParticleSystem == null)
        {
            return;
        }

        ParticleSystem.MainModule mainModule = fogParticleSystem.main;

        Color adjustedColor = targetLight.color * targetLight.intensity;
        adjustedColor.a = mainModule.startColor.color.a;

        mainModule.startColor = adjustedColor;
    }
}
