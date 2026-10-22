using UnityEngine;

/// <summary>Editable reproducible source parameters for AudioGraphSurfaceTools.GenerateAndApply.</summary>
[CreateAssetMenu(menuName = "MR-VD/Audio Graph Texture Settings", fileName = "GraphTextureSettings")]
public sealed class AudioGraphTextureSettings : ScriptableObject
{
    [Range(64, 512)] public int resolution = 256;
    public int seed = 8457;
    [Range(0.05f, 0.5f)] public float starHalo = 0.22f;
    [Range(2f, 12f)] public float mistScale = 5f;
    [Range(8f, 60f)] public float filamentFrequency = 28f;
}
