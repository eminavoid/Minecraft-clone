// File: WorldGenerator.cs
using UnityEngine;

public class WorldGenerator
{
    // --- Settings ---
    private int _seed;
    private float _noiseScale;
    private int _baseTerrainHeight;
    private int _terrainAmplitude;
    private int _dirtLayerDepth;

    // --- Block IDs ---
    private readonly byte _airID;
    private readonly byte _grassID;
    private readonly byte _dirtID;
    private readonly byte _stoneID;

    // --- Constructor ---
    public WorldGenerator(int seed, float noiseScale, int baseHeight, int amplitude, int dirtDepth)
    {
        _seed = seed;
        _noiseScale = noiseScale;
        _baseTerrainHeight = baseHeight;
        _terrainAmplitude = amplitude;
        _dirtLayerDepth = dirtDepth;

        _airID = BlockDatabase.GetBlockType("Air").BlockID;
        _grassID = BlockDatabase.GetBlockType("Grass").BlockID;
        _dirtID = BlockDatabase.GetBlockType("Dirt").BlockID;
        _stoneID = BlockDatabase.GetBlockType("Stone").BlockID;
    }

    /// <summary>
    /// --- NUEVO (Solo para el hilo principal) ---
    /// Genera el mapa de ruido.
    /// </summary>
    public float[,] GetNoiseMap(Vector2Int chunkCoords)
    {
        int chunkOffsetX = chunkCoords.x * Chunk.ChunkWidth;
        int chunkOffsetZ = chunkCoords.y * Chunk.ChunkDepth;
        float[,] noiseMap = new float[Chunk.ChunkWidth, Chunk.ChunkDepth];

        for (int x = 0; x < Chunk.ChunkWidth; x++)
        {
            for (int z = 0; z < Chunk.ChunkDepth; z++)
            {
                float globalX = chunkOffsetX + x;
                float globalZ = chunkOffsetZ + z;

                float noiseCoordX = (globalX + _seed) * _noiseScale;
                float noiseCoordZ = (globalZ + _seed) * _noiseScale;

                noiseMap[x, z] = Mathf.PerlinNoise(noiseCoordX, noiseCoordZ);
            }
        }
        return noiseMap;
    }

    /// <summary>
    /// --- ACTUALIZADO (Solo para el hilo principal) ---
    /// Rellena un chunk usando un mapa de ruido.
    /// </summary>
    public void GenerateChunk(Chunk chunk, float[,] noiseMap)
    {
        for (int x = 0; x < Chunk.ChunkWidth; x++)
        {
            for (int z = 0; z < Chunk.ChunkDepth; z++)
            {
                float noiseValue = noiseMap[x, z];

                int terrainHeight = _baseTerrainHeight + Mathf.FloorToInt(noiseValue * _terrainAmplitude);
                if (terrainHeight >= Chunk.ChunkHeight)
                    terrainHeight = Chunk.ChunkHeight - 1;

                for (int y = 0; y < Chunk.ChunkHeight; y++)
                {
                    if (y > terrainHeight)
                    {
                        chunk.SetBlock(x, y, z, _airID);
                    }
                    else if (y == terrainHeight)
                    {
                        chunk.SetBlock(x, y, z, _grassID);
                    }
                    else if (y >= terrainHeight - _dirtLayerDepth)
                    {
                        chunk.SetBlock(x, y, z, _dirtID);
                    }
                    else
                    {
                        chunk.SetBlock(x, y, z, _stoneID);
                    }
                }
            }
        }
    }
}