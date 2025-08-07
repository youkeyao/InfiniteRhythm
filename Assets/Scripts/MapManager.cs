using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    public LevelManager levelManager;

    public Transform cameraTransform;

    // private variables
    float m_startTime = 0;
    float m_clipSign = 1;
    SceneData m_sceneData;
    float[] m_landDistances;
    List<List<int>> m_landValidMesh = new List<List<int>>();
    List<Queue<Matrix4x4>> m_landSpawnQueues = new List<Queue<Matrix4x4>>();
    List<Queue<Matrix4x4>> m_itemSpawnQueues = new List<Queue<Matrix4x4>>();

    Unity.Mathematics.Random m_random;

    void Start()
    {
        m_landDistances = new float[CurveGenerator.ChildCol * 2 + 1];
        m_random = new Unity.Mathematics.Random(0x12345678);
        for (int i = 0; i < CurveGenerator.ChildCol * 2 + 1; i++)
        {
            m_landDistances[i] = 0;
            m_landValidMesh.Add(new List<int>());
        }
        for (int i = 0; i < m_sceneData.landDatas.Length; i++)
        {
            m_landSpawnQueues.Add(new Queue<Matrix4x4>());
            for (int j = 0; j < m_sceneData.landDatas[i].validateLandCol.Length; j++)
            {
                m_landValidMesh[m_sceneData.landDatas[i].validateLandCol[j] + CurveGenerator.ChildCol].Add(i);
            }
        }
        for (int i = 0; i < m_sceneData.itemDatas.Length; i++)
        {
            m_itemSpawnQueues.Add(new Queue<Matrix4x4>());
        }
        m_startTime = Time.time;
    }


    public void SetSceneData(SceneData data)
    {
        m_sceneData = data;
    }

    public void EraseMap()
    {
        m_clipSign = -1;
        m_startTime = Time.time;
    }

    void Update()
    {
        // generate map
        if (levelManager.IsPlaying)
        {
            GenerateLand();
        }

        // Dispose & Draw
        if (m_sceneData != null)
        {
            float clip = (Time.time - m_startTime) * 100;
            Shader.SetGlobalFloat("_Clip", clip * clip);
            Shader.SetGlobalFloat("_ClipSign", m_clipSign);
            for (int i = 0; i < m_sceneData.landDatas.Length; i++)
            {
                while (m_landSpawnQueues[i].Count > 0 && Vector3.Dot(m_landSpawnQueues[i].Peek().GetPosition() - cameraTransform.position, cameraTransform.forward) < 0)
                {
                    m_landSpawnQueues[i].Dequeue();
                }
                if (m_sceneData.landDatas[i].mesh != null && m_sceneData.landDatas[i].material != null)
                {
                    Graphics.DrawMeshInstanced(m_sceneData.landDatas[i].mesh, 0, m_sceneData.landDatas[i].material, m_landSpawnQueues[i].ToArray(), m_landSpawnQueues[i].Count);
                }
            }
            for (int i = 0; i < m_sceneData.itemDatas.Length; i++)
            {
                while (m_itemSpawnQueues[i].Count > 0 && Vector3.Dot(m_itemSpawnQueues[i].Peek().GetPosition() - cameraTransform.position, cameraTransform.forward) < 0)
                {
                    m_itemSpawnQueues[i].Dequeue();
                }
                if (m_sceneData.itemDatas[i].mesh != null && m_sceneData.itemDatas[i].material != null)
                {
                    Graphics.DrawMeshInstanced(m_sceneData.itemDatas[i].mesh, 0, m_sceneData.itemDatas[i].material, m_itemSpawnQueues[i].ToArray(), m_itemSpawnQueues[i].Count);
                }
            }
        }
    }

    void GenerateLand()
    {
        int flag = 0;
        while (flag != m_landDistances.Length)
        {
            flag = 0;
            for (int i = 0; i < m_landDistances.Length; i++)
            {
                int landColIndex = i - CurveGenerator.ChildCol;
                if (m_landDistances[i] < CurveGenerator.GetLength(landColIndex) && m_landValidMesh[i].Count > 0)
                {
                    int landMeshIndex = m_landValidMesh[i][m_random.NextInt(0, m_landValidMesh[i].Count)];

                    Matrix4x4 offsetTransform = Matrix4x4.TRS(m_sceneData.landDatas[landMeshIndex].offset, Quaternion.Euler(m_sceneData.landDatas[landMeshIndex].rotation), m_sceneData.landDatas[landMeshIndex].scale);
                    Matrix4x4 landTransform = CurveGenerator.GetTransform(m_landDistances[i], landColIndex) * offsetTransform;
                    Vector3 landSize = new Vector3(m_sceneData.landDatas[landMeshIndex].spacing, m_sceneData.landDatas[landMeshIndex].spacing, m_sceneData.landDatas[landMeshIndex].spacing);
                    if (m_sceneData.landDatas[landMeshIndex].mesh != null)
                    {
                        Vector3 meshBoundSize = m_sceneData.landDatas[landMeshIndex].mesh.bounds.size;
                        landSize = new Vector3(meshBoundSize.x * m_sceneData.landDatas[landMeshIndex].scale.x, meshBoundSize.y * m_sceneData.landDatas[landMeshIndex].scale.y, meshBoundSize.z * m_sceneData.landDatas[landMeshIndex].scale.z);
                        m_landSpawnQueues[landMeshIndex].Enqueue(landTransform);
                    }
                    // generate items
                    GenerateItems(landTransform, landSize, m_sceneData.landDatas[landMeshIndex].heightMap, landMeshIndex);
                    m_landDistances[i] += m_sceneData.landDatas[landMeshIndex].spacing;
                }
                else
                {
                    flag++;
                }
            }
        }
    }

    void GenerateItems(Matrix4x4 landTransform, Vector3 landSize, Texture2D heightMap, int landIndex)
    {
        Vector3 landPos = landTransform.GetPosition();
        Quaternion landRotation = landTransform.rotation;
        Matrix4x4 unscaledLandTransform = Matrix4x4.TRS(landPos, landRotation, Vector3.one);
        for (int i = 0; i < m_sceneData.itemDatas.Length; i++)
        {
            if (m_sceneData.itemDatas[i].validateLandIndex.Contains(landIndex))
            {
                float itemSize = m_sceneData.itemDatas[i].mesh.bounds.size.x * m_sceneData.itemDatas[i].scale.x;
                float padding = itemSize / landSize.x;
                padding = Mathf.Min(padding, 0.5f);
                while (m_random.NextFloat(0, 1) < m_sceneData.itemDatas[i].probability)
                {
                    float u = m_random.NextFloat(padding, 1 - padding);
                    float v = m_random.NextFloat(padding, 1 - padding);
                    float X = u * landSize.x - landSize.x / 2;
                    float Z = v * landSize.z - landSize.z / 2;
                    float Y = heightMap.GetPixelBilinear(u, v).r * landSize.y;
                    Vector3 pos = new Vector3(X, Y, Z);
                    Quaternion rotation = Quaternion.Euler(0, m_random.NextFloat(0, 360), 0);
                    Matrix4x4 itemTransform = unscaledLandTransform * Matrix4x4.TRS(pos, rotation, m_sceneData.itemDatas[i].scale);
                    Vector3 itemPos = itemTransform.GetPosition();
                    float baseNoise = Mathf.PerlinNoise(
                        itemPos.x * m_sceneData.baseVegNoiseScale + i * 100,
                        itemPos.z * m_sceneData.baseVegNoiseScale + i * 100
                    );
                    if (baseNoise < m_sceneData.baseVegNoiseThreshold)
                        continue;
                    float detailNoise = Mathf.PerlinNoise(
                        itemPos.x * m_sceneData.detailVegNoiseScale + i * 100,
                        itemPos.z * m_sceneData.detailVegNoiseScale + i * 100
                    );
                    if (detailNoise > m_sceneData.detailVegNoiseThreshold)
                        continue;
                    m_itemSpawnQueues[i].Enqueue(itemTransform);
                }
            }
        }
    }
}