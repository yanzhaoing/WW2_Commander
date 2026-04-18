// CleanupCamera.cs — 一键清理旧 3D CameraController
using UnityEngine;
using UnityEditor;
using SWO1.CommandPost;

public class CleanupCamera : EditorWindow
{
    [MenuItem("WW2 Commander/Cleanup Camera")]
    static void RemoveOld()
    {
        var cam = Camera.main;
        if (cam == null) { Debug.LogWarning("找不到 Main Camera"); return; }

        // 移除旧 3D CameraController
        var old = cam.GetComponent<CameraController>();
        if (old != null) { Undo.DestroyObjectImmediate(old); Debug.Log("✅ 已移除 CameraController"); }

        // 强制恢复光标
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("✅ 光标已恢复");
    }

    // 供命令行调用
    static void CleanupCameraFromCLI()
    {
        // 等一帧让场景加载完
        EditorApplication.delayCall += () =>
        {
            RemoveOld();
            EditorApplication.Exit(0);
        };
    }
}
