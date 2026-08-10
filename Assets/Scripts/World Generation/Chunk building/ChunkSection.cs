using UnityEngine;

/// <summary>
/// Minecraft-style vertical sections inside a column chunk (16x16x16).
/// </summary>
public static class ChunkSection
{
    public const int Size = 16;

    public static readonly int CountY = Chunk.ChunkHeight / Size;

    /// <summary>Bitmask with every section bit set.</summary>
    public static readonly int AllMask = (1 << CountY) - 1;

    public static int IndexFromY(int y) => y / Size;

    public static int MinY(int sectionIndex) => sectionIndex * Size;

    public static int MaxY(int sectionIndex) => sectionIndex * Size + Size - 1;

    public static bool IsValidIndex(int sectionIndex)
    {
        return sectionIndex >= 0 && sectionIndex < CountY;
    }

    public static int Bit(int sectionIndex) => 1 << sectionIndex;

    public static bool ContainsBit(int mask, int sectionIndex)
    {
        return (mask & Bit(sectionIndex)) != 0;
    }
}
