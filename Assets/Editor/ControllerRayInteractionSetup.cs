using System;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.Locomotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ControllerRayInteractionSetup
{
    private const string ScenePath = "Assets/Scenes/v203.0.0.unity";
    private const string MenuPath = "Tools/MR-VD/Setup Controller Ray Interaction";
    private const string RayCanvasPrefabGuid = "8369d93f7b6b99742bbea0649a41b7b1";
    private const string InteractionObjectName = "Controller Ray Canvas Interaction";

    [MenuItem(MenuPath)]
    public static void SetupActiveScene()
    {
        ConfigureScene(SceneManager.GetActiveScene(), true);
    }

    public static void SetupBuildScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ConfigureScene(scene, false);
    }

    private static void ConfigureScene(Scene scene, bool registerUndo)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException("[ControllerRayInteraction] No loaded scene is available.");
        }

        GameObject interactionRig = FindSceneObject(scene, "[BuildingBlock] OVRComprehensiveInteractionRig");
        if (interactionRig == null)
        {
            throw new InvalidOperationException(
                "[ControllerRayInteraction] The configured OVR comprehensive interaction rig was not found.");
        }

        RayInteractor[] controllerRays = interactionRig
            .GetComponentsInChildren<RayInteractor>(true)
            .Where(interactor => interactor.GetComponent<ControllerRef>() != null).ToArray();
        if (controllerRays.Length < 2)
        {
            throw new InvalidOperationException(
                $"[ControllerRayInteraction] Expected left and right controller rays, but found {controllerRays.Length}.");
        }

        GameObject screen = FindSceneObject(scene, "Screen");
        Canvas canvas = screen != null ? screen.transform.Find("Canvas")?.GetComponent<Canvas>() : null;
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
        {
            throw new InvalidOperationException(
                "[ControllerRayInteraction] Screen/Canvas must exist and use World Space render mode.");
        }

        EnsureRayCanvasInteraction(canvas, registerUndo);
        LocomotionEventsConnection[] locomotion = interactionRig.GetComponentsInChildren<LocomotionEventsConnection>(true)
            .Where(connection => connection.name == "LocomotionControllerInteractorGroup").ToArray();
        EnsureScreenManipulation(screen, canvas, controllerRays, locomotion, registerUndo);
        ExpandCanvasToContent(canvas, registerUndo);
        EnsurePointableCanvasModule(scene, canvas, registerUndo);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            $"[ControllerRayInteraction] Configured {controllerRays.Length} controller rays for {canvas.transform.GetHierarchyPath()}, bounds {((RectTransform)canvas.transform).rect.size}.",
            canvas);
    }

    private static void EnsureRayCanvasInteraction(Canvas canvas, bool registerUndo)
    {
        PointableCanvas existing = canvas
            .GetComponentsInChildren<PointableCanvas>(true)
            .FirstOrDefault(pointableCanvas => pointableCanvas.Canvas == canvas);
        GameObject instance = existing != null ? existing.gameObject : null;
        if (instance == null)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(RayCanvasPrefabGuid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new InvalidOperationException("[ControllerRayInteraction] Meta Interaction SDK Ray Canvas template is unavailable.");
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            instance.name = InteractionObjectName;
            if (registerUndo) Undo.RegisterCreatedObjectUndo(instance, "Setup Controller Ray Interaction");
        }

        RectTransform rectTransform = instance.GetComponent<RectTransform>();
        Record(rectTransform, registerUndo);
        rectTransform.localPosition = Vector3.zero;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = Vector2.zero;
        PrefabUtility.RecordPrefabInstancePropertyModifications(rectTransform);

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            AddComponent<GraphicRaycaster>(canvas.gameObject, registerUndo);
        }

        PointableCanvas pointableCanvas = instance.GetComponent<PointableCanvas>();
        Record(pointableCanvas, registerUndo);
        pointableCanvas.InjectCanvas(canvas);
        EditorUtility.SetDirty(pointableCanvas);
        PrefabUtility.RecordPrefabInstancePropertyModifications(pointableCanvas);
    }

    private static void EnsureScreenManipulation(GameObject screen, Canvas canvas, RayInteractor[] rays,
        LocomotionEventsConnection[] locomotion, bool undo)
    {
        ScreenPositionController position = screen.GetComponent<ScreenPositionController>();
        if (position == null) position = AddComponent<ScreenPositionController>(screen, undo);
        CanvasGroup group = canvas.GetComponent<CanvasGroup>();
        if (group == null) group = AddComponent<CanvasGroup>(canvas.gameObject, undo);
        ScreenRayManipulator manipulator = screen.GetComponent<ScreenRayManipulator>();
        bool firstSetup = manipulator == null;
        if (firstSetup) manipulator = AddComponent<ScreenRayManipulator>(screen, undo);

        Transform hint = canvas.transform.Find("ScreenInteractionHint");
        if (hint != null)
        {
            if (undo) Undo.DestroyObjectImmediate(hint.gameObject);
            else UnityEngine.Object.DestroyImmediate(hint.gameObject);
        }

        Record(manipulator, undo);
        SerializedObject serialized = new SerializedObject(manipulator);
        serialized.FindProperty("canvasInteractable").objectReferenceValue = canvas.GetComponentInChildren<PointableCanvas>(true).GetComponent<RayInteractable>();
        serialized.FindProperty("canvasInputGroup").objectReferenceValue = group;
        serialized.FindProperty("interactionHint").objectReferenceValue = null;
        SerializedProperty rayArray = serialized.FindProperty("controllerRays");
        rayArray.arraySize = rays.Length;
        for (int i = 0; i < rays.Length; i++) rayArray.GetArrayElementAtIndex(i).objectReferenceValue = rays[i];
        SerializedProperty locomotionArray = serialized.FindProperty("controllerLocomotion");
        locomotionArray.arraySize = locomotion.Length;
        for (int i = 0; i < locomotion.Length; i++) locomotionArray.GetArrayElementAtIndex(i).objectReferenceValue = locomotion[i];
        serialized.ApplyModifiedProperties();

        // Enable only on Screen, not on the unrelated cube using the same position component.
        // Subsequent setup runs preserve the user's platform and startup choices.
        if (firstSetup)
        {
            Record(position, undo);
            SerializedObject settings = new SerializedObject(position);
            settings.FindProperty("applyPlatformPositionOnStart").boolValue = true;
            settings.ApplyModifiedProperties();
        }
    }

    private static void ExpandCanvasToContent(Canvas canvas, bool undo)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform root = (RectTransform)canvas.transform;
        Transform rayRoot = canvas.GetComponentInChildren<PointableCanvas>(true).transform;
        RectTransform[] children = root.Cast<Transform>().OfType<RectTransform>().Where(t => t != rayRoot).ToArray();
        Vector3[] positions = children.Select(t => t.localPosition).ToArray();
        Vector2[] sizes = children.Select(t => t.rect.size).ToArray();
        Vector2 extent = root.rect.size * 0.5f;
        Vector3[] corners = new Vector3[4];
        foreach (RectTransform child in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (child == root || child.IsChildOf(rayRoot)) continue;
            child.GetWorldCorners(corners);
            foreach (Vector3 worldCorner in corners)
            {
                Vector3 local = root.InverseTransformPoint(worldCorner);
                extent.x = Mathf.Max(extent.x, Mathf.Abs(local.x) + 0.02f);
                extent.y = Mathf.Max(extent.y, Mathf.Abs(local.y) + 0.02f);
            }
        }

        Record(root, undo);
        foreach (RectTransform child in children) Record(child, undo);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, extent.x * 2f);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, extent.y * 2f);
        for (int i = 0; i < children.Length; i++)
        {
            // Root expansion must not stretch, shift, or rescale the authored panels.
            children[i].SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, sizes[i].x);
            children[i].SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sizes[i].y);
            children[i].localPosition = positions[i];
            PrefabUtility.RecordPrefabInstancePropertyModifications(children[i]);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        Canvas.ForceUpdateCanvases();
    }

    private static void Record(UnityEngine.Object target, bool undo)
    {
        if (undo) Undo.RecordObject(target, "Setup Controller Ray Interaction");
        EditorUtility.SetDirty(target);
    }

    private static void EnsurePointableCanvasModule(Scene scene, Canvas canvas, bool registerUndo)
    {
        EventSystem eventSystem = scene
            .GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
            .FirstOrDefault(candidate => candidate.gameObject.activeInHierarchy);
        if (eventSystem == null)
        {
            throw new InvalidOperationException("[ControllerRayInteraction] No active EventSystem was found.");
        }

        PointableCanvasModule module = eventSystem.GetComponent<PointableCanvasModule>();
        if (module == null)
        {
            module = AddComponent<PointableCanvasModule>(eventSystem.gameObject, registerUndo);
        }

        Record(module, registerUndo);
        // Keep Unity's InputSystemUIInputModule enabled so desktop mouse/keyboard input
        // remains available alongside the Interaction SDK controller pointers.
        module.ExclusiveMode = false;
        EditorUtility.SetDirty(module);

        HybridCanvasInputModule hybrid = eventSystem.GetComponent<HybridCanvasInputModule>();
        if (hybrid == null)
        {
            hybrid = AddComponent<HybridCanvasInputModule>(eventSystem.gameObject, registerUndo);
        }
        Record(hybrid, registerUndo);
        SerializedObject hybridSettings = new SerializedObject(hybrid);
        hybridSettings.FindProperty("desktopCanvas").objectReferenceValue = canvas;
        hybridSettings.ApplyModifiedProperties();
    }

    private static T AddComponent<T>(GameObject gameObject, bool registerUndo) where T : Component
    {
        return registerUndo ? Undo.AddComponent<T>(gameObject) : gameObject.AddComponent<T>();
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        return scene
            .GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(transform => transform.name == objectName)
            ?.gameObject;
    }

    private static string GetHierarchyPath(this Transform transform)
    {
        return transform.parent == null
            ? transform.name
            : $"{transform.parent.GetHierarchyPath()}/{transform.name}";
    }
}
