using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Mesh arrays for a single 16^3 section.
/// UV0 = tile units for greedy frac tiling. UV1 = atlas tile origin.
/// </summary>
public class SectionRenderData
{
    public readonly int SectionIndex;
    public readonly Vector3[] Vertices;
    public readonly int[] Triangles;
    public readonly Vector2[] UVs;
    public readonly Vector2[] UV1s;

    public bool IsEmpty => Vertices == null || Vertices.Length == 0;

    public SectionRenderData(
        int sectionIndex,
        Vector3[] vertices,
        int[] triangles,
        Vector2[] uvs,
        Vector2[] uv1s)
    {
        SectionIndex = sectionIndex;
        Vertices = vertices;
        Triangles = triangles;
        UVs = uvs;
        UV1s = uv1s;
    }

    public static SectionRenderData CreateEmpty(int sectionIndex)
    {
        return new SectionRenderData(
            sectionIndex,
            System.Array.Empty<Vector3>(),
            System.Array.Empty<int>(),
            System.Array.Empty<Vector2>(),
            System.Array.Empty<Vector2>());
    }

    public static SectionRenderData FromNative(
        int sectionIndex,
        NativeList<float3> vertices,
        NativeList<int> triangles,
        NativeList<float2> uvs,
        NativeList<float2> uv1s)
    {
        if (!vertices.IsCreated || vertices.Length == 0)
            return CreateEmpty(sectionIndex);

        Vector3[] managedVerts = new Vector3[vertices.Length];
        Vector2[] managedUvs = new Vector2[uvs.Length];
        Vector2[] managedUv1s = new Vector2[uv1s.Length];
        int[] managedTris = new int[triangles.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            float3 v = vertices[i];
            managedVerts[i] = new Vector3(v.x, v.y, v.z);
        }

        for (int i = 0; i < uvs.Length; i++)
        {
            float2 uv = uvs[i];
            managedUvs[i] = new Vector2(uv.x, uv.y);
        }

        for (int i = 0; i < uv1s.Length; i++)
        {
            float2 uv = uv1s[i];
            managedUv1s[i] = new Vector2(uv.x, uv.y);
        }

        for (int i = 0; i < triangles.Length; i++)
            managedTris[i] = triangles[i];

        return new SectionRenderData(sectionIndex, managedVerts, managedTris, managedUvs, managedUv1s);
    }
}

/// <summary>
/// One or more section meshes produced for a chunk.
/// </summary>
public class ChunkRenderData
{
    public readonly Vector2Int ChunkCoords;
    public readonly int SectionMask;
    public readonly SectionRenderData[] Sections;

    public ChunkRenderData(Vector2Int coords, int sectionMask, List<SectionRenderData> sections)
    {
        ChunkCoords = coords;
        SectionMask = sectionMask;
        Sections = sections.ToArray();
    }
}
