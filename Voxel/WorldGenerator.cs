namespace NikCraft.Voxel;

public enum Biome
{
    Plains,
    Forest,
    Desert,
    Snowy,
}

/// <summary>Deterministic procedural terrain: continents, biomes, caves, ores and trees.</summary>
public sealed class WorldGenerator
{
    public const int SeaLevel = 62;
    private const int MinTerrainHeight = 6;

    private readonly int _seed;
    private readonly Noise _continent;
    private readonly Noise _hills;
    private readonly Noise _detail;
    private readonly Noise _temperature;
    private readonly Noise _humidity;
    private readonly Noise _caves;
    private readonly Noise _tunnels;
    private readonly Noise _ores;

    public WorldGenerator(int seed)
    {
        _seed = seed;
        _continent = new Noise(seed + 1);
        _hills = new Noise(seed + 2);
        _detail = new Noise(seed + 3);
        _temperature = new Noise(seed + 4);
        _humidity = new Noise(seed + 5);
        _caves = new Noise(seed + 6);
        _tunnels = new Noise(seed + 7);
        _ores = new Noise(seed + 8);
    }

    public float ComputeTerrainHeight(int worldX, int worldZ)
    {
        float continental = _continent.Fbm2D(worldX * 0.0018f, worldZ * 0.0018f, 4);
        float hills = _hills.Fbm2D(worldX * 0.014f, worldZ * 0.014f, 4);
        float detail = _detail.Fbm2D(worldX * 0.06f, worldZ * 0.06f, 2);

        float baseHeight = SeaLevel + 3f + continental * 26f;

        // Flatten coastal areas so beaches do not become cliffs.
        float coast = Math.Clamp(MathF.Abs(baseHeight - SeaLevel) / 8f, 0f, 1f);

        float height = baseHeight + hills * 9f * coast + detail * 2.2f;
        return Math.Clamp(height, MinTerrainHeight, Chunk.SizeY - 26);
    }

    public Biome GetBiome(int worldX, int worldZ)
    {
        float temperature = _temperature.Fbm2D(worldX * 0.0021f, worldZ * 0.0021f, 3);
        float humidity = _humidity.Fbm2D(worldX * 0.0021f + 512f, worldZ * 0.0021f + 512f, 3);

        if (temperature > 0.30f && humidity < 0.05f)
        {
            return Biome.Desert;
        }

        if (temperature < -0.32f)
        {
            return Biome.Snowy;
        }

        return humidity > 0.12f ? Biome.Forest : Biome.Plains;
    }

    public void GenerateChunk(Chunk chunk)
    {
        int baseX = chunk.ChunkX * Chunk.SizeX;
        int baseZ = chunk.ChunkZ * Chunk.SizeZ;

        var heights = new int[Chunk.SizeX * Chunk.SizeZ];
        var biomes = new Biome[Chunk.SizeX * Chunk.SizeZ];

        for (int z = 0; z < Chunk.SizeZ; z++)
        {
            for (int x = 0; x < Chunk.SizeX; x++)
            {
                int wx = baseX + x;
                int wz = baseZ + z;

                int height = (int)ComputeTerrainHeight(wx, wz);
                heights[z * Chunk.SizeX + x] = height;

                Biome biome = GetBiome(wx, wz);
                biomes[z * Chunk.SizeX + x] = biome;

                bool underwater = height < SeaLevel;
                BlockType surface = SurfaceBlock(biome, underwater);
                BlockType subSurface = SubSurfaceBlock(biome, underwater);

                chunk.SetBlockUnchecked(x, 0, z, BlockType.Bedrock);

                for (int y = 1; y <= height; y++)
                {
                    BlockType block;
                    if (y <= 2 && Hash01(wx, y * 31 + wz, _seed + 55) < 0.55f)
                    {
                        block = BlockType.Bedrock;
                    }
                    else if (y == height)
                    {
                        block = surface;
                    }
                    else if (y > height - 4)
                    {
                        block = subSurface;
                    }
                    else
                    {
                        block = BlockType.Stone;
                    }

                    chunk.SetBlockUnchecked(x, y, z, block);
                }

                if (biome == Biome.Snowy && height >= SeaLevel && height < Chunk.SizeY - 1)
                {
                    chunk.SetBlockUnchecked(x, height, z, BlockType.Snow);
                }

                for (int y = height + 1; y <= SeaLevel; y++)
                {
                    chunk.SetBlockUnchecked(x, y, z, BlockType.Water);
                }

                // Patchy snow/ice sheet on cold oceans and lakes.
                if (biome == Biome.Snowy && height <= SeaLevel && _detail.Noise2D(wx * 0.08f, wz * 0.08f) > 0.25f)
                {
                    chunk.SetBlockUnchecked(x, SeaLevel, z, BlockType.Ice);
                }
            }
        }

        CarveCaves(chunk, heights);
        GenerateOres(chunk, baseX, baseZ);
        Decorate(chunk, baseX, baseZ, biomes);
    }

