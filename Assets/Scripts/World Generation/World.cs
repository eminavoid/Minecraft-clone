// File: World.cs
using UnityEngine;
using System.Collections.Generic;

public class World : MonoBehaviour
{
    [Header("World Assets")]
    [SerializeField] private BlockDatabase _blockDatabase;
    [SerializeField] private Material _worldMaterial;
    [SerializeField] private CharacterController _playerController;
    [SerializeField] private Texture2D _textureAtlas;

    [Header("World Settings")]
    [SerializeField] private int _tileSize = 16;
    [SerializeField] private int _worldSeed = 12345;
    [SerializeField] private int _viewDistance = 8;

    [Header("Generator Settings")]
    [SerializeField] private float _noiseScale = 0.05f;
    [SerializeField] private int _baseTerrainHeight = 60;
    [SerializeField] private int _terrainAmplitude = 40;
    [SerializeField] private int _dirtLayerDepth = 3;

    // --- Private Fields ---
    private WorldGenerator _generator;
    private Vector2Int _currentPlayerChunk;
    private bool _playerHasSpawned = false;

    // --- Diccionarios y Colas (Single-Threaded) ---
    private Dictionary<Vector2Int, Chunk> _chunkDataDictionary = new Dictionary<Vector2Int, Chunk>();
    private Dictionary<Vector2Int, GameObject> _chunkObjectDictionary = new Dictionary<Vector2Int, GameObject>();

    private List<Vector2Int> _chunksToLoad = new List<Vector2Int>();
    private List<Vector2Int> _chunksToUnload = new List<Vector2Int>();
    private List<Vector2Int> _chunksToUpdate = new List<Vector2Int>();

    private void Awake()
    {
        // 1. Init Atlas Manager
        if (_textureAtlas == null)
        {
            Debug.LogError("World: Texture Atlas is not assigned!"); return;
        }
        TextureAtlasManager.Initialize(_textureAtlas, _tileSize);

        // 2. Init Block Database
        if (_blockDatabase == null)
        {
            Debug.LogError("World: BlockDatabase is not assigned!"); return;
        }
        _blockDatabase.Initialize();

        // 3. Create generator instance
        _generator = new WorldGenerator(
            _worldSeed, _noiseScale, _baseTerrainHeight, _terrainAmplitude, _dirtLayerDepth
        );
    }

    private void Start()
    {
        _currentPlayerChunk = GetChunkCoordsFromPosition(_playerController.transform.position);
        UpdateLoadedChunks();
    }

    private void Update()
    {
        // --- 1. Revisar movimiento del jugador ---
        Vector2Int playerChunk = GetChunkCoordsFromPosition(_playerController.transform.position);
        if (playerChunk != _currentPlayerChunk)
        {
            _currentPlayerChunk = playerChunk;
            UpdateLoadedChunks();
        }

        // --- 2. Procesar colas (1 por frame) ---
        if (_chunksToLoad.Count > 0)
        {
            // Cargar un chunk
            Vector2Int coordsToLoad = _chunksToLoad[0];
            _chunksToLoad.RemoveAt(0);
            LoadChunk(coordsToLoad);
        }
        else if (_chunksToUnload.Count > 0)
        {
            // Descargar un chunk
            Vector2Int coordsToUnload = _chunksToUnload[0];
            _chunksToUnload.RemoveAt(0);
            UnloadChunk(coordsToUnload);
        }
        else if (_chunksToUpdate.Count > 0)
        {
            // Actualizar un chunk
            Vector2Int coordsToUpdate = _chunksToUpdate[0];
            _chunksToUpdate.RemoveAt(0);
            UpdateChunk(coordsToUpdate);
        }
    }

    /// <summary>
    /// --- MÉTODO DE CARGA SIMPLE (Hilo Principal) ---
    /// </summary>
    private void LoadChunk(Vector2Int chunkCoords)
    {
        if (IsChunkLoaded(chunkCoords)) return; // Ya está cargado

        // --- 1. Generar Datos (CPU Pesado) ---
        Chunk newChunkData = new Chunk();
        float[,] noiseMap = _generator.GetNoiseMap(chunkCoords); // Rápido
        _generator.GenerateChunk(newChunkData, noiseMap); // Lento
        _chunkDataDictionary.Add(chunkCoords, newChunkData);

        // --- 2. Crear GameObject ---
        GameObject chunkObject = CreateChunkObject(chunkCoords);
        _chunkObjectDictionary.Add(chunkCoords, chunkObject);

        // --- 3. Generar Malla (CPU Muy Pesado) ---
        ChunkRenderer renderer = chunkObject.GetComponent<ChunkRenderer>();
        renderer.Initialize(newChunkData, this); // ¡Esto causa el lag!

        // --- 4. Actualizar Vecinos ---
        UpdateNeighbors(chunkCoords);

        // --- 5. Spawmear Jugador ---
        if (chunkCoords == Vector2Int.zero && !_playerHasSpawned)
        {
            SpawnPlayer(newChunkData, Vector2Int.zero);
            _playerHasSpawned = true;
        }
    }

