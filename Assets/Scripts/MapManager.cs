using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    public LevelManager levelManager;
    public AudioManager audioManager;

    public Transform cameraTransform;

    public SceneData sceneData;

    // private variables
    float[] m_landDistances;
    List<Queue<float>> m_landSampleQueues = new List<Queue<float>>();
    List<Queue<Matrix4x4>> m_landSpawnQueues = new List<Queue<Matrix4x4>>();
    List<Queue<Matrix4x4>> m_itemSpawnQueues = new List<Queue<Matrix4x4>>();

    MaterialPropertyBlock m_landProperties;
    Unity.Mathematics.Random m_random;

    void Start()
    {
        m_random = new Unity.Mathematics.Random(0x12345678);
        m_landProperties = new MaterialPropertyBlock();
        for (int i = 0; i < sceneData.landDatas.Length; i++)
        {
            m_landSpawnQueues.Add(new Queue<Matrix4x4>());
            m_landSampleQueues.Add(new Queue<float>());
        }
        m_landDistances = new float[CurveGenerator.ChildCol * 2 + 1];
        for (int i = 0; i < CurveGenerator.ChildCol * 2 + 1; i++)
        {
            m_landDistances[i] = 0;
        }
        for (int i = 0; i < sceneData.itemDatas.Length; i++)
        {
            m_itemSpawnQueues.Add(new Queue<Matrix4x4>());
        }
    }

    void Update()
    {
        // generate map
        if (levelManager.isPlaying)
        {
            GenerateLand();
        }

        // Dispose & Draw
        // while (m_roadSpawnQueue.Count > 0 && Vector3.Dot(m_roadSpawnQueue.Peek().GetPosition() - cameraTransform.position, cameraTransform.forward) < 0)
        // {
        //     m_roadSpawnQueue.Dequeue();
        //     m_roadSampleQueue.Dequeue();
        // }
        // if (sceneData.roadMesh != null && sceneData.roadMaterial != null)
        // {
        //     if (m_roadSampleQueue.Count > 0)
        //         m_landProperties.SetFloatArray("_Samples", m_roadSampleQueue.ToArray());
        //     Graphics.DrawMeshInstanced(sceneData.roadMesh, 0, sceneData.roadMaterial, m_roadSpawnQueue.ToArray(), m_roadSpawnQueue.Count, m_landProperties);
        // }
        for (int i = 0; i < sceneData.landDatas.Length; i++)
        {
            while (m_landSpawnQueues[i].Count > 0 && Vector3.Dot(m_landSpawnQueues[i].Peek().GetPosition() - cameraTransform.position, cameraTransform.forward) < 0)
            {
                m_landSpawnQueues[i].Dequeue();
                m_landSampleQueues[i].Dequeue();
            }
            if (sceneData.landDatas[i].mesh != null && sceneData.landDatas[i].material != null)
            {
                if (m_landSampleQueues[i].Count > 0)
                    m_landProperties.SetFloatArray("_Samples", m_landSampleQueues[i].ToArray());
                Graphics.DrawMeshInstanced(sceneData.landDatas[i].mesh, 0, sceneData.landDatas[i].material, m_landSpawnQueues[i].ToArray(), m_landSpawnQueues[i].Count, m_landProperties);
            }
        }
        for (int i = 0; i < sceneData.itemDatas.Length; i++)
        {
            while (m_itemSpawnQueues[i].Count > 0 && Vector3.Dot(m_itemSpawnQueues[i].Peek().GetPosition() - cameraTransform.position, cameraTransform.forward) < 0)
            {
                m_itemSpawnQueues[i].Dequeue();
            }
            if (sceneData.itemDatas[i].mesh != null && sceneData.itemDatas[i].material != null)
            {
                Graphics.DrawMeshInstanced(sceneData.itemDatas[i].mesh, 0, sceneData.itemDatas[i].material, m_itemSpawnQueues[i].ToArray(), m_itemSpawnQueues[i].Count);
            }
        }
    }

    void GenerateLand()
    {
        for (int i = 0; i < m_landDistances.Length; i++)
        {
            int landColIndex = i - CurveGenerator.ChildCol;
            while (m_landDistances[i] < CurveGenerator.GetLength(landColIndex))
            {
                int landMeshIndex = m_random.NextInt(0, sceneData.landDatas.Length);
                // valid land col
                // while (!sceneData.landDatas[landMeshIndex].validateLandCol.Contains(landColIndex))
                // {
                //     landMeshIndex = m_random.NextInt(0, sceneData.landDatas.Length);
                // }
                m_landSampleQueues[landMeshIndex].Enqueue(audioManager.GetSample(m_landDistances[i] / levelManager.speed));

                Matrix4x4 landTransform = CurveGenerator.GetTransform(m_landDistances[i], landColIndex) * Matrix4x4.Scale(sceneData.landDatas[landMeshIndex].scale);
                Vector3 landSize = new Vector3(sceneData.landDatas[landMeshIndex].spacing, sceneData.landDatas[landMeshIndex].spacing, sceneData.landDatas[landMeshIndex].spacing);
                if (sceneData.landDatas[landMeshIndex].mesh != null)
                {
                    Vector3 meshBoundSize = sceneData.landDatas[landMeshIndex].mesh.bounds.size;
                    landSize = new Vector3(meshBoundSize.x * sceneData.landDatas[landMeshIndex].scale.x, meshBoundSize.y * sceneData.landDatas[landMeshIndex].scale.y, meshBoundSize.z * sceneData.landDatas[landMeshIndex].scale.z);
                    m_landSpawnQueues[landMeshIndex].Enqueue(landTransform);
                }
                // generate items
                GenerateItems(landTransform, landSize, sceneData.landDatas[landMeshIndex].heightMap, landMeshIndex);
                m_landDistances[i] += sceneData.landDatas[landMeshIndex].spacing;
            }
        }
    }

    void GenerateItems(Matrix4x4 landTransform, Vector3 landSize, Texture2D heightMap, int landIndex)
    {
        Vector3 landPos = landTransform.GetPosition();
        Quaternion landRotation = landTransform.rotation;
        Matrix4x4 unscaledLandTransform = Matrix4x4.TRS(landPos, landRotation, Vector3.one);
        for (int i = 0; i < sceneData.itemDatas.Length; i++)
        {
            if (sceneData.itemDatas[i].validateLandIndex.Contains(landIndex))
            {
                float itemSize = sceneData.itemDatas[i].mesh.bounds.size.x * sceneData.itemDatas[i].scale.x;
                float padding = itemSize / landSize.x;
                padding = Mathf.Min(padding, 0.5f);
                while (m_random.NextFloat(0, 1) < sceneData.itemDatas[i].probability)
                {
                    float u = m_random.NextFloat(padding, 1 - padding);
                    float v = m_random.NextFloat(padding, 1 - padding);
                    float X = u * landSize.x - landSize.x / 2;
                    float Z = v * landSize.z - landSize.z / 2;
                    float Y = heightMap.GetPixelBilinear(u, v).r * landSize.y;
                    Vector3 pos = new Vector3(X, Y, Z);
                    Quaternion rotation = Quaternion.Euler(0, m_random.NextFloat(0, 360), 0);
                    Matrix4x4 itemTransform = unscaledLandTransform * Matrix4x4.TRS(pos, rotation, sceneData.itemDatas[i].scale);
                    Vector3 itemPos = itemTransform.GetPosition();
                    float baseNoise = Mathf.PerlinNoise(
                        itemPos.x * sceneData.baseVegNoiseScale + i * 100,
                        itemPos.z * sceneData.baseVegNoiseScale + i * 100
                    );
                    if (baseNoise < sceneData.baseVegNoiseThreshold)
                        continue;
                    float detailNoise = Mathf.PerlinNoise(
                        itemPos.x * sceneData.detailVegNoiseScale + i * 100,
                        itemPos.z * sceneData.detailVegNoiseScale + i * 100
                    );
                    if (detailNoise > sceneData.detailVegNoiseThreshold)
                        continue;
                    m_itemSpawnQueues[i].Enqueue(itemTransform);
                }
            }
        }
    }
}