using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 自动化舞台搭建器
/// 在编辑器中运行，自动创建完整的虚拟舞台场景
/// </summary>
public class StageBuilder : MonoBehaviour
{
    [Header("舞台尺寸")]
    [Tooltip("舞台宽度")]
    public float stageWidth = 20f;

    [Tooltip("舞台深度")]
    public float stageDepth = 15f;

    [Tooltip("舞台高度")]
    public float stageHeight = 0.5f;

    [Header("灯光配置")]
    [Tooltip("聚光灯数量")]
    public int spotlightCount = 6;

    [Tooltip("追光灯数量")]
    public int chaseLightCount = 4;

    [Tooltip("轮廓灯数量")]
    public int rimLightCount = 8;

    [Tooltip("激光灯数量")]
    public int laserLightCount = 6;

    [Tooltip("频闪灯数量")]
    public int strobeLightCount = 4;

    [Header("装饰配置")]
    [Tooltip("是否添加镜面球")]
    public bool addDiscoBall = true;

    [Tooltip("是否添加LED屏幕墙")]
    public bool addLEDScreens = true;

    [Tooltip("LED屏幕数量")]
    public int ledScreenCount = 3;

#if UNITY_EDITOR
    [ContextMenu("🎪 创建完整舞台")]
    public void BuildCompleteStage()
    {
        Debug.Log("[StageBuilder] 开始构建虚拟舞台...");

        // 清理现有舞台（如果有）
        CleanupStage();

        // 创建舞台根对象
        GameObject stageRoot = new GameObject("VirtualStage");
        stageRoot.transform.position = transform.position;

        // 1. 创建舞台地板
        GameObject floor = CreateStageFloor(stageRoot);

        // 2. 创建灯光系统
        GameObject lightingSystem = CreateLightingSystem(stageRoot);

        // 3. 创建VFX系统容器
        GameObject vfxSystem = CreateVFXSystem(stageRoot);

        // 4. 创建舞台装饰
        GameObject decorations = CreateDecorations(stageRoot);

        // 5. 添加 StageManager 组件
        StageManager stageManager = stageRoot.AddComponent<StageManager>();

        // 6. 链接所有组件
        LinkComponents(stageManager, lightingSystem, vfxSystem, decorations, floor);

        // 7. 设置默认参数
        SetupDefaultParameters(stageManager);

        Debug.Log("[StageBuilder] ✅ 舞台构建完成！");
        Debug.Log("[StageBuilder] 请将 AudioVisualizerCSCore 拖拽到 StageManager 的 audioVisualizer 字段");

        // 选中创建的舞台
        Selection.activeGameObject = stageRoot;
    }

    private void CleanupStage()
    {
        GameObject existing = GameObject.Find("VirtualStage");
        if (existing != null)
        {
            Debug.Log("[StageBuilder] 清理旧舞台...");
            DestroyImmediate(existing);
        }
    }

