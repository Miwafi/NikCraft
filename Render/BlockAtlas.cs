using NikCraft.Core;
using NikCraft.Voxel;
using OpenTK.Mathematics;

namespace NikCraft.Render;

/// <summary>
/// Procedurally paints every block texture at startup and packs them into a single padded atlas,
/// so the game ships with zero external assets.
///
/// Layout: 16x16 cells of 24x24 px (16 px tile + 4 px mirrored padding on each side).
/// The padding prevents neighbouring tiles from bleeding into each other when mip-mapping.
/// </summary>
public static class BlockAtlas
{
    public const int TileSize = 16;
    public const int Padding = 4;
    public const int CellSize = TileSize + Padding * 2;
    public const int GridSize = 16;
    public const int AtlasSize = CellSize * GridSize;

    public static Texture2D CreateTexture()
    {
        MaterialLibrary.EnsureDirectory();

        var atlas = new byte[AtlasSize * AtlasSize * 4];
        var tile = new byte[TileSize * TileSize * 4];
        int loaded = 0;
        int exported = 0;

        for (int i = 0; i < Tile.TileCount; i++)
        {
            Array.Clear(tile, 0, tile.Length);

            if (MaterialLibrary.TryLoad(MaterialLibrary.PathFor(i), tile))
            {
                loaded++;
            }
            else
            {
                PaintTile(i, tile);
                MaterialLibrary.Save(MaterialLibrary.PathFor(i), tile);
                exported++;
            }

            BlitTile(atlas, tile, i);
        }

        Log.Write($"materials: 外置 {loaded} 个, 内置导出 {exported} 个 -> {MaterialLibrary.Directory}");

        return new Texture2D(AtlasSize, AtlasSize, atlas, mipmaps: false, nearest: true);
    }

    /// <summary>Returns the atlas texture coordinates for a point inside a tile (0,0 = top-left of the tile).</summary>
    public static void GetTileUvBounds(int tileIndex, out Vector2 uvMin, out Vector2 uvMax)
    {
        int cellX = (tileIndex % GridSize) * CellSize;
        int cellY = (tileIndex / GridSize) * CellSize;

        float x0 = cellX + Padding;
        float y0 = cellY + Padding;

        uvMin = new Vector2(x0 / AtlasSize, 1f - (y0 + TileSize) / AtlasSize);
        uvMax = new Vector2((x0 + TileSize) / AtlasSize, 1f - y0 / AtlasSize);
    }

    /// <summary>Maps a tile-local uv (0..1, v growing downwards as in image space) to atlas coordinates.</summary>
    public static Vector2 MapUv(int tileIndex, float u, float v)
    {
        GetTileUvBounds(tileIndex, out Vector2 uvMin, out Vector2 uvMax);
        return new Vector2(
            uvMin.X + (uvMax.X - uvMin.X) * u,
            uvMax.Y - (uvMax.Y - uvMin.Y) * v);
    }

    private static void BlitTile(byte[] atlas, byte[] tile, int index)
    {
        int cellX = (index % GridSize) * CellSize;
        int cellY = (index / GridSize) * CellSize;

        for (int py = 0; py < CellSize; py++)
        {
            int sy = Math.Clamp(py - Padding, 0, TileSize - 1);

            // Image row 0 is the top of the picture, but OpenGL texture row 0 is v = 0 (bottom),
            // so the tile rows are written flipped. The UV maths below then line up naturally.
            int dstRow = (AtlasSize - 1 - (cellY + py)) * AtlasSize;

            for (int px = 0; px < CellSize; px++)
            {
                int sx = Math.Clamp(px - Padding, 0, TileSize - 1);

                int src = (sy * TileSize + sx) * 4;
                int dst = (dstRow + cellX + px) * 4;

                atlas[dst + 0] = tile[src + 0];
                atlas[dst + 1] = tile[src + 1];
                atlas[dst + 2] = tile[src + 2];
                atlas[dst + 3] = tile[src + 3];
            }
        }
    }

    // ------------------------------------------------------------------ painting

