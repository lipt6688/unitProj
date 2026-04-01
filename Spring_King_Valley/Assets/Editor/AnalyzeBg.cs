using UnityEngine;
using UnityEditor;

public class AnalyzeBg {
    public static void Run() {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Infinite Background.mat");
        if(mat == null) mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Infinite Background.mat");
        if(mat == null) mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Infinite Background.mat");
        
        string[] guids = AssetDatabase.FindAssets("Infinite Background t:Material");
        foreach(var g in guids) {
            mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
            if(mat != null && mat.mainTexture != null) {
                Debug.Log("Found Mat: " + mat.name + " -> Tex: " + mat.mainTexture.name + " (" + mat.mainTexture.width + "x" + mat.mainTexture.height + ")");
                System.IO.File.WriteAllText("Logs/mat_info.txt", mat.mainTexture.name + " " + mat.mainTexture.width + "x" + mat.mainTexture.height);
            }
        }
    }
}
