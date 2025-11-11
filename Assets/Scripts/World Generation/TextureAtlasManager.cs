// File: TextureAtlasManager.cs
using UnityEngine;
using static BlockType;

/// <summary>
/// A static utility class for converting
/// texture atlas coordinates into UV coordinates.
/// </summary>
public static class TextureAtlasManager
{
    // !! IMPORTANT !!
    // Change these to match your texture atlas.
    // I am guessing 20x12 based on your image.
    private static readonly int _atlasWidthInTiles = 64;
    private static readonly int _atlasHeightInTiles = 32;

    // The normalized size of a single tile (calculated separately)
    private static readonly float _normalizedTileWidth = 1f / _atlasWidthInTiles;
    private static readonly float _normalizedTileHeight = 1f / _atlasHeightInTiles;

    // A pre-allocated array to return UVs in, avoiding 'new' calls
    private static Vector2[] _uvs = new Vector2[4];

    /// <summary>
    /// Gets the four UV coordinates for a quad based on
    /// a block's texture atlas coordinates.
    /// </summary>
    /// <param name="coord">The (X, Y) coordinate from the BlockType.</param>
    /// <returns>An array of 4 Vector2s for the mesh quad.</returns>
    public static Vector2[] GetUVs(TextureAtlasCoord coord)
    {
        // Calculate the min/max UV coordinates
        // Y is inverted: Atlas (0,0) is top-left,
        // UV (0,0) is bottom-left.

        // --- THIS IS THE FIXED LOGIC ---
        // Use width for X, height for Y
        float uvXMin = coord.X * _normalizedTileWidth;
        float uvXMax = (coord.X + 1) * _normalizedTileWidth;

        float uvYMin = 1.0f - (coord.Y + 1) * _normalizedTileHeight;
        float uvYMax = 1.0f - (coord.Y) * _normalizedTileHeight;
        // --- END FIXED LOGIC ---


        // Follow the vertex order:
        // 0: Bottom-Left (MinX, MinY)
        // 1: Top-Left (MinX, MaxY)
        // 2: Top-Right (MaxX, MaxY)
        // 3: Bottom-Right (MaxX, MinY)

        _uvs[0] = new Vector2(uvXMin, uvYMin);
        _uvs[1] = new Vector2(uvXMin, uvYMax);
        _uvs[2] = new Vector2(uvXMax, uvYMax);
        _uvs[3] = new Vector2(uvXMax, uvYMin);

        return _uvs;
    }
}