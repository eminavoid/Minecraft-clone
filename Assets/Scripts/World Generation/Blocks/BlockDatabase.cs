// File: BlockDatabase.cs
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A ScriptableObject that holds all BlockType definitions
/// and provides a fast, ID-based lookup.
/// </summary>
[CreateAssetMenu(fileName = "BlockDatabase", menuName = "MinecraftClone/Block Database")]
public class BlockDatabase : ScriptableObject
{
    [Tooltip("Assign all your BlockType ScriptableObject assets here.")]
    [SerializeField]
    private List<BlockType> _allBlockTypes;

    // The fast lookup dictionary, built at runtime.
    // We make it static so it's globally accessible after initialization.
    private static Dictionary<byte, BlockType> _blockDictionary;

    private static Dictionary<string, BlockType> _blockNameDictionary;

    public static BlockType Air { get; private set; }

    /// <summary>
    /// Call this once at game startup (e.g., from a GameManager).
    /// </summary>
    public void Initialize()
    {
        _blockDictionary = new Dictionary<byte, BlockType>();

        _blockNameDictionary = new Dictionary<string, BlockType>();

        foreach (BlockType block in _allBlockTypes)
        {
            if (_blockDictionary.ContainsKey(block.BlockID))
            {
                Debug.LogError($"BlockDatabase: Duplicate Block ID {block.BlockID} found on {block.name}.");
                continue;
            }
            _blockDictionary.Add(block.BlockID, block);

            if (_blockNameDictionary.ContainsKey(block.BlockName))
            {
                Debug.LogError($"BlockDatabase: Duplicate Block Name \"{block.BlockName}\" found on {block.name}.");
                continue;
            }
            _blockNameDictionary.Add(block.BlockName, block);

            // Find and store the 'Air' block
            if (block.BlockID == 0)
            {
                Air = block;
            }
        }

        if (Air == null)
        {
            Debug.LogError("BlockDatabase: Block ID 0 (Air) was not found!");
        }

        Debug.Log("BlockDatabase initialized.");
    }

    /// <summary>
    /// Gets a BlockType by its unique ID.
    /// This is a high-performance O(1) lookup.
    /// </summary>
    public static BlockType GetBlockType(byte id)
    {
        if (_blockDictionary.TryGetValue(id, out BlockType block))
        {
            return block;
        }

        // Return Air as a safe fallback for unknown IDs
        Debug.LogWarning($"GetBlockType: Unknown Block ID {id}. Returning Air.");
        return Air;
    }

    public static BlockType GetBlockType(string blockName)
    {
        if (_blockNameDictionary == null)
        {
            Debug.LogError("BlockDatabase not initialized!");
            return null;
        }

        if (_blockNameDictionary.TryGetValue(blockName, out BlockType block))
        {
            return block;
        }

        Debug.LogError($"GetBlockType: Unknown Block Name \"{blockName}\".");
        return Air; // Return Air as a safe fallback
    }
}