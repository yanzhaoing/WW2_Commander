// ForceRecompile.cs
// 放在 Assets/Scripts/Editor/ 下，强制重新编译

using UnityEditor;

public static class ForceRecompile
{
    [MenuItem("Tools/Force Recompile")]
    public static void Recompile()
    {
        AssetDatabase.Refresh();
        EditorUtility.RequestScriptReload();
    }
}
