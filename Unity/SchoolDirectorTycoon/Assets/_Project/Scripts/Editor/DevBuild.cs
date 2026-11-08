using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SchoolDirectorTycoon.EditorTools
{
    /// <summary>Windows x64 development build for smoke tests:
    /// <c>-executeMethod SchoolDirectorTycoon.EditorTools.DevBuild.Windows -buildPath &lt;dir&gt;</c>.</summary>
    public static class DevBuild
    {
        public static void Windows()
        {
            string dir = "Builds/Dev";
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-buildPath") dir = args[i + 1];

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Campus.unity" },
                locationPathName = dir + "/SchoolDirectorTycoon.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"SDT: build {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
