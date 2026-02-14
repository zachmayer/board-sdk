#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Pong.Editor
{
    public static class BuildScript
    {
        private static readonly string[] Scenes = { "Assets/Scenes/SampleScene.unity" };

        [MenuItem("Board/Pong/Build Android APK")]
        public static void Build()
        {
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Build/Pong.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);

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
