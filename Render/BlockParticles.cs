using NikCraft.Core;
using NikCraft.Voxel;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace NikCraft.Render;

/// <summary>
/// Debris thrown out when a block is broken. Every particle is a camera-facing quad sampling a
/// small random patch of the broken block's texture, so it reads as a chunk of that block.
/// </summary>
public sealed class BlockParticles : IDisposable
{
    private const int MaxParticles = 4096;
    private const int FloatsPerVertex = 9;   // position(3) + uv(2) + rgba(4)
    private const int VerticesPerParticle = 4;
    private const float Gravity = 18f;
    private const float Drag = 1.4f;

    private struct Particle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Life;
        public float MaxLife;
        public float Size;
        public float Brightness;
        public Vector2 UvMin;
        public Vector2 UvMax;
    }

    private readonly Particle[] _particles = new Particle[MaxParticles];
    private readonly float[] _vertices = new float[MaxParticles * VerticesPerParticle * FloatsPerVertex];
    private readonly uint[] _indices = new uint[MaxParticles * 6];

    private readonly Shader _shader;
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _ebo;

    private int _count;
    private int _next;          // ring buffer cursor, so new debris always appears
    private bool _disposed;

    public int ActiveCount => _count;

    public BlockParticles()
    {
        _shader = new Shader(Shaders.ParticleVertex, Shaders.ParticleFragment, "particle");

        for (int i = 0; i < MaxParticles; i++)
        {
            uint baseVertex = (uint)(i * VerticesPerParticle);
            int offset = i * 6;

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
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, 5 * sizeof(float));

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }

    /// <summary>Burst of debris using the block's side texture.</summary>
    public void SpawnBlockBreak(BlockType type, int blockX, int blockY, int blockZ)
    {
        if (type == BlockType.Air)
        {
            return;
        }

        int tileIndex = Blocks.Get(type).TileSide;
        BlockAtlas.GetTileUvBounds(tileIndex, out Vector2 tileMin, out Vector2 tileMax);

        float tileWidth = tileMax.X - tileMin.X;
        float tileHeight = tileMax.Y - tileMin.Y;

        int count = 24 + Random.Shared.Next(9);

        for (int i = 0; i < count; i++)
        {
            const float patch = 0.25f;
            float offsetU = (float)Random.Shared.NextDouble() * (1f - patch);
            float offsetV = (float)Random.Shared.NextDouble() * (1f - patch);

            var particle = new Particle
            {
                Position = new Vector3(
                    blockX + 0.12f + ((float)Random.Shared.NextDouble() * 0.76f),
                    blockY + 0.12f + ((float)Random.Shared.NextDouble() * 0.76f),
                    blockZ + 0.12f + ((float)Random.Shared.NextDouble() * 0.76f)),

                Velocity = new Vector3(
                    ((float)Random.Shared.NextDouble() - 0.5f) * 4.2f,
                    ((float)Random.Shared.NextDouble() * 3.2f) + 1.4f,
                    ((float)Random.Shared.NextDouble() - 0.5f) * 4.2f),

                Life = 0f,
                MaxLife = 0.55f + ((float)Random.Shared.NextDouble() * 0.65f),
                Size = 0.105f + ((float)Random.Shared.NextDouble() * 0.075f),
                Brightness = 0.82f + ((float)Random.Shared.NextDouble() * 0.26f),

                UvMin = new Vector2(tileMin.X + (tileWidth * offsetU), tileMin.Y + (tileHeight * offsetV)),
                UvMax = new Vector2(tileMin.X + (tileWidth * (offsetU + patch)), tileMin.Y + (tileHeight * (offsetV + patch))),
            };

            _particles[_next] = particle;
            _next = (_next + 1) % MaxParticles;

            if (_count < MaxParticles)
            {
                _count++;
            }
        }
    }

    public void Update(World world, float deltaTime)
    {
        for (int i = 0; i < _count; i++)
        {
            ref Particle particle = ref _particles[i];

            particle.Life += deltaTime;
            if (particle.Life >= particle.MaxLife)
            {
                RemoveAt(i);
                i--;
                continue;
            }

            particle.Velocity.Y -= Gravity * deltaTime;
            particle.Velocity *= 1f - Math.Clamp(Drag * deltaTime, 0f, 0.6f);

            Vector3 position = particle.Position;
            Vector3 velocity = particle.Velocity;

            // Axis-separated collision so debris settles on the ground instead of sinking into it.
            Vector3 stepX = position + new Vector3(velocity.X * deltaTime, 0f, 0f);
            if (IsSolid(world, stepX))
            {
                velocity.X *= -0.25f;
            }
            else
            {
                position.X = stepX.X;
            }

            Vector3 stepZ = position + new Vector3(0f, 0f, velocity.Z * deltaTime);
            if (IsSolid(world, stepZ))
            {
                velocity.Z *= -0.25f;
            }
            else
            {
                position.Z = stepZ.Z;
            }

            Vector3 stepY = position + new Vector3(0f, velocity.Y * deltaTime, 0f);
            if (IsSolid(world, stepY))
            {
                velocity.Y = 0f;
                velocity.X *= 0.7f;
                velocity.Z *= 0.7f;
            }
            else
            {
                position.Y = stepY.Y;
            }

            particle.Position = position;
            particle.Velocity = velocity;
        }
    }

    private static bool IsSolid(World world, Vector3 position) => Blocks.IsSolid(world.GetBlock(
        (int)MathF.Floor(position.X),
        (int)MathF.Floor(position.Y),
        (int)MathF.Floor(position.Z)));

    private void RemoveAt(int index)
    {
        _particles[index] = _particles[_count - 1];
        _count--;
    }

    public void Render(
        Matrix4 viewProjection,
        Vector3 cameraRight,
        Vector3 cameraUp,
        Vector3 cameraPosition,
        Vector3 fogColor,
        float fogStart,
        float fogEnd)
    {
        if (_count == 0)
        {
            return;
        }

        int vertexCursor = 0;

        for (int i = 0; i < _count; i++)
        {
            ref Particle particle = ref _particles[i];

            float lifeRatio = particle.Life / particle.MaxLife;
            float alpha = lifeRatio < 0.65f ? 1f : 1f - ((lifeRatio - 0.65f) / 0.35f);
            alpha = Math.Clamp(alpha, 0f, 1f);

            Vector3 right = cameraRight * particle.Size;
            Vector3 up = cameraUp * particle.Size;

            Vector3 corner0 = particle.Position - right - up;
            Vector3 corner1 = particle.Position + right - up;
            Vector3 corner2 = particle.Position + right + up;
            Vector3 corner3 = particle.Position - right + up;

            float brightness = particle.Brightness;

            WriteVertex(vertexCursor++, corner0, particle.UvMin.X, particle.UvMin.Y, brightness, alpha);
            WriteVertex(vertexCursor++, corner1, particle.UvMax.X, particle.UvMin.Y, brightness, alpha);
            WriteVertex(vertexCursor++, corner2, particle.UvMax.X, particle.UvMax.Y, brightness, alpha);
            WriteVertex(vertexCursor++, corner3, particle.UvMin.X, particle.UvMax.Y, brightness, alpha);
        }

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertexCursor * FloatsPerVertex * sizeof(float), _vertices);

        _shader.Use();
        _shader.SetMatrix4("uViewProjection", viewProjection);
        _shader.SetInt("uAtlas", 0);
        _shader.SetVector3("uFogColor", fogColor);
        _shader.SetFloat("uFogStart", fogStart);
        _shader.SetFloat("uFogEnd", fogEnd);

        GL.ActiveTexture(TextureUnit.Texture0);

        GL.Enable(EnableCap.DepthTest);
        GL.DepthMask(false);
        GL.Disable(EnableCap.CullFace);

        GL.DrawElements(PrimitiveType.Triangles, _count * 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

        GL.DepthMask(true);
        GL.Enable(EnableCap.CullFace);
        GL.BindVertexArray(0);
    }

    private void WriteVertex(int index, Vector3 position, float u, float v, float brightness, float alpha)
    {
        int offset = index * FloatsPerVertex;
        _vertices[offset + 0] = position.X;
        _vertices[offset + 1] = position.Y;
        _vertices[offset + 2] = position.Z;
        _vertices[offset + 3] = u;
        _vertices[offset + 4] = v;
        _vertices[offset + 5] = brightness;
        _vertices[offset + 6] = brightness;
        _vertices[offset + 7] = brightness;
        _vertices[offset + 8] = alpha;
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
