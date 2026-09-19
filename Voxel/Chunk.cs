using NikCraft.Core;

namespace NikCraft.Voxel;

/// <summary>A 16 x 128 x 16 column of blocks plus its two GPU meshes.</summary>
public sealed class Chunk
{
    public const int SizeX = 16;
    public const int SizeY = 128;
    public const int SizeZ = 16;
    public const int BlockCount = SizeX * SizeY * SizeZ;

    private readonly byte[] _blocks = new byte[BlockCount];

    public int ChunkX { get; }
    public int ChunkZ { get; }

    /// <summary>Set once the generator finished writing terrain. Read from worker threads.</summary>
    public volatile bool IsGenerated;

    /// <summary>Incremented on every block edit so stale mesh builds can be discarded.</summary>
    public int Version;

    /// <summary>The chunk version the currently uploaded mesh was built from.</summary>
    public int MeshedVersion = -1;

    /// <summary>Set when the mesh must be rebuilt for the next frame.</summary>
    public volatile bool MeshDirty = true;

    /// <summary>Guard so a chunk is only enqueued once for a rebuild (0 = free, 1 = queued).</summary>
    public int MeshQueued;

    public GpuMesh OpaqueMesh { get; } = new();
    public GpuMesh TransparentMesh { get; } = new();

    /// <summary>Bounding sphere-ish radius used for distance sorting.</summary>
    public float CenterX => ChunkX * SizeX + SizeX * 0.5f;
    public float CenterZ => ChunkZ * SizeZ + SizeZ * 0.5f;

    public Chunk(int chunkX, int chunkZ)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
    }

    public static int Index(int x, int y, int z) => (y * SizeZ + z) * SizeX + x;

    public BlockType GetBlock(int x, int y, int z)
    {
        if ((uint)x >= SizeX || (uint)y >= SizeY || (uint)z >= SizeZ)
        {
            return BlockType.Air;
        }

        return (BlockType)_blocks[Index(x, y, z)];
    }

    /// <summary>Out-of-range writes are silently ignored, which lets trees straddle chunk borders.</summary>
    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if ((uint)x >= SizeX || (uint)y >= SizeY || (uint)z >= SizeZ)
        {
            return;
        }

        _blocks[Index(x, y, z)] = (byte)type;
    }

    /// <summary>Write without any bounds check bookkeeping; used by the generator only.</summary>
    public void SetBlockUnchecked(int x, int y, int z, BlockType type)
    {
        _blocks[Index(x, y, z)] = (byte)type;
    }

    /// <summary>Vertical column count used by the generator to know where the surface is.</summary>
    public int GetTopSolidY(int x, int z)
    {
        for (int y = SizeY - 1; y >= 0; y--)
        {
            if (_blocks[Index(x, y, z)] != (byte)BlockType.Air)
            {
                return y;
            }
        }

        return 0;
    }
}