    private static void PaintTile(int index, byte[] tile)
    {
        switch (index)
        {
            case Tile.GrassTop: PaintGrassTop(tile); break;
            case Tile.GrassSide: PaintGrassSide(tile); break;
            case Tile.Dirt: PaintNoise(tile, 134, 96, 67, 18, 101); break;
            case Tile.Stone: PaintStone(tile); break;
            case Tile.Cobblestone: PaintCobblestone(tile, false); break;
            case Tile.MossyCobblestone: PaintCobblestone(tile, true); break;
            case Tile.Sand: PaintNoise(tile, 220, 208, 163, 12, 107); break;
            case Tile.Sandstone: PaintSandstone(tile); break;
            case Tile.Gravel: PaintGravel(tile); break;
            case Tile.Snow: PaintNoise(tile, 244, 248, 252, 6, 109); break;
            case Tile.Water: PaintWater(tile); break;
            case Tile.Ice: PaintIce(tile); break;
            case Tile.OakLogSide: PaintLogSide(tile, 110, 84, 50, 72, 56, false); break;
            case Tile.OakLogTop: PaintLogTop(tile, 170, 138, 84, 116, 90, 52); break;
            case Tile.OakLeaves: PaintLeaves(tile, 54, 122, 40, 88, 160, 62); break;
            case Tile.BirchLogSide: PaintLogSide(tile, 224, 224, 216, 60, 58, true); break;
            case Tile.BirchLogTop: PaintLogTop(tile, 220, 206, 160, 170, 156, 112); break;
            case Tile.BirchLeaves: PaintLeaves(tile, 106, 160, 64, 132, 186, 84); break;
            case Tile.Planks: PaintPlanks(tile); break;
            case Tile.Glass: PaintGlass(tile); break;
            case Tile.Bricks: PaintBricks(tile); break;
            case Tile.CoalOre: PaintOre(tile, 40, 40, 40, 201); break;
            case Tile.IronOre: PaintOre(tile, 205, 172, 140, 202); break;
            case Tile.GoldOre: PaintOre(tile, 252, 214, 62, 203); break;
            case Tile.DiamondOre: PaintOre(tile, 106, 226, 226, 204); break;
            case Tile.Bedrock: PaintBedrock(tile); break;
            case Tile.CactusSide: PaintCactusSide(tile); break;
            case Tile.CactusTop: PaintCactusTop(tile); break;
            case Tile.PumpkinSide: PaintPumpkinSide(tile); break;
            case Tile.PumpkinTop: PaintNoise(tile, 206, 142, 52, 16, 130); break;
            default: PaintNoise(tile, 200, 0, 200, 20, index + 5000); break;
        }
    }

