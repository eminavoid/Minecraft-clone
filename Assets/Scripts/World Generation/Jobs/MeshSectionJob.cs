using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Greedy meshing for one 16^3 section. Burst-compiled.
/// UV0 = tile units (for frac tiling). UV1 = atlas tile origin.
/// </summary>
[BurstCompile]
public struct MeshSectionJob : IJob
{
    private const int Width = 16;
    private const int Height = 256;
    private const int Depth = 16;
    private const int SectionSize = 16;
    private const int FaceCount = 6;

    public int SectionIndex;

    [ReadOnly] public NativeArray<byte> Blocks;
    [ReadOnly] public NativeArray<byte> NeighborPosZ;
    [ReadOnly] public NativeArray<byte> NeighborNegZ;
    [ReadOnly] public NativeArray<byte> NeighborPosX;
    [ReadOnly] public NativeArray<byte> NeighborNegX;

    [ReadOnly] public NativeArray<byte> IsSolid;
    [ReadOnly] public NativeArray<int> FaceTileX;
    [ReadOnly] public NativeArray<int> FaceTileY;
    public float NormalizedTileWidth;
    public float NormalizedTileHeight;

    public NativeList<float3> Vertices;
    public NativeList<int> Triangles;
    public NativeList<float2> UVs;
    public NativeList<float2> UV1s;

    public void Execute()
    {
        Vertices.Clear();
        Triangles.Clear();
        UVs.Clear();
        UV1s.Clear();

        int minY = SectionIndex * SectionSize;
        int maxY = minY + SectionSize - 1;

        if (!SectionHasSolid(minY, maxY))
            return;

        NativeArray<int> mask = new NativeArray<int>(SectionSize * SectionSize, Allocator.Temp);

        // 0 Top, 1 Bottom, 2 Front, 3 Back, 4 Left, 5 Right
        GreedyMeshFace(0, minY, maxY, mask);
        GreedyMeshFace(1, minY, maxY, mask);
        GreedyMeshFace(2, minY, maxY, mask);
        GreedyMeshFace(3, minY, maxY, mask);
        GreedyMeshFace(4, minY, maxY, mask);
        GreedyMeshFace(5, minY, maxY, mask);

        mask.Dispose();
    }

