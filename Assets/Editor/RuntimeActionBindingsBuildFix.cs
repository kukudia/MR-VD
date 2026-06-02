using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class RuntimeActionBindingsBuildFix : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    private const string RuntimeActionBindingsFileName = "RuntimeActionBindings.json";

    public int callbackOrder => 1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        foreach (string path in GetBuildOutputBindingCandidates(report.summary.outputPath))
        {
            DeleteIfExists(path);
        }

        DeleteIfExists(Path.Combine(Directory.GetCurrentDirectory(), RuntimeActionBindingsFileName));
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if ((report.summary.options & BuildOptions.AutoRunPlayer) == 0)
        {
            return;
        }

        foreach (string path in GetBuildOutputBindingCandidates(report.summary.outputPath))
        {
            DeleteIfExists(path);
        }
    }

    private static IEnumerable<string> GetBuildOutputBindingCandidates(string outputPath)
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            yield break;
        }

        string fullOutputPath = Path.GetFullPath(outputPath);

        // Mirrors Meta XR's Standalone post-build copy target:
        // RuntimeSettings.UpdateBindingsOnDisk(clean: true, buildPath: outputPath).
        yield return Path.GetFullPath(Path.Combine(fullOutputPath, "..", RuntimeActionBindingsFileName));

        string outputDirectory = Directory.Exists(fullOutputPath)
            ? fullOutputPath
            : Path.GetDirectoryName(fullOutputPath);

        if (!string.IsNullOrEmpty(outputDirectory))
        {
            yield return Path.Combine(outputDirectory, RuntimeActionBindingsFileName);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        FileUtil.DeleteFileOrDirectory(path);
        FileUtil.DeleteFileOrDirectory($"{path}.meta");
    }
}
