// File: ChunkRenderData.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A simple, thread-safe class to hold the results
/// of the mesh generation from a worker thread.
/// </summary>
public class ChunkRenderData
{
    public readonly Vector3[] Vertices;
    public readonly int[] Triangles;
    public readonly Vector2[] UVs;
    public readonly Vector2Int ChunkCoords;

    public ChunkRenderData(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Vector2Int coords)
    {
        Vertices = vertices.ToArray();
        Triangles = triangles.ToArray();
        UVs = uvs.ToArray();
        ChunkCoords = coords;
    }
}