    private static void PaintNoise(byte[] tile, int r, int g, int b, int variance, int seed, int alpha = 255)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int n = RandRange(x, y, seed, -variance, variance);
                SetPixel(tile, x, y, r + n, g + n, b + n, alpha);
            }
        }
    }

    private static void PaintGrassTop(byte[] tile)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int n = RandRange(x, y, 11, -24, 24);
                int patch = RandRange(x >> 1, y >> 1, 12, -14, 14);
                SetPixel(tile, x, y, 94 + n + patch, 156 + n + patch, 52 + n / 2, 255);
            }
        }
    }

    private static void PaintGrassSide(byte[] tile)
    {
        for (int x = 0; x < TileSize; x++)
        {
            int depth = 3 + Rand(x, 0, 13) % 3;

            for (int y = 0; y < TileSize; y++)
            {
                if (y < depth)
                {
                    int n = RandRange(x, y, 14, -22, 22);
                    SetPixel(tile, x, y, 92 + n, 154 + n, 50 + n / 2, 255);
                }
                else
                {
                    int n = RandRange(x, y, 15, -18, 18);
                    SetPixel(tile, x, y, 134 + n, 96 + n, 67 + n, 255);
                }
            }
        }
    }

    private static void PaintStone(byte[] tile)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int n = RandRange(x, y, 16, -14, 14);
                int blotch = RandRange(x >> 2, y >> 2, 17, -10, 10);
                int v = 126 + n + blotch;
                SetPixel(tile, x, y, v, v, v, 255);
            }
        }
    }

    private static void PaintCobblestone(byte[] tile, bool mossy)
    {
        const int cell = 5;
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int bx = x / cell;
                int by = y / cell;
                int baseValue = 128 + RandRange(bx, by, 18, -22, 16);
                int n = RandRange(x, y, 19, -10, 10);
                int v = baseValue + n;

                if (x % cell == 0 || y % cell == 0)
                {
                    v -= 42;
                }

                int r = v, g = v, b = v;

                if (mossy)
                {
                    int moss = RandRange(x >> 1, y >> 1, 20, 0, 100);
                    if (moss > 62 && v < 140)
                    {
                        r = 62 + n;
                        g = 108 + n;
                        b = 48 + n;
                    }
                }

                SetPixel(tile, x, y, r, g, b, 255);
            }
        }
    }

    private static void PaintSandstone(byte[] tile)
    {
        for (int y = 0; y < TileSize; y++)
        {
            int band = (y % 8) == 0 ? -26 : 0;

            for (int x = 0; x < TileSize; x++)
            {
                int n = RandRange(x, y, 21, -10, 10);
                SetPixel(tile, x, y, 218 + n + band, 205 + n + band, 158 + n + band, 255);
            }
        }
    }

    private static void PaintGravel(byte[] tile)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int n = RandRange(x, y, 22, -34, 26);
                int tint = RandRange(x >> 1, y >> 1, 23, -12, 12);
                SetPixel(tile, x, y, 130 + n + tint, 122 + n, 118 + n, 255);
            }
        }
    }

    private static void PaintWater(byte[] tile)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int wave = (int)(Math.Sin((x + y * 0.7) * 0.9) * 10);
                int n = RandRange(x, y, 24, -8, 8);
                SetPixel(tile, x, y, 48 + wave + n, 108 + wave + n, 196 + wave + n, 178);
            }
        }
    }

    private static void PaintIce(byte[] tile)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int n = RandRange(x, y, 25, -12, 12);
                int crack = ((x * 3 + y * 5) % 23 == 0) ? 22 : 0;
                SetPixel(tile, x, y, 148 + n + crack, 196 + n + crack, 238 + n, 208);
            }
        }
    }

    private static void PaintLogSide(byte[] tile, int r, int g, int b, int darkR, int darkG, bool birchMarks)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int grain = RandRange(x / 2, y, 26, -14, 14);
                int rr = r + grain;
                int gg = g + grain;
                int bb = b + grain;

                if (x % 7 == 3)
                {
                    rr -= 26;
                    gg -= 22;
                    bb -= 18;
                }

                if (birchMarks)
                {
                    // Short horizontal birch dashes.
                    if ((y % 6 == 0 || y % 6 == 1) && Rand(x / 3, y / 6, 27) > 15000 && x % 3 != 2)
                    {
                        rr = 52;
                        gg = 50;
                        bb = 46;
                    }
                }
                else
                {
                    int bark = RandRange(x, y, 28, 0, 32767);
                    if (bark > 32000)
                    {
                        rr = darkR;
                        gg = darkG;
                        bb = 44;
                    }
                }

                SetPixel(tile, x, y, rr, gg, bb, 255);
            }
        }
    }

    private static void PaintLogTop(byte[] tile, int r, int g, int b, int ringR, int ringG, int ringB)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                double dx = x - 7.5;
                double dy = y - 7.5;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                int ring = (int)(dist * 1.6);

                int n = RandRange(x, y, 29, -10, 10);

                if (ring % 2 == 0)
                {
                    SetPixel(tile, x, y, r + n, g + n, b + n, 255);
                }
                else
                {
                    SetPixel(tile, x, y, ringR + n, ringG + n, ringB + n, 255);
                }
            }
        }
    }

    private static void PaintLeaves(byte[] tile, int r, int g, int b, int r2, int g2, int b2)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int n = RandRange(x, y, 30, -22, 22);
                bool light = Rand(x >> 1, y >> 1, 31) > 16000;

                int rr = (light ? r2 : r) + n;
                int gg = (light ? g2 : g) + n;
                int bb = (light ? b2 : b) + n;

                // Punch irregular holes so canopies look airy.
                int holeRoll = Rand(x, y, 32);
                bool edge = x == 0 || y == 0 || x == TileSize - 1 || y == TileSize - 1;
                int threshold = edge ? 26000 : 6500;

                SetPixel(tile, x, y, rr, gg, bb, holeRoll > threshold ? 0 : 255);
            }
        }
    }

    private static void PaintPlanks(byte[] tile)
    {
        for (int y = 0; y < TileSize; y++)
        {
            int plank = y / 4;
            int shade = RandRange(0, plank, 33, -18, 12);

            for (int x = 0; x < TileSize; x++)
            {
                int n = RandRange(x, y, 34, -8, 8);
                int r = 160 + shade + n;
                int g = 124 + shade + n;
                int b = 74 + shade + n;

                if (y % 4 == 3)
                {
                    r -= 46;
                    g -= 40;
                    b -= 26;
                }

                int seam = 3 + (plank * 7) % 11;
                if (x == seam)
                {
                    r -= 34;
                    g -= 30;
                    b -= 20;
                }

                SetPixel(tile, x, y, r, g, b, 255);
            }
        }
    }

    private static void PaintGlass(byte[] tile)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                bool border = x == 0 || y == 0 || x == TileSize - 1 || y == TileSize - 1;
                if (border)
                {
                    SetPixel(tile, x, y, 226, 240, 248, 226);
                    continue;
                }

                bool corner = (x == 1 || y == 1 || x == TileSize - 2 || y == TileSize - 2) &&
                              (x < 3 || y < 3 || x > TileSize - 4 || y > TileSize - 4);
                if (corner)
                {
                    SetPixel(tile, x, y, 214, 232, 244, 120);
                    continue;
                }

                bool highlight = y == x - 3 || y == x - 4;
                if (highlight && x is > 3 and < 12)
                {
                    SetPixel(tile, x, y, 255, 255, 255, 42);
                    continue;
                }

                SetPixel(tile, x, y, 240, 248, 255, 0);
            }
        }
    }

    private static void PaintBricks(byte[] tile)
    {
        for (int y = 0; y < TileSize; y++)
        {
            int row = y / 4;
            int offset = (row % 2 == 0) ? 0 : 4;

            for (int x = 0; x < TileSize; x++)
            {
                int bx = (x + offset) % 8;
                bool mortar = y % 4 == 0 || bx == 0;
                int n = RandRange(x, y, 35, -14, 14);

                if (mortar)
                {
                    SetPixel(tile, x, y, 168 + n, 164 + n, 158 + n, 255);
                }
                else
                {
                    SetPixel(tile, x, y, 150 + n, 76 + n, 62 + n, 255);
                }
            }
        }
    }

    private static void PaintOre(byte[] tile, int oreR, int oreG, int oreB, int seed)
    {
        PaintStone(tile);

        var rng = new Random(seed);
        int blobCount = 6 + seed % 3;
        for (int i = 0; i < blobCount; i++)
        {
            float cx = 2.5f + (float)rng.NextDouble() * 11f;
            float cy = 2.5f + (float)rng.NextDouble() * 11f;
            float radius = 1.3f + (float)rng.NextDouble() * 1.1f;

            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    if (dx * dx + dy * dy > radius * radius)
                    {
                        continue;
                    }

                    int n = RandRange(x, y, seed, -22, 22);
                    SetPixel(tile, x, y, oreR + n, oreG + n, oreB + n, 255);
                }
            }
        }
    }

    private static void PaintBedrock(byte[] tile)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int n = RandRange(x, y, 36, -30, 30);
                int block = RandRange(x >> 1, y >> 1, 37, -18, 18);
                int v = 62 + n + block;
                SetPixel(tile, x, y, v, v, v, 255);
            }
        }
    }

    private static void PaintCactusSide(byte[] tile)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int n = RandRange(x, y, 38, -14, 14);
                int r = 60 + n, g = 124 + n, b = 62 + n;

                if (x == 0 || x == TileSize - 1 || y == TileSize - 1)
                {
                    r = 40;
                    g = 88;
                    b = 44;
                }

                if (x % 5 == 2)
                {
                    r -= 18;
                    g -= 26;
                    b -= 18;
                }

                SetPixel(tile, x, y, r, g, b, 255);
            }
        }
    }

    private static void PaintCactusTop(byte[] tile)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int n = RandRange(x, y, 39, -12, 12);
                double dx = x - 7.5;
                double dy = y - 7.5;
                bool rim = Math.Sqrt(dx * dx + dy * dy) > 6.6;
                SetPixel(tile, x, y, (rim ? 48 : 78) + n, (rim ? 100 : 146) + n, (rim ? 52 : 70) + n, 255);
            }
        }
    }

    private static void PaintPumpkinSide(byte[] tile)
    {
        for (int y = 0; y < TileSize; y++)
        {
            for (int x = 0; x < TileSize; x++)
            {
                int n = RandRange(x, y, 40, -12, 12);
                int r = 214 + n, g = 138 + n, b = 36 + n;

                if (x % 6 == 2)
                {
                    r -= 38;
                    g -= 28;
                    b -= 12;
                }

                SetPixel(tile, x, y, r, g, b, 255);
            }
        }
    }

    // ------------------------------------------------------------------ helpers

    private static void SetPixel(byte[] tile, int x, int y, int r, int g, int b, int a = 255)
    {
        if ((uint)x >= TileSize || (uint)y >= TileSize)
        {
            return;
        }

        int i = (y * TileSize + x) * 4;
        tile[i + 0] = (byte)Math.Clamp(r, 0, 255);
        tile[i + 1] = (byte)Math.Clamp(g, 0, 255);
        tile[i + 2] = (byte)Math.Clamp(b, 0, 255);
        tile[i + 3] = (byte)Math.Clamp(a, 0, 255);
    }

    private static int Rand(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (int)(h & 0x7FFF);
        }
    }

    private static int RandRange(int x, int y, int seed, int min, int max)
    {
        if (max <= min)
        {
            return min;
        }

        return min + Rand(x, y, seed) % (max - min + 1);
    }
}