    private void GreedyMeshFace(int face, int minY, int maxY, NativeArray<int> mask)
    {
        // For each slice along the face normal, build a 2D mask and merge quads.
        if (face == 0 || face == 1)
        {
            for (int y = minY; y <= maxY; y++)
            {
                ClearMask(mask);
                for (int x = 0; x < Width; x++)
                {
                    for (int z = 0; z < Depth; z++)
                        mask[x + z * Width] = GetFaceType(x, y, z, face);
                }

                EmitGreedyQuads(face, y, mask, Width, Depth);
            }
        }
        else if (face == 2 || face == 3)
        {
            for (int z = 0; z < Depth; z++)
            {
                ClearMask(mask);
                for (int x = 0; x < Width; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        int yLocal = y - minY;
                        mask[x + yLocal * Width] = GetFaceType(x, y, z, face);
                    }
                }

                EmitGreedyQuads(face, z, mask, Width, SectionSize, minY);
            }
        }
        else // Left / Right
        {
            for (int x = 0; x < Width; x++)
            {
                ClearMask(mask);
                for (int z = 0; z < Depth; z++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        int yLocal = y - minY;
                        mask[z + yLocal * Depth] = GetFaceType(x, y, z, face);
                    }
                }

                EmitGreedyQuads(face, x, mask, Depth, SectionSize, minY);
            }
        }
    }

    private void EmitGreedyQuads(int face, int slice, NativeArray<int> mask, int maskW, int maskH, int minY = 0)
    {
        for (int v = 0; v < maskH; v++)
        {
            for (int u = 0; u < maskW;)
            {
                int type = mask[u + v * maskW];
                if (type == 0)
                {
                    u++;
                    continue;
                }

                int quadW = 1;
                while (u + quadW < maskW && mask[(u + quadW) + v * maskW] == type)
                    quadW++;

                int quadH = 1;
                bool done = false;
                while (v + quadH < maskH)
                {
                    for (int k = 0; k < quadW; k++)
                    {
                        if (mask[(u + k) + (v + quadH) * maskW] != type)
                        {
                            done = true;
                            break;
                        }
                    }

                    if (done)
                        break;

                    quadH++;
                }

                for (int dh = 0; dh < quadH; dh++)
                {
                    for (int dw = 0; dw < quadW; dw++)
                        mask[(u + dw) + (v + dh) * maskW] = 0;
                }

                DecodeTile(type, out int tileX, out int tileY);
                AddGreedyQuad(face, slice, u, v, quadW, quadH, tileX, tileY, minY);
                u += quadW;
            }
        }
    }

    private void AddGreedyQuad(
        int face,
        int slice,
        int u,
        int v,
        int quadW,
        int quadH,
        int tileX,
        int tileY,
        int minY)
    {
        float uvXMin = tileX * NormalizedTileWidth;
        float uvYMin = 1f - (tileY + 1) * NormalizedTileHeight;
        float2 atlasOrigin = new float2(uvXMin, uvYMin);

        float3 v0, v1, v2, v3;
        float2 t0, t1, t2, t3;
        bool flipWinding = false;

        switch (face)
        {
            case 0: // Top +Y
                v0 = new float3(u, slice + 1, v);
                v1 = new float3(u, slice + 1, v + quadH);
                v2 = new float3(u + quadW, slice + 1, v + quadH);
                v3 = new float3(u + quadW, slice + 1, v);
                t0 = new float2(0, 0);
                t1 = new float2(0, quadH);
                t2 = new float2(quadW, quadH);
                t3 = new float2(quadW, 0);
                break;

            case 1: // Bottom -Y
                v0 = new float3(u, slice, v + quadH);
                v1 = new float3(u, slice, v);
                v2 = new float3(u + quadW, slice, v);
                v3 = new float3(u + quadW, slice, v + quadH);
                t0 = new float2(0, quadH);
                t1 = new float2(0, 0);
                t2 = new float2(quadW, 0);
                t3 = new float2(quadW, quadH);
                break;

            case 2: // Front +Z
            {
                int y = minY + v;
                v0 = new float3(u, y, slice + 1);
                v1 = new float3(u, y + quadH, slice + 1);
                v2 = new float3(u + quadW, y + quadH, slice + 1);
                v3 = new float3(u + quadW, y, slice + 1);
                t0 = new float2(0, 0);
                t1 = new float2(0, quadH);
                t2 = new float2(quadW, quadH);
                t3 = new float2(quadW, 0);
                flipWinding = true;
                break;
            }

            case 3: // Back -Z
            {
                int y = minY + v;
                v0 = new float3(u + quadW, y, slice);
                v1 = new float3(u + quadW, y + quadH, slice);
                v2 = new float3(u, y + quadH, slice);
                v3 = new float3(u, y, slice);
                t0 = new float2(0, 0);
                t1 = new float2(0, quadH);
                t2 = new float2(quadW, quadH);
                t3 = new float2(quadW, 0);
                flipWinding = true;
                break;
            }

            case 5: // Right +X
            {
                int y = minY + v;
                // mask u = z, mask v = yLocal. Matches old 1x1: (1,0,1),(1,1,1),(1,1,0),(1,0,0)
                v0 = new float3(slice + 1, y, u + quadW);
                v1 = new float3(slice + 1, y + quadH, u + quadW);
                v2 = new float3(slice + 1, y + quadH, u);
                v3 = new float3(slice + 1, y, u);
                t0 = new float2(0, 0);
                t1 = new float2(0, quadH);
                t2 = new float2(quadW, quadH);
                t3 = new float2(quadW, 0);
                flipWinding = true;
                break;
            }

            default: // 4 Left -X
            {
                int y = minY + v;
                // Matches old 1x1: (0,0,0),(0,1,0),(0,1,1),(0,0,1)
                v0 = new float3(slice, y, u);
                v1 = new float3(slice, y + quadH, u);
                v2 = new float3(slice, y + quadH, u + quadW);
                v3 = new float3(slice, y, u + quadW);
                t0 = new float2(0, 0);
                t1 = new float2(0, quadH);
                t2 = new float2(quadW, quadH);
                t3 = new float2(quadW, 0);
                flipWinding = true;
                break;
            }
        }

        int index = Vertices.Length;
        Vertices.Add(v0);
        Vertices.Add(v1);
        Vertices.Add(v2);
        Vertices.Add(v3);
        UVs.Add(t0);
        UVs.Add(t1);
        UVs.Add(t2);
        UVs.Add(t3);
        UV1s.Add(atlasOrigin);
        UV1s.Add(atlasOrigin);
        UV1s.Add(atlasOrigin);
        UV1s.Add(atlasOrigin);

        if (flipWinding)
        {
            // Match previous side-face winding: (0,2,1) (0,3,2)
            Triangles.Add(index + 0);
            Triangles.Add(index + 2);
            Triangles.Add(index + 1);
            Triangles.Add(index + 0);
            Triangles.Add(index + 3);
            Triangles.Add(index + 2);
        }
        else
        {
            Triangles.Add(index + 0);
            Triangles.Add(index + 1);
            Triangles.Add(index + 2);
            Triangles.Add(index + 0);
            Triangles.Add(index + 2);
            Triangles.Add(index + 3);
        }
    }

    private int GetFaceType(int x, int y, int z, int face)
    {
        byte blockId = Blocks[ToIndex(x, y, z)];
        if (IsSolid[blockId] == 0)
            return 0;

        int nx = x;
        int ny = y;
        int nz = z;
        if (face == 0) ny++;
        else if (face == 1) ny--;
        else if (face == 2) nz++;
        else if (face == 3) nz--;
        else if (face == 5) nx++;
        else nx--;

        if (IsNeighborSolid(nx, ny, nz))
            return 0;

        int tileIndex = blockId * FaceCount + face;
        int tileX = FaceTileX[tileIndex];
        int tileY = FaceTileY[tileIndex];
        if (tileX < 0 || tileY < 0)
            return 0;

        return EncodeTile(tileX, tileY);
    }

    private static int EncodeTile(int tileX, int tileY)
    {
        return (tileX + 1) | ((tileY + 1) << 16);
    }

    private static void DecodeTile(int type, out int tileX, out int tileY)
    {
        tileX = (type & 0xFFFF) - 1;
        tileY = ((type >> 16) & 0xFFFF) - 1;
    }

    private static void ClearMask(NativeArray<int> mask)
    {
        for (int i = 0; i < mask.Length; i++)
            mask[i] = 0;
    }

    private static int ToIndex(int x, int y, int z)
    {
        return x + Width * (y + Height * z);
    }

    private bool SectionHasSolid(int minY, int maxY)
    {
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int z = 0; z < Depth; z++)
                {
                    if (IsSolid[Blocks[ToIndex(x, y, z)]] != 0)
                        return true;
                }
            }
        }

        return false;
    }

    private bool IsNeighborSolid(int x, int y, int z)
    {
        if (y < 0)
            return true;
        if (y >= Height)
            return false;

        if (x >= 0 && x < Width && z >= 0 && z < Depth)
            return IsSolid[Blocks[ToIndex(x, y, z)]] != 0;

        NativeArray<byte> neighbor = default;
        int localX = x;
        int localZ = z;
        bool hasNeighbor = false;

        if (x < 0)
        {
            neighbor = NeighborNegX;
            localX = Width - 1;
            hasNeighbor = NeighborNegX.Length > 0;
        }
        else if (x >= Width)
        {
            neighbor = NeighborPosX;
            localX = 0;
            hasNeighbor = NeighborPosX.Length > 0;
        }
        else if (z < 0)
        {
            neighbor = NeighborNegZ;
            localZ = Depth - 1;
            hasNeighbor = NeighborNegZ.Length > 0;
        }
        else if (z >= Depth)
        {
            neighbor = NeighborPosZ;
            localZ = 0;
            hasNeighbor = NeighborPosZ.Length > 0;
        }

        if (!hasNeighbor)
            return false;

        return IsSolid[neighbor[ToIndex(localX, y, localZ)]] != 0;
    }
}
