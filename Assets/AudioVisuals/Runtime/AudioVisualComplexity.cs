using UnityEngine;

/// <summary>
/// Editable mesh and particle counts. Applied explicitly by the Editor workshop, never implicitly
/// at runtime; authored transforms, palettes, timing and audio parameters stay independently editable.
/// </summary>
[CreateAssetMenu(menuName = "MR-VD/Audio Visual Complexity", fileName = "AudioVisualComplexity")]
public sealed class AudioVisualComplexity : ScriptableObject
{
    [Header("Ribbons and Rings")]
    [Range(1, 5)] public int ribbonsPerSide = 3;
    [Range(24, 192)] public int ribbonSegments = 96;
    [Range(2, 12)] public int ringCapacity = 6;
    [Range(32, 160)] public int ringSegments = 96;
    [Header("History and Canopy")]
    [Range(16, 96)] public int terrainBands = 48;
    [Range(16, 96)] public int terrainRows = 48;
    [Range(6, 24)] public int canopyStrands = 15;
    [Range(48, 256)] public int canopySegments = 160;
    [Header("Lattice and Iris")]
    [Range(12, 36)] public int latticeNodes = 20;
    [Range(2, 5)] public int latticeLayers = 3;
    [Range(4, 16)] public int latticeSegments = 8;
    [Range(12, 48)] public int irisPetals = 32;
    [Range(24, 128)] public int irisSegments = 72;
    [Header("GPU Particles")]
    [Range(256, 16384)] public int particleCount = 4096;

    public int MaximumMeshVertices => ribbonsPerSide * 4 * (ribbonSegments + 1)
        + ringCapacity * 2 * (ringSegments + 1) + terrainBands * terrainRows
        + canopyStrands * 2 * (canopySegments + 1)
        + (latticeLayers * (latticeNodes - 1) + (latticeLayers - 1) * latticeNodes) * (latticeSegments + 1) * 3
        + irisPetals * 2 * (irisSegments + 1);
    public int ParticleStateBytes => particleCount * 48;
}
