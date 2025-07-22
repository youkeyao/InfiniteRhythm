using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct LandData
{
    public Vector3 scale;
    public Mesh mesh;
    public Texture2D heightMap;
}

[System.Serializable]
public struct ItemData
{
    public Vector3 scale;
    public Mesh mesh;
    public float probability;
    public float maxY;
}

public class MapManager : MonoBehaviour
{
    public LevelManager levelManager;

    public float showDistance = 200.0f;
    public Transform cameraTransform;
    public Material mapMaterial;

    // road
    public float roadSpacing;
    public Vector3 roadRotation;
    public Vector3 roadScale = Vector3.one;
    public Mesh roadMesh;

    // land
    public float landOverlap = 0.1f;
    public LandData[] landDatas;

    // Item
    public float baseVegNoiseScale = 0.2f;
    public float detailVegNoiseScale = 0.8f;
    public float baseVegNoiseThreshold = 0.6f;
    public float detailVegNoiseThreshold = 0.4f;
    public ItemData[] itemDatas;

    // private variables
    float m_roadDistance = 0;
    float[] m_landDistances;
    Queue<Matrix4x4> m_roadSpawnQueue = new Queue<Matrix4x4>();
    List<Queue<Matrix4x4>> m_landSpawnQueues = new List<Queue<Matrix4x4>>();
    List<int> m_lastLandIndex = new List<int>();
    List<Queue<Matrix4x4>> m_itemSpawnQueues = new List<Queue<Matrix4x4>>();

    Unity.Mathematics.Random m_random;

    void Start()
    {
        m_random = new Unity.Mathematics.Random(0x12345678);
        for (int i = 0; i < landDatas.Length; i++)
        {
            m_landSpawnQueues.Add(new Queue<Matrix4x4>());
        }
        m_landDistances = new float[RoadGenerator.LandCol];
        for (int i = 0; i < RoadGenerator.LandCol; i++)
        {
            m_landDistances[i] = 0;
            m_lastLandIndex.Add(-1);
        }
        for (int i = 0; i < itemDatas.Length; i++)
        {
            m_itemSpawnQueues.Add(new Queue<Matrix4x4>());
        }
    }

    void Update()
    {
        if (levelManager.isPlaying)
        {
            GenerateRoad();
            GenerateLand();
        }

        // Dispose & Draw
        while (m_roadSpawnQueue.Count > 0 && Vector3.Dot(m_roadSpawnQueue.Peek().GetPosition() - cameraTransform.position, cameraTransform.forward) < 0)
        {
            m_roadSpawnQueue.Dequeue();
        }
        Graphics.DrawMeshInstanced(roadMesh, 0, mapMaterial, m_roadSpawnQueue.ToArray(), m_roadSpawnQueue.Count);
        for (int i = 0; i < landDatas.Length; i++)
        {
            while (m_landSpawnQueues[i].Count > 0 && Vector3.Dot(m_landSpawnQueues[i].Peek().GetPosition() - cameraTransform.position, cameraTransform.forward) < 0)
            {
                m_landSpawnQueues[i].Dequeue();
            }
            Graphics.DrawMeshInstanced(landDatas[i].mesh, 0, mapMaterial, m_landSpawnQueues[i].ToArray(), m_landSpawnQueues[i].Count);
        }
        for (int i = 0; i < itemDatas.Length; i++)
        {
            while (m_itemSpawnQueues[i].Count > 0 && Vector3.Dot(m_itemSpawnQueues[i].Peek().GetPosition() - cameraTransform.position, cameraTransform.forward) < 0)
            {
                m_itemSpawnQueues[i].Dequeue();
            }
            Graphics.DrawMeshInstanced(itemDatas[i].mesh, 0, mapMaterial, m_itemSpawnQueues[i].ToArray(), m_itemSpawnQueues[i].Count);
        }
    }

    void GenerateRoad()
    {
        float currentTime = Time.time - levelManager.startTime;
        while (m_roadDistance < currentTime * levelManager.speed + showDistance)
        {
            Quaternion rotation = Quaternion.Euler(roadRotation);
            if (m_random.NextBool())
            {
                rotation *= Quaternion.Euler(0, 180, 0);
            }
            Matrix4x4 targetTransform = RoadGenerator.GetTransform(m_roadDistance);
            m_roadSpawnQueue.Enqueue(targetTransform * Matrix4x4.TRS(Vector3.zero, rotation, roadScale));
            m_roadDistance += roadSpacing;
        }
    }

