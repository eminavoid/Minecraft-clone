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

    [Tooltip("How many chunks to load in each direction (e.g., 8 = 17x17 grid).")]
    [SerializeField]
    private int _viewDistance = 8;


    // --- Private Fields ---
    private WorldGenerator _generator;
    private Vector2Int _currentPlayerChunk; // The chunk the player is in

    // --- Chunk Management ---
    private Dictionary<Vector2Int, Chunk> _chunkDataDictionary = new Dictionary<Vector2Int, Chunk>();
    private Dictionary<Vector2Int, GameObject> _chunkObjectDictionary = new Dictionary<Vector2Int, GameObject>();

    // --- NEW: Chunk loading/unloading queues ---
    private Queue<Vector2Int> _chunksToLoad = new Queue<Vector2Int>();
    private List<Vector2Int> _chunksToUnload = new List<Vector2Int>();

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
        Debug.Log("--- Generating initial spawn chunk ---");

        // 1. Generate and render only the center chunk (0,0)
        Chunk centerChunkData = GenerateAndRenderChunk(Vector2Int.zero);

        // 2. Spawn the player
        SpawnPlayer(centerChunkData, Vector2Int.zero);

        // 3. Set the player's current chunk
        _currentPlayerChunk = Vector2Int.zero;

        // 4. Load the initial chunks around the player
        UpdateLoadedChunks(true);
    }

    private void Update()
    {
        // Find the player's current chunk position
        Vector2Int playerChunk = GetChunkCoordsFromPosition(_playerController.transform.position);

        // If the player has moved to a new chunk, update the world
        if (playerChunk != _currentPlayerChunk)
        {
            _currentPlayerChunk = playerChunk;
            UpdateLoadedChunks(false);
        }

        // Process our loading queue to prevent lag
        // Load one chunk per frame
        if (_chunksToLoad.Count > 0)
        {
            Vector2Int coordsToLoad = _chunksToLoad.Dequeue();
            GenerateAndRenderChunk(coordsToLoad);
        }
    }

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

        _chunkObjectDictionary.Add(chunkCoords, chunkObject);

        // --- Step 3: Add Rendering Components ---
        ChunkRenderer chunkRenderer = chunkObject.AddComponent<ChunkRenderer>();
        MeshRenderer meshRenderer = chunkObject.GetComponent<MeshRenderer>();
        meshRenderer.material = _worldMaterial;

        // --- Step 4: Initialize! ---
        chunkRenderer.Initialize(chunkData, this);

        // --- Step 6: Update chunk eighbors ---
        UpdateNeighbors(chunkCoords);

        // --- Step 5: Return the data ---
        return chunkData;
    }

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

    public Chunk GetChunkData(Vector2Int chunkCoords)
    {
        _chunkDataDictionary.TryGetValue(chunkCoords, out Chunk chunk);
        return chunk;
    }

    public bool IsChunkLoaded(Vector2Int chunkCoords)
    {
        return _chunkDataDictionary.ContainsKey(chunkCoords);
    }

    private void UpdateLoadedChunks(bool isFirstLoad)
    {
        // 1. --- Unload Chunks ---
        // We'll store chunks to unload in a list first to avoid errors
        // by modifying the dictionary while looping.
        _chunksToUnload.Clear();
        foreach (Vector2Int loadedChunkCoords in _chunkObjectDictionary.Keys)
        {
            // Calculate distance (Manhattan distance is faster)
            int dist = Mathf.Abs(loadedChunkCoords.x - _currentPlayerChunk.x) +
                       Mathf.Abs(loadedChunkCoords.y - _currentPlayerChunk.y);

            // If it's too far, queue it for unloading
            if (dist > _viewDistance)
            {
                _chunksToUnload.Add(loadedChunkCoords);
            }
        }

        // Now, safely unload them
        foreach (Vector2Int coords in _chunksToUnload)
        {
            UnloadChunk(coords);
        }

        // 2. --- Load Chunks ---
        // Loop in a square around the player
        for (int x = -_viewDistance; x <= _viewDistance; x++)
        {
            for (int z = -_viewDistance; z <= _viewDistance; z++)
            {
                Vector2Int chunkCoords = new Vector2Int(
                    _currentPlayerChunk.x + x,
                    _currentPlayerChunk.y + z
                );

                // If this chunk isn't loaded and isn't already in the queue...
                if (!IsChunkLoaded(chunkCoords) && !_chunksToLoad.Contains(chunkCoords))
                {
                    // If it's the first time, load it instantly.
                    // Otherwise, add it to the queue.
                    if (isFirstLoad)
                    {
                        GenerateAndRenderChunk(chunkCoords);
                    }
                    else
                    {
                        _chunksToLoad.Enqueue(chunkCoords);
                    }
                }
            }
        }
    }

    private void UnloadChunk(Vector2Int chunkCoords)
    {
        UpdateNeighbors(chunkCoords);

        // 1. Destroy the GameObject
        if (_chunkObjectDictionary.TryGetValue(chunkCoords, out GameObject chunkObject))
        {
            Destroy(chunkObject);
            _chunkObjectDictionary.Remove(chunkCoords);
        }

        // 2. Remove the data
        if (_chunkDataDictionary.ContainsKey(chunkCoords))
        {
            _chunkDataDictionary.Remove(chunkCoords);
        }
    }

    public Chunk GetChunkFromWorldPos(Vector3 worldPos)
    {
        // First, convert the world pos to chunk coords
        Vector2Int chunkCoords = GetChunkCoordsFromPosition(worldPos);

        // Now, try to get the data from our dictionary
        _chunkDataDictionary.TryGetValue(chunkCoords, out Chunk chunk);
        return chunk;
    }

    private Vector2Int GetChunkCoordsFromPosition(Vector3 position)
    {
        int x = Mathf.FloorToInt(position.x / Chunk.ChunkWidth);
        int z = Mathf.FloorToInt(position.z / Chunk.ChunkDepth);
        return new Vector2Int(x, z);
    }

    private void UpdateChunk(Vector2Int chunkCoords)
    {

        // Check if the chunk is loaded and has a GameObject
        if (_chunkObjectDictionary.TryGetValue(chunkCoords, out GameObject chunkObject))
        {
            // Get its renderer and tell it to regenerate
            ChunkRenderer renderer = chunkObject.GetComponent<ChunkRenderer>();
            if (renderer != null)
            {
                renderer.GenerateMesh();
            }
        }
    }

    private void UpdateNeighbors(Vector2Int chunkCoords)
    {
        // Get the coordinates for all 4 neighbors
        Vector2Int front = new Vector2Int(chunkCoords.x, chunkCoords.y + 1);
        Vector2Int back = new Vector2Int(chunkCoords.x, chunkCoords.y - 1);
        Vector2Int right = new Vector2Int(chunkCoords.x + 1, chunkCoords.y);
        Vector2Int left = new Vector2Int(chunkCoords.x - 1, chunkCoords.y);

        // Tell each neighbor to update (if it's loaded)
        UpdateChunk(front);
        UpdateChunk(back);
        UpdateChunk(right);
        UpdateChunk(left);
    }
}