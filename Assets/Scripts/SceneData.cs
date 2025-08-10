using UnityEngine;

[System.Serializable]
public struct LandData
{
    public Vector3 offset;
    public Vector3 rotation;
    public Vector3 scale;
    public Mesh mesh;
    public Material material;
    public Texture2D heightMap;
    public float spacing;
    public int[] validateLandCol;
}

[System.Serializable]
public struct ItemData
{
    public Vector3 scale;
    public Mesh mesh;
    public Material material;
    public float probability;
    public int[] validateLandIndex;
}

[CreateAssetMenu(menuName = "SceneData")]
public class SceneData : ScriptableObject
{
    public GameObject terrainPrefab;
    // land
    public LandData[] landDatas;

    // Item
    public float baseVegNoiseScale = 0.2f;
    public float detailVegNoiseScale = 0.8f;
    public float baseVegNoiseThreshold = 0.4f;
    public float detailVegNoiseThreshold = 0.6f;
    public ItemData[] itemDatas;
}