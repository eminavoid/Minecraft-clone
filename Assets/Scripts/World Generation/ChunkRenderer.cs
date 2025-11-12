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

    // This array maps our 6 directions (1-6) to the BlockFace enum
    private static readonly BlockFace[] _dirToBlockFace =
    {
        BlockFace.Right,  // 1 (+X)
        BlockFace.Left,   // 2 (-X)
        BlockFace.Top,    // 3 (+Y)
        BlockFace.Bottom, // 4 (-Y)
        BlockFace.Front,  // 5 (+Z)
        BlockFace.Back    // 6 (-Z)
    };

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshCollider = GetComponent<MeshCollider>();
        _mesh = new Mesh();
        _mesh.name = "ChunkMesh_Greedy";
        _meshFilter.mesh = _mesh;
    }

    public void Initialize(Chunk chunk, World world)
    {
        _chunkData = chunk;
        _world = world;
        GenerateMesh();
    }

    public void GenerateMesh()
    {
        _vertices.Clear();
        _triangles.Clear();
        _uvs.Clear();

        // Loop 3 times (once for each axis: X, Y, Z)
        for (int axis = 0; axis < 3; axis++)
        {
            int u = (axis + 1) % 3; // U-axis (width)
            int v = (axis + 2) % 3; // V-axis (height)

            // x is our 3D position vector
            int[] x = { 0, 0, 0 };
            // q is the direction we are sweeping in
            int[] q = { 0, 0, 0 };
            q[axis] = 1;

            int sliceWidth = (u == 1) ? Chunk.ChunkHeight : Chunk.ChunkWidth;
            int sliceHeight = (v == 1) ? Chunk.ChunkHeight : Chunk.ChunkWidth;

            byte[] mask = new byte[sliceWidth * sliceHeight * 2];

            // Sweep along the current axis
            for (x[axis] = -1; x[axis] < ((axis == 1) ? Chunk.ChunkHeight : Chunk.ChunkWidth);)
            {
                int n = 0;
                for (x[v] = 0; x[v] < sliceHeight; x[v]++)
                {
                    for (x[u] = 0; x[u] < sliceWidth; x[u]++)
                    {
                        byte currentBlock = GetBlockID(x[0], x[1], x[2]);
                        byte nextBlock = GetBlockID(x[0] + q[0], x[1] + q[1], x[2] + q[2]);

                        bool isCurrentSolid = BlockDatabase.GetBlockType(currentBlock).IsSolid;
                        bool isNextSolid = BlockDatabase.GetBlockType(nextBlock).IsSolid;

                        if (isCurrentSolid == isNextSolid)
                        {
                            mask[n++] = 0; mask[n++] = 0; // No face
                        }
                        else if (isCurrentSolid)
                        {
                            mask[n++] = currentBlock;        // Store BlockID
                            mask[n++] = (byte)((axis * 2) + 1); // Store "Front" Face Dir (e.g., +X, +Y, +Z)
                        }
                        else
                        {
                            mask[n++] = nextBlock;           // Store BlockID
                            mask[n++] = (byte)((axis * 2) + 2); // Store "Back" Face Dir (e.g., -X, -Y, -Z)
                        }
                    }
                }

                x[axis]++; // Increment the sweep

                // --- START THE GREEDY SEARCH ---
                n = 0;
                for (int j = 0; j < sliceHeight; j++)
                {
                    for (int i = 0; i < sliceWidth;)
                    {
                        byte blockID = mask[n];
                        byte faceDir = mask[n + 1];

                        if (faceDir != 0) // We found a face
                        {
                            // Find width (w)
                            int w = 1;
                            while (i + w < sliceWidth &&
                                   mask[n + (w * 2)] == blockID &&
                                   mask[n + (w * 2) + 1] == faceDir)
                            {
                                w++;
                            }

                            // Find height (h)
                            int h = 1;
                            bool done = false;
                            while (j + h < sliceHeight)
                            {
                                for (int k = 0; k < w; k++)
                                {
                                    int index = (n + (k * 2)) + (h * sliceWidth * 2);
                                    if (mask[index] != blockID || mask[index + 1] != faceDir)
                                    {
                                        done = true;
                                        break;
                                    }
                                }
                                if (done) break;
                                h++;
                            }

                            // --- ADD THE QUAD ---
                            x[u] = i;
                            x[v] = j;

                            Vector3 du = Vector3.zero; du[u] = w;
                            Vector3 dv = Vector3.zero; dv[v] = h;

                            Vector3 v1 = new Vector3(x[0], x[1], x[2]);
                            Vector3 v2 = v1 + du;
                            Vector3 v3 = v1 + du + dv;
                            Vector3 v4 = v1 + dv;

                            BlockType blockType = BlockDatabase.GetBlockType(blockID);
                            BlockFace face = _dirToBlockFace[faceDir - 1]; // -1 to match 0-indexed array

                            AddQuad(v1, v2, v3, v4, face, blockType, w, h, axis, faceDir);

                            // --- UPDATE THE MASK ---
                            for (int l = 0; l < h; l++)
                            {
                                for (int k = 0; k < w; k++)
                                {
                                    int index = (n + (k * 2)) + (l * sliceWidth * 2);
                                    mask[index] = 0;
                                    mask[index + 1] = 0;
                                }
                            }

                            i += w;
                            n += (w * 2);
                        }
                        else
                        {
                            i++;
                            n += 2;
                        }
                    }
                }
            }
        }
        ApplyMesh();
    }

    // (Helper method unchanged)
    private byte GetBlockID(int x, int y, int z)
    {
        if (y < 0 || y >= Chunk.ChunkHeight) return 0;
        if (x >= 0 && x < Chunk.ChunkWidth && z >= 0 && z < Chunk.ChunkDepth)
        {
            return _chunkData.GetBlock(x, y, z);
        }

        Vector3 worldPos = transform.position;
        int neighborWorldX = (int)worldPos.x + x;
        int neighborWorldZ = (int)worldPos.z + z;
        Chunk neighborChunk = _world.GetChunkFromWorldPos(new Vector3(neighborWorldX, 0, neighborWorldZ));
        if (neighborChunk == null) return 0;

        int localX = (x + Chunk.ChunkWidth) % Chunk.ChunkWidth;
        int localZ = (z + Chunk.ChunkDepth) % Chunk.ChunkDepth;
        return neighborChunk.GetBlock(localX, y, localZ);
    }

    // (Unchanged)
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

    /// <summary>
    /// --- THIS IS THE FINAL, COMBINED AddQuad METHOD ---
    /// </summary>
    private void AddQuad(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4,
                         BlockFace face, BlockType type, int w, int h, int axis, int faceDir)
    {
        int vIndex = _vertices.Count;

        // --- 1. Correct Winding Order (This is the one that works) ---
        bool isFrontFace = (faceDir % 2 != 0);

        _vertices.Add(v1); // v1 (index 0)
        _vertices.Add(v2); // v2 (index 1)
        _vertices.Add(v3); // v3 (index 2)
        _vertices.Add(v4); // v4 (index 3)

        if (isFrontFace)
        {
            // Clockwise for +X, +Y, +Z
            _triangles.Add(vIndex + 0);
            _triangles.Add(vIndex + 1);
            _triangles.Add(vIndex + 2);
            _triangles.Add(vIndex + 0);
            _triangles.Add(vIndex + 2);
            _triangles.Add(vIndex + 3);
        }
        else
        {
            // Clockwise for -X, -Y, -Z
            _triangles.Add(vIndex + 0);
            _triangles.Add(vIndex + 3);
            _triangles.Add(vIndex + 2);
            _triangles.Add(vIndex + 0);
            _triangles.Add(vIndex + 2);
            _triangles.Add(vIndex + 1);
        }

        // --- 2. Correct UV Mapping (This is the fix) ---
        TextureAtlasCoord texCoord = type.GetTextureCoords(face);
        Vector2[] uvs = TextureAtlasManager.GetUVs(texCoord);

        Vector2 uv0 = uvs[0]; // Bottom-Left
        Vector2 uv1 = uvs[1]; // Top-Left
        Vector2 uv2 = uvs[2]; // Top-Right

        float uvWidth = uv2.x - uv0.x;
        float uvHeight = uv1.y - uv0.y;

        int texWidth, texHeight;

        // axis 0 = X (Left/Right)
        // axis 1 = Y (Top/Bottom)
        // axis 2 = Z (Front/Back)

        // This logic correctly maps the greedy w/h to the texture's U/V
        if (axis == 1) // Y-axis face (Top/Bottom)
        {
            // Greedy w is Z-size, h is X-size
            // We want UV U (width) to map to X-size, V (height) to map to Z-size
            texWidth = h;
            texHeight = w;
        }
        else if (axis == 0) // X-axis face (Left/Right)
        {
            // Greedy w is Y-size, h is Z-size
            // We want UV U (width) to map to Z-size, V (height) to map to Y-size
            texWidth = h;
            texHeight = w;
        }
        else // Z-axis face (Front/Back)
        {
            // Greedy w is X-size, h is Y-size
            // We want UV U (width) to map to X-size, V (height) to map to Y-size
            texWidth = w;
            texHeight = h;
        }

        // Calculate the four tiled UV coordinates
        Vector2 uv_v1 = new Vector2(uv0.x, uv0.y); // Bottom-Left
        Vector2 uv_v2 = new Vector2(uv0.x + (uvWidth * texWidth), uv0.y); // Bottom-Right
        Vector2 uv_v3 = new Vector2(uv0.x + (uvWidth * texWidth), uv0.y + (uvHeight * texHeight)); // Top-Right
        Vector2 uv_v4 = new Vector2(uv0.x, uv0.y + (uvHeight * texHeight)); // Top-Left

        // Add UVs in the correct order to match the vertices
        // This is the part that was wrong before

        if (axis == 2) // Z-axis (Front/Back)
        {
            // v1, v2, v3, v4 = bottom-left, bottom-right, top-right, top-left
            // We need: uv_v1, uv_v2, uv_v3, uv_v4
            _uvs.Add(uv_v1);
            _uvs.Add(uv_v2);
            _uvs.Add(uv_v3);
            _uvs.Add(uv_v4);
        }
        else // X-axis and Y-axis
        {
            // v1, v2, v3, v4 = bottom-left, top-left, top-right, bottom-right
            // We need: uv_v1, uv_v4, uv_v3, uv_v2
            _uvs.Add(uv_v1); // for v1 (bottom-left)
            _uvs.Add(uv_v4); // for v2 (top-left)
            _uvs.Add(uv_v3); // for v3 (top-right)
            _uvs.Add(uv_v2); // for v4 (bottom-right)
        }
    }
}