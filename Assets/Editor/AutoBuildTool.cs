using UnityEngine;
using UnityEditor;
using System.IO;
using System.IO.Compression;
using System;

public class AutoBuildTool
{
    [MenuItem("Build/Build Windows (Auto Version + Zip)")]
    public static void BuildWindows()
    {
        // === 1. 自动版本号 ===
        string version = PlayerSettings.bundleVersion;
        string newVersion = IncrementVersion(version);
        PlayerSettings.bundleVersion = newVersion;

        string buildName = Application.productName;
        string buildFolder = "Builds";
        string exeName = $"{buildName}_v{newVersion}.exe";
        string targetDir = Path.Combine(buildFolder, $"{buildName}_v{newVersion}_Win64");
        string zipPath = Path.Combine(buildFolder, $"{buildName}_v{newVersion}_Win64.zip");

        if (!Directory.Exists(buildFolder))
            Directory.CreateDirectory(buildFolder);

        // === 2. 打包 ===
        string[] scenes = GetEnabledScenes();
        BuildPipeline.BuildPlayer(scenes, Path.Combine(targetDir, exeName), BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log($"Build 完成: {targetDir}");

        // === 3. 生成日志文件 ===
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);
        string logPath = Path.Combine(targetDir, "BuildInfo.txt");
        File.WriteAllText(logPath,
            $"Game: {buildName}\n" +
            $"Version: {newVersion}\n" +
            $"Unity: {Application.unityVersion}\n" +
            $"Date: {DateTime.Now}\n" +
            $"Platform: Windows x64\n");

        // === 4. 压缩 zip ===
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(targetDir, zipPath);
        Debug.Log($"已生成压缩包: {zipPath}");

        // === 5. 打开目录 ===
        EditorUtility.RevealInFinder(zipPath);
    }

    // 自动递增版本号（默认最后一位）
    private static string IncrementVersion(string version)
    {
        string[] parts = version.Split('.');
        if (parts.Length < 3) parts = new string[] { "1", "0", "0" };

        int patch = int.Parse(parts[parts.Length - 1]);
        patch++;
        parts[parts.Length - 1] = patch.ToString();

        return string.Join(".", parts);
    }

    // 获取勾选的场景
    private static string[] GetEnabledScenes()
    {
        var scenes = EditorBuildSettings.scenes;
        string[] enabledScenes = new string[scenes.Length];
        int index = 0;
        foreach (var scene in scenes)
        {
            if (scene.enabled)
                enabledScenes[index++] = scene.path;
        }
        System.Array.Resize(ref enabledScenes, index);
        return enabledScenes;
    }
}
