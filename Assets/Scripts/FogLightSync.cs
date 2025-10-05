using UnityEngine;

public class FogLightSync : MonoBehaviour
{
    public Light targetLight; // 要同步颜色的灯光
    private ParticleSystem particleSystem;

    void Start()
    {
        // 获取粒子系统
        particleSystem = GetComponent<ParticleSystem>();
    }

    void Update()
    {
        if (targetLight != null && particleSystem != null)
        {
            // 获取粒子系统的主模块
            var mainModule = particleSystem.main;

            // 获取灯光颜色和强度
            Color lightColor = targetLight.color;
            float lightIntensity = targetLight.intensity;

            // 根据灯光强度调整颜色亮度
            Color adjustedColor = lightColor * lightIntensity;

            // 确保颜色的 alpha 值不受影响（如果粒子需要透明度）
            adjustedColor.a = mainModule.startColor.color.a;

            // 设置粒子系统的开始颜色
            mainModule.startColor = adjustedColor;
        }
    }
}
