using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class World : MonoBehaviour
{
    private class GenerateJobState
    {
        public Vector2Int Coords;
        public JobHandle Handle;
        public NativeArray<byte> Blocks;
        public bool Disposed;

        public void CompleteAndDispose()
        {
            if (Disposed)
                return;

            Handle.Complete();
            if (Blocks.IsCreated)
                Blocks.Dispose();
            Disposed = true;
        }
    }

    private class MeshJobState
    {
        public Vector2Int Coords;
        public int SectionMask;
        public JobHandle Handle;
        public NativeArray<byte> Blocks;
        public NativeArray<byte> NeighborPosZ;
        public NativeArray<byte> NeighborNegZ;
        public NativeArray<byte> NeighborPosX;
        public NativeArray<byte> NeighborNegX;
        public NativeList<float3>[] VerticesBySection;
        public NativeList<int>[] TrianglesBySection;
        public NativeList<float2>[] UVsBySection;
        public bool Disposed;

        public void CompleteAndDispose()
        {
            if (Disposed)
                return;

            Handle.Complete();
            DisposeNatives();
            Disposed = true;
        }

        public void DisposeNatives()
        {
            if (Blocks.IsCreated) Blocks.Dispose();
            if (NeighborPosZ.IsCreated) NeighborPosZ.Dispose();
            if (NeighborNegZ.IsCreated) NeighborNegZ.Dispose();
            if (NeighborPosX.IsCreated) NeighborPosX.Dispose();
            if (NeighborNegX.IsCreated) NeighborNegX.Dispose();

            if (VerticesBySection != null)
            {
                for (int i = 0; i < VerticesBySection.Length; i++)
                {
                    if (VerticesBySection[i].IsCreated) VerticesBySection[i].Dispose();
                    if (TrianglesBySection[i].IsCreated) TrianglesBySection[i].Dispose();
                    if (UVsBySection[i].IsCreated) UVsBySection[i].Dispose();
                }
            }
        }
    }

    private struct SectionRef
    {
        public Vector2Int Chunk;
        public int Section;

        public SectionRef(Vector2Int chunk, int section)
        {
            Chunk = chunk;
            Section = section;
        }
    }

    [Header("World Assets")]
    [SerializeField] private BlockDatabase _blockDatabase;
    [SerializeField] private Material _worldMaterial;
    [SerializeField] private CharacterController _playerController;
    [SerializeField] private Texture2D _textureAtlas;

    [Header("World Settings")]
    [SerializeField] private int _tileSize = 16;
    [SerializeField] private int _worldSeed = 12345;
    [SerializeField] private int _viewDistance = 8;

    [Header("Async Pipeline")]
    [Tooltip("Max concurrent generate/mesh Unity jobs.")]
    [SerializeField] private int _maxWorkerJobs = 6;

    [Header("Collider Settings")]
    [SerializeField] private int _colliderDistance = 2;
    [SerializeField] private int _colliderSectionRadius = 1;

    [Header("Game Tick Settings")]
    [SerializeField] private float _gameTickRate = 20.0f;

    private float _tickInterval;
    private float _tickTimer;

    [Header("Generator Settings")]
    [SerializeField] private float _noiseScale = 0.05f;
    [SerializeField] private int _baseTerrainHeight = 60;
    [SerializeField] private int _terrainAmplitude = 40;
    [SerializeField] private int _dirtLayerDepth = 3;

    private byte _airID;
    private byte _grassID;
    private byte _dirtID;
    private byte _stoneID;

    private Vector2Int _currentPlayerChunk;
    private int _currentPlayerSection;
    private bool _playerHasSpawned;
    private bool _isShuttingDown;

    private readonly Dictionary<Vector2Int, Chunk> _chunkDataDictionary = new Dictionary<Vector2Int, Chunk>();
    private readonly Dictionary<Vector2Int, GameObject> _chunkObjectDictionary = new Dictionary<Vector2Int, GameObject>();

    private readonly List<Vector2Int> _chunksToLoad = new List<Vector2Int>();
    private readonly List<Vector2Int> _chunksToUnload = new List<Vector2Int>();
    private readonly List<Vector2Int> _chunksToMesh = new List<Vector2Int>();
    private readonly List<SectionRef> _sectionsToBakeCollider = new List<SectionRef>();

    private readonly HashSet<Vector2Int> _generatingChunks = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> _meshingChunks = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, int> _dirtySectionMasks = new Dictionary<Vector2Int, int>();
    private readonly HashSet<long> _pendingColliderBakes = new HashSet<long>();

    private readonly List<GenerateJobState> _activeGenerateJobs = new List<GenerateJobState>();
    private readonly List<MeshJobState> _activeMeshJobs = new List<MeshJobState>();
    private readonly List<ChunkRenderData> _pendingMeshUploads = new List<ChunkRenderData>();

    private int ActiveJobCount => _activeGenerateJobs.Count + _activeMeshJobs.Count;

    private void Awake()
    {
        if (_textureAtlas == null)
        {
            Debug.LogError("World: Texture Atlas is not assigned!");
            return;
        }
        TextureAtlasManager.Initialize(_textureAtlas, _tileSize);

        if (_blockDatabase == null)
        {
            Debug.LogError("World: BlockDatabase is not assigned!");
            return;
        }
        _blockDatabase.Initialize();
        BurstBlockData.Initialize();

        _airID = BlockDatabase.GetBlockType("Air").BlockID;
        _grassID = BlockDatabase.GetBlockType("Grass").BlockID;
        _dirtID = BlockDatabase.GetBlockType("Dirt").BlockID;
        _stoneID = BlockDatabase.GetBlockType("Stone").BlockID;

        if (_maxWorkerJobs < 1)
            _maxWorkerJobs = 1;
        if (_colliderDistance < 0)
            _colliderDistance = 0;
        if (_colliderSectionRadius < 0)
            _colliderSectionRadius = 0;
    }

    private void Start()
    {
        _tickInterval = 1.0f / _gameTickRate;
        _tickTimer = 0.0f;
        _currentPlayerChunk = GetChunkCoordsFromPosition(_playerController.transform.position);
        _currentPlayerSection = GetPlayerSection();
        UpdateLoadedChunks();
    }

    private void OnDestroy()
    {
        _isShuttingDown = true;

        for (int i = 0; i < _activeGenerateJobs.Count; i++)
            _activeGenerateJobs[i].CompleteAndDispose();
        _activeGenerateJobs.Clear();

        for (int i = 0; i < _activeMeshJobs.Count; i++)
            _activeMeshJobs[i].CompleteAndDispose();
        _activeMeshJobs.Clear();

        BurstBlockData.Dispose();
    }

    private void Update()
    {
        _tickTimer += Time.deltaTime;
        while (_tickTimer >= _tickInterval)
        {
            _tickTimer -= _tickInterval;
            Tick();
        }

        PollGenerateJobs();
        PollMeshJobs();
        ProcessMeshedResults();
        ProcessColliderBakes();

        if (_chunksToUnload.Count > 0)
        {
            Vector2Int coordsToUnload = _chunksToUnload[0];
            _chunksToUnload.RemoveAt(0);
            UnloadChunk(coordsToUnload);
        }

        StartPendingJobs();
    }

    private void Tick()
    {
        Vector2Int playerChunk = GetChunkCoordsFromPosition(_playerController.transform.position);
        int playerSection = GetPlayerSection();

        bool chunkChanged = playerChunk != _currentPlayerChunk;
        bool sectionChanged = playerSection != _currentPlayerSection;

        if (chunkChanged)
        {
            _currentPlayerChunk = playerChunk;
            UpdateLoadedChunks();
        }

        if (chunkChanged || sectionChanged)
        {
            _currentPlayerSection = playerSection;
            RefreshColliderStreaming();
        }
    }

    private int GetPlayerSection()
    {
        int y = Mathf.FloorToInt(_playerController.transform.position.y);
        y = Mathf.Clamp(y, 0, Chunk.ChunkHeight - 1);
        return ChunkSection.IndexFromY(y);
    }

    private void StartPendingJobs()
    {
        if (_chunksToMesh.Count > 1)
            SortChunksByDistanceToPlayer(_chunksToMesh);

        while (ActiveJobCount < _maxWorkerJobs)
        {
            if (TryStartMeshJob())
                continue;

            if (TryStartGenerateJob())
                continue;

            break;
        }
    }

    private bool TryStartGenerateJob()
    {
        while (_chunksToLoad.Count > 0)
        {
            Vector2Int coords = _chunksToLoad[0];
            _chunksToLoad.RemoveAt(0);

            if (IsChunkLoaded(coords) || _generatingChunks.Contains(coords))
                continue;

            if (!IsChunkInViewDistance(coords))
                continue;

            _generatingChunks.Add(coords);

            NativeArray<byte> blocks = new NativeArray<byte>(Chunk.BlockCount, Allocator.Persistent);
            GenerateChunkJob job = new GenerateChunkJob
            {
                ChunkCoordX = coords.x,
                ChunkCoordZ = coords.y,
                Seed = _worldSeed,
                NoiseScale = _noiseScale,
                BaseTerrainHeight = _baseTerrainHeight,
                TerrainAmplitude = _terrainAmplitude,
                DirtLayerDepth = _dirtLayerDepth,
                AirID = _airID,
                GrassID = _grassID,
                DirtID = _dirtID,
                StoneID = _stoneID,
                Blocks = blocks
            };

            GenerateJobState state = new GenerateJobState
            {
                Coords = coords,
                Blocks = blocks,
                Handle = job.Schedule()
            };
            _activeGenerateJobs.Add(state);
            return true;
        }

        return false;
    }

    private bool TryStartMeshJob()
    {
        while (_chunksToMesh.Count > 0)
        {
            Vector2Int coords = _chunksToMesh[0];
            _chunksToMesh.RemoveAt(0);

            if (_meshingChunks.Contains(coords))
                continue;

            if (!_chunkDataDictionary.TryGetValue(coords, out Chunk chunkData))
            {
                _dirtySectionMasks.Remove(coords);
                continue;
            }

            if (!IsChunkInViewDistance(coords))
            {
                _dirtySectionMasks.Remove(coords);
                continue;
            }

            if (!_dirtySectionMasks.TryGetValue(coords, out int sectionMask) || sectionMask == 0)
                continue;

            _dirtySectionMasks[coords] = 0;
            _meshingChunks.Add(coords);

            MeshJobState state = new MeshJobState
            {
                Coords = coords,
                SectionMask = sectionMask,
                Blocks = new NativeArray<byte>(Chunk.BlockCount, Allocator.Persistent),
                NeighborPosZ = CopyNeighborOrEmpty(new Vector2Int(coords.x, coords.y + 1)),
                NeighborNegZ = CopyNeighborOrEmpty(new Vector2Int(coords.x, coords.y - 1)),
                NeighborPosX = CopyNeighborOrEmpty(new Vector2Int(coords.x + 1, coords.y)),
                NeighborNegX = CopyNeighborOrEmpty(new Vector2Int(coords.x - 1, coords.y)),
                VerticesBySection = new NativeList<float3>[ChunkSection.CountY],
                TrianglesBySection = new NativeList<int>[ChunkSection.CountY],
                UVsBySection = new NativeList<float2>[ChunkSection.CountY]
            };

            chunkData.CopyTo(state.Blocks);

            NativeList<JobHandle> handles = new NativeList<JobHandle>(ChunkSection.CountY, Allocator.Temp);
            for (int section = 0; section < ChunkSection.CountY; section++)
            {
                if (!ChunkSection.ContainsBit(sectionMask, section))
                    continue;

                state.VerticesBySection[section] = new NativeList<float3>(256, Allocator.Persistent);
                state.TrianglesBySection[section] = new NativeList<int>(384, Allocator.Persistent);
                state.UVsBySection[section] = new NativeList<float2>(256, Allocator.Persistent);

                MeshSectionJob meshJob = new MeshSectionJob
                {
                    SectionIndex = section,
                    Blocks = state.Blocks,
                    NeighborPosZ = state.NeighborPosZ,
                    NeighborNegZ = state.NeighborNegZ,
                    NeighborPosX = state.NeighborPosX,
                    NeighborNegX = state.NeighborNegX,
                    IsSolid = BurstBlockData.IsSolid,
                    FaceTileX = BurstBlockData.FaceTileX,
                    FaceTileY = BurstBlockData.FaceTileY,
                    NormalizedTileWidth = BurstBlockData.NormalizedTileWidth,
                    NormalizedTileHeight = BurstBlockData.NormalizedTileHeight,
                    Vertices = state.VerticesBySection[section],
                    Triangles = state.TrianglesBySection[section],
                    UVs = state.UVsBySection[section]
                };

                handles.Add(meshJob.Schedule());
            }

            state.Handle = handles.Length > 0
                ? JobHandle.CombineDependencies(handles.AsArray())
                : default;
            handles.Dispose();

            _activeMeshJobs.Add(state);
            return true;
        }

        return false;
    }

    private NativeArray<byte> CopyNeighborOrEmpty(Vector2Int coords)
    {
        if (!_chunkDataDictionary.TryGetValue(coords, out Chunk neighbor))
            return new NativeArray<byte>(0, Allocator.Persistent);

        NativeArray<byte> copy = new NativeArray<byte>(Chunk.BlockCount, Allocator.Persistent);
        neighbor.CopyTo(copy);
        return copy;
    }

    private void PollGenerateJobs()
    {
        for (int i = _activeGenerateJobs.Count - 1; i >= 0; i--)
        {
            GenerateJobState state = _activeGenerateJobs[i];
            if (!state.Handle.IsCompleted)
                continue;

            state.Handle.Complete();
            _generatingChunks.Remove(state.Coords);

            if (!_isShuttingDown && IsChunkInViewDistance(state.Coords) && !IsChunkLoaded(state.Coords))
            {
                Chunk chunk = Chunk.FromNative(state.Blocks);
                _chunkDataDictionary.Add(state.Coords, chunk);
                QueueMesh(state.Coords, ChunkSection.AllMask);
                UpdateNeighbors(state.Coords);
            }

            if (state.Blocks.IsCreated)
                state.Blocks.Dispose();
            state.Disposed = true;
            _activeGenerateJobs.RemoveAt(i);
        }
    }

    private void PollMeshJobs()
    {
        for (int i = _activeMeshJobs.Count - 1; i >= 0; i--)
        {
            MeshJobState state = _activeMeshJobs[i];
            if (!state.Handle.IsCompleted)
                continue;

            state.Handle.Complete();
            _meshingChunks.Remove(state.Coords);

            if (!_isShuttingDown && IsChunkLoaded(state.Coords) && IsChunkInViewDistance(state.Coords))
            {
                List<SectionRenderData> sections = new List<SectionRenderData>(ChunkSection.CountY);
                for (int section = 0; section < ChunkSection.CountY; section++)
                {
                    if (!ChunkSection.ContainsBit(state.SectionMask, section))
                        continue;

                    sections.Add(SectionRenderData.FromNative(
                        section,
                        state.VerticesBySection[section],
                        state.TrianglesBySection[section],
                        state.UVsBySection[section]));
                }

                _pendingMeshUploads.Add(new ChunkRenderData(state.Coords, state.SectionMask, sections));
            }

            RequeueDirtyIfNeeded(state.Coords);
            state.DisposeNatives();
            state.Disposed = true;
            _activeMeshJobs.RemoveAt(i);
        }
    }

    private void ProcessMeshedResults()
    {
        if (_pendingMeshUploads.Count == 0)
            return;

        ChunkRenderData result = _pendingMeshUploads[0];
        _pendingMeshUploads.RemoveAt(0);

        if (_isShuttingDown)
            return;

        Vector2Int coords = result.ChunkCoords;
        if (!IsChunkLoaded(coords) || !IsChunkInViewDistance(coords))
            return;

        if (!_chunkObjectDictionary.TryGetValue(coords, out GameObject chunkObject))
        {
            chunkObject = CreateChunkObject(coords);
            _chunkObjectDictionary.Add(coords, chunkObject);

            ChunkRenderer newRenderer = chunkObject.GetComponent<ChunkRenderer>();
            newRenderer.Initialize(_chunkDataDictionary[coords], this, _worldMaterial);
        }

        ChunkRenderer renderer = chunkObject.GetComponent<ChunkRenderer>();
        renderer.ApplyRenderData(result);
        ScheduleCollidersForSections(coords, renderer, result);

        if (coords == Vector2Int.zero && !_playerHasSpawned)
        {
            for (int section = 0; section < ChunkSection.CountY; section++)
            {
                ChunkSectionRenderer sectionRenderer = renderer.GetSection(section);
                if (sectionRenderer != null && sectionRenderer.HasMesh && !sectionRenderer.HasCollider)
                    sectionRenderer.BakeCollider();
            }

            SpawnPlayer(_chunkDataDictionary[coords], Vector2Int.zero);
            _playerHasSpawned = true;
        }
    }

    private void RequeueDirtyIfNeeded(Vector2Int coords)
    {
        if (_dirtySectionMasks.TryGetValue(coords, out int mask) && mask != 0)
            QueueMesh(coords, mask);
    }

    private void ProcessColliderBakes()
    {
        if (_sectionsToBakeCollider.Count == 0)
            return;

        if (_sectionsToBakeCollider.Count > 1)
            SortSectionRefsByDistance(_sectionsToBakeCollider);

        SectionRef sectionRef = _sectionsToBakeCollider[0];
        _sectionsToBakeCollider.RemoveAt(0);
        _pendingColliderBakes.Remove(PackSectionKey(sectionRef.Chunk, sectionRef.Section));

        if (!ShouldSectionHaveCollider(sectionRef.Chunk, sectionRef.Section))
            return;

        if (!_chunkObjectDictionary.TryGetValue(sectionRef.Chunk, out GameObject chunkObject) || chunkObject == null)
            return;

        ChunkRenderer renderer = chunkObject.GetComponent<ChunkRenderer>();
        ChunkSectionRenderer section = renderer != null ? renderer.GetSection(sectionRef.Section) : null;
        if (section == null || !section.HasMesh || section.HasCollider)
            return;

        section.BakeCollider();
    }

    private void ScheduleCollidersForSections(Vector2Int coords, ChunkRenderer renderer, ChunkRenderData renderData)
    {
        if (!IsChunkInColliderDistance(coords))
        {
            renderer.ClearAllColliders();
            CancelColliderBakesForChunk(coords);
            return;
        }

        for (int i = 0; i < renderData.Sections.Length; i++)
        {
            SectionRenderData sectionData = renderData.Sections[i];
            if (sectionData == null)
                continue;

            ChunkSectionRenderer section = renderer.GetSection(sectionData.SectionIndex);
            if (section == null)
                continue;

            if (!section.HasMesh || !ShouldSectionHaveCollider(coords, sectionData.SectionIndex))
            {
                section.ClearCollider();
                CancelColliderBake(coords, sectionData.SectionIndex);
                continue;
            }

            bool isPlayerChunk = GetChebyshevDistance(coords, _currentPlayerChunk) == 0;
            bool nearPlayerSection = Mathf.Abs(sectionData.SectionIndex - _currentPlayerSection) <= _colliderSectionRadius;
            if (isPlayerChunk && nearPlayerSection)
            {
                CancelColliderBake(coords, sectionData.SectionIndex);
                section.BakeCollider();
            }
            else
            {
                QueueColliderBake(coords, sectionData.SectionIndex);
            }
        }
    }

    private void QueueColliderBake(Vector2Int coords, int sectionIndex)
    {
        if (!ShouldSectionHaveCollider(coords, sectionIndex))
            return;

        long key = PackSectionKey(coords, sectionIndex);
        if (!_pendingColliderBakes.Add(key))
            return;

        _sectionsToBakeCollider.Add(new SectionRef(coords, sectionIndex));
    }

    private void CancelColliderBake(Vector2Int coords, int sectionIndex)
    {
        long key = PackSectionKey(coords, sectionIndex);
        if (!_pendingColliderBakes.Remove(key))
            return;

        for (int i = _sectionsToBakeCollider.Count - 1; i >= 0; i--)
        {
            SectionRef sectionRef = _sectionsToBakeCollider[i];
            if (sectionRef.Chunk == coords && sectionRef.Section == sectionIndex)
                _sectionsToBakeCollider.RemoveAt(i);
        }
    }

    private void CancelColliderBakesForChunk(Vector2Int coords)
    {
        for (int i = _sectionsToBakeCollider.Count - 1; i >= 0; i--)
        {
            if (_sectionsToBakeCollider[i].Chunk != coords)
                continue;

            SectionRef sectionRef = _sectionsToBakeCollider[i];
            _sectionsToBakeCollider.RemoveAt(i);
            _pendingColliderBakes.Remove(PackSectionKey(sectionRef.Chunk, sectionRef.Section));
        }
    }

    private void RefreshColliderStreaming()
    {
        foreach (KeyValuePair<Vector2Int, GameObject> pair in _chunkObjectDictionary)
        {
            Vector2Int coords = pair.Key;
            GameObject chunkObject = pair.Value;
            if (chunkObject == null)
                continue;

            ChunkRenderer renderer = chunkObject.GetComponent<ChunkRenderer>();
            if (renderer == null)
                continue;

            for (int section = 0; section < ChunkSection.CountY; section++)
            {
                ChunkSectionRenderer sectionRenderer = renderer.GetSection(section);
                if (sectionRenderer == null || !sectionRenderer.HasMesh)
                    continue;

                if (ShouldSectionHaveCollider(coords, section))
                {
                    if (!sectionRenderer.HasCollider)
                        QueueColliderBake(coords, section);
                }
                else
                {
                    CancelColliderBake(coords, section);
                    sectionRenderer.ClearCollider();
                }
            }
        }
    }

    private bool ShouldSectionHaveCollider(Vector2Int chunkCoords, int sectionIndex)
    {
        if (!IsChunkInColliderDistance(chunkCoords))
            return false;

        return Mathf.Abs(sectionIndex - _currentPlayerSection) <= _colliderSectionRadius;
    }

    private bool IsChunkInColliderDistance(Vector2Int chunkCoords)
    {
        return GetChebyshevDistance(chunkCoords, _currentPlayerChunk) <= _colliderDistance;
    }

    private static long PackSectionKey(Vector2Int coords, int section)
    {
        unchecked
        {
            return ((long)coords.x << 32) ^ ((long)(uint)coords.y << 8) ^ (uint)section;
        }
    }

    private GameObject CreateChunkObject(Vector2Int chunkCoords)
    {
        string chunkName = $"Chunk ({chunkCoords.x}, {chunkCoords.y})";
        GameObject chunkObject = new GameObject(chunkName);
        chunkObject.transform.position = new Vector3(
            chunkCoords.x * Chunk.ChunkWidth, 0, chunkCoords.y * Chunk.ChunkDepth
        );
        chunkObject.transform.SetParent(transform);
        chunkObject.layer = LayerMask.NameToLayer("World");
        chunkObject.AddComponent<ChunkRenderer>();
        return chunkObject;
    }

    private void UpdateLoadedChunks()
    {
        foreach (Vector2Int loadedChunkCoords in _chunkDataDictionary.Keys)
        {
            if (!IsChunkInViewDistance(loadedChunkCoords) && !_chunksToUnload.Contains(loadedChunkCoords))
                _chunksToUnload.Add(loadedChunkCoords);
        }

        for (int i = _chunksToUnload.Count - 1; i >= 0; i--)
        {
            if (IsChunkInViewDistance(_chunksToUnload[i]))
                _chunksToUnload.RemoveAt(i);
        }

        for (int i = _chunksToLoad.Count - 1; i >= 0; i--)
        {
            if (!IsChunkInViewDistance(_chunksToLoad[i]))
                _chunksToLoad.RemoveAt(i);
        }

        for (int i = _chunksToMesh.Count - 1; i >= 0; i--)
        {
            Vector2Int coords = _chunksToMesh[i];
            if (!IsChunkInViewDistance(coords))
            {
                _chunksToMesh.RemoveAt(i);
                _dirtySectionMasks.Remove(coords);
            }
        }

        for (int i = _sectionsToBakeCollider.Count - 1; i >= 0; i--)
        {
            SectionRef sectionRef = _sectionsToBakeCollider[i];
            if (!ShouldSectionHaveCollider(sectionRef.Chunk, sectionRef.Section))
            {
                _sectionsToBakeCollider.RemoveAt(i);
                _pendingColliderBakes.Remove(PackSectionKey(sectionRef.Chunk, sectionRef.Section));
            }
        }

        for (int x = -_viewDistance; x <= _viewDistance; x++)
        {
            for (int z = -_viewDistance; z <= _viewDistance; z++)
            {
                Vector2Int chunkCoords = new Vector2Int(_currentPlayerChunk.x + x, _currentPlayerChunk.y + z);
                if (IsChunkLoaded(chunkCoords) || _generatingChunks.Contains(chunkCoords) || _chunksToLoad.Contains(chunkCoords))
                    continue;

                _chunksToLoad.Add(chunkCoords);
            }
        }

        SortChunksByDistanceToPlayer(_chunksToLoad);
        SortChunksByDistanceToPlayer(_chunksToMesh);
    }

    private bool IsChunkInViewDistance(Vector2Int chunkCoords)
    {
        return GetChebyshevDistance(chunkCoords, _currentPlayerChunk) <= _viewDistance;
    }

    private static int GetChebyshevDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    private int GetDistanceSqToPlayer(Vector2Int chunkCoords)
    {
        int dx = chunkCoords.x - _currentPlayerChunk.x;
        int dz = chunkCoords.y - _currentPlayerChunk.y;
        return dx * dx + dz * dz;
    }

    private void SortChunksByDistanceToPlayer(List<Vector2Int> chunks)
    {
        chunks.Sort((a, b) =>
        {
            int distCompare = GetDistanceSqToPlayer(a).CompareTo(GetDistanceSqToPlayer(b));
            if (distCompare != 0)
                return distCompare;

            int chebyshevCompare = GetChebyshevDistance(a, _currentPlayerChunk)
                .CompareTo(GetChebyshevDistance(b, _currentPlayerChunk));
            if (chebyshevCompare != 0)
                return chebyshevCompare;

            int xCompare = a.x.CompareTo(b.x);
            return xCompare != 0 ? xCompare : a.y.CompareTo(b.y);
        });
    }

    private void SortSectionRefsByDistance(List<SectionRef> sections)
    {
        sections.Sort((a, b) =>
        {
            int distCompare = GetDistanceSqToPlayer(a.Chunk).CompareTo(GetDistanceSqToPlayer(b.Chunk));
            if (distCompare != 0)
                return distCompare;

            int sectionCompare = Mathf.Abs(a.Section - _currentPlayerSection)
                .CompareTo(Mathf.Abs(b.Section - _currentPlayerSection));
            if (sectionCompare != 0)
                return sectionCompare;

            return a.Section.CompareTo(b.Section);
        });
    }

    private void UnloadChunk(Vector2Int chunkCoords)
    {
        if (_chunkObjectDictionary.TryGetValue(chunkCoords, out GameObject chunkObject))
        {
            Destroy(chunkObject);
            _chunkObjectDictionary.Remove(chunkCoords);
        }

        if (_chunkDataDictionary.ContainsKey(chunkCoords))
            _chunkDataDictionary.Remove(chunkCoords);

        _chunksToMesh.Remove(chunkCoords);
        _meshingChunks.Remove(chunkCoords);
        _dirtySectionMasks.Remove(chunkCoords);
        CancelColliderBakesForChunk(chunkCoords);

        UpdateNeighbors(chunkCoords);
    }

    private void QueueMesh(Vector2Int chunkCoords, int sectionMask)
    {
        if (!IsChunkLoaded(chunkCoords) || sectionMask == 0)
            return;

        if (_dirtySectionMasks.TryGetValue(chunkCoords, out int existing))
            _dirtySectionMasks[chunkCoords] = existing | sectionMask;
        else
            _dirtySectionMasks[chunkCoords] = sectionMask;

        if (_meshingChunks.Contains(chunkCoords))
            return;

        if (!_chunksToMesh.Contains(chunkCoords))
            _chunksToMesh.Add(chunkCoords);
    }

    private void UpdateChunk(Vector2Int chunkCoords)
    {
        QueueMesh(chunkCoords, ChunkSection.AllMask);
    }

    private void UpdateChunkSections(Vector2Int chunkCoords, int sectionMask)
    {
        QueueMesh(chunkCoords, sectionMask);
    }

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
        if (_playerController == null)
            return;

        int spawnX = Chunk.ChunkWidth / 2;
        int spawnZ = Chunk.ChunkDepth / 2;
        int spawnY = 0;

        for (int y = Chunk.ChunkHeight - 1; y >= 0; y--)
        {
            if (BlockDatabase.IsSolid(chunk.GetBlock(spawnX, y, spawnZ)))
            {
                spawnY = y;
                break;
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

        _currentPlayerChunk = GetChunkCoordsFromPosition(spawnPosition);
        _currentPlayerSection = GetPlayerSection();
        RefreshColliderStreaming();

        Debug.Log($"Player spawned at {spawnPosition}");
    }

    private void UpdateNeighbors(Vector2Int chunkCoords)
    {
        UpdateChunk(new Vector2Int(chunkCoords.x, chunkCoords.y + 1));
        UpdateChunk(new Vector2Int(chunkCoords.x, chunkCoords.y - 1));
        UpdateChunk(new Vector2Int(chunkCoords.x + 1, chunkCoords.y));
        UpdateChunk(new Vector2Int(chunkCoords.x - 1, chunkCoords.y));
    }

    public byte GetBlock(Vector3 worldPos)
    {
        Vector2Int chunkCoords = GetChunkCoordsFromPosition(worldPos);
        if (!IsChunkLoaded(chunkCoords))
            return 0;

        Chunk chunk = GetChunkData(chunkCoords);

        int localX = (int)worldPos.x % Chunk.ChunkWidth;
        int localY = (int)worldPos.y;
        int localZ = (int)worldPos.z % Chunk.ChunkDepth;

        if (localX < 0) localX += Chunk.ChunkWidth;
        if (localZ < 0) localZ += Chunk.ChunkDepth;

        return chunk.GetBlock(localX, localY, localZ);
    }

    public void SetBlock(Vector3 worldPos, byte blockID)
    {
        Vector2Int chunkCoords = GetChunkCoordsFromPosition(worldPos);
        if (!IsChunkLoaded(chunkCoords))
            return;

        Chunk chunk = GetChunkData(chunkCoords);

        int localX = (int)worldPos.x % Chunk.ChunkWidth;
        int localY = (int)worldPos.y;
        int localZ = (int)worldPos.z % Chunk.ChunkDepth;

        if (localX < 0) localX += Chunk.ChunkWidth;
        if (localY < 0 || localY >= Chunk.ChunkHeight) return;
        if (localZ < 0) localZ += Chunk.ChunkDepth;

        chunk.SetBlock(localX, localY, localZ, blockID);

        int sectionMask = BuildSectionDirtyMask(localY);
        UpdateChunkSections(chunkCoords, sectionMask);

        if (localX == 0)
            UpdateChunkSections(new Vector2Int(chunkCoords.x - 1, chunkCoords.y), sectionMask);
        if (localX == Chunk.ChunkWidth - 1)
            UpdateChunkSections(new Vector2Int(chunkCoords.x + 1, chunkCoords.y), sectionMask);
        if (localZ == 0)
            UpdateChunkSections(new Vector2Int(chunkCoords.x, chunkCoords.y - 1), sectionMask);
        if (localZ == Chunk.ChunkDepth - 1)
            UpdateChunkSections(new Vector2Int(chunkCoords.x, chunkCoords.y + 1), sectionMask);
    }

    private static int BuildSectionDirtyMask(int localY)
    {
        int section = ChunkSection.IndexFromY(localY);
        int mask = ChunkSection.Bit(section);

        int yInSection = localY - ChunkSection.MinY(section);
        if (yInSection == 0 && section > 0)
            mask |= ChunkSection.Bit(section - 1);
        if (yInSection == ChunkSection.Size - 1 && section < ChunkSection.CountY - 1)
            mask |= ChunkSection.Bit(section + 1);

        return mask;
    }

    public Texture2D GetWorldAtlasTexture()
    {
        return _textureAtlas;
    }
}
