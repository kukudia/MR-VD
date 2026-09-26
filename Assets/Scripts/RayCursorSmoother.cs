using UnityEngine;

/// <summary>Smooths Meta's beam and cursor display; hit testing stays at the tracked pose.</summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class RayCursorSmoother : MonoBehaviour
{
    [SerializeField, Range(0.01f, 0.25f)] private float positionResponse = 0.055f;
    [SerializeField, Range(0.01f, 0.25f)] private float scaleResponse = 0.08f;
    [SerializeField, Range(0.01f, 0.25f)] private float rotationResponse = 0.055f;

    private bool initialized;
    private Vector3 visualPosition;
    private Vector3 visualScale;
    private Quaternion visualRotation;

    private void OnEnable() => initialized = false;

    private void LateUpdate()
    {
        Vector3 targetPosition = transform.position;
        Vector3 targetScale = transform.localScale;
        Quaternion targetRotation = transform.rotation;
        if (!initialized)
        {
            visualPosition = targetPosition;
            visualScale = targetScale;
            visualRotation = targetRotation;
            initialized = true;
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        visualPosition = Vector3.Lerp(visualPosition, targetPosition,
            1f - Mathf.Exp(-dt / positionResponse));
        visualScale = Vector3.Lerp(visualScale, targetScale,
            1f - Mathf.Exp(-dt / scaleResponse));
        visualRotation = Quaternion.Slerp(visualRotation, targetRotation,
            1f - Mathf.Exp(-dt / rotationResponse));
        transform.position = visualPosition;
        transform.localScale = visualScale;
        transform.rotation = visualRotation;
    }
}
