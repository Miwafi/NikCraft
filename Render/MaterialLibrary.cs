using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using NikCraft.Core;

namespace NikCraft.Render;

/// <summary>
/// External block textures. Every tile is a 16x16 PNG inside the "materials" folder next to the
/// executable; on first launch the built-in procedural textures are written out there so they can
/// be repainted in any image editor. Delete a file to fall back to the built-in version.
/// </summary>
public static class MaterialLibrary
{
    public const int TileSize = BlockAtlas.TileSize;

    /// <summary>File name (without extension) for every atlas tile, in <see cref="Voxel.Tile"/> order.</summary>
    public static readonly string[] TileFileNames =
    {
        "grass_top",          // 0
        "grass_side",         // 1
        "dirt",               // 2
        "stone",              // 3
        "cobblestone",        // 4
        "sand",               // 5
        "sandstone",          // 6
        "gravel",             // 7
        "snow",               // 8
        "water",              // 9
        "ice",                // 10
        "oak_log_side",       // 11
        "oak_log_top",        // 12
        "oak_leaves",         // 13
        "birch_log_side",     // 14
        "birch_log_top",      // 15
        "birch_leaves",       // 16
        "planks",             // 17
        "glass",              // 18
        "bricks",             // 19
        "coal_ore",           // 20
        "iron_ore",           // 21
        "gold_ore",           // 22
        "diamond_ore",        // 23
        "mossy_cobblestone",  // 24
        "bedrock",            // 25
        "cactus_side",        // 26
        "cactus_top",         // 27
        "pumpkin_side",       // 28
        "pumpkin_top",        // 29
    };

    public static string Directory => Path.Combine(AppContext.BaseDirectory, "materials");

    public static string PathFor(int tileIndex)
    {
        string name = tileIndex >= 0 && tileIndex < TileFileNames.Length
            ? TileFileNames[tileIndex]
            : tileIndex.ToString();
        return Path.Combine(Directory, name + ".png");
    }

    public static void EnsureDirectory()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
        }
        catch (Exception ex)
        {
            Log.Write($"materials: 无法创建目录 {Directory}: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads a PNG and rescales it into a 16x16 RGBA buffer using nearest-neighbour sampling,
    /// so pixel art keeps hard edges. The returned buffer is top-down (row 0 = top of the image).
    /// </summary>
    public static bool TryLoad(string filePath, byte[] destination)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            using var source = new Bitmap(filePath);
            using var canvas = new Bitmap(TileSize, TileSize, PixelFormat.Format32bppArgb);

            using (Graphics graphics = Graphics.FromImage(canvas))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.DrawImage(source, new Rectangle(0, 0, TileSize, TileSize));
            }

            BitmapData data = canvas.LockBits(
                new Rectangle(0, 0, TileSize, TileSize),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* scan = (byte*)data.Scan0;
                    for (int y = 0; y < TileSize; y++)
                    {
                        byte* row = scan + (y * data.Stride);
                        for (int x = 0; x < TileSize; x++)
                        {
                            int dst = ((y * TileSize) + x) * 4;
                            destination[dst + 0] = row[(x * 4) + 2]; // R
                            destination[dst + 1] = row[(x * 4) + 1]; // G
                            destination[dst + 2] = row[(x * 4) + 0]; // B
                            destination[dst + 3] = row[(x * 4) + 3]; // A
                        }
                    }
                }
            }
            finally
            {
                canvas.UnlockBits(data);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Write($"materials: 读取 {filePath} 失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>Writes a 16x16 RGBA buffer (top-down) to a PNG file.</summary>
    public static void Save(string filePath, byte[] tile)
    {
        try
        {
            using var bitmap = new Bitmap(TileSize, TileSize, PixelFormat.Format32bppArgb);
            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, TileSize, TileSize),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* scan = (byte*)data.Scan0;
                    for (int y = 0; y < TileSize; y++)
                    {
                        byte* row = scan + (y * data.Stride);
                        for (int x = 0; x < TileSize; x++)
                        {
                            int src = ((y * TileSize) + x) * 4;
                            row[(x * 4) + 0] = tile[src + 2]; // B
                            row[(x * 4) + 1] = tile[src + 1]; // G
                            row[(x * 4) + 2] = tile[src + 0]; // R
                            row[(x * 4) + 3] = tile[src + 3]; // A
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            bitmap.Save(filePath, ImageFormat.Png);
        }
        catch (Exception ex)
        {
            Log.Write($"materials: 写入 {filePath} 失败: {ex.Message}");
        }
    }
}
