using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.Locomotion;
using Oculus.Interaction.Surfaces;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Hardware-independent probes against the saved scene and real manipulation component.</summary>
public static class ScreenInteractionVerification
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("Tools/MR-VD/Verify Screen Interaction")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run this verification in Edit Mode.");
        GameObject screen = GameObject.Find("Screen");
        Require(screen != null, "Screen exists");
        VerifyCoverage(screen.GetComponentInChildren<Canvas>());
        VerifyManipulation(screen);
        Debug.Log("[ScreenInteractionVerification] PASS: panel coverage/uGUI raycasts, desktop/headset placement, grip ownership, anchored move/scale, limits, tracking/focus/disable/recenter release.");
    }

    private static void VerifyCoverage(Canvas canvas)
    {
        RectTransform root = (RectTransform)canvas.transform;
        PointableCanvas pointable = canvas.GetComponentInChildren<PointableCanvas>();
        Require(pointable != null && pointable.Canvas == canvas, "PointableCanvas reference persisted");
        ClippedPlaneSurface surface = pointable.GetComponentInChildren<ClippedPlaneSurface>();
        surface.InjectClippers(surface.GetComponents<BoundsClipper>());
        Camera previousCamera = canvas.worldCamera;
        GameObject probe = new GameObject("Screen Verification Camera") { hideFlags = HideFlags.HideAndDontSave };
        RenderTexture preview = new RenderTexture(640, 480, 24);
        try
        {
            Camera camera = probe.AddComponent<Camera>();
            camera.enabled = false;
            camera.nearClipPlane = 0.01f;
            camera.targetTexture = preview;
            canvas.worldCamera = camera;
            Canvas.ForceUpdateCanvases();
            foreach (string panelName in new[] { "AudioPanel", "InfoPanel" })
            {
                RectTransform panel = (RectTransform)root.Find(panelName);
                Vector3[] corners = new Vector3[4];
                panel.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = root.InverseTransformPoint(corner);
                    Require(root.rect.Contains(local), panelName + " corner is inside Canvas");
                    Require(surface.Raycast(new Ray(corner - root.forward, root.forward), out _, 2f),
                        panelName + " outer corner hits the Meta ray surface");
                }

                Button button = panel.GetComponentsInChildren<Button>(true)
                    .First(b => b.gameObject.activeInHierarchy && b.interactable);
                Vector3 center = button.GetComponent<RectTransform>().TransformPoint(button.GetComponent<RectTransform>().rect.center);
                camera.transform.SetPositionAndRotation(center - root.forward, root.rotation);
                // In Edit Mode, a newly loaded Canvas has depth=-1 until rendered; uGUI skips it.
                camera.Render();
                var data = new PointerEventData(EventSystem.current) { position = camera.WorldToScreenPoint(center) };
                var hits = new List<RaycastResult>();
                canvas.GetComponent<GraphicRaycaster>().Raycast(data, hits);
                Require(hits.Any(h => h.gameObject.GetComponentInParent<Button>() == button), panelName + " button accepts uGUI raycast");
            }
        }
        finally
        {
            canvas.worldCamera = previousCamera;
            UnityEngine.Object.DestroyImmediate(probe);
            UnityEngine.Object.DestroyImmediate(preview);
        }
    }

    private static void VerifyManipulation(GameObject screen)
    {
        ScreenPositionController position = screen.GetComponent<ScreenPositionController>();
        ScreenRayManipulator manipulator = screen.GetComponent<ScreenRayManipulator>();
        Require(manipulator != null, "Screen manipulator persisted");
        var restores = new Stack<Action>();
        Vector3 oldPosition = screen.transform.position;
        Quaternion oldRotation = screen.transform.rotation;
        Vector3 oldScale = screen.transform.localScale;
        RayInteractor[] rays = (RayInteractor[])Get(manipulator, "controllerRays");
        Require(rays.Length == 2 && rays.All(r => r != null), "Both controller rays persisted");
        RayInteractable target = (RayInteractable)Get(manipulator, "canvasInteractable");
        CanvasGroup group = (CanvasGroup)Get(manipulator, "canvasInputGroup");
        bool oldBlocks = group.blocksRaycasts;
        var locomotion = (LocomotionEventsConnection[])Get(manipulator, "controllerLocomotion");
        Require(locomotion.Length == 2 && locomotion.All(c => c != null), "Both controller locomotion connections persisted");
        bool[] oldLocomotion = locomotion.Select(c => c.enabled).ToArray();
        Text hint = (Text)Get(manipulator, "interactionHint");
        string oldText = hint.text;
        Color oldColor = hint.color;
        var controllers = new FakeController[2];
        bool subscribed = false;
        try
        {
            RememberSet(position, "platform", ScreenPositionController.PositioningPlatform.Desktop, restores);
            RememberSet(position, "cameraOverride", Camera.main.transform, restores);
            RememberSet(position, "isFollowedCamera", true, restores);
            position.RecenterScreen();
            Require(!position.UsesHeadsetPlacement, "Desktop profile selected");
            Require(Vector3.Distance(screen.transform.position, Camera.main.transform.position) > 1f, "Desktop distance applied");
            Set(position, "platform", ScreenPositionController.PositioningPlatform.Headset);
            position.RecenterScreen();
            Require(position.UsesHeadsetPlacement, "Headset override selected without hardware");
            Require(Mathf.Abs(Vector3.Distance(screen.transform.position, Camera.main.transform.position)
                - position.recenterDistance) < 0.001f, "Headset scene distance applied");

            screen.transform.SetPositionAndRotation(new Vector3(0f, 0f, 1f), Quaternion.identity);
            for (int i = 0; i < rays.Length; i++)
            {
                ControllerRef controller = rays[i].GetComponent<ControllerRef>();
                RememberSet(controller, "Controller", controllers[i] = new FakeController(), restores);
                RememberSet(rays[i], "_state", InteractorState.Hover, restores);
                RememberSet(rays[i], "_interactable", target, restores);
                RememberSet(rays[i], "_selectedInteractable", null, restores);
                RememberSet(rays[i], "<Origin>k__BackingField", Vector3.zero, restores);
                RememberSet(rays[i], "<Forward>k__BackingField", Vector3.forward, restores);
                RememberSet(rays[i], "<CollisionInfo>k__BackingField",
                    (SurfaceHit?)new SurfaceHit { Point = new Vector3(0.3f, 0f, 1f), Distance = 1f }, restores);
            }
            // The hit is intentionally off-center to detect the common scale-around-pivot jump.
            Vector3 initialHit = new Vector3(0.3f, 0f, 1f);
            Set(rays[0], "<Forward>k__BackingField", initialHit.normalized);
            Call(manipulator, "Awake");
            Call(manipulator, "OnEnable");
            subscribed = true;
            Call(manipulator, "LateUpdate");
            Set(rays[1], "_selectedInteractable", target);
            controllers[0].Grip(true);
            Call(manipulator, "LateUpdate");
            Require(!manipulator.IsManipulating, "An existing UI drag prevents screen grab");
            Set(rays[1], "_selectedInteractable", null);
            controllers[0].Grip(false);
            Call(manipulator, "LateUpdate");
            controllers[0].Grip(true);
            Call(manipulator, "LateUpdate");
            Require(manipulator.IsManipulating && position.IsManuallyControlled && !group.blocksRaycasts, "Grip acquires screen and blocks UI");
            Require(!position.isFollowedCamera, "Manual placement disables follow");
            Require(locomotion.All(c => !c.enabled), "Screen grip suppresses controller locomotion");
            Vector3 localHit = screen.transform.InverseTransformPoint(initialHit);
            Vector3 shiftedOrigin = new Vector3(0.2f, 0.3f, 0.4f);
            Set(rays[0], "<Origin>k__BackingField", shiftedOrigin);
            Call(manipulator, "LateUpdate");
            Require(Vector3.Distance(screen.transform.TransformPoint(localHit), initialHit + shiftedOrigin) < 0.0001f, "3D movement keeps hit anchored");
            controllers[1].Grip(true);
            Call(manipulator, "LateUpdate");
            Require((int)Get(manipulator, "activeController") == 0, "Second hand cannot steal ownership");
            Vector3 unchangedPosition = screen.transform.position;
            Vector3 unchangedScale = screen.transform.localScale;
            controllers[0].Stick(new Vector2(0.1f, -0.1f));
            Call(manipulator, "LateUpdate");
            Require(screen.transform.position == unchangedPosition && screen.transform.localScale == unchangedScale, "Thumbstick dead zone prevents drift");

            RememberSet(manipulator, "scaleSpeed", 1000000f, restores);
            RememberSet(manipulator, "distanceSpeed", 1000000f, restores);
            Require(Time.unscaledDeltaTime > 0f, "Editor supplies a nonzero delta for the probe");
            controllers[0].Stick(Vector2.one);
            Call(manipulator, "LateUpdate");
            Require(Vector3.Distance(screen.transform.localScale, oldScale * (float)Get(manipulator, "maximumScale")) < 0.0001f, "Maximum scale and aspect ratio");
            float distance = (float)Get(manipulator, "grabDistance");
            Require(Mathf.Abs(distance - (float)Get(manipulator, "maximumDistance")) < 0.0001f, "Maximum distance");
            Require(Vector3.Distance(screen.transform.TransformPoint(localHit), rays[0].Origin + rays[0].Forward * distance) < 0.0001f, "Off-center scaling keeps grab point anchored");
            controllers[0].Stick(-Vector2.one);
            Call(manipulator, "LateUpdate");
            Require(Vector3.Distance(screen.transform.localScale, oldScale * (float)Get(manipulator, "minimumScale")) < 0.0001f, "Minimum scale");
            Require(Mathf.Abs((float)Get(manipulator, "grabDistance") - (float)Get(manipulator, "minimumDistance")) < 0.0001f, "Minimum distance");

            controllers[0].Tracked = false;
            Call(manipulator, "LateUpdate");
            Require(!manipulator.IsManipulating && group.blocksRaycasts == oldBlocks && !position.IsManuallyControlled, "Tracking loss releases and restores UI");
            Require(locomotion.Select(c => c.enabled).SequenceEqual(oldLocomotion), "Tracking loss restores prior locomotion states");
            controllers[0].Tracked = true;
            Call(manipulator, "LateUpdate");
            Require(!manipulator.IsManipulating, "Held grip cannot reacquire after tracking loss");

            foreach (string release in new[] { "release", "recenter", "focus", "disable" })
            {
                controllers[0].Grip(false);
                controllers[0].Stick(Vector2.zero);
                Call(manipulator, "LateUpdate");
                controllers[0].Grip(true);
                Call(manipulator, "LateUpdate");
                Require(manipulator.IsManipulating, "Fresh grip before " + release);
                if (release == "release") { controllers[0].Grip(false); Call(manipulator, "LateUpdate"); }
                if (release == "recenter") position.RecenterScreen();
                if (release == "focus") { Call(manipulator, "OnApplicationFocus", false); Call(manipulator, "OnApplicationFocus", true); }
                if (release == "disable") { Call(manipulator, "OnDisable"); subscribed = false; }
                Require(!manipulator.IsManipulating && group.blocksRaycasts == oldBlocks, release + " releases UI");
            }
        }
        finally
        {
            if (subscribed) Call(manipulator, "OnDisable");
            while (restores.Count > 0) restores.Pop()();
            group.blocksRaycasts = oldBlocks;
            for (int i = 0; i < locomotion.Length; i++) locomotion[i].enabled = oldLocomotion[i];
            hint.text = oldText;
            hint.color = oldColor;
            screen.transform.SetPositionAndRotation(oldPosition, oldRotation);
            screen.transform.localScale = oldScale;
        }
    }

    // Reflection is confined to this Editor probe: no test hooks or fake input enter player code.
    private static FieldInfo Field(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Fields | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }
    private static object Get(object target, string name) => Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void RememberSet(object target, string name, object value, Stack<Action> restores)
    {
        object previous = Get(target, name);
        restores.Push(() => Set(target, name, previous));
        Set(target, name, value);
    }
    private static void Call(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[ScreenInteractionVerification] " + message);
    }

    private sealed class FakeController : IController
    {
        public bool Tracked = true;
        private ControllerInput input;
        public Handedness Handedness => Handedness.Left;
        public float Scale => 1f;
        public bool IsConnected => Tracked;
        public bool IsPoseValid => Tracked;
        public ControllerInput ControllerInput => input;
        public event Action WhenUpdated { add { } remove { } }
        public bool TryGetPose(out Pose pose) { pose = Pose.identity; return Tracked; }
        public bool TryGetPointerPose(out Pose pose) => TryGetPose(out pose);
        public bool IsButtonUsageAnyActive(ControllerButtonUsage usage) => (input.ButtonUsageMask & usage) != 0;
        public bool IsButtonUsageAllActive(ControllerButtonUsage usage) => (input.ButtonUsageMask & usage) == usage;
        public void Grip(bool held) => input.ButtonUsageMask = held ? ControllerButtonUsage.GripButton : ControllerButtonUsage.None;
        public void Stick(Vector2 value) => input.SetAxis2D(ControllerAxis2DUsage.Primary2DAxis, value);
    }
}
