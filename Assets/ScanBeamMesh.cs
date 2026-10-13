using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ScanBeamMesh : MonoBehaviour
{
    [SerializeField] private float _height = 5f;
    [SerializeField] private float _baseWidth = 3f;  // X axis size
    [SerializeField] private float _baseLength = 5f; // Z axis size

    private void Awake()
    {
        GenerateMesh();
    }

    private void GenerateMesh()
    {
        var mesh = new Mesh { name = "ScanBeam" };

        float halfW = _baseWidth * 0.5f;
        float halfL = _baseLength * 0.5f;

        Vector3 apex = Vector3.zero;
        Vector3 a = new Vector3(-halfW, -_height, -halfL);
        Vector3 b = new Vector3(halfW, -_height, -halfL);
        Vector3 c = new Vector3(halfW, -_height, halfL);
        Vector3 d = new Vector3(-halfW, -_height, halfL);

        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();

        void AddFace(Vector3 baseA, Vector3 baseB)
        {
            int start = verts.Count;
            verts.Add(apex);   uvs.Add(new Vector2(0.5f, 1f));
            verts.Add(baseA);  uvs.Add(new Vector2(0f, 0f));
            verts.Add(baseB);  uvs.Add(new Vector2(1f, 0f));
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
        }

        AddFace(a, b);
        AddFace(b, c);
        AddFace(c, d);
        AddFace(d, a);

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }
}