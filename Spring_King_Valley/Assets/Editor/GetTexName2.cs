using UnityEditor;
using UnityEngine;
using System.IO;

public class GetTexName2 {
    public static void Run() {
        var path = AssetDatabase.GUIDToAssetPath("42b11be826072496a9d57814c0ca6576");
        File.WriteAllText("Logs/tex2.txt", path);
    }
}
