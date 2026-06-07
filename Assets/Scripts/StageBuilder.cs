using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor utility that creates a complete virtual stage with lights, VFX placeholders, and stage decor.
/// </summary>
public class StageBuilder : MonoBehaviour
{
    [Header("Stage Dimensions")]
    [Tooltip("Stage width in meters.")]
    public float stageWidth = 20f;

    [Tooltip("Stage depth in meters.")]
    public float stageDepth = 15f;

    [Tooltip("Stage floor height in meters.")]
    public float stageHeight = 0.5f;

    [Header("Lighting Setup")]
    [Tooltip("Number of top-down spotlights to create.")]
    public int spotlightCount = 6;

    [Tooltip("Number of moving chase lights to create around the stage.")]
    public int chaseLightCount = 4;

    [Tooltip("Number of rim lights to place around the stage edge.")]
    public int rimLightCount = 8;

    [Tooltip("Number of laser lights to create.")]
    public int laserLightCount = 6;

    [Tooltip("Number of strobe lights to create at the stage corners.")]
    public int strobeLightCount = 4;

    [Header("Decor Setup")]
    [Tooltip("Creates a reflective disco ball above the stage.")]
    public bool addDiscoBall = true;

    [Tooltip("Creates an LED screen wall behind the stage.")]
    public bool addLEDScreens = true;

    [Tooltip("Number of LED screen panels to create.")]
    public int ledScreenCount = 3;

#if UNITY_EDITOR
    [ContextMenu("Create Complete Stage")]
    public void BuildCompleteStage()
    {
        Debug.Log("[StageBuilder] Building virtual stage...");

        CleanupStage();

        GameObject stageRoot = new GameObject("VirtualStage");
        stageRoot.transform.position = transform.position;

        GameObject floor = CreateStageFloor(stageRoot);
        GameObject lightingSystem = CreateLightingSystem(stageRoot);
        GameObject vfxSystem = CreateVFXSystem(stageRoot);
        GameObject decorations = CreateDecorations(stageRoot);

        StageManager stageManager = stageRoot.AddComponent<StageManager>();
        LinkComponents(stageManager, lightingSystem, vfxSystem, decorations, floor);
        SetupDefaultParameters(stageManager);

        Debug.Log("[StageBuilder] Stage build complete.");
        Debug.Log("[StageBuilder] Assign the AudioVisualizer object to the StageManager audioVisualizer field.");

        Selection.activeGameObject = stageRoot;
    }

    private void CleanupStage()
    {
        GameObject existing = GameObject.Find("VirtualStage");
        if (existing != null)
        {
            Debug.Log("[StageBuilder] Removing existing virtual stage...");
            DestroyImmediate(existing);
        }
    }

    private GameObject CreateStageFloor(GameObject parent)
    {
        Debug.Log("[StageBuilder] Creating stage floor...");

        GameObject floorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floorObject.name = "StageFloor";
        floorObject.transform.parent = parent.transform;
        floorObject.transform.localPosition = Vector3.zero;
        floorObject.transform.localScale = new Vector3(stageWidth, stageHeight, stageDepth);

        Material floorMaterial = new Material(Shader.Find("Standard"));
        floorMaterial.name = "StageFloorMaterial";
        floorMaterial.SetFloat("_Metallic", 0.8f);
        floorMaterial.SetFloat("_Smoothness", 0.9f);
        floorMaterial.EnableKeyword("_EMISSION");
        floorMaterial.SetColor("_EmissionColor", Color.black);

        floorObject.GetComponent<Renderer>().material = floorMaterial;

        return floorObject;
    }

