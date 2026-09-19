using NikCraft.Core;
using NikCraft.Render;

namespace NikCraft.Voxel;

public sealed class ChunkMeshData
{
    public float[] OpaqueVertices = Array.Empty<float>();
    public uint[] OpaqueIndices = Array.Empty<uint>();
    public float[] TransparentVertices = Array.Empty<float>();
    public uint[] TransparentIndices = Array.Empty<uint>();

    public bool IsEmpty => OpaqueIndices.Length == 0 && TransparentIndices.Length == 0;

    public int ApproximateBytes =>
        (OpaqueVertices.Length + TransparentVertices.Length) * sizeof(float) +
        (OpaqueIndices.Length + TransparentIndices.Length) * sizeof(uint);
}

/// <summary>
/// Turns a chunk into triangle soup: hidden faces are dropped, corners get baked ambient
/// occlusion and every face receives a constant directional light factor.
/// </summary>
public static class ChunkMesher
{
    private readonly struct Face
    {
        public readonly sbyte NX, NY, NZ;   // outward normal
        public readonly sbyte TX, TY, TZ;   // tangent
        public readonly sbyte BX, BY, BZ;   // bitangent (t x b == n)
        public readonly sbyte OX, OY, OZ;   // origin corner of the quad
        public readonly float Light;

        public Face(
            sbyte nx, sbyte ny, sbyte nz,
            sbyte tx, sbyte ty, sbyte tz,
            sbyte bx, sbyte by, sbyte bz,
            sbyte ox, sbyte oy, sbyte oz,
            float light)
        {
            NX = nx; NY = ny; NZ = nz;
            TX = tx; TY = ty; TZ = tz;
            BX = bx; BY = by; BZ = bz;
            OX = ox; OY = oy; OZ = oz;
            Light = light;
        }
    }

    private static readonly Face[] Faces =
    {
        // +Y top
        new(0, 1, 0, 1, 0, 0, 0, 0, -1, 0, 1, 1, 1.000f),
        // -Y bottom
        new(0, -1, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0.520f),
        // +Z south
        new(0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0.820f),
        // -Z north
        new(0, 0, -1, -1, 0, 0, 0, 1, 0, 1, 0, 0, 0.820f),
        // +X east
        new(1, 0, 0, 0, 0, -1, 0, 1, 0, 1, 0, 1, 0.680f),
        // -X west
        new(-1, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0.680f),
    };

    // Corner order is always: origin, origin+t, origin+t+b, origin+b.
    private static readonly float[] CornerU = { 0f, 1f, 1f, 0f };
    private static readonly float[] CornerV = { 1f, 1f, 0f, 0f };
    private static readonly int[] CornerTSign = { -1, 1, 1, -1 };
    private static readonly int[] CornerBSign = { -1, -1, 1, 1 };
    private static readonly float[] AoTables = { 0.55f, 0.72f, 0.87f, 1.00f };

