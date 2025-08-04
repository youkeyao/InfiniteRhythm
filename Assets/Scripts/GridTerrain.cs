using UnityEngine;
using System.Collections.Generic;

public class GridTerrain : MonoBehaviour
{
    public Vector3 offset;
    public Vector3 scale = Vector3.one;
    public Mesh mesh;
    public Material material;
    public int size;
    public int range;
    public GameObject fogPlane;

    Matrix4x4[] m_spawnQueues;

    void Start()
    {
        int w = range * 2 + 1;
        m_spawnQueues = new Matrix4x4[w * w];
    }

    void Update()
    {
        Vector3 pos = transform.position;
        fogPlane.transform.position = new Vector3(pos.x, fogPlane.transform.position.y, pos.z);
        int x = (int)pos.x / size;
        int z = (int)pos.z / size;
        int count = 0;
        int range2 = range * range;
        for (int i = -range; i <= range; i++)
        {
            if (i * i > range2) continue;
            for (int j = -range; j <= range; j++)
            {
                if (i * i + j * j <= range2)
                {
                    m_spawnQueues[count++] = Matrix4x4.TRS(offset + new Vector3((x + i) * size, 0, (z + j) * size), Quaternion.identity, scale);
                }
            }
        }
        Graphics.DrawMeshInstanced(mesh, 0, material, m_spawnQueues, count);
    }
}