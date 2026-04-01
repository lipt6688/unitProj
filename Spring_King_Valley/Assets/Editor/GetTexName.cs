using UnityEditor;
using UnityEngine;
using System.IO;

public class GetTexName {
    public static void Run() {
        var path = AssetDatabase.GUIDToAssetPath("42b11be826072496a9d57814c0ca6576");
        File.WriteAllText("Logs/tex_path.txt", path);
        if(!string.IsNullOrEmpty(path)) {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(tex != null) {
                File.AppendAllText("Logs/tex_path.txt", "\nDim: " + tex.width + "x" + tex.height);
            }
        }
    }
}