    public static ChunkMeshData Build(Chunk chunk, World world)
    {
        var opaqueVertices = new List<float>(16384);
        var opaqueIndices = new List<uint>(8192);
        var transparentVertices = new List<float>(2048);
        var transparentIndices = new List<uint>(1024);

        // Allocated once and reused for every face to keep the hot loop allocation free.
        Span<float> ao = stackalloc float[4];

        for (int y = 0; y < Chunk.SizeY; y++)
        {
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                for (int x = 0; x < Chunk.SizeX; x++)
                {
                    BlockType type = chunk.GetBlock(x, y, z);
                    if (type == BlockType.Air)
                    {
                        continue;
                    }

                    BlockInfo info = Blocks.Get(type);
                    bool transparentPass = info.IsTransparent;

                    List<float> vertices = transparentPass ? transparentVertices : opaqueVertices;
                    List<uint> indices = transparentPass ? transparentIndices : opaqueIndices;

                    // A liquid surface that is open to the sky is rendered slightly lower.
                    float shrink = 0f;
                    if (info.IsLiquid && !Blocks.IsLiquid(Sample(chunk, world, x, y + 1, z)))
                    {
                        shrink = 0.12f;
                    }

                    for (int faceIndex = 0; faceIndex < 6; faceIndex++)
                    {
                        Face face = Faces[faceIndex];

                        BlockType neighbourType = Sample(chunk, world, x + face.NX, y + face.NY, z + face.NZ);
                        if (!ShouldDrawFace(info, Blocks.Get(neighbourType)))
                        {
                            continue;
                        }

                        int tileIndex = info.TileForFace(faceIndex);
                        BlockAtlas.GetTileUvBounds(tileIndex, out var uvMin, out var uvMax);

                        int neighborX = x + face.NX;
                        int neighborY = y + face.NY;
                        int neighborZ = z + face.NZ;

                        for (int corner = 0; corner < 4; corner++)
                        {
                            int ts = CornerTSign[corner];
                            int bs = CornerBSign[corner];

                            bool side1 = Blocks.IsOpaque(Sample(
                                chunk, world,
                                neighborX + ts * face.TX,
                                neighborY + ts * face.TY,
                                neighborZ + ts * face.TZ));

                            bool side2 = Blocks.IsOpaque(Sample(
                                chunk, world,
                                neighborX + bs * face.BX,
                                neighborY + bs * face.BY,
                                neighborZ + bs * face.BZ));

                            bool cornerBlock = Blocks.IsOpaque(Sample(
                                chunk, world,
                                neighborX + ts * face.TX + bs * face.BX,
                                neighborY + ts * face.TY + bs * face.BY,
                                neighborZ + ts * face.TZ + bs * face.BZ));

                            int level = side1 && side2
                                ? 0
                                : 3 - ((side1 ? 1 : 0) + (side2 ? 1 : 0) + (cornerBlock ? 1 : 0));

                            ao[corner] = AoTables[level];
                        }

                        uint baseVertex = (uint)(vertices.Count / GpuMesh.FloatsPerVertex);

                        for (int corner = 0; corner < 4; corner++)
                        {
                            int tCoeff = corner is 1 or 2 ? 1 : 0;
                            int bCoeff = corner is 2 or 3 ? 1 : 0;

                            float px = x + face.OX + (tCoeff * face.TX) + (bCoeff * face.BX);
                            float py = y + face.OY + (tCoeff * face.TY) + (bCoeff * face.BY);
                            float pz = z + face.OZ + (tCoeff * face.TZ) + (bCoeff * face.BZ);

                            if (shrink > 0f && (tCoeff * face.TY) + (bCoeff * face.BY) > 0)
                            {
                                py -= shrink;
                            }

                            float u = uvMin.X + (uvMax.X - uvMin.X) * CornerU[corner];
                            float v = uvMax.Y - (uvMax.Y - uvMin.Y) * CornerV[corner];

                            vertices.Add(px);
                            vertices.Add(py);
                            vertices.Add(pz);
                            vertices.Add(u);
                            vertices.Add(v);
                            vertices.Add(face.Light * ao[corner]);
                        }

                        // Flip the quad diagonal when the ambient occlusion would produce a visible seam.
                        if (ao[0] + ao[2] > ao[1] + ao[3])
                        {
                            indices.Add(baseVertex + 0);
                            indices.Add(baseVertex + 1);
                            indices.Add(baseVertex + 2);
                            indices.Add(baseVertex + 0);
                            indices.Add(baseVertex + 2);
                            indices.Add(baseVertex + 3);
                        }
                        else
                        {
                            indices.Add(baseVertex + 1);
                            indices.Add(baseVertex + 2);
                            indices.Add(baseVertex + 3);
                            indices.Add(baseVertex + 1);
                            indices.Add(baseVertex + 3);
                            indices.Add(baseVertex + 0);
                        }
                    }
                }
            }
        }

        return new ChunkMeshData
        {
            OpaqueVertices = opaqueVertices.ToArray(),
            OpaqueIndices = opaqueIndices.ToArray(),
            TransparentVertices = transparentVertices.ToArray(),
            TransparentIndices = transparentIndices.ToArray(),
        };
    }

    private static bool ShouldDrawFace(in BlockInfo self, in BlockInfo neighbour)
    {
        if (neighbour.IsOpaque)
        {
            return false;
        }

        // Two identical translucent blocks (glass next to glass, water next to water) share no face.
        return neighbour.Type != self.Type;
    }

    private static BlockType Sample(Chunk chunk, World world, int x, int y, int z)
    {
        if ((uint)y >= Chunk.SizeY)
        {
            return BlockType.Air;
        }

        if ((uint)x < Chunk.SizeX && (uint)z < Chunk.SizeZ)
        {
            return chunk.GetBlock(x, y, z);
        }

        return world.GetBlock(
            chunk.ChunkX * Chunk.SizeX + x,
            y,
            chunk.ChunkZ * Chunk.SizeZ + z);
    }
}