    private static BlockType SurfaceBlock(Biome biome, bool underwater)
    {
        if (underwater)
        {
            return biome == Biome.Snowy ? BlockType.Gravel : BlockType.Sand;
        }

        return biome switch
        {
            Biome.Desert => BlockType.Sand,
            Biome.Snowy => BlockType.Snow,
            _ => BlockType.Grass,
        };
    }

    private static BlockType SubSurfaceBlock(Biome biome, bool underwater)
    {
        if (biome == Biome.Desert || underwater)
        {
            return BlockType.Sandstone;
        }

        return BlockType.Dirt;
    }

    private void CarveCaves(Chunk chunk, int[] heights)
    {
        int baseX = chunk.ChunkX * Chunk.SizeX;
        int baseZ = chunk.ChunkZ * Chunk.SizeZ;

        for (int z = 0; z < Chunk.SizeZ; z++)
        {
            for (int x = 0; x < Chunk.SizeX; x++)
            {
                int surface = heights[z * Chunk.SizeX + x];
                int maxY = Math.Min(surface - 2, Chunk.SizeY - 2);
                int wx = baseX + x;
                int wz = baseZ + z;

                for (int y = 3; y < maxY; y++)
                {
                    BlockType current = chunk.GetBlock(x, y, z);
                    if (current == BlockType.Air || current == BlockType.Water || current == BlockType.Bedrock)
                    {
                        continue;
                    }

                    float cheese = _caves.Fbm3D(wx * 0.052f, y * 0.085f, wz * 0.052f, 3);
                    float tunnel = _tunnels.Noise3D(wx * 0.032f, y * 0.06f, wz * 0.032f);

                    bool isCheese = cheese > 0.60f;
                    bool isTunnel = MathF.Abs(tunnel) < 0.042f;

                    if (isCheese || isTunnel)
                    {
                        chunk.SetBlockUnchecked(x, y, z, BlockType.Air);
                    }
                }
            }
        }
    }

    private void GenerateOres(Chunk chunk, int baseX, int baseZ)
    {
        for (int z = 0; z < Chunk.SizeZ; z++)
        {
            for (int x = 0; x < Chunk.SizeX; x++)
            {
                int wx = baseX + x;
                int wz = baseZ + z;

                for (int y = 1; y < 72; y++)
                {
                    if (chunk.GetBlock(x, y, z) != BlockType.Stone)
                    {
                        continue;
                    }

                    float vein = _ores.Noise3D(wx * 0.10f, y * 0.10f, wz * 0.10f);

                    if (MathF.Abs(vein) > 0.30f)
                    {
                        continue;
                    }

                    float roll = Hash01(wx * 7 + y, wz * 13 + y * 3, _seed + 91);

                    if (y < 14 && roll > 0.9955f)
                    {
                        chunk.SetBlockUnchecked(x, y, z, BlockType.DiamondOre);
                    }
                    else if (y < 28 && roll > 0.9925f)
                    {
                        chunk.SetBlockUnchecked(x, y, z, BlockType.GoldOre);
                    }
                    else if (y < 46 && roll > 0.980f)
                    {
                        chunk.SetBlockUnchecked(x, y, z, BlockType.IronOre);
                    }
                    else if (roll > 0.960f)
                    {
                        chunk.SetBlockUnchecked(x, y, z, BlockType.CoalOre);
                    }
                }
            }
        }
    }

