// CursorFix.cs — 挂到任意 GameObject 上，立即恢复鼠标光标并移除旧 CameraController
using UnityEngine;
using SWO1.CommandPost;

public class CursorFix : MonoBehaviour
{
    void Awake()
    {
        // 强制恢复光标
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 移除旧的 3D CameraController（如果存在）
        var cam = Camera.main;
        if (cam != null)
        {
            var old = cam.GetComponent<CameraController>();
            if (old != null)
            {
                Destroy(old);
                Debug.Log("[CursorFix] 已移除旧 CameraController");
            }
        }

        // 自毁
        Destroy(this);
    }
}
