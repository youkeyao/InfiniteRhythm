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

    Matrix4x4[] m_instances;

    void Start()
    {
        int w = range * 2 + 1;
        Queue<Matrix4x4> spawnQueue = new Queue<Matrix4x4>();
        int range2 = range * range;
        for (int i = -range; i <= range; i++)
        {
            if (i * i > range2) continue;
            for (int j = -range; j <= range; j++)
            {
                if (i * i + j * j <= range2)
                {
                    spawnQueue.Enqueue(Matrix4x4.TRS(offset + new Vector3(i * size, 0, j * size), Quaternion.identity, scale));
                }
            }
        }
        m_instances = spawnQueue.ToArray();

        Mesh newMesh = new Mesh();
        newMesh.vertices = mesh.vertices;
        newMesh.triangles = mesh.triangles;
        newMesh.uv = mesh.uv;
        mesh = newMesh;
    }

    void Update()
    {
        material.SetInt("_GridX", ((int)transform.position.x) / size * size);
        material.SetInt("_GridZ", ((int)transform.position.z) / size * size);
        mesh.bounds = new Bounds(transform.position, new Vector3(1, 1, 1));
        Graphics.DrawMeshInstanced(mesh, 0, material, m_instances, m_instances.Length);
    }
}