    private void Decorate(Chunk chunk, int baseX, int baseZ, Biome[] biomes)
    {
        // Scan a margin around the chunk so trees rooted in a neighbour still contribute their canopy.
        for (int dz = -3; dz < Chunk.SizeZ + 3; dz++)
        {
            for (int dx = -3; dx < Chunk.SizeX + 3; dx++)
            {
                int wx = baseX + dx;
                int wz = baseZ + dz;

                Biome biome = dx >= 0 && dx < Chunk.SizeX && dz >= 0 && dz < Chunk.SizeZ
                    ? biomes[dz * Chunk.SizeX + dx]
                    : GetBiome(wx, wz);

                if (!IsTreeCandidate(wx, wz, biome))
                {
                    continue;
                }

                PlaceTree(chunk, dx, dz, wx, wz, biome);
            }
        }
    }

    private bool IsTreeCandidate(int wx, int wz, Biome biome)
    {
        float chance = biome switch
        {
            Biome.Forest => 0.085f,
            Biome.Plains => 0.014f,
            Biome.Snowy => 0.020f,
            _ => 0f,
        };

        if (chance <= 0f)
        {
            return false;
        }

        float roll = Hash01(wx, wz, _seed + 313);
        if (roll >= chance)
        {
            return false;
        }

        // Keep a minimum spacing: only the strongest candidate inside a 5x5 window survives.
        float best = roll;
        for (int oz = -2; oz <= 2; oz++)
        {
            for (int ox = -2; ox <= 2; ox++)
            {
                if (ox == 0 && oz == 0)
                {
                    continue;
                }

                float other = Hash01(wx + ox, wz + oz, _seed + 313);
                if (other < best)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void PlaceTree(Chunk chunk, int localX, int localZ, int wx, int wz, Biome biome)
    {
        float heightF = ComputeTerrainHeight(wx, wz);
        int groundY = (int)heightF;

        if (groundY <= SeaLevel || groundY >= Chunk.SizeY - 12)
        {
            return;
        }

        bool birch = biome == Biome.Snowy || Hash01(wx, wz, _seed + 71) > 0.68f;
        BlockType log = birch ? BlockType.BirchLog : BlockType.OakLog;
        BlockType leaves = birch ? BlockType.BirchLeaves : BlockType.OakLeaves;

        int trunkHeight = 4 + (int)(Hash01(wx, wz, _seed + 97) * 3f);

        for (int y = 0; y <= trunkHeight; y++)
        {
            chunk.SetBlock(localX, groundY + y, localZ, log);
        }

        int canopyBase = groundY + trunkHeight - 2;
        int canopyTop = groundY + trunkHeight + 1;

        for (int y = canopyBase; y <= canopyTop; y++)
        {
            int radius = y >= canopyTop ? 1 : 2;

            for (int oz = -radius; oz <= radius; oz++)
            {
                for (int ox = -radius; ox <= radius; ox++)
                {
                    if (radius == 2 && Math.Abs(ox) == 2 && Math.Abs(oz) == 2)
                    {
                        // Trim the corners of the lower two layers so the canopy is rounded.
                        if (y != canopyBase)
                        {
                            continue;
                        }
                    }

                    if (chunk.GetBlock(localX + ox, y, localZ + oz) != BlockType.Air)
                    {
                        continue;
                    }

                    chunk.SetBlock(localX + ox, y, localZ + oz, leaves);
                }
            }
        }
    }

    /// <summary>Deterministic 0..1 hash for a world column.</summary>
    private static float Hash01(int x, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 0x27d4eb2d) ^ (uint)(z * 0x165667b1) ^ ((uint)seed * 0x9e3779b9);
            h ^= h >> 15;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
