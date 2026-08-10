using Unity.Collections;
using UnityEngine;
using static BlockType;

/// <summary>
/// Burst-friendly block/atlas tables baked once on the main thread.
/// </summary>
public static class BurstBlockData
{
    public const int MaxBlockIds = 256;
    public const int FaceCount = 6;

    public static NativeArray<byte> IsSolid;
    public static NativeArray<int> FaceTileX;
    public static NativeArray<int> FaceTileY;
    public static float NormalizedTileWidth;
    public static float NormalizedTileHeight;
    public static bool IsInitialized { get; private set; }

    public static void Initialize()
    {
        Dispose();

        IsSolid = new NativeArray<byte>(MaxBlockIds, Allocator.Persistent);
        FaceTileX = new NativeArray<int>(MaxBlockIds * FaceCount, Allocator.Persistent);
        FaceTileY = new NativeArray<int>(MaxBlockIds * FaceCount, Allocator.Persistent);

        for (int id = 0; id < MaxBlockIds; id++)
        {
            IsSolid[id] = 0;
            for (int face = 0; face < FaceCount; face++)
            {
                int index = id * FaceCount + face;
                FaceTileX[index] = -1;
                FaceTileY[index] = -1;
            }
        }

        for (int id = 0; id < MaxBlockIds; id++)
        {
            BlockType type = BlockDatabase.GetBlockTypeSilent((byte)id);
            if (type == null)
                continue;

            IsSolid[id] = (byte)(type.IsSolid ? 1 : 0);

            WriteFace(id, (int)BlockFace.Top, type.GetTextureCoords(BlockFace.Top));
            WriteFace(id, (int)BlockFace.Bottom, type.GetTextureCoords(BlockFace.Bottom));
            WriteFace(id, (int)BlockFace.Front, type.GetTextureCoords(BlockFace.Front));
            WriteFace(id, (int)BlockFace.Back, type.GetTextureCoords(BlockFace.Back));
            WriteFace(id, (int)BlockFace.Left, type.GetTextureCoords(BlockFace.Left));
            WriteFace(id, (int)BlockFace.Right, type.GetTextureCoords(BlockFace.Right));
        }

        NormalizedTileWidth = TextureAtlasManager.NormalizedTileWidth;
        NormalizedTileHeight = TextureAtlasManager.NormalizedTileHeight;
        IsInitialized = true;
    }

    private static void WriteFace(int blockId, int face, TextureAtlasCoord coord)
    {
        int index = blockId * FaceCount + face;
        if (coord == null)
        {
            FaceTileX[index] = -1;
            FaceTileY[index] = -1;
            return;
        }

        FaceTileX[index] = coord.X;
        FaceTileY[index] = coord.Y;
    }

    public static void Dispose()
    {
        if (IsSolid.IsCreated) IsSolid.Dispose();
        if (FaceTileX.IsCreated) FaceTileX.Dispose();
        if (FaceTileY.IsCreated) FaceTileY.Dispose();
        IsInitialized = false;
    }
}
