using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Explicit desktop research binding for the independent masked mist graph. LateUpdate submits
/// one mono eye/screen contract; missing camera, stereo or disable hides output without respawning.
/// This conservative particle-center proxy does not certify per-pixel XR reading clearance.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public sealed class AudioMistClearanceBinding : MonoBehaviour
{
    [SerializeField] private VisualEffect effect;
    [SerializeField] private Camera eye;
    [SerializeField] private RectTransform readingScreen;
    [SerializeField, Min(0f)] private float padding = 0.12f;
    [SerializeField, Min(0f)] private float particleGuardRadius = 0.6f;
    [SerializeField, Min(0f)] private float nearStart = 0.8f;
    [SerializeField, Min(0.1f)] private float nearEnd = 1.4f;

    private void LateUpdate()
    {
        if (effect == null || !effect.HasFloat("ClearanceVisibility"))
        {
            return;
        }
        bool valid = eye != null && eye.isActiveAndEnabled && !eye.stereoEnabled;
        effect.SetFloat("ClearanceVisibility", valid ? 1f : 0f);
        if (!valid)
        {
            return;
        }
        effect.SetVector3("ClearanceEye", eye.transform.position);
        effect.SetVector4("ClearanceSettings", new Vector4(readingScreen != null ? 1f : 0f,
            nearStart, Mathf.Max(nearStart + 0.1f, nearEnd), particleGuardRadius));
        if (readingScreen != null)
        {
            Rect rect = readingScreen.rect;
            effect.SetMatrix4x4("ClearanceWorldToScreen", readingScreen.worldToLocalMatrix);
            effect.SetVector4("ClearanceRect", new Vector4(rect.xMin - padding, rect.yMin - padding,
                rect.xMax + padding, rect.yMax + padding));
        }
    }

    private void OnDisable()
    {
        if (effect != null && effect.HasFloat("ClearanceVisibility"))
        {
            effect.SetFloat("ClearanceVisibility", 0f);
        }
    }
}