    private GameObject CreateChunkObject(Vector2Int chunkCoords)
    {
        string chunkName = $"Chunk ({chunkCoords.x}, {chunkCoords.y})";
        GameObject chunkObject = new GameObject(chunkName);
        chunkObject.transform.position = new Vector3(
            chunkCoords.x * Chunk.ChunkWidth, 0, chunkCoords.y * Chunk.ChunkDepth
        );
        chunkObject.transform.SetParent(this.transform);
        chunkObject.AddComponent<ChunkRenderer>();
        chunkObject.GetComponent<MeshRenderer>().material = _worldMaterial;
        return chunkObject;
    }

    private void UpdateLoadedChunks()
    {
        // Descargar chunks
        foreach (Vector2Int loadedChunkCoords in _chunkObjectDictionary.Keys)
        {
            int dist = Mathf.Max(
                Mathf.Abs(loadedChunkCoords.x - _currentPlayerChunk.x),
                Mathf.Abs(loadedChunkCoords.y - _currentPlayerChunk.y)
            );

            if (dist > _viewDistance)
            {
                if (!_chunksToUnload.Contains(loadedChunkCoords))
                    _chunksToUnload.Add(loadedChunkCoords);
            }
        }

        // Cargar nuevos chunks
        for (int x = -_viewDistance; x <= _viewDistance; x++)
        {
            for (int z = -_viewDistance; z <= _viewDistance; z++)
            {
                Vector2Int chunkCoords = new Vector2Int(_currentPlayerChunk.x + x, _currentPlayerChunk.y + z);
                if (!IsChunkLoaded(chunkCoords) && !_chunksToLoad.Contains(chunkCoords))
                {
                    _chunksToLoad.Add(chunkCoords);
                }
            }
        }
    }

    private void UnloadChunk(Vector2Int chunkCoords)
    {
        if (_chunkObjectDictionary.TryGetValue(chunkCoords, out GameObject chunkObject))
        {
            Destroy(chunkObject);
            _chunkObjectDictionary.Remove(chunkCoords);
        }
        if (_chunkDataDictionary.ContainsKey(chunkCoords))
        {
            _chunkDataDictionary.Remove(chunkCoords);
        }
        UpdateNeighbors(chunkCoords);
    }

    /// <summary>
    /// Pone en cola a un chunk para ser reconstruido.
    /// </summary>
    private void UpdateChunk(Vector2Int chunkCoords)
    {
        if (IsChunkLoaded(chunkCoords) && !_chunksToUpdate.Contains(chunkCoords))
        {
            _chunksToUpdate.Add(chunkCoords);
        }
    }

    // --- (Mantén tus otros métodos: GetChunkData, IsChunkLoaded, etc.) ---

    public Chunk GetChunkData(Vector2Int chunkCoords)
    {
        _chunkDataDictionary.TryGetValue(chunkCoords, out Chunk chunk);
        return chunk;
    }
    public Chunk GetChunkFromWorldPos(Vector3 worldPos)
    {
        Vector2Int chunkCoords = GetChunkCoordsFromPosition(worldPos);
        _chunkDataDictionary.TryGetValue(chunkCoords, out Chunk chunk);
        return chunk;
    }
    public bool IsChunkLoaded(Vector2Int chunkCoords)
    {
        return _chunkDataDictionary.ContainsKey(chunkCoords);
    }
    public Vector2Int GetChunkCoordsFromPosition(Vector3 position)
    {
        int x = Mathf.FloorToInt(position.x / Chunk.ChunkWidth);
        int z = Mathf.FloorToInt(position.z / Chunk.ChunkDepth);
        return new Vector2Int(x, z);
    }
    public void SpawnPlayer(Chunk chunk, Vector2Int chunkCoords)
    {
        if (_playerController == null) return;
        int spawnX = Chunk.ChunkWidth / 2;
        int spawnZ = Chunk.ChunkDepth / 2;
        int spawnY = 0;
        for (int y = Chunk.ChunkHeight - 1; y >= 0; y--)
        {
            if (BlockDatabase.GetBlockType(chunk.GetBlock(spawnX, y, spawnZ)).IsSolid)
            {
                spawnY = y; break;
            }
        }
        Vector3 spawnPosition = new Vector3(
            spawnX + (chunkCoords.x * Chunk.ChunkWidth),
            spawnY + 2f,
            spawnZ + (chunkCoords.y * Chunk.ChunkDepth)
        );
        _playerController.enabled = false;
        _playerController.transform.position = spawnPosition;
        _playerController.enabled = true;
        Debug.Log($"Player spawned at {spawnPosition}");
    }
    private void UpdateNeighbors(Vector2Int chunkCoords)
    {
        UpdateChunk(new Vector2Int(chunkCoords.x, chunkCoords.y + 1));
        UpdateChunk(new Vector2Int(chunkCoords.x, chunkCoords.y - 1));
        UpdateChunk(new Vector2Int(chunkCoords.x + 1, chunkCoords.y));
        UpdateChunk(new Vector2Int(chunkCoords.x - 1, chunkCoords.y));
    }
}