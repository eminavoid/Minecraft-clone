// File: ChunkRenderer.cs
using System.Collections.Generic;
using UnityEngine;
using static BlockType;

/// <summary>
/// This MonoBehaviour is responsible for rendering the visual mesh
/// of a single Chunk data object.
/// 
/// It follows SOLID by having one responsibility:
/// Convert Chunk data into a visual Mesh.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ChunkRenderer : MonoBehaviour
{
    // --- Component References ---
    private MeshFilter _meshFilter;
    private MeshCollider _meshCollider;
    private Mesh _mesh;

    // --- Data ---
    private Chunk _chunkData;

    // --- Mesh Build Data ---
    // We create these lists once and reuse them
    // to avoid allocating new memory every time.
    private List<Vector3> _vertices = new List<Vector3>();
    private List<int> _triangles = new List<int>();
    private List<Vector2> _uvs = new List<Vector2>();

    // --- STATIC DATA (for performance) ---
    // This data is shared by all ChunkRenderer instances.

    // The 6 triangle indices for a quad.
    // This order (Clockwise) ensures the face is visible from the outside.
    private static readonly int[] _faceTriangles = { 0, 2, 1, 0, 3, 2 };

    // Vertex positions for all 6 faces of a 1x1x1 cube.
    // We add (x,y,z) to these in the loop.
    private static readonly Vector3[] _topFaceVertices =
        { new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0) };
    private static readonly Vector3[] _bottomFaceVertices =
        { new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1) };
    private static readonly Vector3[] _frontFaceVertices =
        { new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 0, 1) };
    private static readonly Vector3[] _backFaceVertices =
        { new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 0) };
    private static readonly Vector3[] _rightFaceVertices =
        { new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0), new Vector3(1, 0, 0) };
    private static readonly Vector3[] _leftFaceVertices =
        { new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(0, 0, 1) };

    // --- END STATIC DATA ---

    private void Awake()
    {
        // Get component references
        _meshFilter = GetComponent<MeshFilter>();
        _meshCollider = GetComponent<MeshCollider>();

        // Create a new mesh for this chunk
        _mesh = new Mesh();
        _mesh.name = "ChunkMesh";
        _meshFilter.mesh = _mesh;
    }

    /// <summary>
    /// Sets the chunk data and triggers the initial mesh generation.
    /// </summary>
    public void Initialize(Chunk chunk)
    {
        _chunkData = chunk;
        GenerateMesh();
    }

    /// <summary>
    /// The core mesh generation logic.
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

                    // Check Top (+Y)
                    if (!IsNeighborSolid(x, y + 1, z))
                        AddFace(BlockFace.Top, x, y, z, blockID);

                    // Check Bottom (-Y)
                    if (!IsNeighborSolid(x, y - 1, z))
                        AddFace(BlockFace.Bottom, x, y, z, blockID);

                    // Check Front (+Z)
                    if (!IsNeighborSolid(x, y, z + 1))
                        AddFace(BlockFace.Front, x, y, z, blockID);

                    // Check Back (-Z)
                    if (!IsNeighborSolid(x, y, z - 1))
                        AddFace(BlockFace.Back, x, y, z, blockID);

                    // Check Right (+X)
                    if (!IsNeighborSolid(x + 1, y, z))
                        AddFace(BlockFace.Right, x, y, z, blockID);

                    // Check Left (-X)
                    if (!IsNeighborSolid(x - 1, y, z))
                        AddFace(BlockFace.Left, x, y, z, blockID);
                }
            }
        }

        // 3. Apply the generated data to the Mesh
        ApplyMesh();
    }

    /// <summary>
    /// Checks if a block at a given local coordinate is solid.
    /// This handles bounds checks for outside the chunk.
    /// </summary>
    private bool IsNeighborSolid(int x, int y, int z)
    {
        // --- Y-Axis Bounds Check ---
        // If below the world, it's solid (bedrock)
        if (y < 0) return true;
        // If above the world, it's air
        if (y >= Chunk.ChunkHeight) return false;

        // --- X/Z-Axis Bounds Check (Cross-Chunk) ---
        // TODO: This is where you would check neighbor chunks.
        // For now, if it's outside our X/Z bounds,
        // we'll assume it's air so we can see the edge of the chunk.
        if (x < 0 || x >= Chunk.ChunkWidth || z < 0 || z >= Chunk.ChunkDepth)
        {
            return false;
        }

        // It's inside our chunk. Get its ID and check its type.
        byte neighborID = _chunkData.GetBlock(x, y, z);
        return BlockDatabase.GetBlockType(neighborID).IsSolid;
    }

    /// <summary>
    /// Adds the 4 vertices, 6 triangle indices, and 4 UVs
    /// for a single block face.
    /// </summary>
    private void AddFace(BlockFace face, int x, int y, int z, byte blockID)
    {
        // 1. Get the BlockType
        BlockType type = BlockDatabase.GetBlockType(blockID);

        // 2. Get the texture coordinates for this face
        TextureAtlasCoord texCoord = type.GetTextureCoords(face);
        if (texCoord == null)
        {
            Debug.LogError($"BlockType {type.name} has null texture coords for face {face}");
            return; // Stop this face from rendering
        }
        Vector2[] uvs = TextureAtlasManager.GetUVs(texCoord);

        // 3. Get the starting vertex index
        int vIndex = _vertices.Count;
        Vector3 blockPosition = new Vector3(x, y, z);

        // 4. Add vertices and UVs based on face
        // The UV mapping logic here is correct, but the triangle winding
        // needs to be different for the Top face.

        switch (face)
        {
            case BlockFace.Top:
                _vertices.Add(_topFaceVertices[0] + blockPosition);
                _vertices.Add(_topFaceVertices[1] + blockPosition);
                _vertices.Add(_topFaceVertices[2] + blockPosition);
                _vertices.Add(_topFaceVertices[3] + blockPosition);
                _uvs.Add(uvs[1]); // Top-Left
                _uvs.Add(uvs[0]); // Bottom-Left
                _uvs.Add(uvs[3]); // Bottom-Right
                _uvs.Add(uvs[2]); // Top-Right

                // --- THIS IS THE FIX ---
                // Add triangles MANUALLY with correct winding for Top
                _triangles.Add(vIndex + 0);
                _triangles.Add(vIndex + 1);
                _triangles.Add(vIndex + 2);
                _triangles.Add(vIndex + 0);
                _triangles.Add(vIndex + 2);
                _triangles.Add(vIndex + 3);
                break;

            case BlockFace.Bottom:
                _vertices.Add(_bottomFaceVertices[0] + blockPosition);
                _vertices.Add(_bottomFaceVertices[1] + blockPosition);
                _vertices.Add(_bottomFaceVertices[2] + blockPosition);
                _vertices.Add(_bottomFaceVertices[3] + blockPosition);
                _uvs.Add(uvs[1]); // Top-Left
                _uvs.Add(uvs[0]); // Bottom-Left
                _uvs.Add(uvs[3]); // Bottom-Right
                _uvs.Add(uvs[2]); // Top-Right

                // Use the default triangle winding
                _triangles.Add(vIndex + _faceTriangles[0]);
                _triangles.Add(vIndex + _faceTriangles[1]);
                _triangles.Add(vIndex + _faceTriangles[2]);
                _triangles.Add(vIndex + _faceTriangles[3]);
                _triangles.Add(vIndex + _faceTriangles[4]);
                _triangles.Add(vIndex + _faceTriangles[5]);
                break;

            case BlockFace.Front:
                _vertices.Add(_frontFaceVertices[0] + blockPosition);
                _vertices.Add(_frontFaceVertices[1] + blockPosition);
                _vertices.Add(_frontFaceVertices[2] + blockPosition);
                _vertices.Add(_frontFaceVertices[3] + blockPosition);
                _uvs.Add(uvs[0]); // Bottom-Left
                _uvs.Add(uvs[1]); // Top-Left
                _uvs.Add(uvs[2]); // Top-Right
                _uvs.Add(uvs[3]); // Bottom-Right

                // Use the default triangle winding
                _triangles.Add(vIndex + _faceTriangles[0]);
                _triangles.Add(vIndex + _faceTriangles[1]);
                _triangles.Add(vIndex + _faceTriangles[2]);
                _triangles.Add(vIndex + _faceTriangles[3]);
                _triangles.Add(vIndex + _faceTriangles[4]);
                _triangles.Add(vIndex + _faceTriangles[5]);
                break;

            case BlockFace.Back:
                _vertices.Add(_backFaceVertices[0] + blockPosition);
                _vertices.Add(_backFaceVertices[1] + blockPosition);
                _vertices.Add(_backFaceVertices[2] + blockPosition);
                _vertices.Add(_backFaceVertices[3] + blockPosition);
                _uvs.Add(uvs[3]); // Bottom-Right
                _uvs.Add(uvs[2]); // Top-Right
                _uvs.Add(uvs[1]); // Top-Left
                _uvs.Add(uvs[0]); // Bottom-Left

                // Use the default triangle winding
                _triangles.Add(vIndex + _faceTriangles[0]);
                _triangles.Add(vIndex + _faceTriangles[1]);
                _triangles.Add(vIndex + _faceTriangles[2]);
                _triangles.Add(vIndex + _faceTriangles[3]);
                _triangles.Add(vIndex + _faceTriangles[4]);
                _triangles.Add(vIndex + _faceTriangles[5]);
                break;

            case BlockFace.Right:
                _vertices.Add(_rightFaceVertices[0] + blockPosition);
                _vertices.Add(_rightFaceVertices[1] + blockPosition);
                _vertices.Add(_rightFaceVertices[2] + blockPosition);
                _vertices.Add(_rightFaceVertices[3] + blockPosition);
                _uvs.Add(uvs[0]); // Bottom-Left
                _uvs.Add(uvs[1]); // Top-Left
                _uvs.Add(uvs[2]); // Top-Right
                _uvs.Add(uvs[3]); // Bottom-Right

                // Use the default triangle winding
                _triangles.Add(vIndex + _faceTriangles[0]);
                _triangles.Add(vIndex + _faceTriangles[1]);
                _triangles.Add(vIndex + _faceTriangles[2]);
                _triangles.Add(vIndex + _faceTriangles[3]);
                _triangles.Add(vIndex + _faceTriangles[4]);
                _triangles.Add(vIndex + _faceTriangles[5]);
                break;

            case BlockFace.Left:
                _vertices.Add(_leftFaceVertices[0] + blockPosition);
                _vertices.Add(_leftFaceVertices[1] + blockPosition);
                _vertices.Add(_leftFaceVertices[2] + blockPosition);
                _vertices.Add(_leftFaceVertices[3] + blockPosition);
                _uvs.Add(uvs[3]); // Bottom-Right
                _uvs.Add(uvs[2]); // Top-Right
                _uvs.Add(uvs[1]); // Top-Left
                _uvs.Add(uvs[0]); // Bottom-Left

                // Use the default triangle winding
                _triangles.Add(vIndex + _faceTriangles[0]);
                _triangles.Add(vIndex + _faceTriangles[1]);
                _triangles.Add(vIndex + _faceTriangles[2]);
                _triangles.Add(vIndex + _faceTriangles[3]);
                _triangles.Add(vIndex + _faceTriangles[4]);
                _triangles.Add(vIndex + _faceTriangles[5]);
                break;
        }
    }

    /// <summary>
    /// Applies all the vertex, triangle, and UV data
    /// to the mesh and recalculates physics.
    /// </summary>
    private void ApplyMesh()
    {
        // Debugging check
        if (_vertices.Count != _uvs.Count)
        {
            Debug.LogError($"Vertex ({_vertices.Count}) and UV ({_uvs.Count}) count MISMATCH. Mesh will be black/broken.");
        }

        _mesh.Clear(); // Clear any old data
        _mesh.SetVertices(_vertices);
        _mesh.SetTriangles(_triangles, 0); // '0' is the submesh index
        _mesh.SetUVs(0, _uvs); // '0' is the UV channel

        _mesh.RecalculateNormals(); // Calculate lighting

        // Update the collider
        _meshCollider.sharedMesh = null; // Must clear old one first
        _meshCollider.sharedMesh = _mesh;
    }
}