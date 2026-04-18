// BuildGame.cs — 打包脚本（支持中世纪骑兵场景）
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace SWO1.Editor
{
    public static class BuildGame
    {
        [MenuItem("WW2 Commander/Build Linux", priority = 100)]
        public static void BuildLinux()
        {
            string tempOutput = "/tmp/ww2_build/ww2_commander";
            string finalDir = "/home/yanzhaoharsh/桌面/qaq/ww2 commander (主界面)";
            string finalPath = finalDir + "/ww2_commander";

            Directory.CreateDirectory(Path.GetDirectoryName(tempOutput));
            Directory.CreateDirectory(finalDir);

            // 确保有场景可以打包
            var scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogWarning("[BuildGame] 没有场景，自动扫描 Assets/Scenes/");
                foreach (var f in Directory.GetFiles("Assets/Scenes", "*.unity"))
                {
                    string path = f.Replace("\\", "/");
                    var editorScene = new EditorBuildSettingsScene(path, true);
                    var list = EditorBuildSettings.scenes.ToList();
                    list.Add(editorScene);
                    EditorBuildSettings.scenes = list.ToArray();
                    scenes = new[] { path };
                }
            }

            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildGame] ❌ 没有可用场景！请先运行 Setup Medieval Scene。");
                return;
            }

            Debug.Log($"[BuildGame] 打包场景: {string.Join(", ", scenes)}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = tempOutput,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[BuildGame] ✅ 打包成功: {report.summary.totalSize / 1024 / 1024}MB, 耗时 {report.summary.totalTime.TotalSeconds:F1}s");

                // 复制到目标目录
                try
                {
                    if (Directory.Exists(finalPath + "_Data"))
                        Directory.Delete(finalPath + "_Data", true);
                    if (File.Exists(finalPath))
                        File.Delete(finalPath);

                    if (File.Exists(tempOutput))
                    {
                        File.Copy(tempOutput, finalPath);
                        File.Copy(tempOutput, finalDir + "/ww2_commander");
                    }
                    if (Directory.Exists(tempOutput + "_Data"))
                        CopyDir(tempOutput + "_Data", finalPath + "_Data");
                    if (File.Exists(tempOutput + ".x86_64"))
                        File.Copy(tempOutput + ".x86_64", finalPath + ".x86_64");

                    Debug.Log($"[BuildGame] 已复制到: {finalDir}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[BuildGame] 复制失败: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[BuildGame] ❌ 打包失败: {report.summary.result}");
            }
        }

        static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src))
                CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        static string[] GetEnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }
    }
}
