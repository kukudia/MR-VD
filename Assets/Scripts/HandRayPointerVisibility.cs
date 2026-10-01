using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>Shows hand ray feedback only when the user starts an intentional pinch.</summary>
[DefaultExecutionOrder(11000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RayInteractor), typeof(HandRef))]
public sealed class HandRayPointerVisibility : MonoBehaviour
{
    [SerializeField, Range(0.1f, 1f)] private float pinchShowThreshold = 0.65f;
    [SerializeField, Range(0f, 0.5f)] private float releaseGraceSeconds = 0.18f;
    [SerializeField] private Renderer[] pointerRenderers;
    private HandRef hand;
    private RayInteractor ray;
    private float visibleUntil;
    private bool hasFocus = true;
    private bool paused;

    private void Awake()
    {
        hand = GetComponent<HandRef>();
        ray = GetComponent<RayInteractor>();
        if (pointerRenderers == null || pointerRenderers.Length == 0)
            pointerRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable() { visibleUntil = float.NegativeInfinity; SetVisible(false); }
    private void OnApplicationFocus(bool focused) { hasFocus = focused; if (!focused) SetVisible(false); }
    private void OnApplicationPause(bool value) { paused = value; if (value) SetVisible(false); }

    private void LateUpdate()
    {
        IHand tracked = hand != null ? hand.Hand : null;
        bool valid = hasFocus && !paused && tracked != null && tracked.IsConnected
            && tracked.IsTrackedDataValid && tracked.IsPointerPoseValid
            && ray.State != InteractorState.Disabled;
        // Keep ordinary tracking and hit testing active; only the visual feedback is gated.
        if (valid && (tracked.GetFingerPinchStrength(HandFinger.Index) >= pinchShowThreshold
            || ray.State == InteractorState.Select))
            visibleUntil = Time.unscaledTime + releaseGraceSeconds;
        if (!valid) visibleUntil = float.NegativeInfinity;
        SetVisible(valid && Time.unscaledTime <= visibleUntil);
    }

    private void SetVisible(bool visible)
    {
        if (pointerRenderers == null) return;
        foreach (Renderer visual in pointerRenderers)
            if (visual != null) visual.forceRenderingOff = !visible;
    }

    private void OnDisable() => SetVisible(true);
}
