using NikCraft.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace NikCraft.Render;

/// <summary>Draws the black wire cube around the block the player is aiming at.</summary>
public sealed class SelectionRenderer : IDisposable
{
    private readonly Shader _shader;
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _vertexCount;
    private bool _disposed;

    public SelectionRenderer()
    {
        _shader = new Shader(Shaders.LineVertex, Shaders.LineFragment, "selection");

        float[] corners =
        {
            0f, 0f, 0f,
            1f, 0f, 0f,
            1f, 0f, 1f,
            0f, 0f, 1f,
            0f, 1f, 0f,
            1f, 1f, 0f,
            1f, 1f, 1f,
            0f, 1f, 1f,
        };

        int[] edges =
        {
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7,
        };

        var vertices = new float[edges.Length * 3];
        for (int i = 0; i < edges.Length; i++)
        {
            int corner = edges[i] * 3;
            vertices[i * 3 + 0] = corners[corner + 0];
            vertices[i * 3 + 1] = corners[corner + 1];
            vertices[i * 3 + 2] = corners[corner + 2];
        }

        _vertexCount = edges.Length;

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    public void Render(Matrix4 viewProjection, Vector3 blockPosition, Vector4 color, float expand = 0.003f)
    {
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthMask(false);
        GL.Disable(EnableCap.CullFace);

        _shader.Use();
        _shader.SetMatrix4("uViewProjection", viewProjection);
        _shader.SetVector3("uOrigin", blockPosition - new Vector3(expand));
        _shader.SetFloat("uScale", 1f + (expand * 2f));
        _shader.SetVector4("uColor", color);

        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Lines, 0, _vertexCount);
        GL.BindVertexArray(0);

        GL.DepthMask(true);
        GL.Disable(EnableCap.Blend);
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
    }
}
