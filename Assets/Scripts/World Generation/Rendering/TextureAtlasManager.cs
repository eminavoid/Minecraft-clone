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
    /// --- ¡MÉTODO ACTUALIZADO Y THREAD-SAFE! ---
    /// Gets the four UV coordinates for a quad.
    /// </summary>
    public static Vector2[] GetUVs(TextureAtlasCoord coord)
    {
        if (_atlasWidthInTiles == 0)
        {
            Debug.LogError("TextureAtlasManager not initialized! Call Initialize() first.");
            return new Vector2[4]; // Devuelve un array vacío seguro
        }

        // --- ¡ESTE ES EL FIX! ---
        // Crea un *nuevo* array cada vez.
        // Esto es 100% thread-safe.
        Vector2[] newUVs = new Vector2[4];

        // (La matemática es la misma)
        float uvXMin = coord.X * _normalizedTileWidth;
        float uvXMax = (coord.X + 1) * _normalizedTileWidth;

        float uvYMin = 1.0f - (coord.Y + 1) * _normalizedTileHeight;
        float uvYMax = 1.0f - (coord.Y) * _normalizedTileHeight;

        // (Escribimos en el *nuevo* array)
        newUVs[0] = new Vector2(uvXMin, uvYMin);
        newUVs[1] = new Vector2(uvXMin, uvYMax);
        newUVs[2] = new Vector2(uvXMax, uvYMax);
        newUVs[3] = new Vector2(uvXMax, uvYMin);

        return newUVs; // <-- Retorna el nuevo array
    }
}