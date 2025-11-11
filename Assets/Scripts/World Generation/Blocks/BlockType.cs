using TreeEditor;
using Unity.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "BlockType", menuName = "Scriptable Objects/BlockType")]
public class BlockType : ScriptableObject
{
    [Tooltip("A unique ID for this block. (0 is usually 'Air')")]
    [SerializeField]
    private byte _blockID;

    [Tooltip("A human-readable name for debugging.")]
    [SerializeField]
    private string _blockName;

    [Tooltip("Is this block a solid cube that collides and blocks vision?")]
    [SerializeField]
    private bool _isSolid;

    [Tooltip("Does this block stop light, or does it pass through (like glass or leaves)?")]
    [SerializeField]
    private bool _isTransparent;

    // --- New Texture Mapping Section ---

    [Tooltip("The 'side' texture for all faces. (Top, Bottom, Left, Right, etc.)")]
    [SerializeField]
    private TextureAtlasCoord _sideTexture;

    [Tooltip("Overrides the top texture. If null, uses _sideTexture.")]
    [SerializeField]
    private TextureAtlasCoord _topTexture;

    [Tooltip("Overrides the bottom texture. If null, uses _sideTexture.")]
    [SerializeField]
    private TextureAtlasCoord _bottomTexture;

    // --- Public Getters ---

    public byte BlockID => _blockID;
    public string BlockName => _blockName;
    public bool IsSolid => _isSolid;
    public bool IsTransparent => _isTransparent;

    public TextureAtlasCoord GetTextureCoords(BlockFace face)
    {
        switch (face)
        {
            case BlockFace.Top:
                return _topTexture ?? _sideTexture; // ?? means "if _topTexture is null, use _sideTexture"
            case BlockFace.Bottom:
                return _bottomTexture ?? _sideTexture;
            default:
                return _sideTexture;
        }
    }

    [System.Serializable]
    public class TextureAtlasCoord
    {
        // Example: If your atlas is 4x4, Grass Top might be at (0, 3)
        [Tooltip("X coordinate in the atlas (from 0)")]
        public int X;
        [Tooltip("Y coordinate in the atlas (from 0)")]
        public int Y;
    }

    public enum BlockFace { Top, Bottom, Front, Back, Left, Right }

    // WIP:
    // - Texture atlas coordinates (e.g., GetTextureID(FaceDirection direction))
    // - Hardness (for mining)
    // - Sound type (for footsteps)
}
