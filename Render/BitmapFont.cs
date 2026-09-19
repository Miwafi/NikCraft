using NikCraft.Core;

namespace NikCraft.Render;

/// <summary>
/// A hand-encoded 5x7 pixel font baked into a strip texture at startup.
/// Glyphs are stored as five column bytes; bit 0 is the top row.
/// </summary>
public sealed class BitmapFont : IDisposable
{
    public const int GlyphWidth = 5;
    public const int GlyphHeight = 7;
    public const int Advance = 6;
    public const int CellHeight = 7;
    public const int FirstChar = 32;
    public const int LastChar = 96;
    private const int GlyphCount = LastChar - FirstChar + 1;
    private const int TextureWidth = GlyphCount * Advance;
    private const int TextureHeight = CellHeight;

    /// <summary>
    /// ASCII 32..96, five columns per glyph. Bit 0 = top pixel row.
    /// Every glyph is two hex characters per column.
    /// </summary>
    private static readonly string[] GlyphData =
    {
        "0000000000", // (space)
        "00005F0000", // !
        "0007000700", // "
        "147F147F14", // #
        "242A7F2A12", // $
        "2313086462", // %
        "3649552250", // &
        "0005030000", // '
        "001C224100", // (
        "0041221C00", // )
        "14083E0814", // *
        "08083E0808", // +
        "0050300000", // ,
        "0808080808", // -
        "0060600000", // .
        "2010080402", // /
        "3E5149453E", // 0
        "00427F4000", // 1
        "4261514946", // 2
        "2141454B31", // 3
        "1814127F10", // 4
        "2745454539", // 5
        "3C4A494930", // 6
        "0171090503", // 7
        "3649494936", // 8
        "064949291E", // 9
        "0036360000", // :
        "0056360000", // ;
        "0008142241", // <
        "1414141414", // =
        "4122140800", // >
        "0201510906", // ?
        "324979413E", // @
        "7E1111117E", // A
        "7F49494936", // B
        "3E41414122", // C
        "7F4141221C", // D
        "7F49494941", // E
        "7F09090101", // F
        "3E41415132", // G
        "7F0808087F", // H
        "00417F4100", // I
        "2040413F01", // J
        "7F08142241", // K
        "7F40404040", // L
        "7F0204027F", // M
        "7F0408107F", // N
        "3E4141413E", // O
        "7F09090906", // P
        "3E4151215E", // Q
        "7F09192946", // R
        "4649494931", // S
        "01017F0101", // T
        "3F4040403F", // U
        "1F2040201F", // V
        "7F2018207F", // W
        "6314081463", // X
        "0304780403", // Y
        "6151494543", // Z
        "00007F4141", // [
        "0204081020", // backslash
        "41417F0000", // ]
        "0402010204", // ^
        "4040404040", // _
        "0001020400", // `
    };

    private bool _disposed;

    public Texture2D Texture { get; }

    public BitmapFont()
    {
        var pixels = new byte[TextureWidth * TextureHeight * 4];

        for (int glyph = 0; glyph < GlyphCount; glyph++)
        {
            string hex = GlyphData[glyph];

            for (int column = 0; column < GlyphWidth; column++)
            {
                byte bits = Convert.ToByte(hex.Substring(column * 2, 2), 16);

                for (int row = 0; row < GlyphHeight; row++)
                {
                    bool on = (bits & (1 << row)) != 0;
                    int x = glyph * Advance + column;

                    // Texture row 0 is the bottom of the image, so flip the glyph vertically.
                    int y = GlyphHeight - 1 - row;

                    int index = ((y * TextureWidth) + x) * 4;
                    byte value = on ? (byte)255 : (byte)0;

                    pixels[index + 0] = 255;
                    pixels[index + 1] = 255;
                    pixels[index + 2] = 255;
                    pixels[index + 3] = value;
                }
            }
        }

        Texture = new Texture2D(TextureWidth, TextureHeight, pixels, mipmaps: false, nearest: true);
    }

    public static int AdvanceFor(char c) => Advance;

    /// <summary>Maps a character to its glyph slot; unknown characters fall back to '?'.</summary>
    public static int GlyphIndex(char c)
    {
        if (c >= 'a' && c <= 'z')
        {
            c = (char)(c - 32);
        }

        if (c < FirstChar || c > LastChar)
        {
            return '?' - FirstChar;
        }

        return c - FirstChar;
    }

    public static void GetGlyphUv(char c, out float u0, out float u1)
    {
        int index = GlyphIndex(c);
        u0 = (index * Advance) / (float)TextureWidth;
        u1 = ((index * Advance) + GlyphWidth) / (float)TextureWidth;
    }

    /// <summary>Width in font pixels (before scaling) of a string.</summary>
    public static float MeasureWidth(string text, float scale)
    {
        return text.Length * Advance * scale;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Texture.Dispose();
    }
}
