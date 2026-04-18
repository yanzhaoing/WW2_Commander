// BuildGame.cs — 命令行打包脚本
using UnityEditor;
using UnityEngine;
using System.IO;

namespace SWO1.Editor
{
    public static class BuildGame
    {
        [MenuItem("WW2 Commander/Build Linux", priority = 100)]
        public static void BuildLinux()
        {
            string outputPath = "/home/yanzhaoharsh/桌面/qaq/ww2 commander (主界面)/ww2 commander (主界面)";
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var options = new BuildPlayerOptions
            {
                scenes = GetEnabledScenes(),
                locationPathName = outputPath,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[BuildGame] ✅ 打包成功: {report.summary.totalSize / 1024 / 1024}MB, 耗时 {report.summary.totalTime.TotalSeconds:F1}s");
            }
            else
            {
                Debug.LogError($"[BuildGame] ❌ 打包失败: {report.summary.result}");
            }
        }

        private static string[] GetEnabledScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                    scenes.Add(scene.path);
            }
            // 如果没有配置 Build Settings 场景，扫描 Assets/Scenes/
            if (scenes.Count == 0)
            {
                foreach (var f in Directory.GetFiles("Assets/Scenes", "*.unity"))
                    scenes.Add(f);
            }
            return scenes.ToArray();
        }
    }
}
