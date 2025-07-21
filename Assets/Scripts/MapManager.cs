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
public struct VegetationData
{
    public Mesh mesh;
    public int minCountPerLand;
    public int maxCountPerLand;
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
    public int landCol = 2;
    public float landOverlap = 0.1f;
    public Vector2 landSpacing;
    public LandData[] landDatas;

    // Veg
    public VegetationData[] vegDatas;

    // private variables
    float m_roadDistance = 0;
    float[] m_landDistances;
    Queue<Matrix4x4> m_roadSpawnQueue = new Queue<Matrix4x4>();
    List<Queue<Matrix4x4>> m_landSpawnQueues = new List<Queue<Matrix4x4>>();
    List<int> m_lastLandIndex = new List<int>();
    List<Queue<Matrix4x4>> m_vegSpawnQueues = new List<Queue<Matrix4x4>>();

    Unity.Mathematics.Random m_random;

    void Start()
    {
        m_random = new Unity.Mathematics.Random(0x12345678);
        for (int i = 0; i < landDatas.Length; i++)
        {
            m_landSpawnQueues.Add(new Queue<Matrix4x4>());
        }
        m_landDistances = new float[landCol];
        for (int i = 0; i < landCol; i++)
        {
            m_landDistances[i] = 0;
            m_lastLandIndex.Add(-1);
        }
        for (int i = 0; i < vegDatas.Length; i++)
        {
            m_vegSpawnQueues.Add(new Queue<Matrix4x4>());
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
            while (m_landSpawnQueues[i].Count > 0 && Vector3.Dot(m_landSpawnQueues[i].Peek().GetPosition() - cameraTransform.position + cameraTransform.forward * landSpacing[1], cameraTransform.forward) < 0)
            {
                m_landSpawnQueues[i].Dequeue();
            }
            Graphics.DrawMeshInstanced(landDatas[i].mesh, 0, mapMaterial, m_landSpawnQueues[i].ToArray(), m_landSpawnQueues[i].Count);
        }
        // for (int i = 0; i < vegDatas.Length; i++)
        // {
        //     while (m_vegSpawnQueues[i].Count > 0 && m_vegSpawnQueues[i].Peek().GetPosition().z < cameraTransform.position.z)
        //     {
        //         m_vegSpawnQueues[i].Dequeue();
        //     }
        //     Graphics.DrawMeshInstanced(vegDatas[i].mesh, 0, mapMaterial, m_vegSpawnQueues[i].ToArray(), m_vegSpawnQueues[i].Count);
        // }
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
        int minIndex = 0;
        for (int i = 1; i < landCol; i++)
        {
            if (m_landDistances[i] < m_landDistances[minIndex])
            {
                minIndex = i;
            }
        }
        while (m_landDistances[minIndex] < currentTime * levelManager.speed + showDistance)
        {
            int landIndex = m_random.NextInt(0, landDatas.Length);

            // avoid continuous land
            while (landIndex == m_lastLandIndex[minIndex])
            {
                landIndex = m_random.NextInt(0, landDatas.Length);
            }
            m_lastLandIndex[minIndex] = landIndex;
            float landColOffset = minIndex - (landCol - 1) / 2.0f;
            float X = landSpacing[0] * Mathf.Sign(landColOffset) + landColOffset * landSpacing[1];
            Vector3 meshBoundSize = landDatas[landIndex].mesh.bounds.size;
            Vector3 landSize = new Vector3(meshBoundSize.x * landDatas[landIndex].scale.x, meshBoundSize.y * landDatas[landIndex].scale.y, meshBoundSize.z * landDatas[landIndex].scale.z);
            Matrix4x4 landTransform = RoadGenerator.GetTransform(m_landDistances[minIndex]) * Matrix4x4.TRS(new Vector3(X, 0, 0), Quaternion.identity, landDatas[landIndex].scale);
            m_landSpawnQueues[landIndex].Enqueue(landTransform);
            m_landDistances[minIndex] += (1 - landOverlap) * landSize.z;

            // generate vegetation
            GenerateVegetation(landTransform, landSize, landDatas[landIndex].heightMap);
            for (int i = 0; i < landCol; i++)
            {
                if (m_landDistances[i] < m_landDistances[minIndex])
                {
                    minIndex = i;
                }
            }
        }
    }

    void GenerateVegetation(Matrix4x4 landTransform, Vector3 landSize, Texture2D heightMap)
    {
        Vector3 landPos = landTransform.GetPosition();
        for (int i = 0; i < vegDatas.Length; i++)
        {
            int vegCount = m_random.NextInt(vegDatas[i].minCountPerLand, vegDatas[i].maxCountPerLand);
            for (int j = 0; j < vegCount; j++)
            {
                float u = m_random.NextFloat(landOverlap, 1 - landOverlap);
                float v = m_random.NextFloat(landOverlap, 1 - landOverlap);
                float X = u * landSize.x + landPos.x - landSize.x / 2;
                float Z = v * landSize.z + landPos.z - landSize.z / 2;
                float Y = heightMap.GetPixelBilinear(u, v).r * landSize.y;
                Vector3 pos = new Vector3(X, Y, Z);
                Quaternion rotation = Quaternion.Euler(0, m_random.NextFloat(0, 360), 0);
                m_vegSpawnQueues[i].Enqueue(Matrix4x4.TRS(pos, rotation, Vector3.one));
            }
        }
    }
}