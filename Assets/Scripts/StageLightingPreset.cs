using UnityEngine;
using System;

/// <summary>
/// 灯光预设配置
/// 为不同音乐风格提供预设的灯光方案
/// </summary>
[CreateAssetMenu(fileName = "LightingPreset", menuName = "Stage/Lighting Preset")]
public class StageLightingPreset : ScriptableObject
{
    [Header("预设信息")]
    public string presetName = "Default";

    [TextArea(3, 5)]
    public string description = "默认灯光配置";

    public MusicGenre genre = MusicGenre.Electronic;

    [Header("主灯光参数")]
    [Range(0f, 10f)]
    public float baseLightIntensity = 2f;

    [Range(0f, 20f)]
    public float beatLightIntensity = 8f;

    [Range(0f, 5f)]
    public float colorChangeSpeed = 1f;

    [Header("颜色方案")]
    public ColorScheme colorScheme;

    [Header("动画参数")]
    [Range(0f, 2f)]
    public float chaseLightSpeed = 0.5f;

    [Range(0f, 5f)]
    public float spotlightRotationSpeed = 0f;

    public bool enableAutoColor = true;

    [Header("特效参数")]
    [Range(0f, 1f)]
    public float strobeFrequency = 0.7f;

    [Range(0f, 1f)]
    public float laserDensity = 0.5f;

    [Range(0f, 1f)]
    public float smokeAmount = 0.3f;

    [Header("节拍响应")]
    public BeatResponse kickResponse;
    public BeatResponse snareResponse;
    public BeatResponse bassResponse;

    /// <summary>
    /// 应用预设到 StageManager
    /// </summary>
    public void ApplyToStage(StageManager stage)
    {
        if (stage == null)
        {
            Debug.LogError("[LightingPreset] StageManager 引用为空");
            return;
        }

        stage.baseLightIntensity = baseLightIntensity;
        stage.beatLightIntensity = beatLightIntensity;
        stage.colorChangeSpeed = colorChangeSpeed;
        stage.chaseLightSpeed = chaseLightSpeed;
        stage.strobeThreshold = 1f - strobeFrequency;
        stage.laserThreshold = 1f - laserDensity;
        stage.smokeDensity = smokeAmount;

        Debug.Log($"[LightingPreset] 已应用预设: {presetName}");
    }
}

/// <summary>
/// 音乐风格枚举
/// </summary>
public enum MusicGenre
{
    Electronic,     // 电子音乐
    Rock,          // 摇滚
    HipHop,        // 嘻哈
    Jazz,          // 爵士
    Pop,           // 流行
    Classical,     // 古典
    Metal,         // 金属
    Ambient        // 氛围
}

/// <summary>
/// 颜色方案
/// </summary>
[Serializable]
public class ColorScheme
{
    [Header("主色调")]
    public Color primaryColor = Color.red;
    public Color secondaryColor = Color.blue;
    public Color accentColor = Color.white;

    [Header("调式映射")]
    public Gradient majorMoodGradient;
    public Gradient minorMoodGradient;

    [Header("能量映射")]
    public Gradient lowEnergyGradient;
    public Gradient highEnergyGradient;
}

/// <summary>
/// 节拍响应配置
/// </summary>
[Serializable]
public class BeatResponse
{
    [Header("响应类型")]
    public bool flashLights = true;
    public bool triggerParticles = true;
    public bool pulseIntensity = true;

    [Header("响应强度")]
    [Range(0f, 2f)]
    public float intensityMultiplier = 1f;

    [Range(0f, 1f)]
    public float smoothing = 0.5f;

    [Header("颜色变化")]
    public bool changeColor = false;
    public Color responseColor = Color.white;
}