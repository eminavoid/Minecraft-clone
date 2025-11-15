// File: ChunkRenderer.cs
using System.Collections.Generic;
using UnityEngine;
using static BlockType;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ChunkRenderer : MonoBehaviour
{
    // --- Data ---
    private Chunk _chunkData;
    private World _world;

    // --- Component References ---
    private MeshFilter _meshFilter;
    private MeshCollider _meshCollider;
    private Mesh _mesh;

    // --- Mesh Build Data ---
    private List<Vector3> _vertices = new List<Vector3>();
    private List<int> _triangles = new List<int>();
    private List<Vector2> _uvs = new List<Vector2>();

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshCollider = GetComponent<MeshCollider>();
        _mesh = new Mesh();
        _mesh.name = "ChunkMesh_Simple_Correct";
        _meshFilter.mesh = _mesh;
    }

    /// <summary>
    /// Sets the chunk data and triggers the initial mesh generation.
    /// </summary>
    public void Initialize(Chunk chunk, World world)
    {
        _chunkData = chunk;
        _world = world;
        GenerateMesh();
    }

    /// <summary>
    /// The core mesh generation logic (Simple Culling).
    /// </summary>
    public void GenerateMesh()
    {
        // 1. Clear old data from the lists
        _vertices.Clear();
        _triangles.Clear();
        _uvs.Clear();

        // 2. Loop through every block in the chunk
        for (int y = 0; y < Chunk.ChunkHeight; y++)
        {
            for (int x = 0; x < Chunk.ChunkWidth; x++)
            {
                for (int z = 0; z < Chunk.ChunkDepth; z++)
                {
                    // Get the block type
                    byte blockID = _chunkData.GetBlock(x, y, z);
                    BlockType type = BlockDatabase.GetBlockType(blockID);

                    // Skip this block if it's Air or not solid
                    if (!type.IsSolid)
                    {
                        continue;
                    }

                    // This is a solid block. Check its 6 neighbors.
                    Vector3 blockPosition = new Vector3(x, y, z);

                    // Check Top (+Y)
                    if (!IsNeighborSolid(x, y + 1, z))
                        AddFace(BlockFace.Top, blockPosition, type);

                    // Check Bottom (-Y)
                    if (!IsNeighborSolid(x, y - 1, z))
                        AddFace(BlockFace.Bottom, blockPosition, type);

                    // Check Front (+Z)
                    if (!IsNeighborSolid(x, y, z + 1))
                        AddFace(BlockFace.Front, blockPosition, type);

                    // Check Back (-Z)
                    if (!IsNeighborSolid(x, y, z - 1))
                        AddFace(BlockFace.Back, blockPosition, type);

                    // Check Right (+X)
                    if (!IsNeighborSolid(x + 1, y, z))
                        AddFace(BlockFace.Right, blockPosition, type);

                    // Check Left (-X)
                    if (!IsNeighborSolid(x - 1, y, z))
                        AddFace(BlockFace.Left, blockPosition, type);
                }
            }
        }

        // 3. Apply the generated data to the Mesh
        ApplyMesh();
    }

    /// <summary>
    /// Checks if a block at a given local coordinate is solid.
    /// (This method is correct and world-aware)
    /// </summary>
    private bool IsNeighborSolid(int x, int y, int z)
    {
        // Y-axis is absolute, no cross-chunk
        if (y < 0 || y >= Chunk.ChunkHeight) return (y < 0); // Bottom of world is solid

        // Is it inside *this* chunk's (X, Z) bounds?
        if (x >= 0 && x < Chunk.ChunkWidth &&
            z >= 0 && z < Chunk.ChunkDepth)
        {
            return BlockDatabase.GetBlockType(_chunkData.GetBlock(x, y, z)).IsSolid;
        }

        // It's outside. We must ask the World.
        Vector3 worldPos = transform.position;
        int neighborWorldX = (int)worldPos.x + x;
        int neighborWorldZ = (int)worldPos.z + z;

        Chunk neighborChunk = _world.GetChunkFromWorldPos(new Vector3(neighborWorldX, 0, neighborWorldZ));
        if (neighborChunk == null) return false; // Not loaded, so it's "air"

        // Wrap the coordinates
        int localX = (x + Chunk.ChunkWidth * 1000) % Chunk.ChunkWidth;
        int localZ = (z + Chunk.ChunkDepth * 1000) % Chunk.ChunkDepth;

        return BlockDatabase.GetBlockType(neighborChunk.GetBlock(localX, y, localZ)).IsSolid;
    }

    /// <summary>
    /// --- ESTE ES EL AddFace CON EL WINDING ORDER CORREGIDO ---
    /// </summary>
    private void AddFace(BlockFace face, Vector3 blockPosition, BlockType type)
    {
        TextureAtlasCoord texCoord = type.GetTextureCoords(face);
        if (texCoord == null) return;

        Vector2[] uvs = TextureAtlasManager.GetUVs(texCoord);
        int vIndex = _vertices.Count;

        switch (face)
        {
            case BlockFace.Top: // +Y (Esta funciona)
                _vertices.Add(blockPosition + new Vector3(0, 1, 0)); // Back-Left (0)
                _vertices.Add(blockPosition + new Vector3(0, 1, 1)); // Front-Left (1)
                _vertices.Add(blockPosition + new Vector3(1, 1, 1)); // Front-Right (2)
                _vertices.Add(blockPosition + new Vector3(1, 1, 0)); // Back-Right (3)
                // (0, 1, 2) (0, 2, 3) - Clockwise
                _triangles.Add(vIndex + 0); _triangles.Add(vIndex + 1); _triangles.Add(vIndex + 2);
                _triangles.Add(vIndex + 0); _triangles.Add(vIndex + 2); _triangles.Add(vIndex + 3);
                _uvs.Add(uvs[1]); _uvs.Add(uvs[0]); _uvs.Add(uvs[3]); _uvs.Add(uvs[2]);
                break;

            case BlockFace.Bottom: // -Y
                _vertices.Add(blockPosition + new Vector3(0, 0, 1)); // Front-Left (0)
                _vertices.Add(blockPosition + new Vector3(0, 0, 0)); // Back-Left (1)
                _vertices.Add(blockPosition + new Vector3(1, 0, 0)); // Back-Right (2)
                _vertices.Add(blockPosition + new Vector3(1, 0, 1)); // Front-Right (3)

                // --- ¡ESTE ES EL FIX! ---
                // El orden (0, 1, 2) (0, 2, 3) es el correcto para ser visto desde abajo.
                _triangles.Add(vIndex + 0); _triangles.Add(vIndex + 1); _triangles.Add(vIndex + 2);
                _triangles.Add(vIndex + 0); _triangles.Add(vIndex + 2); _triangles.Add(vIndex + 3);
                // --- FIN DEL FIX ---

                _uvs.Add(uvs[0]); _uvs.Add(uvs[1]); _uvs.Add(uvs[2]); _uvs.Add(uvs[3]);
                break;

            case BlockFace.Front: // +Z
                _vertices.Add(blockPosition + new Vector3(0, 0, 1)); // Bottom-Left (0)
                _vertices.Add(blockPosition + new Vector3(0, 1, 1)); // Top-Left (1)
                _vertices.Add(blockPosition + new Vector3(1, 1, 1)); // Top-Right (2)
                _vertices.Add(blockPosition + new Vector3(1, 0, 1)); // Bottom-Right (3)
                // (0, 2, 1) (0, 3, 2) - Clockwise
                _triangles.Add(vIndex + 0); _triangles.Add(vIndex + 2); _triangles.Add(vIndex + 1);
                _triangles.Add(vIndex + 0); _triangles.Add(vIndex + 3); _triangles.Add(vIndex + 2);
                _uvs.Add(uvs[0]); _uvs.Add(uvs[1]); _uvs.Add(uvs[2]); _uvs.Add(uvs[3]);
                break;

            case BlockFace.Back: // -Z
                _vertices.Add(blockPosition + new Vector3(1, 0, 0)); // Bottom-Right (0)
                _vertices.Add(blockPosition + new Vector3(1, 1, 0)); // Top-Right (1)
                _vertices.Add(blockPosition + new Vector3(0, 1, 0)); // Top-Left (2)
                _vertices.Add(blockPosition + new Vector3(0, 0, 0)); // Bottom-Left (3)
                // (0, 2, 1) (0, 3, 2) - Clockwise
                _triangles.Add(vIndex + 0); _triangles.Add(vIndex + 2); _triangles.Add(vIndex + 1);
                _triangles.Add(vIndex + 0); _triangles.Add(vIndex + 3); _triangles.Add(vIndex + 2);
                _uvs.Add(uvs[0]); _uvs.Add(uvs[1]); _uvs.Add(uvs[2]); _uvs.Add(uvs[3]);
                break;

            case BlockFace.Right: // +X
                _vertices.Add(blockPosition + new Vector3(1, 0, 1)); // Front-Bottom (0)
                _vertices.Add(blockPosition + new Vector3(1, 1, 1)); // Front-Top (1)
                _vertices.Add(blockPosition + new Vector3(1, 1, 0)); // Back-Top (2)
                _vertices.Add(blockPosition + new Vector3(1, 0, 0)); // Back-Bottom (3)
                // (0, 2, 1) (0, 3, 2) - Clockwise
                _triangles.Add(vIndex + 0); _triangles.Add(vIndex + 2); _triangles.Add(vIndex + 1);
                _triangles.Add(vIndex + 0); _triangles.Add(vIndex + 3); _triangles.Add(vIndex + 2);
                _uvs.Add(uvs[0]); _uvs.Add(uvs[1]); _uvs.Add(uvs[2]); _uvs.Add(uvs[3]);
                break;

            case BlockFace.Left: // -X
                _vertices.Add(blockPosition + new Vector3(0, 0, 0)); // Back-Bottom (0)
                _vertices.Add(blockPosition + new Vector3(0, 1, 0)); // Back-Top (1)
                _vertices.Add(blockPosition + new Vector3(0, 1, 1)); // Front-Top (2)
                _vertices.Add(blockPosition + new Vector3(0, 0, 1)); // Front-Bottom (3)
                // (0, 2, 1) (0, 3, 2) - Clockwise
                _triangles.Add(vIndex + 0); _triangles.Add(vIndex + 2); _triangles.Add(vIndex + 1);
                _triangles.Add(vIndex + 0); _triangles.Add(vIndex + 3); _triangles.Add(vIndex + 2);
                _uvs.Add(uvs[0]); _uvs.Add(uvs[1]); _uvs.Add(uvs[2]); _uvs.Add(uvs[3]);
                break;
        }
    }

    /// <summary>
    /// Applies all the vertex, triangle, and UV data
    /// to the mesh and recalculates physics.
    /// </summary>
    private void ApplyMesh()
    {
        if (_vertices.Count != _uvs.Count)
            Debug.LogError($"Vertex ({_vertices.Count}) and UV ({_uvs.Count}) count MISMATCH.");

        _mesh.Clear();
        _mesh.SetVertices(_vertices);
        _mesh.SetTriangles(_triangles, 0);
        _mesh.SetUVs(0, _uvs);
        _mesh.RecalculateNormals();
        _meshCollider.sharedMesh = null;
        _meshCollider.sharedMesh = _mesh;
    }
}