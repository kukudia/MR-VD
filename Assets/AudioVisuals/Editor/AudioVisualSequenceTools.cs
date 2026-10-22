using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Creates a native five-look playlist and opt-in showcase player; validates real Play-mode controls.</summary>
public static class AudioVisualSequenceTools
{
    public const string AssetPath = AudioVisualShowcaseTools.LookRoot + "/Five Look Journey.asset";

    [MenuItem("Tools/MR-VD/Spatial Audio/Build Missing Editable Look Sequence")]
    public static void BuildMissing()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Build outside Play Mode.");
        }
        if (AssetDatabase.LoadAssetAtPath<AudioVisualSequence>(AssetPath) == null)
        {
            AudioVisualSequence sequence = ScriptableObject.CreateInstance<AudioVisualSequence>();
            SerializedObject data = new SerializedObject(sequence);
            SerializedProperty cues = data.FindProperty("cues");
            string[] names = { "Aurora Cathedral", "Jade Orbit", "Opal Iris", "Violet Knot", "Solar Percussion" };
            cues.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                SerializedProperty cue = cues.GetArrayElementAtIndex(i);
                cue.FindPropertyRelative("look").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioVisualLook>(
                    AudioVisualShowcaseTools.LookRoot + "/" + names[i] + ".asset");
                cue.FindPropertyRelative("duration").floatValue = i == 2 ? 64f : 32f;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(sequence, AssetPath);
        }
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(AudioVisualShowcaseTools.ScenePath);
        bool opened = !scene.isLoaded;
        if (opened)
        {
            scene = EditorSceneManager.OpenScene(AudioVisualShowcaseTools.ScenePath, OpenSceneMode.Additive);
        }
        try
        {
            if (scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<AudioVisualSequencePlayer>()).Any())
            {
                return;
            }
            AudioVisualLookPlayer target = scene.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<AudioVisualLookPlayer>()).Single();
            AudioVisualSequencePlayer player = target.gameObject.AddComponent<AudioVisualSequencePlayer>();
            SerializedObject data = new SerializedObject(player);
            data.FindProperty("target").objectReferenceValue = target;
            data.FindProperty("sequence").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioVisualSequence>(AssetPath);
            data.FindProperty("clockSource").objectReferenceValue = scene.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<AudioVisualField>()).Single();
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            SceneManager.SetActiveScene(previous);
            AssetDatabase.SaveAssets();
        }
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Validate Running Look Sequence")]
    public static void ValidateRunning()
    {
        if (!EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Requires Play Mode; uses an isolated controller without a render field.");
        }
        GameObject host = new GameObject("Isolated sequence validation");
        AudioVisualSequence sequence = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AudioVisualSequence>(AssetPath));
        try
        {
            AudioVisualLookPlayer target = host.AddComponent<AudioVisualLookPlayer>();
            AudioVisualLook original = AssetDatabase.LoadAssetAtPath<AudioVisualLook>(
                AudioVisualShowcaseTools.LookRoot + "/Solar Percussion.asset");
            target.SetLook(original);
            AudioVisualSequencePlayer player = host.AddComponent<AudioVisualSequencePlayer>();
            SerializedObject data = new SerializedObject(player);
            data.FindProperty("target").objectReferenceValue = target;
            data.FindProperty("sequence").objectReferenceValue = sequence;
            data.ApplyModifiedPropertiesWithoutUndo();
            string before = JsonUtility.ToJson(sequence);
            player.PlaySequence();
            Require(player.IsRunning && player.CueIndex == 0, "Sequence did not start.");
            player.Advance(float.NaN, 120f);
            player.Advance(1f, float.NaN);
            Require(player.Elapsed == 0f, "Invalid clock changed elapsed time.");
            for (int i = 0; i < 16; i++)
            {
                player.Advance(1f, 120f);
            }
            Require(player.CueIndex == 1 && player.Elapsed < 0.001f, "Display-beat boundary failed.");
            player.PauseSequence();
            player.Advance(1f, 120f);
            Require(player.CueIndex == 1 && player.Elapsed == 0f, "Paused clock advanced.");
            player.PlaySequence();
            player.NextCue();
            Require(player.CueIndex == 2 && target.Look == sequence.GetCue(2).look, "Manual next failed.");
            player.StopSequence();
            Require(!player.IsRunning && player.CueIndex == -1 && target.Look == original, "Stop did not restore original look.");
            player.PlaySequence();
            player.enabled = false;
            Require(target.Look == original && !player.IsRunning, "Disable did not restore look.");
            Require(before == JsonUtility.ToJson(sequence), "Playback mutated sequence asset.");
            player.enabled = true;
            SerializedObject sequenceData = new SerializedObject(sequence);
            sequenceData.FindProperty("clock").enumValueIndex = (int)AudioVisualSequence.Clock.Seconds;
            sequenceData.FindProperty("loop").boolValue = false;
            SerializedProperty cues = sequenceData.FindProperty("cues");
            cues.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                cues.GetArrayElementAtIndex(i).FindPropertyRelative("duration").floatValue = 1f;
            }
            sequenceData.ApplyModifiedPropertiesWithoutUndo();
            player.PlaySequence();
            player.Advance(1f, 60f);
            Require(player.IsRunning && player.CueIndex == 1, "Seconds clock failed.");
            player.Advance(1f, 60f);
            Require(!player.IsRunning && target.Look == sequence.GetCue(1).look, "Non-loop end did not hold final look.");
            player.PlaySequence();
            Require(player.IsRunning && player.CueIndex == 0, "Completed sequence did not restart.");
            sequenceData.Update();
            sequenceData.FindProperty("loop").boolValue = true;
            sequenceData.ApplyModifiedPropertiesWithoutUndo();
            player.Advance(1f, 60f);
            player.Advance(1f, 60f);
            Require(player.IsRunning && player.CueIndex == 0, "Loop did not wrap.");
            player.NextCue();
            sequenceData.Update();
            sequenceData.FindProperty("cues").arraySize = 1;
            sequenceData.ApplyModifiedPropertiesWithoutUndo();
            player.Advance(0.1f, 60f);
            Require(player.CueIndex == 0, "Live sequence shortening left an invalid cue.");
            player.StopSequence();
            Require(target.Look == original, "Edited sequence did not restore original look.");
            Debug.Log("[AudioSequence] PASS: explicit start, invalid clock, beats/seconds, pause/resume, next, stop/disable restore, "
                + "asset immutability, non-loop completion/restart, loop wrap and live shortening.");
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(sequence);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
