using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StageVisibleLightBeamTools
{
    [MenuItem("Tools/Stage Lighting/Apply Visible Beams To Open Scenes")]
    public static void ApplyVisibleBeamsToOpenScenes()
    {
        int changed = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            bool sceneChanged = false;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Light[] lights = roots[rootIndex].GetComponentsInChildren<Light>(true);
                for (int lightIndex = 0; lightIndex < lights.Length; lightIndex++)
                {
                    Light light = lights[lightIndex];
                    if (light == null || light.type != LightType.Spot)
                    {
                        continue;
                    }

                    if (light.GetComponent<StageVisibleLightBeam>() == null)
                    {
                        Undo.AddComponent<StageVisibleLightBeam>(light.gameObject);
                        changed++;
                        sceneChanged = true;
                    }
                }
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        Debug.Log($"[StageVisibleLightBeamTools] Added visible beams to {changed} spot light(s).");
    }

    [MenuItem("Tools/Stage Lighting/Remove Visible Beams From Open Scenes")]
    public static void RemoveVisibleBeamsFromOpenScenes()
    {
        int changed = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            bool sceneChanged = false;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                StageVisibleLightBeam[] beams = roots[rootIndex].GetComponentsInChildren<StageVisibleLightBeam>(true);
                for (int beamIndex = 0; beamIndex < beams.Length; beamIndex++)
                {
                    if (beams[beamIndex] == null)
                    {
                        continue;
                    }

                    Undo.DestroyObjectImmediate(beams[beamIndex]);
                    changed++;
                    sceneChanged = true;
                }
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        Debug.Log($"[StageVisibleLightBeamTools] Removed {changed} visible beam component(s).");
    }
}
