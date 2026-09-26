using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.Locomotion;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Grip-to-place on the existing ISDK canvas ray. Trigger remains the UI selector.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScreenPositionController))]
public sealed class ScreenRayManipulator : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private RayInteractable canvasInteractable;
    [SerializeField] private RayInteractor[] controllerRays = new RayInteractor[0];
    [SerializeField] private CanvasGroup canvasInputGroup;
    [SerializeField] private Text interactionHint;
    [Tooltip("Temporarily suspend controller locomotion while the sticks place the screen.")]
    [SerializeField] private LocomotionEventsConnection[] controllerLocomotion = new LocomotionEventsConnection[0];

    [Header("Placement Limits")]
    [SerializeField, Min(0.1f)] private float minimumDistance = 0.25f;
    [SerializeField, Min(0.1f)] private float maximumDistance = 4f;
    [Tooltip("Meters per second while holding grip and pushing the thumbstick up/down.")]
    [SerializeField, Min(0f)] private float distanceSpeed = 1f;
    [Tooltip("Uniform multipliers relative to the authored Screen scale. Aspect ratio is preserved.")]
    [SerializeField, Min(0.01f)] private float minimumScale = 0.35f;
    [SerializeField, Min(0.01f)] private float maximumScale = 3f;
    [Tooltip("Exponential scale rate while holding grip and pushing the thumbstick right/left.")]
    [SerializeField, Min(0f)] private float scaleSpeed = 0.8f;
    [SerializeField, Range(0f, 0.9f)] private float stickDeadZone = 0.2f;

    private ScreenPositionController positionController;
    private ControllerRef[] controllers;
    private bool[] gripArmed;
    private bool[] locomotionWasEnabled;
    private Vector3 authoredScale;
    private Vector3 localGrabPoint;
    private float grabDistance;
    private float scaleMultiplier = 1f;
    private int activeController = -1;
    private bool previousBlocksRaycasts;
    private bool hasFocus = true;
    private bool isPaused;

    public bool IsManipulating => activeController >= 0;

    private void Awake()
    {
        positionController = GetComponent<ScreenPositionController>();
        authoredScale = transform.localScale;
        controllers = new ControllerRef[controllerRays.Length];
        gripArmed = new bool[controllerRays.Length];
        locomotionWasEnabled = new bool[controllerLocomotion.Length];
        for (int i = 0; i < controllerRays.Length; i++)
            controllers[i] = controllerRays[i] != null ? controllerRays[i].GetComponent<ControllerRef>() : null;
    }

    private void OnEnable()
    {
        positionController.BeforeRecenter += EndManipulation;
        UpdateHint();
    }

    private void OnDisable()
    {
        EndManipulation();
        positionController.BeforeRecenter -= EndManipulation;
    }

    private void OnApplicationFocus(bool focused)
    {
        hasFocus = focused;
        if (!focused) EndManipulation();
    }

    private void OnApplicationPause(bool paused)
    {
        isPaused = paused;
        if (paused) EndManipulation();
    }

    private void LateUpdate()
    {
        if (!hasFocus || isPaused || canvasInteractable == null || !canvasInteractable.isActiveAndEnabled
            || canvasInputGroup == null)
        {
            EndManipulation();
            return;
        }

        if (IsManipulating)
        {
            UpdateManipulation(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            return;
        }

        bool uiBusy = false;
        for (int i = 0; i < controllerRays.Length; i++)
            uiBusy |= controllerRays[i] != null && controllerRays[i].SelectedInteractable == canvasInteractable;

        for (int i = 0; i < controllers.Length; i++)
        {
            if (!IsTracked(i))
            {
                gripArmed[i] = false;
                continue;
            }

            ControllerInput input = controllers[i].ControllerInput;
            bool pressed = input.GripButton && gripArmed[i];
            gripArmed[i] = !input.GripButton;
            RayInteractor ray = controllerRays[i];
            if (!pressed || uiBusy || input.TriggerButton || ray.Interactable != canvasInteractable
                || !ray.CollisionInfo.HasValue || ray.State != InteractorState.Hover)
                continue;

            float distance = Vector3.Distance(ray.Origin, ray.CollisionInfo.Value.Point);
            if (distance < minimumDistance || distance > Mathf.Min(maximumDistance, ray.MaxRayLength))
                continue;

            activeController = i;
            grabDistance = distance;
            localGrabPoint = transform.InverseTransformPoint(ray.CollisionInfo.Value.Point);
            positionController.BeginManualControl();
            previousBlocksRaycasts = canvasInputGroup.blocksRaycasts;
            canvasInputGroup.blocksRaycasts = false;
            for (int j = 0; j < controllerLocomotion.Length; j++)
            {
                if (controllerLocomotion[j] == null) continue;
                locomotionWasEnabled[j] = controllerLocomotion[j].enabled;
                controllerLocomotion[j].enabled = false;
            }
            UpdateHint();
            break;
        }
    }

    private void UpdateManipulation(float dt)
    {
        int index = activeController;
        if (!IsTracked(index) || !controllers[index].ControllerInput.GripButton)
        {
            EndManipulation();
            return;
        }

        RayInteractor ray = controllerRays[index];
        Vector2 stick = controllers[index].ControllerInput.Primary2DAxis;
        grabDistance = Mathf.Clamp(grabDistance + ApplyDeadZone(stick.y) * distanceSpeed * dt,
            minimumDistance, Mathf.Min(maximumDistance, ray.MaxRayLength));
        scaleMultiplier = Mathf.Clamp(scaleMultiplier * Mathf.Exp(ApplyDeadZone(stick.x) * scaleSpeed * dt),
            minimumScale, maximumScale);

        transform.localScale = authoredScale * scaleMultiplier;
        // Keep the original hit point on the ray, including while scaling off-center.
        // Rotation is deliberately retained so wrist roll does not tilt the whole desktop.
        Vector3 target = ray.Origin + ray.Forward * grabDistance;
        transform.position += target - transform.TransformPoint(localGrabPoint);
    }

    private bool IsTracked(int index)
    {
        return controllers[index] != null && controllers[index].isActiveAndEnabled
            && controllers[index].IsConnected && controllers[index].IsPoseValid
            && controllerRays[index] != null && controllerRays[index].isActiveAndEnabled
            && controllerRays[index].State != InteractorState.Disabled;
    }

    private float ApplyDeadZone(float value)
    {
        return Mathf.Sign(value) * Mathf.Clamp01((Mathf.Abs(value) - stickDeadZone) / (1f - stickDeadZone));
    }

    public void EndManipulation()
    {
        if (IsManipulating)
        {
            if (canvasInputGroup != null) canvasInputGroup.blocksRaycasts = previousBlocksRaycasts;
            for (int i = 0; i < controllerLocomotion.Length; i++)
                if (controllerLocomotion[i] != null) controllerLocomotion[i].enabled = locomotionWasEnabled[i];
            positionController.EndManualControl();
            activeController = -1;
        }
        // Reconnection, recenter and a second hand all require a fresh grip, not a held button.
        if (gripArmed != null) System.Array.Clear(gripArmed, 0, gripArmed.Length);
        UpdateHint();
    }

    private void UpdateHint()
    {
        if (interactionHint == null) return;
        interactionHint.text = IsManipulating
            ? "MOVING SCREEN  |  Stick up/down: distance  |  left/right: size  |  Release grip: pin"
            : "Point + hold GRIP: move screen  |  Stick: distance / size  |  A / R: recenter";
        interactionHint.color = IsManipulating ? new Color(0.35f, 0.95f, 1f) : new Color(0.7f, 0.8f, 0.85f);
    }

    private void OnValidate()
    {
        minimumDistance = Mathf.Max(0.1f, minimumDistance);
        maximumDistance = Mathf.Max(minimumDistance, maximumDistance);
        minimumScale = Mathf.Clamp(minimumScale, 0.01f, 1f);
        maximumScale = Mathf.Max(1f, maximumScale);
    }
}
