using System.IO;
using UnityEngine;

// Render Mesh Height Map
public class RenderHeightMap : MonoBehaviour
{
    public Mesh mesh;
    public string savePath = "Assets/heightmap.png";
    public int textureSize = 1024;

    void Start()
    {
        GameObject go = new GameObject("Mesh");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Unlit/HeightMap"));
        Camera.main.orthographicSize = mesh.bounds.size.x / 2;
        Shader.SetGlobalFloat("_HeightScale", mesh.bounds.size.y);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Texture2D outputTex = new Texture2D(textureSize, textureSize, TextureFormat.RFloat, false);
        RenderTexture.active = source;
        outputTex.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
        outputTex.Apply();
        RenderTexture.active = null;

        byte[] pngBytes = outputTex.EncodeToPNG();
        File.WriteAllBytes(savePath, pngBytes);
    }
}
