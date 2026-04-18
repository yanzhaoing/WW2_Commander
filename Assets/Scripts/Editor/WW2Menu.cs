using UnityEngine;
using UnityEditor;

public class WW2Menu : EditorWindow
{
    [MenuItem("WW2 Commander/Setup 2D Scene (Simple)", priority = 51)]
    public static void ShowWindow()
    {
        // 简单测试菜单
        EditorUtility.DisplayDialog("WW2 Commander", "Menu is working!", "OK");
    }
}
