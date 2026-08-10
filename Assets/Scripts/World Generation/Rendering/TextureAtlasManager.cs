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
    private static int _atlasWidthInTiles;
    private static int _atlasHeightInTiles;

    private static float _normalizedTileWidth;
    private static float _normalizedTileHeight;

    // --- ¡FIX! Se eliminó el array estático `_uvs` ---

    public static float NormalizedTileWidth => _normalizedTileWidth;
    public static float NormalizedTileHeight => _normalizedTileHeight;

    /// <summary>
    /// Call this once at game startup (from World.cs)
    /// </summary>
    public static void Initialize(Texture2D atlasTexture, int tileSize)
    {
        if (atlasTexture == null)
        {
            Debug.LogError("TextureAtlasManager: Atlas Texture is null!");
            return;
        }

        _atlasWidthInTiles = atlasTexture.width / tileSize;
        _atlasHeightInTiles = atlasTexture.height / tileSize;

        _normalizedTileWidth = 1f / _atlasWidthInTiles;
        _normalizedTileHeight = 1f / _atlasHeightInTiles;

        Debug.Log($"TextureAtlasManager Initialized: Atlas is {_atlasWidthInTiles}x{_atlasHeightInTiles} tiles.");
    }

    /// <summary>
    /// Thread-safe UV lookup without allocating an array.
    /// </summary>
    public static void GetUVs(
        TextureAtlasCoord coord,
        out Vector2 uv0,
        out Vector2 uv1,
        out Vector2 uv2,
        out Vector2 uv3)
    {
        if (_atlasWidthInTiles == 0 || coord == null)
        {
            uv0 = uv1 = uv2 = uv3 = Vector2.zero;
            return;
        }

        float uvXMin = coord.X * _normalizedTileWidth;
        float uvXMax = (coord.X + 1) * _normalizedTileWidth;
        float uvYMin = 1.0f - (coord.Y + 1) * _normalizedTileHeight;
        float uvYMax = 1.0f - (coord.Y) * _normalizedTileHeight;

        uv0 = new Vector2(uvXMin, uvYMin);
        uv1 = new Vector2(uvXMin, uvYMax);
        uv2 = new Vector2(uvXMax, uvYMax);
        uv3 = new Vector2(uvXMax, uvYMin);
    }

    /// <summary>
    /// Thread-safe UV lookup. Prefer the out-parameter overload in hot paths.
    /// </summary>
    public static Vector2[] GetUVs(TextureAtlasCoord coord)
    {
        GetUVs(coord, out Vector2 uv0, out Vector2 uv1, out Vector2 uv2, out Vector2 uv3);
        return new Vector2[] { uv0, uv1, uv2, uv3 };
    }

    /// <summary>
    /// --- ¡NUEVO MÉTODO! ---
    /// Obtiene el Rect (de 0 a 1) para un RawImage.uvRect
    /// </summary>
    public static Rect GetUVRect(TextureAtlasCoord coord)
    {
        if (_atlasWidthInTiles == 0)
        {
            Debug.LogError("TextureAtlasManager not initialized!");
            return new Rect(0, 0, 1, 1);
        }

        float x = coord.X * _normalizedTileWidth;
        float y = 1.0f - (coord.Y + 1) * _normalizedTileHeight;
        float w = _normalizedTileWidth;
        float h = _normalizedTileHeight;

        return new Rect(x, y, w, h);
    }
}