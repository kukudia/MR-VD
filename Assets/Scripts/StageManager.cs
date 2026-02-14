using UnityEngine;
using UnityEngine.VFX;
using System.Collections.Generic;

/// <summary>
/// 虚拟舞台管理器
/// 统一管理舞台灯光、VFX特效、环境氛围
/// 与 AudioVisualizerCSCore 协同工作
/// </summary>
public class StageManager : MonoBehaviour
{
    [Header("舞台核心引用")]
    [Tooltip("音频可视化器引用")]
    public AudioVisualizerCSCore audioVisualizer;

    [Header("舞台灯光系统")]
    [Tooltip("主聚光灯阵列 - 顶部向下照射")]
    public Light[] spotlights;

    [Tooltip("舞台边缘轮廓灯 - RGB彩色")]
    public Light[] rimLights;

    [Tooltip("追光灯 - 跟随节拍移动")]
    public Light[] chaseLights;

    [Tooltip("激光灯阵列 - 快速扫描效果")]
    public Light[] laserLights;

    [Tooltip("频闪灯 - 节拍同步")]
    public Light[] strobeLights;

    [Header("VFX 粒子系统")]
    [Tooltip("背景粒子雨 - 持续效果")]
    public VisualEffect backgroundParticles;

    [Tooltip("烟雾效果 - 氛围营造")]
    public VisualEffect smokeEffect;

    [Tooltip("节拍爆发粒子 - Kick触发")]
    public VisualEffect beatBurstEffect;

    [Tooltip("激光束效果 - 高频段触发")]
    public VisualEffect laserBeamEffect;

    [Tooltip("地面光圈 - Bass驱动")]
    public VisualEffect groundRingEffect;

    [Header("舞台装饰")]
    [Tooltip("LED屏幕墙")]
    public Renderer[] ledScreens;

    [Tooltip("镜面球 - Disco球")]
    public GameObject discoBall;

    [Tooltip("舞台地板")]
    public Renderer stageFloor;

    [Header("灯光参数")]
    [Tooltip("主灯光强度基础值")]
    [Range(0f, 10f)]
    public float baseLightIntensity = 2f;

    [Tooltip("节拍时的灯光强度峰值")]
    [Range(0f, 20f)]
    public float beatLightIntensity = 8f;

    [Tooltip("灯光颜色变化速度")]
    [Range(0f, 5f)]
    public float colorChangeSpeed = 1f;

    [Tooltip("追光灯旋转速度（基于BPM）")]
    [Range(0f, 2f)]
    public float chaseLightSpeed = 0.5f;

    [Header("VFX 参数")]
    [Tooltip("背景粒子生成速率（粒子/秒）")]
    [Range(0f, 1000f)]
    public float particleSpawnRate = 100f;

    [Tooltip("烟雾密度")]
    [Range(0f, 1f)]
    public float smokeDensity = 0.3f;

    [Tooltip("激光束强度")]
    [Range(0f, 10f)]
    public float laserIntensity = 5f;

    [Header("调式氛围映射")]
    [Tooltip("大调（Major）- 暖色调（橙黄色）")]
    public Gradient majorMoodGradient;

    [Tooltip("小调（Minor）- 冷色调（蓝紫色）")]
    public Gradient minorMoodGradient;

    [Header("能量阈值")]
    [Tooltip("触发频闪灯的能量阈值")]
    [Range(0f, 1f)]
    public float strobeThreshold = 0.7f;

    [Tooltip("触发激光束的能量阈值")]
    [Range(0f, 1f)]
    public float laserThreshold = 0.5f;

    // 私有变量
    private float currentHue = 0f;
    private float chaseAngle = 0f;
    private float strobeTimer = 0f;
    private bool strobeActive = false;
    private Color currentMoodColor = Color.white;
    private float lastBeatTime = 0f;
    private List<LightMemory> spotlightMemories = new List<LightMemory>();

    private class LightMemory
    {
        public Light light;
        public float targetIntensity;
        public Color targetColor;
        public float smoothTime;
    }

    void Start()
    {
        // 初始化聚光灯记忆
        if (spotlights != null)
        {
            foreach (var light in spotlights)
            {
                if (light != null)
                {
                    spotlightMemories.Add(new LightMemory
                    {
                        light = light,
                        targetIntensity = baseLightIntensity,
                        targetColor = Color.white,
                        smoothTime = 0.2f
                    });
                }
            }
        }

        // 初始化渐变（如果未在Inspector中设置）
        if (majorMoodGradient == null)
        {
            majorMoodGradient = new Gradient();
            var colorKeys = new GradientColorKey[3];
            colorKeys[0] = new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0f);    // 金黄
            colorKeys[1] = new GradientColorKey(new Color(1f, 0.5f, 0f), 0.5f);    // 橙色
            colorKeys[2] = new GradientColorKey(new Color(1f, 0.2f, 0.2f), 1f);    // 红色
            var alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            majorMoodGradient.SetKeys(colorKeys, alphaKeys);
        }

