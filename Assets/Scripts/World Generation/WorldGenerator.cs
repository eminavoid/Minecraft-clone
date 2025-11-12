// File: WorldGenerator.cs
using UnityEngine;

/// <summary>
/// Generates the block data for chunks using procedural noise.
/// This is a plain C# class, not a MonoBehaviour.
/// </summary>
public class WorldGenerator
{
    // --- Generator Settings ---
    // In a real project, you might pass these in via a
    // 'GenerationSettings' ScriptableObject.

    private int _seed;

    [Tooltip("How 'zoomed-in' the noise is. Smaller = bigger features.")]
    private float _noiseScale = 0.05f;

    [Tooltip("The base height of the terrain in blocks.")]
    private int _baseTerrainHeight = 60;

    [Tooltip("How much the noise affects the height (amplitude).")]
    private int _terrainAmplitude = 40;

    [Tooltip("How many layers of dirt to place below grass.")]
    private int _dirtLayerDepth = 3;

    private readonly byte _airID;
    private readonly byte _grassID;
    private readonly byte _dirtID;
    private readonly byte _stoneID;

    /// <summary>
    /// Constructor to set up the generator with a seed.
    /// </summary>
    public WorldGenerator(int seed)
    {
        _seed = seed;

        _airID = BlockDatabase.GetBlockType("Air").BlockID;
        _grassID = BlockDatabase.GetBlockType("Grass").BlockID;
        _dirtID = BlockDatabase.GetBlockType("Dirt").BlockID;
        _stoneID = BlockDatabase.GetBlockType("Stone").BlockID;
    }

    /// <summary>
    /// Fills the provided Chunk data object with terrain.
    /// </summary>
    /// <param name="chunk">The Chunk object to be filled.</param>
    /// <param name="chunkPosition">The world-space position of this chunk (e.g., (0,0), (1,0)).</param>
    public void GenerateChunk(Chunk chunk, Vector2Int chunkPosition)
    {
        // Calculate the world-space offset for this chunk
        int chunkOffsetX = chunkPosition.x * Chunk.ChunkWidth;
        int chunkOffsetZ = chunkPosition.y * Chunk.ChunkDepth; // Note: Y in Vector2 is Z in world

        for (int x = 0; x < Chunk.ChunkWidth; x++)
        {
            for (int z = 0; z < Chunk.ChunkDepth; z++)
            {
                // 1. GET GLOBAL COORDINATES
                // We must use global coords for the noise function
                // to make terrain seamless between chunks.
                float globalX = chunkOffsetX + x;
                float globalZ = chunkOffsetZ + z;

                // 2. GET NOISE VALUE
                // We add the seed to the coordinate to get a unique world.
                // We multiply by scale to 'zoom' the noise.
                float noiseCoordX = (globalX + _seed) * _noiseScale;
                float noiseCoordZ = (globalZ + _seed) * _noiseScale;

                // Mathf.PerlinNoise returns a value between 0.0 and 1.0
                float noiseValue = Mathf.PerlinNoise(noiseCoordX, noiseCoordZ);

                // 3. CALCULATE TERRAIN HEIGHT
                // Map the 0-1 noise value to our desired height range.
                int terrainHeight = _baseTerrainHeight + Mathf.FloorToInt(noiseValue * _terrainAmplitude);

                // Clamp height to world bounds
                if (terrainHeight >= Chunk.ChunkHeight)
                    terrainHeight = Chunk.ChunkHeight - 1;

                // 4. FILL THE CHUNK COLUMN (Y-Axis)
                for (int y = 0; y < Chunk.ChunkHeight; y++)
                {
                    if (y > terrainHeight)
                    {
                        // Anything above the terrain is Air
                        chunk.SetBlock(x, y, z, _airID);
                    }
                    else if (y == terrainHeight)
                    {
                        // The very top layer is Grass
                        chunk.SetBlock(x, y, z, _grassID);
                    }
                    else if (y >= terrainHeight - _dirtLayerDepth)
                    {
                        // Just below the grass is Dirt
                        chunk.SetBlock(x, y, z, _dirtID);
                    }
                    else
                    {
                        // Everything else below is Stone
                        chunk.SetBlock(x, y, z, _stoneID);
                    }
                }
            }
        }
    }
}