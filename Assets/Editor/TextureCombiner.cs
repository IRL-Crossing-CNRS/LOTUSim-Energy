using UnityEngine;
using UnityEditor;
using System.IO;

public class TextureCombiner : EditorWindow
{
    public Texture2D baseColor;
    public Texture2D opacityMap;

    [MenuItem("LOTUSim/Utilities/Texture Combiner")]
    public static void ShowWindow()
    {
        GetWindow<TextureCombiner>("Texture Combiner");
    }

    void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Input Textures", EditorStyles.boldLabel);
        
        baseColor = (Texture2D)EditorGUILayout.ObjectField("Base Color (RGB)", baseColor, typeof(Texture2D), false);
        opacityMap = (Texture2D)EditorGUILayout.ObjectField("Opacity Map (Grayscale)", opacityMap, typeof(Texture2D), false);

        GUILayout.Space(15);
        if (GUILayout.Button("Combine & Save PNG", GUILayout.Height(40)))
        {
            if (baseColor != null && opacityMap != null)
            {
                Combine(baseColor, opacityMap);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Please assign both textures before combining.", "OK");
            }
        }
    }

    void Combine(Texture2D colorTex, Texture2D alphaTex)
    {
        string colorPath = AssetDatabase.GetAssetPath(colorTex);
        string alphaPath = AssetDatabase.GetAssetPath(alphaTex);
        
        MakeReadable(colorPath);
        MakeReadable(alphaPath);

        Texture2D finalTex = new Texture2D(colorTex.width, colorTex.height, TextureFormat.RGBA32, false);
        
        Color[] colors = colorTex.GetPixels();
        Color[] alphas = alphaTex.GetPixels(); // Assume same size for simplicity, as per user setup
        
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i].a = alphas[i].r; 
        }
        
        finalTex.SetPixels(colors);
        finalTex.Apply();

        byte[] bytes = finalTex.EncodeToPNG();
        string newPath = colorPath.Replace(".jpg", "_Transparent.png");
        File.WriteAllBytes(newPath, bytes);
        
        AssetDatabase.Refresh();
        
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(newPath);
        if (importer != null)
        {
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        EditorUtility.DisplayDialog("Success", "The transparent PNG has been created.", "OK");
    }

    void MakeReadable(string path)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }
}