        if (minorMoodGradient == null)
        {
            minorMoodGradient = new Gradient();
            var colorKeys = new GradientColorKey[3];
            colorKeys[0] = new GradientColorKey(new Color(0.2f, 0.2f, 1f), 0f);    // 深蓝
            colorKeys[1] = new GradientColorKey(new Color(0.5f, 0.2f, 0.8f), 0.5f); // 紫色
            colorKeys[2] = new GradientColorKey(new Color(0.2f, 0.8f, 0.8f), 1f);   // 青色
            var alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            minorMoodGradient.SetKeys(colorKeys, alphaKeys);
        }

        Debug.Log("[StageManager] 舞台系统初始化完成");
    }

    void Update()
    {
        if (audioVisualizer == null)
        {
            Debug.LogWarning("[StageManager] AudioVisualizer 引用缺失");
            return;
        }

        // 获取音频数据
        float kickEnergy = audioVisualizer.kickEnergy;
        float bassEnergy = audioVisualizer.bassEnergy;
        float synthEnergy = audioVisualizer.synthEnergy;
        float bpm = audioVisualizer.limitedBPM;
        bool isBeat = audioVisualizer.showBeatText;
        string currentMode = audioVisualizer.currentMode;

        // 更新调式氛围色
        UpdateMoodColor(currentMode);

        // 更新各灯光系统
        UpdateSpotlights(kickEnergy, isBeat);
        UpdateRimLights(bassEnergy);
        UpdateChaseLights(bpm, synthEnergy);
        UpdateLaserLights(synthEnergy);
        UpdateStrobeLights(kickEnergy, isBeat);

        // 更新VFX系统
        UpdateBackgroundParticles(bassEnergy);
        UpdateSmokeEffect(bassEnergy);
        UpdateBeatBurstEffect(kickEnergy, isBeat);
        UpdateLaserBeamEffect(synthEnergy);
        UpdateGroundRingEffect(bassEnergy);

        // 更新舞台装饰
        UpdateLEDScreens(kickEnergy, bassEnergy, synthEnergy);
        UpdateDiscoBall(bpm);
        UpdateStageFloor(bassEnergy);
    }

    /// <summary>
    /// 更新主聚光灯 - 节拍响应 + 颜色循环
    /// </summary>
    private void UpdateSpotlights(float kickEnergy, bool isBeat)
    {
        if (spotlights == null || spotlights.Length == 0) return;

        // 节拍触发时增强亮度
        float targetIntensity = isBeat ? beatLightIntensity : baseLightIntensity;
        targetIntensity *= (1f + kickEnergy * 0.5f);

        // 更新每个聚光灯
        for (int i = 0; i < spotlightMemories.Count; i++)
        {
            var memory = spotlightMemories[i];
            if (memory.light == null) continue;

            // 每个灯有不同的相位偏移
            float phaseOffset = (float)i / spotlights.Length;
            float hue = Mathf.Repeat(currentHue + phaseOffset, 1f);

            // 根据调式选择颜色
            Color targetColor = Color.HSVToRGB(hue, 0.8f, 1f);
            targetColor = Color.Lerp(targetColor, currentMoodColor, 0.3f);

            // 平滑过渡
            memory.light.intensity = Mathf.Lerp(
                memory.light.intensity,
                targetIntensity,
                Time.deltaTime * 10f
            );

            memory.light.color = Color.Lerp(
                memory.light.color,
                targetColor,
                Time.deltaTime * colorChangeSpeed
            );
        }

        // 色相循环
        currentHue += Time.deltaTime * colorChangeSpeed * 0.1f;
        if (currentHue > 1f) currentHue -= 1f;
    }

    /// <summary>
    /// 更新轮廓灯 - Bass能量驱动
    /// </summary>
    private void UpdateRimLights(float bassEnergy)
    {
        if (rimLights == null) return;

        foreach (var light in rimLights)
        {
            if (light == null) continue;

            // Bass能量映射到亮度
            light.intensity = baseLightIntensity * (0.5f + bassEnergy * 1.5f);

            // 彩虹色循环
            float hue = Mathf.Repeat(Time.time * 0.2f + light.transform.position.x * 0.1f, 1f);
            light.color = Color.HSVToRGB(hue, 1f, 1f);
        }
    }

    /// <summary>
    /// 更新追光灯 - BPM驱动旋转
    /// </summary>
    private void UpdateChaseLights(float bpm, float synthEnergy)
    {
        if (chaseLights == null) return;

        // 根据BPM计算旋转速度
        float rotationSpeed = bpm > 0 ? (bpm / 60f) * chaseLightSpeed : 0.5f;
        chaseAngle += Time.deltaTime * rotationSpeed * 360f;

        for (int i = 0; i < chaseLights.Length; i++)
        {
            var light = chaseLights[i];
            if (light == null) continue;

            // 每个追光灯有不同的角度偏移
            float offset = (360f / chaseLights.Length) * i;
            float angle = chaseAngle + offset;

            // 圆周运动
            float radius = 5f;
            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
            float z = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;

            light.transform.localPosition = new Vector3(x, light.transform.localPosition.y, z);

            // 朝向舞台中心
            light.transform.LookAt(transform.position);

            // 高频段能量驱动亮度
            light.intensity = baseLightIntensity * (0.8f + synthEnergy * 2f);

            // 颜色变化
            float hue = Mathf.Repeat(angle / 360f, 1f);
            light.color = Color.HSVToRGB(hue, 0.9f, 1f);
        }
    }

    /// <summary>
    /// 更新激光灯 - Synth能量触发
    /// </summary>
    private void UpdateLaserLights(float synthEnergy)
    {
        if (laserLights == null) return;

        bool shouldActivate = synthEnergy > laserThreshold;

        foreach (var light in laserLights)
        {
            if (light == null) continue;

            if (shouldActivate)
            {
                light.enabled = true;
                light.intensity = synthEnergy * 10f;

                // 快速随机方向变化
                if (Random.value > 0.95f)
                {
                    light.transform.rotation = Quaternion.Euler(
                        Random.Range(-45f, 45f),
                        Random.Range(0f, 360f),
                        0f
                    );
                }

                // 激光色彩
                float hue = Random.value;
                light.color = Color.HSVToRGB(hue, 1f, 1f);
            }
            else
            {
                light.intensity = Mathf.Lerp(light.intensity, 0f, Time.deltaTime * 5f);
                if (light.intensity < 0.1f) light.enabled = false;
            }
        }
    }

    /// <summary>
    /// 更新频闪灯 - 节拍同步
    /// </summary>
    private void UpdateStrobeLights(float kickEnergy, bool isBeat)
    {
        if (strobeLights == null) return;

        // 节拍触发或高能量触发频闪
        if ((isBeat && kickEnergy > strobeThreshold) || strobeActive)
        {
            strobeActive = true;
            strobeTimer += Time.deltaTime;

            // 快速闪烁（每秒10次）
            bool isOn = Mathf.FloorToInt(strobeTimer * 10f) % 2 == 0;

            foreach (var light in strobeLights)
            {
                if (light == null) continue;
                light.enabled = isOn;
                light.intensity = isOn ? beatLightIntensity * 2f : 0f;
                light.color = Color.white;
            }

            // 频闪持续0.2秒
            if (strobeTimer > 0.2f)
            {
                strobeActive = false;
                strobeTimer = 0f;
            }
        }
        else
        {
            // 关闭频闪灯
            foreach (var light in strobeLights)
            {
                if (light != null) light.enabled = false;
            }
        }
    }

    /// <summary>
    /// 更新背景粒子 - 持续效果
    /// </summary>
    private void UpdateBackgroundParticles(float bassEnergy)
    {
        if (backgroundParticles == null) return;

        // Bass能量驱动生成速率
        float rate = particleSpawnRate * (0.5f + bassEnergy);
        backgroundParticles.SetFloat("SpawnRate", rate);

        // 颜色变化
        backgroundParticles.SetVector4("ParticleColor", currentMoodColor);
    }

    /// <summary>
    /// 更新烟雾效果
    /// </summary>
    private void UpdateSmokeEffect(float bassEnergy)
    {
        if (smokeEffect == null) return;

        smokeEffect.SetVector3("TransformPosition", smokeEffect.transform.position);

        // 烟雾密度随Bass能量变化
        float density = smokeDensity * (0.3f + bassEnergy * 0.7f);
        smokeEffect.SetFloat("Density", density);

        // 烟雾颜色
        Color smokeColor = currentMoodColor;
        smokeColor.a = 0.5f;
        smokeEffect.SetVector4("SmokeColor", smokeColor);
    }

    /// <summary>
    /// 更新节拍爆发粒子 - Kick触发
    /// </summary>
    private void UpdateBeatBurstEffect(float kickEnergy, bool isBeat)
    {
        if (beatBurstEffect == null) return;

        if (isBeat && kickEnergy > 0.3f)
        {
            beatBurstEffect.SendEvent("OnBeatBurst");
            beatBurstEffect.SetFloat("BurstStrength", kickEnergy * 0.1f);
            beatBurstEffect.SetVector4("BurstColor", currentMoodColor);
        }
    }

    /// <summary>
    /// 更新激光束效果 - 高频段驱动
    /// </summary>
    private void UpdateLaserBeamEffect(float synthEnergy)
    {
        if (laserBeamEffect == null) return;

        if (synthEnergy > laserThreshold)
        {
            laserBeamEffect.SetFloat("BeamIntensity", synthEnergy * laserIntensity);
            laserBeamEffect.SetVector4("BeamColor", Color.HSVToRGB(Random.value, 1f, 1f));
        }
        else
        {
            laserBeamEffect.SetFloat("BeamIntensity", 0f);
        }
    }

    /// <summary>
    /// 更新地面光圈 - Bass驱动
    /// </summary>
    private void UpdateGroundRingEffect(float bassEnergy)
    {
        if (groundRingEffect == null) return;

        groundRingEffect.SetFloat("RingExpansion", bassEnergy * 5f);
        groundRingEffect.SetVector4("RingColor", currentMoodColor);
    }

    /// <summary>
    /// 更新LED屏幕墙 - 频谱可视化
    /// </summary>
    private void UpdateLEDScreens(float kick, float bass, float synth)
    {
        if (ledScreens == null) return;

        foreach (var screen in ledScreens)
        {
            if (screen == null) continue;

            // 根据能量混合颜色
            Color screenColor = new Color(
                kick * 2f,
                bass * 2f,
                synth * 2f,
                1f
            );

            screenColor = Color.Lerp(screenColor, currentMoodColor, 0.5f);

            // 设置材质自发光
            if (screen.material.HasProperty("_EmissionColor"))
            {
                screen.material.SetColor("_EmissionColor", screenColor * 2f);
            }
        }
    }

    /// <summary>
    /// 更新镜面球（Disco球）- BPM驱动旋转
    /// </summary>
    private void UpdateDiscoBall(float bpm)
    {
        if (discoBall == null) return;

        // 根据BPM旋转
        float rotationSpeed = bpm > 0 ? bpm / 60f : 0.5f;
        discoBall.transform.Rotate(Vector3.up, rotationSpeed * 360f * Time.deltaTime);
    }

    /// <summary>
    /// 更新舞台地板 - Bass能量驱动发光
    /// </summary>
    private void UpdateStageFloor(float bassEnergy)
    {
        if (stageFloor == null) return;

        // 地板发光
        Color floorColor = currentMoodColor * (0.5f + bassEnergy);

        if (stageFloor.material.HasProperty("_EmissionColor"))
        {
            stageFloor.material.SetColor("_EmissionColor", floorColor);
        }
    }

    /// <summary>
    /// 根据调式更新氛围颜色
    /// </summary>
    private void UpdateMoodColor(string mode)
    {
        Gradient targetGradient = mode == "Major" ? majorMoodGradient : minorMoodGradient;
        float t = Mathf.PingPong(Time.time * 0.2f, 1f);
        Color targetColor = targetGradient.Evaluate(t);

        currentMoodColor = Color.Lerp(currentMoodColor, targetColor, Time.deltaTime * 2f);
    }

    /// <summary>
    /// 调试用：在Scene视图中显示灯光位置
    /// </summary>
    void OnDrawGizmos()
    {
        // 聚光灯
        if (spotlights != null)
        {
            Gizmos.color = Color.yellow;
            foreach (var light in spotlights)
            {
                if (light != null)
                {
                    Gizmos.DrawWireSphere(light.transform.position, 0.3f);
                }
            }
        }

        // 追光灯
        if (chaseLights != null)
        {
            Gizmos.color = Color.cyan;
            foreach (var light in chaseLights)
            {
                if (light != null)
                {
                    Gizmos.DrawWireSphere(light.transform.position, 0.2f);
                    Gizmos.DrawLine(light.transform.position, transform.position);
                }
            }
        }

        // 轮廓灯
        if (rimLights != null)
        {
            Gizmos.color = Color.magenta;
            foreach (var light in rimLights)
            {
                if (light != null)
                {
                    Gizmos.DrawWireSphere(light.transform.position, 0.2f);
                }
            }
        }
    }
}