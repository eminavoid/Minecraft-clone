// File: World.cs
using UnityEngine;
using System.Collections.Generic; // We need this for Dictionaries

/// <summary>
/// The main "manager" script that controls the world.
/// It spawns and manages chunks.
/// </summary>
public class World : MonoBehaviour
{
    [Header("World Assets")]
    [Tooltip("Assign your BlockDatabase.asset here.")]
    [SerializeField]
    private BlockDatabase _blockDatabase;

    [Tooltip("Assign your WorldMaterial.mat (with the texture atlas) here.")]
    [SerializeField]
    private Material _worldMaterial;

    [Tooltip("Assign your Player's CharacterController component here.")]
    [SerializeField]
    private CharacterController _playerController;

    [Header("World Settings")]
    [Tooltip("The seed for your procedural world.")]
    [SerializeField]
    private int _worldSeed = 12345;

    // --- Private Fields ---
    private WorldGenerator _generator;

    // --- NEW CHUNK MANAGEMENT ---
    // These dictionaries will track all active chunks

    [Tooltip("Tracks all loaded chunk data.")]
    private Dictionary<Vector2Int, Chunk> _chunkDataDictionary = new Dictionary<Vector2Int, Chunk>();

    [Tooltip("Tracks all loaded chunk GameObjects.")]
    private Dictionary<Vector2Int, GameObject> _chunkObjectDictionary = new Dictionary<Vector2Int, GameObject>();
    // ---

    private void Awake()
    {
        // 1. Initialize the Block Database
        if (_blockDatabase == null)
        {
            Debug.LogError("World: BlockDatabase is not assigned!");
            return;
        }
        _blockDatabase.Initialize();

        // 2. Create our generator instance
        _generator = new WorldGenerator(_worldSeed);
    }

    // --- THIS METHOD IS UPDATED ---
    private void Start()
    {
        Debug.Log("--- Generating 5x5 initial world ---");

        // Loop from -2 to 2 (total of 5)
        for (int x = -2; x <= 2; x++)
        {
            // Loop from -2 to 2 (total of 5)
            for (int z = -2; z <= 2; z++)
            {
                // Generate and render each chunk
                GenerateAndRenderChunk(new Vector2Int(x, z));
            }
        }

        Debug.Log("--- Initial world generation complete ---");

        // Now spawn the player at the center (0,0) chunk
        // We get the data from our new dictionary
        if (_chunkDataDictionary.TryGetValue(Vector2Int.zero, out Chunk centerChunkData))
        {
            SpawnPlayer(centerChunkData, Vector2Int.zero);
        }
        else
        {
            Debug.LogError("Center chunk (0,0) was not generated! Cannot spawn player.");
        }
    }

    // --- THIS METHOD IS UPDATED ---
    /// <summary>
    /// Creates data, builds GameObject, and renders a single chunk.
    /// Now stores the chunk in dictionaries.
    /// </summary>
    private Chunk GenerateAndRenderChunk(Vector2Int chunkCoords)
    {
        // --- Step 1: Create Chunk Data ---
        Chunk chunkData = new Chunk();
        _generator.GenerateChunk(chunkData, chunkCoords);

        // --- NEW: Store data in dictionary ---
        _chunkDataDictionary.Add(chunkCoords, chunkData);

        // --- Step 2: Create GameObject ---
        string chunkName = $"Chunk ({chunkCoords.x}, {chunkCoords.y})";
        GameObject chunkObject = new GameObject(chunkName);

        chunkObject.transform.position = new Vector3(
            chunkCoords.x * Chunk.ChunkWidth,
            0,
            chunkCoords.y * Chunk.ChunkDepth
        );
        chunkObject.transform.SetParent(this.transform);

        // --- NEW: Store GameObject in dictionary ---
        _chunkObjectDictionary.Add(chunkCoords, chunkObject);

        // --- Step 3: Add Rendering Components ---
        ChunkRenderer chunkRenderer = chunkObject.AddComponent<ChunkRenderer>();
        MeshRenderer meshRenderer = chunkObject.GetComponent<MeshRenderer>();
        meshRenderer.material = _worldMaterial;

        // --- Step 4: Initialize! ---
        chunkRenderer.Initialize(chunkData);

        // --- Step 5: Return the data ---
        return chunkData;
    }

    // --- THIS METHOD IS UNCHANGED ---
    /// <summary>
    /// Finds a safe spawn point in the chunk and teleports the player.
    /// </summary>
    private void SpawnPlayer(Chunk chunk, Vector2Int chunkCoords)
    {
        if (_playerController == null)
        {
            Debug.LogError("PlayerController not assigned to World! Cannot spawn player.");
            return;
        }

        // 1. Define a spawn position (center of the chunk)
        int spawnX = Chunk.ChunkWidth / 2;
        int spawnZ = Chunk.ChunkDepth / 2;

        // 2. Find the highest solid block at that X,Z
        int spawnY = 0;
        for (int y = Chunk.ChunkHeight - 1; y >= 0; y--)
        {
            byte blockID = chunk.GetBlock(spawnX, y, spawnZ);
            if (BlockDatabase.GetBlockType(blockID).IsSolid)
            {
                spawnY = y;
                break;
            }
        }

        // 3. Calculate the final world-space position
        float worldX = spawnX + (chunkCoords.x * Chunk.ChunkWidth);
        float worldZ = spawnZ + (chunkCoords.y * Chunk.ChunkDepth);
        Vector3 spawnPosition = new Vector3(worldX, spawnY + 2f, worldZ);

        // 4. Teleport the player
        _playerController.enabled = false;
        _playerController.transform.position = spawnPosition;
        _playerController.enabled = true;

        Debug.Log($"Player spawned at {spawnPosition}");
    }

    // --- NEW HELPER METHODS (for our next step) ---

    /// <summary>
    /// Gets the data for a chunk at the given coordinates.
    /// </summary>
    /// <returns>Chunk data, or null if not loaded.</returns>
    public Chunk GetChunkData(Vector2Int chunkCoords)
    {
        _chunkDataDictionary.TryGetValue(chunkCoords, out Chunk chunk);
        return chunk;
    }

    /// <summary>
    /// Checks if a chunk is already loaded.
    /// </summary>
    public bool IsChunkLoaded(Vector2Int chunkCoords)
    {
        return _chunkDataDictionary.ContainsKey(chunkCoords);
    }
}