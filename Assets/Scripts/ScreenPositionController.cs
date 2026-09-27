using System;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Repositions the attached screen in front of the headset and optionally follows the main camera.
/// </summary>
[DisallowMultipleComponent]
public class ScreenPositionController : MonoBehaviour
{
    private const string LogPrefix = "[ScreenPositionController]";

    public enum PositioningPlatform { Automatic, Desktop, Headset }

    [Header("Platform Placement")]
    [SerializeField] private PositioningPlatform platform = PositioningPlatform.Automatic;
    [Tooltip("Apply the desktop or headset placement at startup. Headset also includes PC VR / Quest Link.")]
    [SerializeField] private bool applyPlatformPositionOnStart;
    [SerializeField, Min(0.1f)] private float desktopDistance = 1.5f;
    [SerializeField] private float desktopHeightOffset;
    [SerializeField] private Vector3 desktopFollowOffset = new Vector3(0f, 0f, 1.5f);
    [Tooltip("Allow XR tracking to initialize before final startup placement. Manual placement ends this window.")]
    [SerializeField, Min(0f)] private float startupTrackingWait = 3f;

    public event Action BeforeRecenter;
    public bool IsManuallyControlled { get; private set; }
    public Transform CameraTransform => GetCameraTransform();
    public bool UsesHeadsetPlacement => platform == PositioningPlatform.Headset
        || (platform == PositioningPlatform.Automatic
            && (Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive));

    [Header("Follow Camera")]
    [Tooltip("When enabled, this object follows the main camera using the configured offset.")]
    public bool isFollowedCamera;

    [Tooltip("World-space offset applied from the main camera while following.")]
    public Vector3 positionOffset = new Vector3(0, -1, 2);

    [Tooltip("How quickly the screen follows the camera when follow mode is enabled.")]
    [Min(0.01f)]
    public float followSmoothing = 8f;

    [Header("Recenter")]
    [Tooltip("Optional camera reference. When empty, Camera.main is used.")]
    public Transform cameraOverride;

    [Tooltip("Places the screen in front of the camera when the scene starts.")]
    public bool recenterOnStart;

    [Tooltip("Allows keyboard recentering for editor and desktop testing.")]
    public bool enableKeyboardRecenter = true;

    [Tooltip("Keyboard key used to recenter the screen.")]
    public KeyCode keyboardRecenterKey = KeyCode.R;

    [Tooltip("Allows Meta Quest controller recentering through OVRInput.")]
    public bool enableQuestRecenter = true;

    [Tooltip("Meta Quest controller button used to recenter the screen. One is A on the right Touch controller.")]
    public OVRInput.Button questRecenterButton = OVRInput.Button.One;

    [Tooltip("Meta Quest controller mask used for the recenter button.")]
    public OVRInput.Controller questRecenterController = OVRInput.Controller.RTouch;

    [Tooltip("Distance from the camera to the screen after recentering.")]
    [Min(0.1f)]
    public float recenterDistance = 1f;

    [Tooltip("Vertical offset from the camera after recentering.")]
    public float recenterHeightOffset = -0.25f;

    [Tooltip("Lowest allowed headset pitch angle used for the recenter direction.")]
    [Range(-89f, 89f)]
    public float minPitchAngle = -15f;

    [Tooltip("Highest allowed headset pitch angle used for the recenter direction.")]
    [Range(-89f, 89f)]
    public float maxPitchAngle = 15f;

    [Tooltip("Aligns the screen rotation with the pitch-limited camera direction.")]
    public bool rotateToCameraForward = true;

    private Camera cachedMainCamera;
    private bool startupPlacementPending;
    private bool desktopStartupApplied;
    private float startupDeadline;

    private void Start()
    {
        startupPlacementPending = applyPlatformPositionOnStart || recenterOnStart;
        startupDeadline = Time.unscaledTime + startupTrackingWait;
    }

