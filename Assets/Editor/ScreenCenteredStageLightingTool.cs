using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ScreenCenteredStageLightingTool
{
    private const string MenuPath = "Tools/MR-VD/Rebuild Screen-Centered Stage Lighting";

    [MenuItem(MenuPath)]
    public static void RebuildActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject screen = GameObject.Find("Screen");
        GameObject stage = GameObject.Find("VirtualStage");
        if (screen == null || stage == null)
        {
            Debug.LogError("[ScreenCenteredStageLighting] Screen or VirtualStage was not found.");
            return;
        }

        StageManager manager = stage.GetComponent<StageManager>();
        Transform lightingRoot = stage.transform.Find("LightingSystem");
        if (manager == null || lightingRoot == null)
        {
            Debug.LogError("[ScreenCenteredStageLighting] StageManager or LightingSystem was not found.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(stage, "Rebuild Screen-Centered Stage Lighting");
        ClearLightGroups(lightingRoot);
        lightingRoot.SetPositionAndRotation(screen.transform.position, screen.transform.rotation);
        lightingRoot.localScale = Vector3.one;

        Transform spotGroup = CreateGroup(lightingRoot, "Spotlights");
        Transform rimGroup = CreateGroup(lightingRoot, "RimLights");
        Transform chaseGroup = CreateGroup(lightingRoot, "ChaseLights");
        Transform laserGroup = CreateGroup(lightingRoot, "LaserLights");
        Transform strobeGroup = CreateGroup(lightingRoot, "StrobeLights");

        List<Light> spots = new List<Light>();
        for (int i = 0; i < 6; i++)
        {
            float x = Mathf.Lerp(-1.05f, 1.05f, i / 5f);
            float y = i % 2 == 0 ? 0.82f : 0.94f;
            Light light = CreateSpot(spotGroup, $"Spotlight_{i}", new Vector3(x, y, -0.52f), 2.8f, 32f, 2.4f, true);
            ConfigureBeam(light, 0.18f, 0.78f, 0.72f);
            spots.Add(light);
        }

        Vector3[] rimPositions =
        {
            new Vector3(-1.15f, 0.62f, 0.02f),
            new Vector3(1.15f, 0.62f, 0.02f),
            new Vector3(-1.15f, -0.62f, 0.02f),
            new Vector3(1.15f, -0.62f, 0.02f)
        };
        List<Light> rims = new List<Light>();
        for (int i = 0; i < rimPositions.Length; i++)
        {
            Light light = CreateLight(rimGroup, $"RimLight_{i}", rimPositions[i], LightType.Point, 1.6f, 0f, 1.25f);
            rims.Add(light);
        }

        Vector3[] chasePositions =
        {
            new Vector3(-1.18f, 0.42f, -0.36f),
            new Vector3(1.18f, 0.42f, -0.36f),
            new Vector3(-1.18f, -0.42f, -0.36f),
            new Vector3(1.18f, -0.42f, -0.36f)
        };
        List<Light> chases = new List<Light>();
        for (int i = 0; i < chasePositions.Length; i++)
        {
            Light light = CreateSpot(chaseGroup, $"ChaseLight_{i}", chasePositions[i], 3.1f, 24f, 2f, false);
            ConfigureBeam(light, 0.16f, 0.85f, 0.58f);
            chases.Add(light);
        }

        List<Light> lasers = new List<Light>();
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -1.28f : 1.28f;
            Light light = CreateSpot(laserGroup, $"LaserLight_{i}", new Vector3(x, 0.72f, -0.68f), 3.6f, 7f, 2.8f, false);
            light.color = new Color(0.2f, 0.8f, 1f);
            light.enabled = false;
            ConfigureBeam(light, 0.32f, 0.95f, 0.32f);
            lasers.Add(light);
        }

        List<Light> strobes = new List<Light>();
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -0.78f : 0.78f;
            Light light = CreateSpot(strobeGroup, $"StrobeLight_{i}", new Vector3(x, 0.98f, -0.3f), 2.7f, 48f, 3.5f, false);
            light.enabled = false;
            ConfigureBeam(light, 0.12f, 0.72f, 0.9f);
            strobes.Add(light);
        }

        manager.lightingFocusTarget = screen.transform;
        manager.lightingRigRoot = lightingRoot;
        manager.spotlights = spots.ToArray();
        manager.rimLights = rims.ToArray();
        manager.chaseLights = chases.ToArray();
        manager.laserLights = lasers.ToArray();
        manager.strobeLights = strobes.ToArray();
        manager.lighting.followFocusTarget = true;
        manager.lighting.focusWidth = 2.4f;
        manager.lighting.focusHeight = 1.35f;
        manager.lighting.focusDepth = 1.2f;
        manager.lighting.focusFollowSpeed = 10f;
        manager.lighting.chaseOrbitRadius = 1.05f;
        manager.lighting.chaseOrbitHeight = 0.55f;
        manager.lighting.minSpotAngle = 16f;
        manager.lighting.maxSpotAngle = 38f;
        manager.lighting.transformSmoothing = 7f;
        manager.lighting.visibleBeamOpacity = 0.18f;
        manager.lighting.visibleBeamLengthScale = 0.82f;
        manager.lighting.visibleBeamRadiusScale = 0.7f;

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = stage;
        Debug.Log("[ScreenCenteredStageLighting] Rebuilt 18 lights around Screen: 6 spot, 4 rim, 4 chase, 2 laser, 2 strobe.");
    }

    private static void ClearLightGroups(Transform lightingRoot)
    {
        string[] names = { "Spotlights", "RimLights", "ChaseLights", "LaserLights", "StrobeLights" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform child = lightingRoot.Find(names[i]);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static Transform CreateGroup(Transform parent, string name)
    {
        GameObject group = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(group, "Create Screen-Centered Light Group");
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private static Light CreateSpot(Transform parent, string name, Vector3 localPosition, float range, float spotAngle, float intensity, bool softShadows)
    {
        Light light = CreateLight(parent, name, localPosition, LightType.Spot, range, spotAngle, intensity);
        light.shadows = softShadows ? LightShadows.Soft : LightShadows.None;
        AimAtFocus(light.transform);
        return light;
    }

    private static Light CreateLight(Transform parent, string name, Vector3 localPosition, LightType type, float range, float spotAngle, float intensity)
    {
        GameObject lightObject = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(lightObject, "Create Screen-Centered Light");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = localPosition;
        Light light = lightObject.AddComponent<Light>();
        light.type = type;
        light.range = range;
        light.spotAngle = spotAngle;
        light.intensity = intensity;
        light.color = Color.white;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        return light;
    }

    private static void AimAtFocus(Transform lightTransform)
    {
        Vector3 direction = lightTransform.parent.TransformPoint(Vector3.zero) - lightTransform.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            lightTransform.rotation = Quaternion.LookRotation(direction.normalized, lightTransform.parent.up);
        }
    }

    private static void ConfigureBeam(Light light, float opacity, float lengthScale, float radiusScale)
    {
        StageVisibleLightBeam beam = light.gameObject.AddComponent<StageVisibleLightBeam>();
        beam.Configure(opacity, lengthScale, radiusScale, 4f);
        beam.sideSegments = 16;
        beam.lengthSegments = 4;
        beam.minVisibleIntensity = 0.02f;
    }
}
