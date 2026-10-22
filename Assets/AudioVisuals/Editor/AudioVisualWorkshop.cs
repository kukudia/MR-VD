using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Authoring entrypoint for spatial visual controls, explicit synthetic previews and saved mesh
/// snapshots. Operates on the selected field; baking creates new native assets and never replaces
/// the procedural source or its theme. Generated captures stay outside Assets.
/// </summary>
public sealed class AudioVisualWorkshop : EditorWindow
{
    private AudioVisualField field;
    private GpuAudioParticleVisualizer particles;
    private AudioVisualLook look;
    private AudioVisualSequencePlayer sequencePlayer;
    private AudioVisualComplexity complexity;
    private Vector2 scroll;
    private float previewTime = 5f;
    private string notice;

    [MenuItem("Tools/MR-VD/Spatial Audio/Open Visual Workshop")]
    public static void Open()
    {
        GetWindow<AudioVisualWorkshop>("Audio Visual Workshop");
    }

    private void OnSelectionChange()
    {
        if (Selection.activeGameObject != null)
        {
            AudioVisualField selected = Selection.activeGameObject.GetComponentInParent<AudioVisualField>();
            if (selected != null)
            {
                field = selected;
            }
        }
        Repaint();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Spatial Audio Workshop", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Themes and mesh parameters remain editable assets. Play controls are temporary; "
            + "synthetic input is only a visual demonstration.", MessageType.Info);
        field = (AudioVisualField)EditorGUILayout.ObjectField("Visual field", field, typeof(AudioVisualField), true);
        particles = (GpuAudioParticleVisualizer)EditorGUILayout.ObjectField("GPU particles", particles,
            typeof(GpuAudioParticleVisualizer), true);
        look = (AudioVisualLook)EditorGUILayout.ObjectField("Authored look", look, typeof(AudioVisualLook), false);
        sequencePlayer = (AudioVisualSequencePlayer)EditorGUILayout.ObjectField("Sequence player", sequencePlayer,
            typeof(AudioVisualSequencePlayer), true);
        if (sequencePlayer != null && EditorApplication.isPlaying)
        {
            EditorGUILayout.LabelField("Sequence cue / clock", $"{sequencePlayer.CueIndex + 1} / {sequencePlayer.Elapsed:F1}");
            if (GUILayout.Button(sequencePlayer.IsRunning ? "Pause Sequence" : "Play / Resume Sequence"))
            {
                if (sequencePlayer.IsRunning)
                {
                    sequencePlayer.PauseSequence();
                }
                else
                {
                    sequencePlayer.PlaySequence();
                }
            }
            if (GUILayout.Button("Next Sequence Cue"))
            {
                sequencePlayer.NextCue();
            }
            if (GUILayout.Button("Stop Sequence / Restore Starting Look"))
            {
                sequencePlayer.StopSequence();
            }
        }
        if (look != null && field != null && GUILayout.Button("Apply Authored Look"))
        {
            if (sequencePlayer != null && sequencePlayer.IsRunning)
            {
                sequencePlayer.PauseSequence();
            }
            Undo.RecordObject(field, "Apply Audio Visual Look");
            if (particles != null)
            {
                Undo.RecordObject(particles, "Apply Audio Particle Look");
            }
            look.Apply(field, particles);
            EditorUtility.SetDirty(field);
            PrefabUtility.RecordPrefabInstancePropertyModifications(field);
            if (particles != null)
            {
                EditorUtility.SetDirty(particles);
                PrefabUtility.RecordPrefabInstancePropertyModifications(particles);
            }
        }
        if (GUILayout.Button("Find in Active Scene"))
        {
            field = Object.FindFirstObjectByType<AudioVisualField>();
            particles = Object.FindFirstObjectByType<GpuAudioParticleVisualizer>();
            sequencePlayer = Object.FindFirstObjectByType<AudioVisualSequencePlayer>();
        }
        if (field != null)
        {
            DrawField();
            complexity = (AudioVisualComplexity)EditorGUILayout.ObjectField("Complexity profile", complexity,
                typeof(AudioVisualComplexity), false);
            if (complexity != null)
            {
                EditorGUILayout.LabelField("Maximum mesh vertices", complexity.MaximumMeshVertices.ToString("N0"));
                EditorGUILayout.LabelField("Particle state buffer", $"{complexity.ParticleStateBytes / 1024f:F0} KiB");
                EditorGUILayout.HelpBox("Counts are geometry budgets, not measured GPU time. Apply changes counts only; Undo restores them.",
                    MessageType.None);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    if (GUILayout.Button("Apply Complexity Profile to This Field"))
                    {
                        AudioVisualComplexityTools.Apply(complexity, field, particles);
                    }
                    if (GUILayout.Button("Preview Mesh Themes at This Complexity"))
                    {
                        AudioVisualFieldTools.RenderThemeSheet(complexity);
                        notice = "Mesh-only complexity contact sheet saved in project Temp/AudioVisualResearch.";
                    }
                }
            }
        }
        if (particles != null)
        {
            GpuAudioParticleVisualizer.ParticleFormation next = (GpuAudioParticleVisualizer.ParticleFormation)
                EditorGUILayout.EnumPopup("GPU formation", particles.Formation);
            if (next != particles.Formation)
            {
                Undo.RecordObject(particles, "Change Particle Formation");
                particles.SetFormation(next);
                EditorUtility.SetDirty(particles);
            }
        }
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Build Missing Assets"))
            {
                AudioVisualFieldTools.BuildAssets();
            }
            if (GUILayout.Button("Build Missing Cinematic Showcase"))
            {
                AudioVisualShowcaseTools.BuildMissing();
            }
            if (GUILayout.Button("Build Missing Editable Look Sequence"))
            {
                AudioVisualSequenceTools.BuildMissing();
            }
            if (GUILayout.Button("Build Missing Complexity Presets"))
            {
                AudioVisualComplexityTools.BuildMissing();
            }
            if (GUILayout.Button("Upgrade Spectral Lattice in Prefab"))
            {
                AudioVisualFieldTools.UpgradeLattice();
            }
            if (GUILayout.Button("Upgrade Spectral Iris in Prefab"))
            {
                AudioVisualFieldTools.UpgradeIris();
            }
            if (GUILayout.Button("Upgrade Canopy in Prefab and Laboratory"))
            {
                AudioVisualFieldTools.UpgradeCanopy();
            }
            if (GUILayout.Button("Install / Repair Active Audio Scene"))
            {
                AudioVisualFieldTools.InstallActiveScene();
            }
            if (GUILayout.Button("Render Four Theme Previews"))
            {
                AudioVisualFieldTools.RenderContactSheet();
                notice = "Theme sheet saved in project Temp/AudioVisualResearch.";
            }
        }
        if (EditorApplication.isPlaying && GUILayout.Button("Capture Game View to Project Temp"))
        {
            notice = CaptureGameView();
        }
        if (!string.IsNullOrEmpty(notice))
        {
            EditorGUILayout.HelpBox(notice, MessageType.Info);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawField()
    {
        SerializedObject data = new SerializedObject(field);
        data.Update();
        EditorGUILayout.PropertyField(data.FindProperty("theme"));
        EditorGUILayout.PropertyField(data.FindProperty("composition"));
        EditorGUILayout.PropertyField(data.FindProperty("intensity"));
        EditorGUILayout.PropertyField(data.FindProperty("transitionSeconds"));
        data.ApplyModifiedProperties();
        if (field.UsesDemonstration)
        {
            EditorGUILayout.HelpBox("SYNTHETIC DEMONSTRATION — not live audio analysis.", MessageType.Warning);
            EditorGUILayout.PropertyField(data.FindProperty("demonstrationBpm"));
            EditorGUILayout.PropertyField(data.FindProperty("demonstrationEnergy"));
            data.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                previewTime = EditorGUILayout.Slider("Preview time (seconds)", previewTime, 0f, 60f);
                if (GUILayout.Button("Render Synthetic Preview at Time"))
                {
                    for (int i = 0; i < 90; i++)
                    {
                        field.PreviewDemonstration(Mathf.Max(0f, previewTime - 3f) + i / 30f, 1f / 30f);
                    }
                    SceneView.RepaintAll();
                }
            }
        }
        AudioVisualField.Frame frame = field.CurrentFrame;
        EditorGUILayout.LabelField("Bass / Detail / Energy", $"{frame.bass:F2} / {frame.detail:F2} / {frame.energy:F2}");
        EditorGUILayout.LabelField("Shape: center / spread / flat / rise", frame.spectralShape.ToString("F2"));
        EditorGUILayout.LabelField("Median: sustain / broad / contrast / warm", frame.spectralLayers.ToString("F2"));
        if (GUILayout.Button("Bake Current Meshes into New Editable Prefab"))
        {
            notice = BakeSnapshot(field);
        }
        EditorGUILayout.HelpBox("A snapshot freezes geometry and shader time. Its reading-screen mask is disabled; "
            + "the procedural field retains the live camera/screen protection.", MessageType.None);
    }

    public static string CaptureGameView()
    {
        if (!EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Game view capture requires Play Mode.");
        }
        System.IO.Directory.CreateDirectory(AudioVisualFieldTools.PreviewDirectory);
        string path = System.IO.Path.GetFullPath(AudioVisualFieldTools.PreviewDirectory + "/play-"
            + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
        // The project also defines a ScreenCapture component; qualify the engine capture API.
        UnityEngine.ScreenCapture.CaptureScreenshot(path);
        return path;
    }

    [MenuItem("Tools/MR-VD/Spatial Audio/Capture Desktop Camera without Debug Overlay")]
    public static string CaptureDesktopCamera()
    {
        Camera camera = Camera.main;
        if (!EditorApplication.isPlaying || camera == null || camera.stereoEnabled)
        {
            throw new InvalidOperationException("Requires a running desktop main camera; headset capture is separate.");
        }
        RenderTexture output = RenderTexture.GetTemporary(1600, 1000, 24, RenderTextureFormat.ARGB32);
        Texture2D pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest
            {
                destination = output
            };
            if (!UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, request))
            {
                throw new InvalidOperationException("Current pipeline does not support single camera requests.");
            }
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = output;
            pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
            pixels.Apply();
            System.IO.Directory.CreateDirectory(AudioVisualFieldTools.PreviewDirectory);
            string path = System.IO.Path.GetFullPath(AudioVisualFieldTools.PreviewDirectory + "/camera-"
                + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
            System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
            return path;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(output);
            Object.DestroyImmediate(pixels);
        }
    }

    public static string BakeSnapshot(AudioVisualField source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        AudioVisualMesh[] effects = source.GetComponentsInChildren<AudioVisualMesh>(true);
        bool hasGeometry = false;
        foreach (AudioVisualMesh effect in effects)
        {
            hasGeometry |= effect.GetComponent<MeshFilter>().sharedMesh != null;
        }
        if (!hasGeometry)
        {
            throw new InvalidOperationException("Render a synthetic preview or enter Play Mode before baking.");
        }
        string parent = AudioVisualFieldTools.Root + "/Snapshots";
        if (!AssetDatabase.IsValidFolder(parent))
        {
            AssetDatabase.CreateFolder(AudioVisualFieldTools.Root, "Snapshots");
        }
        string folder = AssetDatabase.GenerateUniqueAssetPath(parent + "/Snapshot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        GameObject root = new GameObject("Spatial Audio Snapshot");
        try
        {
            SnapshotManifest manifest = new SnapshotManifest
            {
                sourceScene = source.gameObject.scene.path,
                sourceTheme = AssetDatabase.GetAssetPath(source.Theme),
                themeSettings = JsonUtility.ToJson(source.DisplayedTheme, true),
                syntheticInput = source.UsesDemonstration,
                visualTime = source.CurrentFrame.time,
                effects = new SnapshotEffect[effects.Length]
            };
            for (int i = 0; i < effects.Length; i++)
            {
                manifest.effects[i] = new SnapshotEffect
                {
                    type = effects[i].GetType().FullName,
                    parameters = JsonUtility.ToJson(effects[i], true)
                };
            }
            root.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            foreach (AudioVisualMesh effect in effects)
            {
                Mesh mesh = effect.GetComponent<MeshFilter>().sharedMesh;
                if (mesh == null)
                {
                    continue;
                }
                string label = effect.GetType().Name;
                Mesh copy = Object.Instantiate(mesh);
                copy.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(copy, folder + "/" + label + ".asset");
                Material material = new Material(effect.GetComponent<MeshRenderer>().sharedMaterial);
                material.hideFlags = HideFlags.None;
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                effect.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                foreach (string property in new[] { "_Emission", "_Opacity", "_Style", "_VisualTime", "_Detail",
                    "_FiberDensity", "_Turbulence", "_FlowSpeed", "_Sparkle", "_NearFadeStart", "_NearFadeEnd" })
                {
                    material.SetFloat(property, block.GetFloat(property));
                }
                material.SetVector("_SpectralShape", block.GetVector("_SpectralShape"));
                material.SetVector("_SpectralLayers", block.GetVector("_SpectralLayers"));
                material.SetVector("_TerrainDetail", block.GetVector("_TerrainDetail"));
                // A saved world-to-screen matrix would become incorrect when the snapshot moves.
                // Keep the mask on the live field; static decorative snapshots have no screen binding.
                material.SetFloat("_ScreenEnabled", 0f);
                AssetDatabase.CreateAsset(material, folder + "/" + label + ".mat");
                GameObject child = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(root.transform, false);
                child.transform.SetPositionAndRotation(effect.transform.position, effect.transform.rotation);
                child.transform.localScale = effect.transform.lossyScale;
                child.GetComponent<MeshFilter>().sharedMesh = copy;
                MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            string path = folder + "/Snapshot.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            System.IO.File.WriteAllText(folder + "/SourceParameters.json", JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(folder + "/SourceParameters.json");
            AssetDatabase.SaveAssets();
            return path;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Serializable]
    private sealed class SnapshotManifest
    {
        public string sourceScene;
        public string sourceTheme;
        public string themeSettings;
        public bool syntheticInput;
        public float visualTime;
        public SnapshotEffect[] effects;
    }

    [Serializable]
    private sealed class SnapshotEffect
    {
        public string type;
        public string parameters;
    }
}