    private GameObject CreateStageFloor(GameObject parent)
    {
        Debug.Log("[StageBuilder] 创建舞台地板...");

        GameObject floorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floorObject.name = "StageFloor";
        floorObject.transform.parent = parent.transform;
        floorObject.transform.localPosition = Vector3.zero;
        floorObject.transform.localScale = new Vector3(stageWidth, stageHeight, stageDepth);

        // 创建发光材质
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
        Debug.Log("[StageBuilder] 创建灯光系统...");

        GameObject lightingRoot = new GameObject("LightingSystem");
        lightingRoot.transform.parent = parent.transform;
        lightingRoot.transform.localPosition = Vector3.zero;

        // 1. 聚光灯阵列（顶部向下）
        GameObject spotlightsGroup = new GameObject("Spotlights");
        spotlightsGroup.transform.parent = lightingRoot.transform;

        float spotlightHeight = 8f;
        for (int i = 0; i < spotlightCount; i++)
        {
            GameObject spotObj = new GameObject($"Spotlight_{i}");
            spotObj.transform.parent = spotlightsGroup.transform;

            float xPos = -stageWidth * 0.4f + (stageWidth * 0.8f / (spotlightCount - 1)) * i;
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

        // 2. 追光灯（环绕舞台）
        GameObject chaseLightsGroup = new GameObject("ChaseLights");
        chaseLightsGroup.transform.parent = lightingRoot.transform;

        float chaseRadius = 8f;
        float chaseHeight = 6f;
        for (int i = 0; i < chaseLightCount; i++)
        {
            GameObject chaseObj = new GameObject($"ChaseLight_{i}");
            chaseObj.transform.parent = chaseLightsGroup.transform;

            float angle = (360f / chaseLightCount) * i * Mathf.Deg2Rad;
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

        // 3. 轮廓灯（舞台边缘）
        GameObject rimLightsGroup = new GameObject("RimLights");
        rimLightsGroup.transform.parent = lightingRoot.transform;

        for (int i = 0; i < rimLightCount; i++)
        {
            GameObject rimObj = new GameObject($"RimLight_{i}");
            rimObj.transform.parent = rimLightsGroup.transform;

            // 沿舞台边缘分布
            float t = (float)i / rimLightCount;
            float x, z;

            if (t < 0.25f) // 前边
            {
                x = Mathf.Lerp(-stageWidth * 0.5f, stageWidth * 0.5f, t * 4f);
                z = stageDepth * 0.5f;
            }
            else if (t < 0.5f) // 右边
            {
                x = stageWidth * 0.5f;
                z = Mathf.Lerp(stageDepth * 0.5f, -stageDepth * 0.5f, (t - 0.25f) * 4f);
            }
            else if (t < 0.75f) // 后边
            {
                x = Mathf.Lerp(stageWidth * 0.5f, -stageWidth * 0.5f, (t - 0.5f) * 4f);
                z = -stageDepth * 0.5f;
            }
            else // 左边
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

        // 4. 激光灯（随机位置）
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
            laserLight.enabled = false; // 默认关闭
        }

        // 5. 频闪灯（顶部四角）
        GameObject strobeLightsGroup = new GameObject("StrobeLights");
        strobeLightsGroup.transform.parent = lightingRoot.transform;

        Vector3[] strobePositions = new Vector3[]
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
            strobeLight.enabled = false; // 默认关闭
        }

        return lightingRoot;
    }

    private GameObject CreateVFXSystem(GameObject parent)
    {
        Debug.Log("[StageBuilder] 创建VFX系统容器...");

        GameObject vfxRoot = new GameObject("VFXSystem");
        vfxRoot.transform.parent = parent.transform;
        vfxRoot.transform.localPosition = Vector3.zero;

        // 创建空的VFX容器（需要手动添加Visual Effect组件和VFX Graph）
        GameObject[] vfxContainers = new GameObject[]
        {
            new GameObject("BackgroundParticles"),
            new GameObject("SmokeEffect"),
            new GameObject("BeatBurstEffect"),
            new GameObject("LaserBeamEffect"),
            new GameObject("GroundRingEffect")
        };

        foreach (var container in vfxContainers)
        {
            container.transform.parent = vfxRoot.transform;
            container.transform.localPosition = Vector3.zero;

            // 添加 Visual Effect 组件
            // 注意：需要在Unity编辑器中手动分配VFX Graph资源
#if UNITY_VFX_GRAPH
            container.AddComponent<UnityEngine.VFX.VisualEffect>();
#endif
        }

        Debug.Log("[StageBuilder] ⚠️ 请手动为VFX容器添加Visual Effect Graph资源");

        return vfxRoot;
    }

    private GameObject CreateDecorations(GameObject parent)
    {
        Debug.Log("[StageBuilder] 创建舞台装饰...");

        GameObject decorRoot = new GameObject("Decorations");
        decorRoot.transform.parent = parent.transform;
        decorRoot.transform.localPosition = Vector3.zero;

        // 1. 镜面球（Disco球）
        if (addDiscoBall)
        {
            GameObject discoBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            discoBall.name = "DiscoBall";
            discoBall.transform.parent = decorRoot.transform;
            discoBall.transform.localPosition = new Vector3(0f, 6f, 0f);
            discoBall.transform.localScale = Vector3.one * 1.5f;

            // 镜面材质
            Material discoBallMaterial = new Material(Shader.Find("Standard"));
            discoBallMaterial.name = "DiscoBallMaterial";
            discoBallMaterial.SetFloat("_Metallic", 1f);
            discoBallMaterial.SetFloat("_Smoothness", 1f);
            discoBallMaterial.color = Color.white;

            discoBall.GetComponent<Renderer>().material = discoBallMaterial;

            // 添加反射探针
            ReflectionProbe probe = discoBall.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
        }

        // 2. LED屏幕墙
        if (addLEDScreens)
        {
            GameObject screensGroup = new GameObject("LEDScreens");
            screensGroup.transform.parent = decorRoot.transform;

            for (int i = 0; i < ledScreenCount; i++)
            {
                GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
                screen.name = $"LEDScreen_{i}";
                screen.transform.parent = screensGroup.transform;

                float xPos = -stageWidth * 0.3f + (stageWidth * 0.6f / (ledScreenCount - 1)) * i;
                screen.transform.localPosition = new Vector3(xPos, 5f, -stageDepth * 0.5f);
                screen.transform.localScale = new Vector3(4f, 3f, 1f);

                // 发光屏幕材质
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
        Debug.Log("[StageBuilder] 链接组件...");

        // 链接灯光
        manager.spotlights = lighting.transform.Find("Spotlights").GetComponentsInChildren<Light>();
        manager.chaseLights = lighting.transform.Find("ChaseLights").GetComponentsInChildren<Light>();
        manager.rimLights = lighting.transform.Find("RimLights").GetComponentsInChildren<Light>();
        manager.laserLights = lighting.transform.Find("LaserLights").GetComponentsInChildren<Light>();
        manager.strobeLights = lighting.transform.Find("StrobeLights").GetComponentsInChildren<Light>();

        // 链接VFX（需要手动分配Visual Effect组件）
        // manager.backgroundParticles = vfx.transform.Find("BackgroundParticles").GetComponent<VisualEffect>();
        // ... 等等

        // 链接装饰
        if (addDiscoBall)
        {
            manager.discoBall = decor.transform.Find("DiscoBall")?.gameObject;
        }

        if (addLEDScreens)
        {
            manager.ledScreens = decor.transform.Find("LEDScreens")?.GetComponentsInChildren<Renderer>();
        }

        // 链接地板
        manager.stageFloor = floor.GetComponent<Renderer>();

        Debug.Log($"[StageBuilder] 已链接 {manager.spotlights.Length} 个聚光灯");
        Debug.Log($"[StageBuilder] 已链接 {manager.chaseLights.Length} 个追光灯");
        Debug.Log($"[StageBuilder] 已链接 {manager.rimLights.Length} 个轮廓灯");
    }

    private void SetupDefaultParameters(StageManager manager)
    {
        Debug.Log("[StageBuilder] 设置默认参数...");

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

    [ContextMenu("🗑️ 清理舞台")]
    public void CleanupStageMenu()
    {
        CleanupStage();
        Debug.Log("[StageBuilder] 舞台已清理");
    }
#endif
}