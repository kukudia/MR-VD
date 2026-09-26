using System;
using System.Linq;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class QuestVisualAndUiTuneup
{
    private const string ScenePath = "Assets/Scenes/v203.0.0.unity";
    private const string MaterialFolder = "Assets/Materials/QuestUI";

    [MenuItem("Tools/MR-VD/Apply Quest Visual and UI Tuneup")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || EditorApplication.isPlaying)
            throw new InvalidOperationException("Open v203.0.0 in Edit Mode before applying the Quest tuneup.");

        EnsureFolder("Assets/Materials");
        EnsureFolder(MaterialFolder);
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) throw new MissingReferenceException("URP Unlit shader is unavailable.");

        Material hand = CreateOrUpdateMaterial(MaterialFolder + "/VirtualHandOpaque.mat", unlit,
            null, new Color(0.68f, 0.83f, 0.91f, 1f));
        Material left = null;
        Material right = null;
        foreach (SkinnedMeshRenderer renderer in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<SkinnedMeshRenderer>(true)))
        {
            if (renderer.name.StartsWith("[BuildingBlock] Hand Tracking", StringComparison.Ordinal))
            {
                SetMaterial(renderer, hand);
                continue;
            }

            bool isLeft = renderer.name == "oculus_controller_l_MeshX";
            bool isRight = renderer.name == "oculus_controller_r_MeshX";
            if (!isLeft && !isRight) continue;
            Material source = renderer.sharedMaterial;
            Texture texture = source != null && source.HasProperty("_BaseMap")
                ? source.GetTexture("_BaseMap") : null;
            if (texture == null) continue;
            if (isLeft)
                left ??= CreateOrUpdateMaterial(MaterialFolder + "/Quest3ControllerLeft.mat", unlit, texture, Color.white);
            else
                right ??= CreateOrUpdateMaterial(MaterialFolder + "/Quest3ControllerRight.mat", unlit, texture, Color.white);
            SetMaterial(renderer, isLeft ? left : right);
        }

        GameObject screen = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Screen");
        if (screen == null) throw new MissingReferenceException("Screen is missing.");
        ConfigurePlacementOutline(screen, unlit);
        foreach (RayInteractorCursorVisual cursor in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<RayInteractorCursorVisual>(true)))
            if (cursor.GetComponent<RayCursorSmoother>() == null)
                cursor.gameObject.AddComponent<RayCursorSmoother>();
        foreach (RayInteractorRayVisual beam in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<RayInteractorRayVisual>(true)))
            if (beam.GetComponent<RayCursorSmoother>() == null)
                beam.gameObject.AddComponent<RayCursorSmoother>();
        ConfigureSettings(screen.transform);
        ConfigureAudio(screen.transform);
        Transform hint = screen.transform.Find("Canvas/ScreenInteractionHint");
        if (hint != null) UnityEngine.Object.DestroyImmediate(hint.gameObject);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[QuestVisualAndUiTuneup] Saved hand/controller materials, placement outline, settings hit areas and audio tuning.");
    }

    private static void ConfigurePlacementOutline(GameObject screen, Shader unlit)
    {
        Transform plane = screen.transform.Find("Plane");
        if (plane == null) throw new MissingReferenceException("Screen/Plane is missing.");
        Transform existing = screen.transform.Find("PlacementOutline");
        GameObject outlineObject = existing != null ? existing.gameObject : new GameObject("PlacementOutline");
        outlineObject.transform.SetParent(screen.transform, false);
        LineRenderer outline = outlineObject.GetComponent<LineRenderer>();
        if (outline == null) outline = outlineObject.AddComponent<LineRenderer>();
        Material material = CreateOrUpdateMaterial(MaterialFolder + "/PlacementOutline.mat", unlit,
            null, new Color(0.25f, 0.9f, 1f, 1f));
        outline.sharedMaterial = material;
        outline.useWorldSpace = false;
        outline.loop = true;
        outline.positionCount = 4;
        outline.widthMultiplier = 0.006f;
        outline.numCornerVertices = 4;
        outline.numCapVertices = 4;
        outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outline.receiveShadows = false;
        Mesh mesh = plane.GetComponent<MeshFilter>().sharedMesh;
        Vector3 min = mesh.bounds.min;
        Vector3 max = mesh.bounds.max;
        Vector3[] points =
        {
            new Vector3(min.x, 0.06f, min.z), new Vector3(max.x, 0.06f, min.z),
            new Vector3(max.x, 0.06f, max.z), new Vector3(min.x, 0.06f, max.z)
        };
        for (int i = 0; i < points.Length; i++)
            outline.SetPosition(i, screen.transform.InverseTransformPoint(plane.TransformPoint(points[i])));
        outline.enabled = false;
        ScreenRayManipulator manipulator = screen.GetComponent<ScreenRayManipulator>();
        SerializedObject serialized = new SerializedObject(manipulator);
        serialized.FindProperty("placementOutline").objectReferenceValue = outline;
        serialized.FindProperty("interactionHint").objectReferenceValue = null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureSettings(Transform screen)
    {
        Transform settings = screen.Find("Canvas/InfoPanel/RuntimeDashboard/SettingsModule");
        Transform body = settings?.Find("SettingsBody");
        if (body == null) throw new MissingReferenceException("SettingsBody is missing.");
        foreach (RectTransform row in body.Cast<Transform>().OfType<RectTransform>())
        {
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            rowLayout.minHeight = 28f;
            rowLayout.preferredHeight = 28f;
            foreach (Toggle toggle in row.GetComponentsInChildren<Toggle>(true))
            {
                LayoutElement hitArea = toggle.GetComponent<LayoutElement>();
                if (hitArea != null)
                {
                    hitArea.minHeight = 28f;
                    hitArea.preferredHeight = 28f;
                }
                RectTransform rect = (RectTransform)toggle.transform;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 28f);
            }
        }
        LayoutElement bodyLayout = body.GetComponent<LayoutElement>();
        bodyLayout.minHeight = 132f;
        bodyLayout.preferredHeight = 132f;
        ((RectTransform)body).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 132f);
        SerializedObject animator = new SerializedObject(settings.GetComponent<ScreenCanvasModuleAnimator>());
        animator.FindProperty("expandedHeight").floatValue = 172f;
        animator.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureAudio(Transform screen)
    {
        AudioCaptureCSCore capture = screen.GetComponentInChildren<AudioCaptureCSCore>(true);
        AudioVisualizer visualizer = screen.GetComponentInChildren<AudioVisualizer>(true);
        if (capture == null || visualizer == null) throw new MissingReferenceException("Audio components are missing.");
        capture.screenCanvasRefreshInterval = 0.05f;
        capture.screenCompactPrimaryFontSize = 24;
        visualizer.keyUpdateInterval = 0.05f;
        EditorUtility.SetDirty(capture);
        EditorUtility.SetDirty(visualizer);
        Text status = screen.Find("Canvas/AudioPanel/AudioCaptureCanvasContent/AudioStatusModule/AudioStatusText")?.GetComponent<Text>();
        if (status != null) { status.fontSize = 24; EditorUtility.SetDirty(status); }
    }

    private static void SetMaterial(Renderer renderer, Material material)
    {
        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(renderer);
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
    }

    private static Material CreateOrUpdateMaterial(string path, Shader shader, Texture texture, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        material.SetColor("_BaseColor", color);
        material.SetTexture("_BaseMap", texture);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
