using System;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Input;
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

        int controllerRayCount = interactionRig
            .GetComponentsInChildren<RayInteractor>(true)
            .Count(interactor => interactor.GetComponent<ControllerRef>() != null);
        if (controllerRayCount < 2)
        {
            throw new InvalidOperationException(
                $"[ControllerRayInteraction] Expected left and right controller rays, but found {controllerRayCount}.");
        }

        GameObject screen = FindSceneObject(scene, "Screen");
        Canvas canvas = screen != null ? screen.transform.Find("Canvas")?.GetComponent<Canvas>() : null;
        if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
        {
            throw new InvalidOperationException(
                "[ControllerRayInteraction] Screen/Canvas must exist and use World Space render mode.");
        }

        EnsureRayCanvasInteraction(canvas, registerUndo);
        EnsurePointableCanvasModule(scene, registerUndo);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            $"[ControllerRayInteraction] Configured {controllerRayCount} controller rays for {canvas.transform.GetHierarchyPath()}.",
            canvas);
    }

    private static void EnsureRayCanvasInteraction(Canvas canvas, bool registerUndo)
    {
        PointableCanvas existing = canvas
            .GetComponentsInChildren<PointableCanvas>(true)
            .FirstOrDefault(pointableCanvas => pointableCanvas.Canvas == canvas);
        if (existing != null)
        {
            return;
        }

        string prefabPath = AssetDatabase.GUIDToAssetPath(RayCanvasPrefabGuid);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException(
                "[ControllerRayInteraction] Meta Interaction SDK Ray Canvas template is unavailable.");
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
        instance.name = InteractionObjectName;
        if (registerUndo)
        {
            Undo.RegisterCreatedObjectUndo(instance, "Setup Controller Ray Interaction");
        }

        RectTransform rectTransform = instance.GetComponent<RectTransform>();
        rectTransform.localPosition = Vector3.zero;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = Vector2.zero;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            AddComponent<GraphicRaycaster>(canvas.gameObject, registerUndo);
        }

        PointableCanvas pointableCanvas = instance.GetComponent<PointableCanvas>();
        pointableCanvas.InjectCanvas(canvas);
        EditorUtility.SetDirty(pointableCanvas);
    }

    private static void EnsurePointableCanvasModule(Scene scene, bool registerUndo)
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

        // ISDK owns UI pointer dispatch in headset builds; this avoids two input modules
        // competing for hover, selection, and drag state on the same EventSystem.
        module.ExclusiveMode = true;
        EditorUtility.SetDirty(module);
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