    private void Update()
    {
        if (ShouldRecenter())
        {
            RecenterScreen();
        }

        if (isFollowedCamera && !IsManuallyControlled && !startupPlacementPending)
        {
            Transform cameraTransform = GetCameraTransform();
            if (cameraTransform == null)
            {
                return;
            }

            Vector3 offset = UsesHeadsetPlacement ? positionOffset : desktopFollowOffset;
            Vector3 targetPosition = cameraTransform.position + offset;
            float followT = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, followT);
        }
    }

    private void LateUpdate()
    {
        if (!startupPlacementPending || IsManuallyControlled) return;
        bool headset = UsesHeadsetPlacement;
        InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        bool trackingReady = head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
        bool timedOut = Time.unscaledTime >= startupDeadline;
        Transform cameraTransform = GetCameraTransform();
        if (cameraTransform != null && (!headset || trackingReady || timedOut))
        {
            // Desktop can render immediately; allow a late PC VR startup to replace that initial pose once.
            if (headset || !desktopStartupApplied)
            {
                ApplyRecenter(cameraTransform);
                desktopStartupApplied = true;
            }
            if (headset || platform == PositioningPlatform.Desktop || timedOut)
                startupPlacementPending = false;
        }
        if (timedOut) startupPlacementPending = false;
    }

    public void BeginManualControl()
    {
        startupPlacementPending = false;
        IsManuallyControlled = true;
        // A deliberate placement pins the screen instead of snapping back on release.
        isFollowedCamera = false;
    }

    public void EndManualControl() => IsManuallyControlled = false;

    [ContextMenu("Recenter Screen")]
    public void RecenterScreen()
    {
        BeforeRecenter?.Invoke();
        startupPlacementPending = false;
        Transform cameraTransform = GetCameraTransform();
        if (cameraTransform == null)
        {
            Debug.LogWarning(LogPrefix + " Main camera is missing; screen recenter skipped.");
            return;
        }

        ApplyRecenter(cameraTransform);
    }

    private void ApplyRecenter(Transform cameraTransform)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = Vector3.forward;
        }

        flatForward.Normalize();

        float minPitch = Mathf.Min(minPitchAngle, maxPitchAngle);
        float maxPitch = Mathf.Max(minPitchAngle, maxPitchAngle);
        float cameraPitch = Mathf.Asin(Mathf.Clamp(cameraTransform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        float clampedPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);
        Vector3 clampedForward = (flatForward + Vector3.up * Mathf.Tan(clampedPitch * Mathf.Deg2Rad)).normalized;

        float distance = UsesHeadsetPlacement ? recenterDistance : desktopDistance;
        float height = UsesHeadsetPlacement ? recenterHeightOffset : desktopHeightOffset;
        transform.position = cameraTransform.position + clampedForward * distance + Vector3.up * height;

        if (rotateToCameraForward)
        {
            transform.rotation = Quaternion.LookRotation(clampedForward, Vector3.up);
        }
    }

    private bool ShouldRecenter()
    {
        if (enableKeyboardRecenter && keyboardRecenterKey != KeyCode.None && Input.GetKeyDown(keyboardRecenterKey))
        {
            return true;
        }

        return UsesHeadsetPlacement && enableQuestRecenter
               && questRecenterButton != OVRInput.Button.None
               && OVRInput.GetDown(questRecenterButton, questRecenterController);
    }

    private Transform GetCameraTransform()
    {
        if (cameraOverride != null)
        {
            return cameraOverride;
        }

        if (cachedMainCamera == null)
        {
            cachedMainCamera = Camera.main;
        }

        return cachedMainCamera != null ? cachedMainCamera.transform : null;
    }

    private void OnValidate()
    {
        followSmoothing = Mathf.Max(0.01f, followSmoothing);
        recenterDistance = Mathf.Max(0.1f, recenterDistance);
        desktopDistance = Mathf.Max(0.1f, desktopDistance);
        startupTrackingWait = Mathf.Max(0f, startupTrackingWait);
    }
}
