using NikCraft.Core;
using NikCraft.Voxel;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace NikCraft.Render;

/// <summary>Draws all loaded chunks: opaque pass first, then the sorted blended pass.</summary>
public sealed class WorldRenderer : IDisposable
{
    private readonly Shader _shader;
    private readonly Frustum _frustum = new();
    private readonly List<(Chunk Chunk, float Distance)> _opaqueBatch = new(512);
    private readonly List<(Chunk Chunk, float Distance)> _transparentBatch = new(128);
    private bool _disposed;

    public Texture2D Atlas { get; }

    public int DrawnChunks { get; private set; }
    public int DrawnTriangles { get; private set; }

    /// <summary>Meshes that had indices but no GL resources behind them (diagnostic only).</summary>
    public int SkippedMeshes { get; private set; }

    public WorldRenderer()
    {
        _shader = new Shader(Shaders.ChunkVertex, Shaders.ChunkFragment, "chunk");
        Atlas = BlockAtlas.CreateTexture();
    }

    /// <summary>A block icon triangle used by the hotbar, resolved into atlas uv space.</summary>
    public static void GetFaceUv(int tileIndex, out float u0, out float v0, out float u1, out float v1)
    {
        BlockAtlas.GetTileUvBounds(tileIndex, out var min, out var max);
        u0 = min.X;
        u1 = max.X;
        v0 = 1f - min.Y;
        v1 = 1f - max.Y;
    }

    public void Render(
        World world,
        Matrix4 viewProjection,
        Vector3 cameraPosition,
        Vector3 fogColor,
        float fogStart,
        float fogEnd,
        float ambient,
        bool wireframe,
        bool drawTransparentPass,
        bool drawOpaquePass)
    {
        CollectVisible(world, viewProjection, cameraPosition, fogEnd);

        _shader.Use();
        _shader.SetMatrix4("uViewProjection", viewProjection);
        _shader.SetInt("uAtlas", 0);
        _shader.SetVector3("uFogColor", fogColor);
        _shader.SetFloat("uFogStart", fogStart);
        _shader.SetFloat("uFogEnd", fogEnd);
        _shader.SetFloat("uAmbient", ambient);

        Atlas.Bind(TextureUnit.Texture0);

        DrawnChunks = 0;
        DrawnTriangles = 0;
        SkippedMeshes = 0;

        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);

        if (wireframe)
        {
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);
        }

        if (drawOpaquePass)
        {
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(TriangleFace.Back);

            // Cutout foliage keeps the depth-writing path but needs alpha testing.
            _shader.SetFloat("uAlphaCutoff", 0.5f);

            foreach ((Chunk chunk, _) in _opaqueBatch)
            {
                if (chunk.OpaqueMesh.IsEmpty)
                {
                    continue;
                }

                if (!chunk.OpaqueMesh.IsReady)
                {
                    SkippedMeshes++;
                    continue;
                }

                Atlas.Bind(TextureUnit.Texture0);
                _shader.SetVector3("uChunkOffset", new Vector3(chunk.ChunkX * Chunk.SizeX, 0f, chunk.ChunkZ * Chunk.SizeZ));
                chunk.OpaqueMesh.Draw();

                DrawnChunks++;
                DrawnTriangles += chunk.OpaqueMesh.IndexCount / 3;
            }
        }

        if (drawTransparentPass)
        {
            GL.DepthMask(false);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Disable(EnableCap.CullFace);

            _shader.SetFloat("uAlphaCutoff", 0f);

            // Far to near so blending stacks correctly.
            for (int i = _transparentBatch.Count - 1; i >= 0; i--)
            {
                Chunk chunk = _transparentBatch[i].Chunk;
                if (chunk.TransparentMesh.IsEmpty)
                {
                    continue;
                }

                if (!chunk.TransparentMesh.IsReady)
                {
                    SkippedMeshes++;
                    continue;
                }

                _shader.SetVector3("uChunkOffset", new Vector3(chunk.ChunkX * Chunk.SizeX, 0f, chunk.ChunkZ * Chunk.SizeZ));
                chunk.TransparentMesh.Draw();

                DrawnChunks++;
                DrawnTriangles += chunk.TransparentMesh.IndexCount / 3;
            }

            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.CullFace);
        }

        if (wireframe)
        {
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        }
    }

    private void CollectVisible(World world, Matrix4 viewProjection, Vector3 cameraPosition, float maxDistance)
    {
        _frustum.Update(viewProjection);
        _opaqueBatch.Clear();
        _transparentBatch.Clear();

        float maxDistanceSquared = maxDistance * maxDistance;

        foreach (Chunk chunk in world.LoadedChunks)
        {
            if (!chunk.IsGenerated)
            {
                continue;
            }

            Vector3 min = new(chunk.ChunkX * Chunk.SizeX, 0f, chunk.ChunkZ * Chunk.SizeZ);
            Vector3 max = new(min.X + Chunk.SizeX, Chunk.SizeY, min.Z + Chunk.SizeZ);

            if (!_frustum.IntersectsAabb(min, max))
            {
                continue;
            }

            float dx = chunk.CenterX - cameraPosition.X;
            float dz = chunk.CenterZ - cameraPosition.Z;
            float distanceSquared = (dx * dx) + (dz * dz);

            if (distanceSquared > maxDistanceSquared)
            {
                continue;
            }

            float distance = MathF.Sqrt(distanceSquared);
            _opaqueBatch.Add((chunk, distance));

            if (!chunk.TransparentMesh.IsEmpty)
            {
                _transparentBatch.Add((chunk, distance));
            }
        }

        _opaqueBatch.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
        _transparentBatch.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shader.Dispose();
        Atlas.Dispose();
    }
}
