using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/// <summary>
/// Fills a chunk column using Mathf.PerlinNoise.
/// Not Burst-compiled so terrain stays identical to the previous generator.
/// Still runs on the Unity Job worker threads.
/// </summary>
public struct GenerateChunkJob : IJob
{
    public int ChunkCoordX;
    public int ChunkCoordZ;
    public int Seed;
    public float NoiseScale;
    public int BaseTerrainHeight;
    public int TerrainAmplitude;
    public int DirtLayerDepth;
    public byte AirID;
    public byte GrassID;
    public byte DirtID;
    public byte StoneID;

    public NativeArray<byte> Blocks;

    public void Execute()
    {
        int chunkOffsetX = ChunkCoordX * Chunk.ChunkWidth;
        int chunkOffsetZ = ChunkCoordZ * Chunk.ChunkDepth;

        for (int x = 0; x < Chunk.ChunkWidth; x++)
        {
            for (int z = 0; z < Chunk.ChunkDepth; z++)
            {
                float globalX = chunkOffsetX + x;
                float globalZ = chunkOffsetZ + z;

                float noiseCoordX = (globalX + Seed) * NoiseScale;
                float noiseCoordZ = (globalZ + Seed) * NoiseScale;
                float noiseValue = Mathf.PerlinNoise(noiseCoordX, noiseCoordZ);

                int terrainHeight = BaseTerrainHeight + Mathf.FloorToInt(noiseValue * TerrainAmplitude);
                if (terrainHeight >= Chunk.ChunkHeight)
                    terrainHeight = Chunk.ChunkHeight - 1;

                for (int y = 0; y < Chunk.ChunkHeight; y++)
                {
                    byte blockId;
                    if (y > terrainHeight)
                        blockId = AirID;
                    else if (y == terrainHeight)
                        blockId = GrassID;
                    else if (y >= terrainHeight - DirtLayerDepth)
                        blockId = DirtID;
                    else
                        blockId = StoneID;

                    Blocks[Chunk.ToIndex(x, y, z)] = blockId;
                }
            }
        }
    }
}
