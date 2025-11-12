// File: TextureAtlasManager.cs
using UnityEngine;
using static BlockType;

/// <summary>
/// A static utility class for converting
/// texture atlas coordinates into UV coordinates.
/// </summary>
public static class TextureAtlasManager
{
    // --- Private Static Fields ---
    // These will be set by the Initialize method
    private static int _atlasWidthInTiles;
    private static int _atlasHeightInTiles;

    private static float _normalizedTileWidth;
    private static float _normalizedTileHeight;

    // A pre-allocated array to return UVs in, avoiding 'new' calls
    private static Vector2[] _uvs = new Vector2[4];

    /// <summary>
    /// Call this once at game startup (from World.cs)
    /// to configure the manager with the atlas settings.
    /// </summary>
    /// <param name="atlasTexture">The Texture2D of the atlas file.</param>
    /// <param name="tileSize">The size of one tile in pixels (e.g., 16).</param>
    public static void Initialize(Texture2D atlasTexture, int tileSize)
    {
        if (atlasTexture == null)
        {
            Debug.LogError("TextureAtlasManager: Atlas Texture is null!");
            return;
        }

        // Calculate the atlas size in tiles
        _atlasWidthInTiles = atlasTexture.width / tileSize;
        _atlasHeightInTiles = atlasTexture.height / tileSize;

        // Calculate the normalized (0-1) size of a single tile
        _normalizedTileWidth = 1f / _atlasWidthInTiles;
        _normalizedTileHeight = 1f / _atlasHeightInTiles;

        Debug.Log($"TextureAtlasManager Initialized: Atlas is {_atlasWidthInTiles}x{_atlasHeightInTiles} tiles.");
    }

    /// <summary>
    /// Gets the four UV coordinates for a quad based on
    /// a block's texture atlas coordinates.
    /// </summary>
    /// <param name="coord">The (X, Y) coordinate from the BlockType.</param>
    /// <returns>An array of 4 Vector2s for the mesh quad.</returns>
    public static Vector2[] GetUVs(TextureAtlasCoord coord)
    {
        if (_atlasWidthInTiles == 0)
        {
            Debug.LogError("TextureAtlasManager not initialized! Call Initialize() first.");
            return new Vector2[4]; // Return empty array to avoid crashes
        }

        // Calculate the min/max UV coordinates
        float uvXMin = coord.X * _normalizedTileWidth;
        float uvXMax = (coord.X + 1) * _normalizedTileWidth;

        float uvYMin = 1.0f - (coord.Y + 1) * _normalizedTileHeight;
        float uvYMax = 1.0f - (coord.Y) * _normalizedTileHeight;

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