// File: World.cs
using UnityEngine;

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

    [Header("World Settings")]
    [Tooltip("The seed for your procedural world.")]
    [SerializeField]
    private int _worldSeed = 12345;

    // --- Private Fields ---
    private WorldGenerator _generator;

    private void Awake()
    {
        // 1. Initialize the Block Database
        // This is critical. It builds the fast-lookup dictionary.
        if (_blockDatabase == null)
        {
            Debug.LogError("World: BlockDatabase is not assigned!");
            return;
        }
        _blockDatabase.Initialize();

        // 2. Create our generator instance
        _generator = new WorldGenerator(_worldSeed);
    }

    private void Start()
    {
        // 3. Generate and render our very first chunk at (0, 0)
        Debug.Log("--- Generating initial chunk ---");
        GenerateAndRenderChunk(Vector2Int.zero);
    }

    /// <summary>
    /// Creates the data and spawns the GameObject for a single chunk.
    /// </summary>
    private void GenerateAndRenderChunk(Vector2Int chunkCoords)
    {
        // --- Step 1: Create Chunk Data ---
        Chunk chunkData = new Chunk();

        // --- Step 2: Fill Data with Generator ---
        _generator.GenerateChunk(chunkData, chunkCoords);

        // --- Step 3: Create GameObject ---
        // Create a new, empty GameObject for this chunk
        string chunkName = $"Chunk ({chunkCoords.x}, {chunkCoords.y})";
        GameObject chunkObject = new GameObject(chunkName);

        // Position it correctly in the world
        chunkObject.transform.position = new Vector3(
            chunkCoords.x * Chunk.ChunkWidth,
            0,
            chunkCoords.y * Chunk.ChunkDepth // Note: Vector2.y maps to world Z
        );

        // Keep the hierarchy clean
        chunkObject.transform.SetParent(this.transform);



        // --- Step 4: Add Rendering Components ---
        // Add the ChunkRenderer (which also adds MeshFilter, MeshRenderer, MeshCollider)
        ChunkRenderer chunkRenderer = chunkObject.AddComponent<ChunkRenderer>();

        // Get the MeshRenderer and assign our single world material
        MeshRenderer meshRenderer = chunkObject.GetComponent<MeshRenderer>();
        meshRenderer.material = _worldMaterial;

        // --- Step 5: Initialize! ---
        // This is the most important call.
        // It tells the renderer to take the data and build the mesh.
        chunkRenderer.Initialize(chunkData);

        Debug.Log($"Successfully generated and rendered {chunkName}.");
    }
}