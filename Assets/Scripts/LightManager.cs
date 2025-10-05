using System;
using UnityEngine;

public class LightManager : MonoBehaviour
{
    [Header("Light Groups")]
    public LightGroup[] lightGroups;

    [Header("Audio Source")]
    public AudioVisualizerCSCore visualizer; // 直接拖 AudioVisualizerCSCore 脚本进来

    [Header("Beat Settings")]
    public float beatFlashIntensity = 10f;
    public float beatFadeSpeed = 5f;

    [Header("Key Settings")]
    public Gradient majorKeyGradient;  // 大调配色
    public Gradient minorKeyGradient;  // 小调配色
    private Color currentKeyColor;

    private float beatFlashValue = 0f;

    void Update()
    {
        if (visualizer == null || visualizer.frequencyData == null) return;

        UpdateFrequencyLights();
        UpdateBeatLights();
        UpdateKeyLights();
    }

    private void UpdateFrequencyLights()
    {
        float low = GetAverageEnergy(0, visualizer.lowFrequencyRange);
        float mid = GetAverageEnergy(visualizer.lowFrequencyRange, visualizer.lowFrequencyRange * 2);
        float high = GetAverageEnergy(visualizer.lowFrequencyRange * 2, visualizer.lowFrequencyRange * 4);

        foreach (var group in lightGroups)
        {
            float intensity = 0f;

            switch (group.groupType)
            {
                case LightGroupType.LowFrequency:
                    intensity = low * group.intensityMultiplier;
                    break;
                case LightGroupType.MidFrequency:
                    intensity = mid * group.intensityMultiplier;
                    break;
                case LightGroupType.HighFrequency:
                    intensity = high * group.intensityMultiplier;
                    break;
            }

            ApplyLightGroup(group, intensity, group.baseColor);
        }
    }

    private void UpdateBeatLights()
    {
        if (visualizer.showBeatText) // 检测到节拍时闪烁
        {
            beatFlashValue = beatFlashIntensity;
        }

        beatFlashValue = Mathf.Lerp(beatFlashValue, 0, Time.deltaTime * beatFadeSpeed);

        foreach (var group in lightGroups)
        {
            if (group.groupType == LightGroupType.Beat)
            {
                ApplyLightGroup(group, beatFlashValue, Color.white);
            }
        }
    }

    private void UpdateKeyLights()
    {
        if (string.IsNullOrEmpty(GetKey())) return;

        bool isMinor = GetKey().Contains("m"); // 简化判定是否小调
        Gradient grad = isMinor ? minorKeyGradient : majorKeyGradient;

        float t = Mathf.PingPong(Time.time * 0.1f, 1f); // 循环变换颜色
        currentKeyColor = grad.Evaluate(t);

        foreach (var group in lightGroups)
        {
            if (group.groupType == LightGroupType.Key)
            {
                ApplyLightGroup(group, 3f, currentKeyColor);
            }
        }
    }

    private void ApplyLightGroup(LightGroup group, float intensity, Color color)
    {
        foreach (var light in group.lights)
        {
            if (light == null) continue;

            light.intensity = Mathf.Clamp(intensity, 0, 20);
            if (group.enableColorShift)
            {
                float hue = Mathf.Repeat(Time.time * 0.1f, 1f);
                Color hsvColor = Color.HSVToRGB(hue, 1f, 1f);
                light.color = Color.Lerp(color, hsvColor, 0.5f);
            }
            else
            {
                light.color = Color.Lerp(light.color, color, Time.deltaTime * 2f);
            }
        }
    }

    private float GetAverageEnergy(int start, int end)
    {
        if (visualizer.smoothedFftData == null || visualizer.smoothedFftData.Length == 0) return 0f;

        start = Mathf.Clamp(start, 0, visualizer.smoothedFftData.Length - 1);
        end = Mathf.Clamp(end, start, visualizer.smoothedFftData.Length - 1);

        float sum = 0f;
        for (int i = start; i < end; i++) sum += visualizer.smoothedFftData[i];
        return sum / (end - start + 1);
    }

    private string GetKey()
    {
        // 直接读取 visualizer 的 Key 字段
        var keyField = typeof(AudioVisualizerCSCore).GetField("currentKey",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (string)keyField?.GetValue(visualizer) ?? "C";
    }
}

[Serializable]
public class LightGroup
{
    public LightGroupType groupType;
    public Light[] lights;
    public Color baseColor = Color.white;
    public float intensityMultiplier = 5f;
    public bool enableColorShift = false;
}

public enum LightGroupType
{
    LowFrequency,
    MidFrequency,
    HighFrequency,
    Beat,
    Key
}