    private GameObject CreateLightingSystem(GameObject parent)
    {
        Debug.Log("[StageBuilder] Creating lighting system...");

        GameObject lightingRoot = new GameObject("LightingSystem");
        lightingRoot.transform.parent = parent.transform;
        lightingRoot.transform.localPosition = Vector3.zero;

        GameObject spotlightsGroup = new GameObject("Spotlights");
        spotlightsGroup.transform.parent = lightingRoot.transform;

        float spotlightHeight = 8f;
        for (int i = 0; i < spotlightCount; i++)
        {
            GameObject spotObj = new GameObject($"Spotlight_{i}");
            spotObj.transform.parent = spotlightsGroup.transform;

            float xPos = -stageWidth * 0.4f + (stageWidth * 0.8f / Mathf.Max(1, spotlightCount - 1)) * i;
            spotObj.transform.localPosition = new Vector3(xPos, spotlightHeight, 0f);
            spotObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Light spotlight = spotObj.AddComponent<Light>();
            spotlight.type = LightType.Spot;
            spotlight.intensity = 5f;
            spotlight.range = 15f;
            spotlight.spotAngle = 45f;
            spotlight.color = Color.white;
            spotlight.shadows = LightShadows.Soft;
        }

        GameObject chaseLightsGroup = new GameObject("ChaseLights");
        chaseLightsGroup.transform.parent = lightingRoot.transform;

        float chaseRadius = 8f;
        float chaseHeight = 6f;
        for (int i = 0; i < chaseLightCount; i++)
        {
            GameObject chaseObj = new GameObject($"ChaseLight_{i}");
            chaseObj.transform.parent = chaseLightsGroup.transform;

            float angle = (360f / Mathf.Max(1, chaseLightCount)) * i * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * chaseRadius;
            float z = Mathf.Sin(angle) * chaseRadius;
            chaseObj.transform.localPosition = new Vector3(x, chaseHeight, z);
            chaseObj.transform.LookAt(parent.transform);

            Light chaseLight = chaseObj.AddComponent<Light>();
            chaseLight.type = LightType.Spot;
            chaseLight.intensity = 3f;
            chaseLight.range = 12f;
            chaseLight.spotAngle = 30f;
            chaseLight.color = Color.white;
        }

        GameObject rimLightsGroup = new GameObject("RimLights");
        rimLightsGroup.transform.parent = lightingRoot.transform;

        for (int i = 0; i < rimLightCount; i++)
        {
            GameObject rimObj = new GameObject($"RimLight_{i}");
            rimObj.transform.parent = rimLightsGroup.transform;

            float t = (float)i / Mathf.Max(1, rimLightCount);
            float x;
            float z;

            if (t < 0.25f)
            {
                x = Mathf.Lerp(-stageWidth * 0.5f, stageWidth * 0.5f, t * 4f);
                z = stageDepth * 0.5f;
            }
            else if (t < 0.5f)
            {
                x = stageWidth * 0.5f;
                z = Mathf.Lerp(stageDepth * 0.5f, -stageDepth * 0.5f, (t - 0.25f) * 4f);
            }
            else if (t < 0.75f)
            {
                x = Mathf.Lerp(stageWidth * 0.5f, -stageWidth * 0.5f, (t - 0.5f) * 4f);
                z = -stageDepth * 0.5f;
            }
            else
            {
                x = -stageWidth * 0.5f;
                z = Mathf.Lerp(-stageDepth * 0.5f, stageDepth * 0.5f, (t - 0.75f) * 4f);
            }

            rimObj.transform.localPosition = new Vector3(x, 1f, z);
            rimObj.transform.LookAt(parent.transform);

            Light rimLight = rimObj.AddComponent<Light>();
            rimLight.type = LightType.Point;
            rimLight.intensity = 2f;
            rimLight.range = 5f;
            rimLight.color = Color.white;
        }

        GameObject laserLightsGroup = new GameObject("LaserLights");
        laserLightsGroup.transform.parent = lightingRoot.transform;

        for (int i = 0; i < laserLightCount; i++)
        {
            GameObject laserObj = new GameObject($"LaserLight_{i}");
            laserObj.transform.parent = laserLightsGroup.transform;

            float x = Random.Range(-stageWidth * 0.4f, stageWidth * 0.4f);
            float z = Random.Range(-stageDepth * 0.4f, stageDepth * 0.4f);
            laserObj.transform.localPosition = new Vector3(x, 10f, z);
            laserObj.transform.rotation = Quaternion.Euler(Random.Range(-45f, 45f), Random.Range(0f, 360f), 0f);

            Light laserLight = laserObj.AddComponent<Light>();
            laserLight.type = LightType.Spot;
            laserLight.intensity = 5f;
            laserLight.range = 20f;
            laserLight.spotAngle = 10f;
            laserLight.color = Color.red;
            laserLight.enabled = false;
        }

        GameObject strobeLightsGroup = new GameObject("StrobeLights");
        strobeLightsGroup.transform.parent = lightingRoot.transform;

        Vector3[] strobePositions =
        {
            new Vector3(-stageWidth * 0.4f, 10f, stageDepth * 0.4f),
            new Vector3(stageWidth * 0.4f, 10f, stageDepth * 0.4f),
            new Vector3(-stageWidth * 0.4f, 10f, -stageDepth * 0.4f),
            new Vector3(stageWidth * 0.4f, 10f, -stageDepth * 0.4f)
        };

        for (int i = 0; i < Mathf.Min(strobeLightCount, strobePositions.Length); i++)
        {
            GameObject strobeObj = new GameObject($"StrobeLight_{i}");
            strobeObj.transform.parent = strobeLightsGroup.transform;
            strobeObj.transform.localPosition = strobePositions[i];
            strobeObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Light strobeLight = strobeObj.AddComponent<Light>();
            strobeLight.type = LightType.Spot;
            strobeLight.intensity = 10f;
            strobeLight.range = 15f;
            strobeLight.spotAngle = 60f;
            strobeLight.color = Color.white;
            strobeLight.enabled = false;
        }

        return lightingRoot;
    }

