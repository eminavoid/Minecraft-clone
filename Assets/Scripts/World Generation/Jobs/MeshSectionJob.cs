using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Face-culled meshing for one 16^3 section. Burst-compiled.
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

    public void Execute()
    {
        Vertices.Clear();
        Triangles.Clear();
        UVs.Clear();

        int minY = SectionIndex * SectionSize;
        int maxY = minY + SectionSize - 1;

        if (!SectionHasSolid(minY, maxY))
            return;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int z = 0; z < Depth; z++)
                {
                    byte blockId = Blocks[ToIndex(x, y, z)];
                    if (IsSolid[blockId] == 0)
                        continue;

                    float3 blockPos = new float3(x, y, z);

                    if (!IsNeighborSolid(x, y + 1, z))
                        AddFace(0, blockPos, blockId);
                    if (!IsNeighborSolid(x, y - 1, z))
                        AddFace(1, blockPos, blockId);
                    if (!IsNeighborSolid(x, y, z + 1))
                        AddFace(2, blockPos, blockId);
                    if (!IsNeighborSolid(x, y, z - 1))
                        AddFace(3, blockPos, blockId);
                    if (!IsNeighborSolid(x + 1, y, z))
                        AddFace(5, blockPos, blockId);
                    if (!IsNeighborSolid(x - 1, y, z))
                        AddFace(4, blockPos, blockId);
                }
            }
        }
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

    private void AddFace(int face, float3 blockPos, byte blockId)
    {
        int tileIndex = blockId * FaceCount + face;
        int tileX = FaceTileX[tileIndex];
        int tileY = FaceTileY[tileIndex];
        if (tileX < 0 || tileY < 0)
            return;

        float uvXMin = tileX * NormalizedTileWidth;
        float uvXMax = (tileX + 1) * NormalizedTileWidth;
        float uvYMin = 1f - (tileY + 1) * NormalizedTileHeight;
        float uvYMax = 1f - tileY * NormalizedTileHeight;

        float2 uv0 = new float2(uvXMin, uvYMin);
        float2 uv1 = new float2(uvXMin, uvYMax);
        float2 uv2 = new float2(uvXMax, uvYMax);
        float2 uv3 = new float2(uvXMax, uvYMin);

        int vIndex = Vertices.Length;

        switch (face)
        {
            case 0:
                Vertices.Add(blockPos + new float3(0, 1, 0));
                Vertices.Add(blockPos + new float3(0, 1, 1));
                Vertices.Add(blockPos + new float3(1, 1, 1));
                Vertices.Add(blockPos + new float3(1, 1, 0));
                Triangles.Add(vIndex + 0); Triangles.Add(vIndex + 1); Triangles.Add(vIndex + 2);
                Triangles.Add(vIndex + 0); Triangles.Add(vIndex + 2); Triangles.Add(vIndex + 3);
                UVs.Add(uv1); UVs.Add(uv0); UVs.Add(uv3); UVs.Add(uv2);
                break;

            case 1:
                Vertices.Add(blockPos + new float3(0, 0, 1));
                Vertices.Add(blockPos + new float3(0, 0, 0));
                Vertices.Add(blockPos + new float3(1, 0, 0));
                Vertices.Add(blockPos + new float3(1, 0, 1));
                Triangles.Add(vIndex + 0); Triangles.Add(vIndex + 1); Triangles.Add(vIndex + 2);
                Triangles.Add(vIndex + 0); Triangles.Add(vIndex + 2); Triangles.Add(vIndex + 3);
                UVs.Add(uv0); UVs.Add(uv1); UVs.Add(uv2); UVs.Add(uv3);
                break;

            case 2:
                Vertices.Add(blockPos + new float3(0, 0, 1));
                Vertices.Add(blockPos + new float3(0, 1, 1));
                Vertices.Add(blockPos + new float3(1, 1, 1));
                Vertices.Add(blockPos + new float3(1, 0, 1));
                Triangles.Add(vIndex + 0); Triangles.Add(vIndex + 2); Triangles.Add(vIndex + 1);
                Triangles.Add(vIndex + 0); Triangles.Add(vIndex + 3); Triangles.Add(vIndex + 2);
                UVs.Add(uv0); UVs.Add(uv1); UVs.Add(uv2); UVs.Add(uv3);
                break;

            case 3:
                Vertices.Add(blockPos + new float3(1, 0, 0));
                Vertices.Add(blockPos + new float3(1, 1, 0));
                Vertices.Add(blockPos + new float3(0, 1, 0));
                Vertices.Add(blockPos + new float3(0, 0, 0));
                Triangles.Add(vIndex + 0); Triangles.Add(vIndex + 2); Triangles.Add(vIndex + 1);
                Triangles.Add(vIndex + 0); Triangles.Add(vIndex + 3); Triangles.Add(vIndex + 2);
                UVs.Add(uv0); UVs.Add(uv1); UVs.Add(uv2); UVs.Add(uv3);
                break;

            case 5:
                Vertices.Add(blockPos + new float3(1, 0, 1));
                Vertices.Add(blockPos + new float3(1, 1, 1));
                Vertices.Add(blockPos + new float3(1, 1, 0));
                Vertices.Add(blockPos + new float3(1, 0, 0));
                Triangles.Add(vIndex + 0); Triangles.Add(vIndex + 2); Triangles.Add(vIndex + 1);
                Triangles.Add(vIndex + 0); Triangles.Add(vIndex + 3); Triangles.Add(vIndex + 2);
                UVs.Add(uv0); UVs.Add(uv1); UVs.Add(uv2); UVs.Add(uv3);
                break;

            case 4:
                Vertices.Add(blockPos + new float3(0, 0, 0));
                Vertices.Add(blockPos + new float3(0, 1, 0));
                Vertices.Add(blockPos + new float3(0, 1, 1));
                Vertices.Add(blockPos + new float3(0, 0, 1));
                Triangles.Add(vIndex + 0); Triangles.Add(vIndex + 2); Triangles.Add(vIndex + 1);
                Triangles.Add(vIndex + 0); Triangles.Add(vIndex + 3); Triangles.Add(vIndex + 2);
                UVs.Add(uv0); UVs.Add(uv1); UVs.Add(uv2); UVs.Add(uv3);
                break;
        }
    }
}
