using OpenTK.Graphics.OpenGL4;

namespace NikCraft.Core;

/// <summary>
/// GPU-side vertex/index storage. Vertex layout:
/// location 0 -> vec3 position, location 1 -> vec2 uv, location 2 -> float bakedLight.
/// </summary>
public sealed class GpuMesh : IDisposable
{
    public const int FloatsPerVertex = 6;
    public const int Stride = FloatsPerVertex * sizeof(float);

    private bool _disposed;
    private int _vao;
    private int _vbo;
    private int _ebo;

    public int IndexCount { get; private set; }
    public bool IsEmpty => IndexCount == 0;

    /// <summary>True once the VAO/VBO/EBO triplet exists and the mesh can actually be drawn.</summary>
    public bool IsReady => _vao != 0;

    /// <summary>Total number of meshes that tried to allocate GL resources.</summary>
    public static int CreatedCount;

    /// <summary>
    /// Creates the GL objects on demand.
    /// Chunks (and therefore their meshes) are constructed on background worker threads which have no
    /// current OpenGL context, so nothing may touch GL from the constructor.
    /// <see cref="Upload"/> is only ever called from the render thread.
    /// </summary>
    private void EnsureResources()
    {
        if (_vao != 0)
        {
            return;
        }

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ebo = GL.GenBuffer();

        Interlocked.Increment(ref CreatedCount);

        if (_vao == 0)
        {
            Log.Write($"GpuMesh: GL.GenVertexArray() 返回 0 (vao={_vao} vbo={_vbo} ebo={_ebo})");
        }

        GL.BindVertexArray(_vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);

        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, Stride, 0);

        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, Stride, 3 * sizeof(float));

        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, Stride, 5 * sizeof(float));

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }

    /// <summary>Must be called from the render thread.</summary>
    public void Upload(float[] vertices, uint[] indices)
    {
        IndexCount = indices.Length;

        if (indices.Length == 0)
        {
            return;
        }

        EnsureResources();

        GL.BindVertexArray(_vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        GL.BindVertexArray(0);
    }

    /// <summary>Must be called from the render thread.</summary>
    public void Draw()
    {
        if (IndexCount == 0 || _vao == 0)
        {
            return;
        }

        GL.BindVertexArray(_vao);
        GL.DrawElements(PrimitiveType.Triangles, IndexCount, DrawElementsType.UnsignedInt, 0);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IndexCount = 0;

        if (_vao != 0)
        {
            GL.DeleteVertexArray(_vao);
            _vao = 0;
        }

        if (_vbo != 0)
        {
            GL.DeleteBuffer(_vbo);
            _vbo = 0;
        }

        if (_ebo != 0)
        {
            GL.DeleteBuffer(_ebo);
            _ebo = 0;
        }
    }
}
