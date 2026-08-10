using System;
using Unity.Collections;
using UnityEngine;

public class Chunk
{
    public static readonly int ChunkWidth = 16;
    public static readonly int ChunkHeight = 256;
    public static readonly int ChunkDepth = 16;
    public static readonly int BlockCount = ChunkWidth * ChunkHeight * ChunkDepth;

    private readonly byte[] _blockIDs;

    public Chunk()
    {
        _blockIDs = new byte[BlockCount];
    }

    public static int ToIndex(int x, int y, int z)
    {
        return x + ChunkWidth * (y + ChunkHeight * z);
    }

    public byte GetBlock(int x, int y, int z)
    {
        if (!IsPositionInBounds(x, y, z))
            return 0;

        return _blockIDs[ToIndex(x, y, z)];
    }

    public void SetBlock(int x, int y, int z, byte blockID)
    {
        if (!IsPositionInBounds(x, y, z))
            return;

        _blockIDs[ToIndex(x, y, z)] = blockID;
    }

    public bool IsPositionInBounds(int x, int y, int z)
    {
        return x >= 0 && x < ChunkWidth &&
               y >= 0 && y < ChunkHeight &&
               z >= 0 && z < ChunkDepth;
    }

    public Chunk Clone()
    {
        Chunk clone = new Chunk();
        Array.Copy(_blockIDs, clone._blockIDs, _blockIDs.Length);
        return clone;
    }

    public void CopyTo(NativeArray<byte> destination)
    {
        if (!destination.IsCreated || destination.Length != BlockCount)
            throw new ArgumentException("NativeArray must be created with Chunk.BlockCount length.");

        destination.CopyFrom(_blockIDs);
    }

    public void CopyFrom(NativeArray<byte> source)
    {
        if (!source.IsCreated || source.Length != BlockCount)
            throw new ArgumentException("NativeArray must be created with Chunk.BlockCount length.");

        source.CopyTo(_blockIDs);
    }

    public static Chunk FromNative(NativeArray<byte> source)
    {
        Chunk chunk = new Chunk();
        chunk.CopyFrom(source);
        return chunk;
    }
}
