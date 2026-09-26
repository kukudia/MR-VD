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
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null || !lit.isSupported)
            throw new MissingReferenceException("URP Lit shader is unavailable.");

        Material hand = CreateOrUpdateMaterial(MaterialFolder + "/VirtualHandOpaque.mat", lit,
            null, new Color(0.68f, 0.83f, 0.91f, 1f), new Color(0.18f, 0.25f, 0.29f));
        Material left = null;
        Material right = null;
        foreach (SkinnedMeshRenderer renderer in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<SkinnedMeshRenderer>(true)))
        {
            if (renderer.name.StartsWith("[BuildingBlock] Hand Tracking", StringComparison.Ordinal))
            {
                SetMaterial(renderer, hand);
                OVRMeshRenderer handMesh = renderer.GetComponent<OVRMeshRenderer>();
                if (handMesh != null)
                {
                    SerializedObject meshSettings = new SerializedObject(handMesh);
                    meshSettings.FindProperty("_systemGestureMaterial").objectReferenceValue = hand;
                    meshSettings.ApplyModifiedPropertiesWithoutUndo();
                }
                continue;
            }

            Material current = renderer.sharedMaterial;
            if (current != null && current.shader != null && current.shader.name == "Interaction/OculusHand")
            {
                SetMaterial(renderer, hand);
                continue;
            }

            bool isLeft = renderer.name == "oculus_controller_l_MeshX";
            bool isRight = renderer.name == "oculus_controller_r_MeshX";
            if (!isLeft && !isRight) continue;
            Material source = current;
            Texture texture = source != null && source.HasProperty("_BaseMap")
                ? source.GetTexture("_BaseMap") : null;
            if (texture == null) continue;
            if (isLeft)
                left ??= CreateOrUpdateMaterial(MaterialFolder + "/Quest3ControllerLeft.mat", lit, texture,
                    Color.white, new Color(0.32f, 0.32f, 0.32f));
            else
                right ??= CreateOrUpdateMaterial(MaterialFolder + "/Quest3ControllerRight.mat", lit, texture,
                    Color.white, new Color(0.32f, 0.32f, 0.32f));
            SetMaterial(renderer, isLeft ? left : right);
        }

        GameObject screen = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Screen");
        if (screen == null) throw new MissingReferenceException("Screen is missing.");
        ConfigurePlacementOutline(screen);
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

    private static void ConfigurePlacementOutline(GameObject screen)
    {
        RectTransform canvas = screen.transform.Find("Canvas") as RectTransform;
        if (canvas == null) throw new MissingReferenceException("Screen/Canvas is missing.");
        Transform oldOutline = screen.transform.Find("PlacementOutline");
        if (oldOutline != null) UnityEngine.Object.DestroyImmediate(oldOutline.gameObject);
        Transform oldCanvasOutline = canvas.Find("PlacementOutline");
        if (oldCanvasOutline != null) UnityEngine.Object.DestroyImmediate(oldCanvasOutline.gameObject);

        // The ray hit area is taller than the visible desktop. Frame the actual panels.
        RectTransform[] visiblePanels =
        {
            canvas.Find("RawImage") as RectTransform,
            canvas.Find("AudioPanel") as RectTransform,
            canvas.Find("InfoPanel") as RectTransform
        };
        if (visiblePanels.Any(panel => panel == null))
            throw new MissingReferenceException("A visible Screen panel is missing.");
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        Vector3[] corners = new Vector3[4];
        foreach (RectTransform panel in visiblePanels)
        {
            panel.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 point = canvas.InverseTransformPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }
        const float margin = 0.012f;
        min -= Vector2.one * margin;
        max += Vector2.one * margin;

        GameObject outlineObject = new GameObject("PlacementOutline", typeof(RectTransform));
        RectTransform outline = (RectTransform)outlineObject.transform;
        outline.SetParent(canvas, false);
        outline.SetAsLastSibling();
        outline.anchorMin = outline.anchorMax = outline.pivot = new Vector2(0.5f, 0.5f);
        outline.anchoredPosition3D = new Vector3((min.x + max.x) * 0.5f,
            (min.y + max.y) * 0.5f, -0.01f);
        outline.sizeDelta = max - min;
        const float thickness = 0.006f;
        Color color = new Color(0.25f, 0.9f, 1f, 1f);
        CreateBorder("Top", outline, new Vector2(0.5f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, thickness * 0.5f),
            new Vector2(outline.sizeDelta.x, thickness), color);
        CreateBorder("Bottom", outline, new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -thickness * 0.5f),
            new Vector2(outline.sizeDelta.x, thickness), color);
        CreateBorder("Left", outline, new Vector2(0f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(-thickness * 0.5f, 0f),
            new Vector2(thickness, outline.sizeDelta.y), color);
        CreateBorder("Right", outline, new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(thickness * 0.5f, 0f),
            new Vector2(thickness, outline.sizeDelta.y), color);
        outlineObject.SetActive(false);
        ScreenRayManipulator manipulator = screen.GetComponent<ScreenRayManipulator>();
        SerializedObject serialized = new SerializedObject(manipulator);
        serialized.FindProperty("placementOutline").objectReferenceValue = outlineObject;
        serialized.FindProperty("interactionHint").objectReferenceValue = null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateBorder(string name, RectTransform parent, Vector2 anchor,
        Vector2 pivot, Vector2 position, Vector2 size, Color color)
    {
        GameObject edge = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)edge.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = edge.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
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

    private static Material CreateOrUpdateMaterial(string path, Shader shader, Texture texture,
        Color color, Color emission)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        material.SetColor("_BaseColor", color);
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_EmissionColor", emission);
        material.EnableKeyword("_EMISSION");
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