    void GenerateLand()
    {
        float currentTime = Time.time - levelManager.startTime;
        int landIndex = 0;
        for (int i = 1; i < RoadGenerator.LandCol; i++)
        {
            if (m_landDistances[i] / RoadGenerator.GetLandRatio(i) < m_landDistances[landIndex] / RoadGenerator.GetLandRatio(landIndex))
            {
                landIndex = i;
            }
        }
        while (m_landDistances[landIndex] < RoadGenerator.GetLandRatio(landIndex) * currentTime * levelManager.speed + showDistance)
        {
            int landMeshIndex = m_random.NextInt(0, landDatas.Length);
            // avoid continuous land
            while (landMeshIndex == m_lastLandIndex[landIndex])
            {
                landMeshIndex = m_random.NextInt(0, landDatas.Length);
            }
            m_lastLandIndex[landIndex] = landMeshIndex;

            Vector3 meshBoundSize = landDatas[landMeshIndex].mesh.bounds.size;
            Vector3 landSize = new Vector3(meshBoundSize.x * landDatas[landMeshIndex].scale.x, meshBoundSize.y * landDatas[landMeshIndex].scale.y, meshBoundSize.z * landDatas[landMeshIndex].scale.z);
            Matrix4x4 landTransform = RoadGenerator.GetXTransform(m_landDistances[landIndex], landIndex) * Matrix4x4.TRS(new Vector3(0, 0, 0), Quaternion.identity, landDatas[landMeshIndex].scale);
            m_landSpawnQueues[landMeshIndex].Enqueue(landTransform);
            m_landDistances[landIndex] += (1 - landOverlap) * landSize.z;

            // generate items
            GenerateItems(landTransform, landSize, landDatas[landMeshIndex].heightMap);
            for (int i = 0; i < RoadGenerator.LandCol; i++)
            {
                if (m_landDistances[i] < m_landDistances[landIndex])
                {
                    landIndex = i;
                }
            }
        }
    }

    void GenerateItems(Matrix4x4 landTransform, Vector3 landSize, Texture2D heightMap)
    {
        Vector3 landPos = landTransform.GetPosition();
        Quaternion landRotation = landTransform.rotation;
        Matrix4x4 unscaledLandTransform = Matrix4x4.TRS(landPos, landRotation, Vector3.one);
        for (int i = 0; i < itemDatas.Length; i++)
        {
            float itemSize = itemDatas[i].mesh.bounds.size.x * itemDatas[i].scale.x;
            float padding = itemSize / landSize.x;
            padding = Mathf.Min(padding, 0.5f);
            while (m_random.NextFloat(0, 1) < itemDatas[i].probability)
            {
                float u = m_random.NextFloat(padding, 1 - padding);
                float v = m_random.NextFloat(padding, 1 - padding);
                float X = u * landSize.x - landSize.x / 2;
                float Z = v * landSize.z - landSize.z / 2;
                float Y = Mathf.Min(heightMap.GetPixelBilinear(u, v).r * landSize.y, itemDatas[i].maxY);
                Vector3 pos = new Vector3(X, Y, Z);
                Quaternion rotation = Quaternion.Euler(0, m_random.NextFloat(0, 360), 0);
                Matrix4x4 itemTransform = unscaledLandTransform * Matrix4x4.TRS(pos, rotation, itemDatas[i].scale);
                Vector3 itemPos = itemTransform.GetPosition();
                float baseNoise = Mathf.PerlinNoise(
                    itemPos.x * baseVegNoiseScale + i * 100,
                    itemPos.z * baseVegNoiseScale + i * 100
                );
                if (baseNoise < baseVegNoiseThreshold)
                    continue;
                float detailNoise = Mathf.PerlinNoise(
                    itemPos.x * detailVegNoiseScale + i * 100,
                    itemPos.z * detailVegNoiseScale + i * 100
                );
                if (detailNoise > detailVegNoiseThreshold)
                    continue;
                m_itemSpawnQueues[i].Enqueue(itemTransform);
            }
        }
    }
}