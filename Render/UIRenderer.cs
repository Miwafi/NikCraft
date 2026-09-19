using NikCraft.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace NikCraft.Render;

/// <summary>Immediate-mode style batched 2D renderer working in screen pixels (origin top-left).</summary>
public sealed class UIRenderer : IDisposable
{
    private const int FloatsPerVertex = 8;
    private const int MaxVertices = 65536;
    private const int MaxQuads = MaxVertices / 4;
    private const int MaxIndices = MaxQuads * 6;

    private readonly Shader _shader;
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _ebo;

    private readonly float[] _vertices = new float[MaxVertices * FloatsPerVertex];
    private readonly uint[] _indices = new uint[MaxIndices];

    private int _vertexCount;
    private int _indexCount;
    private int _currentTexture;
    private Matrix4 _projection;
    private bool _disposed;

    public int DrawCalls { get; private set; }

    public UIRenderer()
    {
        _shader = new Shader(Shaders.UiVertex, Shaders.UiFragment, "ui");

        for (int quad = 0; quad < MaxQuads; quad++)
        {
            uint baseVertex = (uint)(quad * 4);
            int offset = quad * 6;

            _indices[offset + 0] = baseVertex + 0;
            _indices[offset + 1] = baseVertex + 1;
            _indices[offset + 2] = baseVertex + 2;
            _indices[offset + 3] = baseVertex + 0;
            _indices[offset + 4] = baseVertex + 2;
            _indices[offset + 5] = baseVertex + 3;
        }

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ebo = GL.GenBuffer();

        GL.BindVertexArray(_vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(_vertices.Length * sizeof(float)), IntPtr.Zero, BufferUsageHint.DynamicDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);

        int stride = FloatsPerVertex * sizeof(float);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, 4 * sizeof(float));

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }

    public void Begin(float screenWidth, float screenHeight)
    {
        _projection = Matrix4.CreateOrthographicOffCenter(0f, screenWidth, screenHeight, 0f, -1f, 1f);
        _vertexCount = 0;
        _indexCount = 0;
        _currentTexture = 0;
        DrawCalls = 0;
    }

    public void End() => Flush();

    private void Flush()
    {
        if (_indexCount == 0)
        {
            return;
        }

        GL.UseProgram(_shader.Handle);
        _shader.SetMatrix4("uProjection", _projection);
        _shader.SetInt("uTexture", 0);
        _shader.SetInt("uUseTexture", _currentTexture != 0 ? 1 : 0);

        if (_currentTexture != 0)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _currentTexture);
        }

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, _vertexCount * FloatsPerVertex * sizeof(float), _vertices);
        GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, IntPtr.Zero);

        DrawCalls++;
        _vertexCount = 0;
        _indexCount = 0;
    }

    private void EnsureTexture(int textureHandle)
    {
        if (_currentTexture == textureHandle)
        {
            return;
        }

        Flush();
        _currentTexture = textureHandle;
    }

    public void DrawQuad(float x, float y, float width, float height, Vector4 color)
    {
        EnsureTexture(0);
        AddQuad(x, y, width, height, 0f, 0f, 0f, 0f, color);
    }

    public void DrawTexturedQuad(float x, float y, float width, float height, float u0, float v0, float u1, float v1, Vector4 color, int textureHandle)
    {
        EnsureTexture(textureHandle);
        AddQuad(x, y, width, height, u0, v0, u1, v1, color);
    }

    public void DrawText(BitmapFont font, string text, float x, float y, float scale, Vector4 color, bool shadow = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (shadow)
        {
            DrawTextInternal(font, text, x + scale, y + scale, scale, new Vector4(0f, 0f, 0f, color.W * 0.65f));
        }

        DrawTextInternal(font, text, x, y, scale, color);
    }

    private void DrawTextInternal(BitmapFont font, string text, float x, float y, float scale, Vector4 color)
    {
        EnsureTexture(font.Texture.Handle);

        float glyphWidth = BitmapFont.GlyphWidth * scale;
        float glyphHeight = BitmapFont.GlyphHeight * scale;
        float advance = BitmapFont.Advance * scale;

        foreach (char c in text)
        {
            if (c == ' ')
            {
                x += advance;
                continue;
            }

            BitmapFont.GetGlyphUv(c, out float u0, out float u1);
            AddQuad(x, y, glyphWidth, glyphHeight, u0, 1f, u1, 0f, color);
            x += advance;
        }
    }

    private void AddQuad(float x, float y, float width, float height, float u0, float v0, float u1, float v1, Vector4 color)
    {
        if (_vertexCount + 4 > MaxVertices || _indexCount + 6 > MaxIndices)
        {
            Flush();
        }

        float x1 = x + width;
        float y1 = y + height;
        int v = _vertexCount;

        WriteVertex(v + 0, x, y, u0, v0, color);
        WriteVertex(v + 1, x1, y, u1, v0, color);
        WriteVertex(v + 2, x1, y1, u1, v1, color);
        WriteVertex(v + 3, x, y1, u0, v1, color);

        _vertexCount += 4;
        _indexCount += 6;
    }

    private void WriteVertex(int index, float x, float y, float u, float v, Vector4 color)
    {
        int offset = index * FloatsPerVertex;
        _vertices[offset + 0] = x;
        _vertices[offset + 1] = y;
        _vertices[offset + 2] = u;
        _vertices[offset + 3] = v;
        _vertices[offset + 4] = color.X;
        _vertices[offset + 5] = color.Y;
        _vertices[offset + 6] = color.Z;
        _vertices[offset + 7] = color.W;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shader.Dispose();
        GL.DeleteVertexArray(_vao);
        GL.DeleteBuffer(_vbo);
        GL.DeleteBuffer(_ebo);
    }
}