    private GameObject CreateVFXSystem(GameObject parent)
    {
        Debug.Log("[StageBuilder] Creating VFX containers...");

        GameObject vfxRoot = new GameObject("VFXSystem");
        vfxRoot.transform.parent = parent.transform;
        vfxRoot.transform.localPosition = Vector3.zero;

        GameObject[] vfxContainers =
        {
            new GameObject("BackgroundParticles"),
            new GameObject("SmokeEffect"),
            new GameObject("BeatBurstEffect"),
            new GameObject("LaserBeamEffect"),
            new GameObject("GroundRingEffect")
        };

        foreach (GameObject container in vfxContainers)
        {
            container.transform.parent = vfxRoot.transform;
            container.transform.localPosition = Vector3.zero;

#if UNITY_VFX_GRAPH
            container.AddComponent<UnityEngine.VFX.VisualEffect>();
#endif
        }

        Debug.LogWarning("[StageBuilder] Assign Visual Effect Graph assets to the generated VFX containers.");

        return vfxRoot;
    }

    private GameObject CreateDecorations(GameObject parent)
    {
        Debug.Log("[StageBuilder] Creating stage decor...");

        GameObject decorRoot = new GameObject("Decorations");
        decorRoot.transform.parent = parent.transform;
        decorRoot.transform.localPosition = Vector3.zero;

        if (addDiscoBall)
        {
            GameObject discoBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            discoBall.name = "DiscoBall";
            discoBall.transform.parent = decorRoot.transform;
            discoBall.transform.localPosition = new Vector3(0f, 6f, 0f);
            discoBall.transform.localScale = Vector3.one * 1.5f;

            Material discoBallMaterial = new Material(Shader.Find("Standard"));
            discoBallMaterial.name = "DiscoBallMaterial";
            discoBallMaterial.SetFloat("_Metallic", 1f);
            discoBallMaterial.SetFloat("_Smoothness", 1f);
            discoBallMaterial.color = Color.white;

            discoBall.GetComponent<Renderer>().material = discoBallMaterial;

            ReflectionProbe probe = discoBall.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
        }

        if (addLEDScreens)
        {
            GameObject screensGroup = new GameObject("LEDScreens");
            screensGroup.transform.parent = decorRoot.transform;

            for (int i = 0; i < ledScreenCount; i++)
            {
                GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
                screen.name = $"LEDScreen_{i}";
                screen.transform.parent = screensGroup.transform;

                float xPos = -stageWidth * 0.3f + (stageWidth * 0.6f / Mathf.Max(1, ledScreenCount - 1)) * i;
                screen.transform.localPosition = new Vector3(xPos, 5f, -stageDepth * 0.5f);
                screen.transform.localScale = new Vector3(4f, 3f, 1f);

                Material screenMaterial = new Material(Shader.Find("Standard"));
                screenMaterial.name = $"LEDScreenMaterial_{i}";
                screenMaterial.EnableKeyword("_EMISSION");
                screenMaterial.SetColor("_EmissionColor", Color.black);

                screen.GetComponent<Renderer>().material = screenMaterial;
            }
        }

        return decorRoot;
    }

    private void LinkComponents(StageManager manager, GameObject lighting, GameObject vfx, GameObject decor, GameObject floor)
    {
        Debug.Log("[StageBuilder] Linking generated components...");

        manager.spotlights = lighting.transform.Find("Spotlights").GetComponentsInChildren<Light>();
        manager.chaseLights = lighting.transform.Find("ChaseLights").GetComponentsInChildren<Light>();
        manager.rimLights = lighting.transform.Find("RimLights").GetComponentsInChildren<Light>();
        manager.laserLights = lighting.transform.Find("LaserLights").GetComponentsInChildren<Light>();
        manager.strobeLights = lighting.transform.Find("StrobeLights").GetComponentsInChildren<Light>();

        if (addDiscoBall)
        {
            manager.discoBall = decor.transform.Find("DiscoBall")?.gameObject;
        }

        if (addLEDScreens)
        {
            manager.ledScreens = decor.transform.Find("LEDScreens")?.GetComponentsInChildren<Renderer>();
        }

        manager.stageFloor = floor.GetComponent<Renderer>();

        Debug.Log($"[StageBuilder] Linked {manager.spotlights.Length} spotlight(s).");
        Debug.Log($"[StageBuilder] Linked {manager.chaseLights.Length} chase light(s).");
        Debug.Log($"[StageBuilder] Linked {manager.rimLights.Length} rim light(s).");
    }

    private void SetupDefaultParameters(StageManager manager)
    {
        Debug.Log("[StageBuilder] Applying default stage parameters...");

        manager.baseLightIntensity = 2f;
        manager.beatLightIntensity = 8f;
        manager.colorChangeSpeed = 1f;
        manager.chaseLightSpeed = 0.5f;
        manager.particleSpawnRate = 100f;
        manager.smokeDensity = 0.3f;
        manager.laserIntensity = 5f;
        manager.strobeThreshold = 0.7f;
        manager.laserThreshold = 0.5f;
    }

    [ContextMenu("Cleanup Stage")]
    public void CleanupStageMenu()
    {
        CleanupStage();
        Debug.Log("[StageBuilder] Stage cleanup complete.");
    }
#endif
}
