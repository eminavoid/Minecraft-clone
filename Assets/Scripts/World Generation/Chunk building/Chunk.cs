using UnityEngine;

public class Chunk
{
    public static readonly int ChunkWidth = 16;
    public static readonly int ChunkHeight = 256;
    public static readonly int ChunkDepth = 16;

    private byte[,,] _blockIDs;

    public Chunk()
    {
        _blockIDs = new byte[ChunkWidth, ChunkHeight, ChunkDepth];
    }

    // --- Public API ---
    /// <param name="x">Local X (0 to ChunkWidth-1)</param>
    /// <param name="y">Local Y (0 to ChunkHeight-1)</param>
    /// <param name="z">Local Z (0 to ChunkDepth-1)</param>
    /// <param name="blockID">The new byte ID for this block.</param>

    public byte GetBlock(int x, int y, int z)
    {
        if (!IsPositionInBounds(x, y, z))
        {
            Debug.LogError($"GetBlock: Position ({x},{y},{z}) is out of bounds.");
            return 0;
        }
        return _blockIDs[x, y, z];
    }

    public void SetBlock(int x, int y, int z, byte blockID)
    {
        if (!IsPositionInBounds(x, y, z))
        {
            Debug.LogError($"SetBlock: Position ({x},{y},{z}) is out of bounds.");
            return;
        }
        _blockIDs[x, y, z] = blockID;
    }

    public bool IsPositionInBounds(int x, int y, int z)
    {
        return x >= 0 && x < ChunkWidth &&
               y >= 0 && y < ChunkHeight &&
               z >= 0 && z < ChunkDepth;
    }
}
