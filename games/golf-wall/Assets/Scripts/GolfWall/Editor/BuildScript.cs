#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GolfWall.Editor
{
    public static class BuildScript
    {
        private static readonly string[] Scenes = { "Assets/Scenes/SampleScene.unity" };

        [MenuItem("Board/Golf Wall/Build Android APK")]
        public static void Build()
        {
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Build/GolfWall.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            Report(BuildPipeline.BuildPlayer(options));
        }

        [MenuItem("Board/Golf Wall/Build macOS App")]
        public static void BuildMac()
        {
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Build/GolfWall.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };

            Report(BuildPipeline.BuildPlayer(options));
        }

        private static void Report(BuildReport report)
        {
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build succeeded: {report.summary.outputPath}");
            }
            else
            {
                Debug.LogError($"Build failed: {report.summary.result}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
