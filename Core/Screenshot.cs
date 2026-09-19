using OpenTK.Graphics.OpenGL4;

namespace NikCraft.Core;

/// <summary>Reads the current frame buffer into an uncompressed 24-bit BMP file.</summary>
public static class Screenshot
{
    /// <summary>Captures the default frame buffer and returns the written file path.</summary>
    public static string Capture(int width, int height, string? directory = null)
    {
        width = Math.Max(width, 1);
        height = Math.Max(height, 1);

        var pixels = new byte[width * height * 3];

        GL.PixelStore(PixelStoreParameter.PackAlignment, 1);
        GL.ReadPixels(0, 0, width, height, PixelFormat.Bgr, PixelType.UnsignedByte, pixels);

        string targetDirectory = directory ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(targetDirectory);

        string path = Path.Combine(targetDirectory, $"nikcraft-{DateTime.Now:yyyyMMdd-HHmmss-fff}.bmp");
        WriteBmp(path, width, height, pixels);
        return path;
    }

    private static void WriteBmp(string path, int width, int height, byte[] bgrBottomUp)
    {
        int rowStride = ((width * 3) + 3) & ~3;
        int imageSize = rowStride * height;
        int fileSize = 54 + imageSize;

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new BinaryWriter(stream);

        // BITMAPFILEHEADER
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(54);

        // BITMAPINFOHEADER
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(imageSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        var row = new byte[rowStride];
        for (int y = 0; y < height; y++)
        {
            Array.Copy(bgrBottomUp, y * width * 3, row, 0, width * 3);
            writer.Write(row);
        }
    }
}
