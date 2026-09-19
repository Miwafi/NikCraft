using OpenTK.Graphics.OpenGL4;

namespace NikCraft.Core;

/// <summary>A 2D RGBA8 texture uploaded straight from a managed byte buffer.</summary>
public sealed class Texture2D : IDisposable
{
    private bool _disposed;

    public int Handle { get; }
    public int Width { get; }
    public int Height { get; }

    public Texture2D(int width, int height, byte[] rgba, bool mipmaps = true, bool nearest = true)
    {
        Width = width;
        Height = height;

        Handle = GL.GenTexture();
        if (Handle == 0)
        {
            Log.Write($"Texture2D: GL.GenTexture() 返回 0 ({width}x{height})");
            return;
        }

        GL.BindTexture(TextureTarget.Texture2D, Handle);

        TextureMinFilter minFilter = nearest
            ? (mipmaps ? TextureMinFilter.NearestMipmapLinear : TextureMinFilter.Nearest)
            : (mipmaps ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.Linear);
        TextureMagFilter magFilter = nearest ? TextureMagFilter.Nearest : TextureMagFilter.Linear;

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)minFilter);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magFilter);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);


        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, rgba);

        ErrorCode error = GL.GetError();
        if (error != ErrorCode.NoError)
        {
            Log.Write($"Texture2D: TexImage2D({width}x{height}) 失败: {error}");
        }

        if (mipmaps)
        {
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }

        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        GL.ActiveTexture(unit);
        GL.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (Handle != 0)
        {
            GL.DeleteTexture(Handle);
        }
    }
}
