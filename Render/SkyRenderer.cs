using NikCraft.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace NikCraft.Render;

/// <summary>Draws the gradient sky dome, the sun disc and its glow with a single full-screen triangle.</summary>
public sealed class SkyRenderer : IDisposable
{
    private readonly Shader _shader;
    private readonly int _vao;
    private readonly int _vbo;
    private bool _disposed;

    public SkyRenderer()
    {
        _shader = new Shader(Shaders.SkyVertex, Shaders.SkyFragment, "sky");

        // A single oversized triangle covers the whole clip space with no diagonal seam.
        float[] vertices = { -1f, -1f, 3f, -1f, -1f, 3f };

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    public void Render(
        Matrix4 viewProjection,
        Vector3 cameraPosition,
        Vector3 sunDirection,
        Vector3 skyTopColor,
        Vector3 horizonColor,
        Vector3 sunColor)
    {
        GL.Disable(EnableCap.DepthTest);
        GL.DepthMask(false);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.Blend);

        _shader.Use();
        _shader.SetMatrix4("uInverseViewProjection", viewProjection.Inverted());
        _shader.SetVector3("uCameraPosition", cameraPosition);
        _shader.SetVector3("uSkyTopColor", skyTopColor);
        _shader.SetVector3("uSkyHorizonColor", horizonColor);
        _shader.SetVector3("uSunDirection", Vector3.Normalize(sunDirection));
        _shader.SetVector3("uSunColor", sunColor);

        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.BindVertexArray(0);

        GL.DepthMask(true);
        GL.Enable(EnableCap.DepthTest);
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
