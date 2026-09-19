using OpenTK.Mathematics;

namespace NikCraft.Voxel;

public readonly struct RaycastHit
{
    public readonly bool Hit;
    public readonly int X;
    public readonly int Y;
    public readonly int Z;

    /// <summary>Face normal of the hit block, pointing back towards the ray origin.</summary>
    public readonly int NormalX;
    public readonly int NormalY;
    public readonly int NormalZ;
    public readonly BlockType Block;
    public readonly float Distance;

    private RaycastHit(bool hit, int x, int y, int z, int nx, int ny, int nz, BlockType block, float distance)
    {
        Hit = hit;
        X = x;
        Y = y;
        Z = z;
        NormalX = nx;
        NormalY = ny;
        NormalZ = nz;
        Block = block;
        Distance = distance;
    }

    public static readonly RaycastHit None = new(false, 0, 0, 0, 0, 0, 0, BlockType.Air, 0f);

    public static RaycastHit Create(int x, int y, int z, int nx, int ny, int nz, BlockType block, float distance)
        => new(true, x, y, z, nx, ny, nz, block, distance);

    public Vector3i Neighbour => new(X + NormalX, Y + NormalY, Z + NormalZ);
}

/// <summary>Amanatides &amp; Woo voxel traversal.</summary>
public static class VoxelRaycast
{
    public static RaycastHit Cast(World world, Vector3 origin, Vector3 direction, float maxDistance, bool hitLiquids = false)
    {
        if (direction.LengthSquared < 1e-8f)
        {
            return RaycastHit.None;
        }

        direction = Vector3.Normalize(direction);

        int x = (int)MathF.Floor(origin.X);
        int y = (int)MathF.Floor(origin.Y);
        int z = (int)MathF.Floor(origin.Z);

        int stepX = Math.Sign(direction.X);
        int stepY = Math.Sign(direction.Y);
        int stepZ = Math.Sign(direction.Z);

        float tDeltaX = stepX != 0 ? MathF.Abs(1f / direction.X) : float.PositiveInfinity;
        float tDeltaY = stepY != 0 ? MathF.Abs(1f / direction.Y) : float.PositiveInfinity;
        float tDeltaZ = stepZ != 0 ? MathF.Abs(1f / direction.Z) : float.PositiveInfinity;

        float tMaxX = stepX > 0 ? (x + 1 - origin.X) / direction.X : stepX < 0 ? (x - origin.X) / direction.X : float.PositiveInfinity;
        float tMaxY = stepY > 0 ? (y + 1 - origin.Y) / direction.Y : stepY < 0 ? (y - origin.Y) / direction.Y : float.PositiveInfinity;
        float tMaxZ = stepZ > 0 ? (z + 1 - origin.Z) / direction.Z : stepZ < 0 ? (z - origin.Z) / direction.Z : float.PositiveInfinity;

        int normalX = 0, normalY = 0, normalZ = 0;
        float travelled = 0f;

        // The origin voxel itself is skipped on purpose: the camera may sit inside a leaf or water block.
        for (int guard = 0; guard < 512; guard++)
        {
            if (tMaxX < tMaxY)
            {
                if (tMaxX < tMaxZ)
                {
                    x += stepX;
                    travelled = tMaxX;
                    tMaxX += tDeltaX;
                    normalX = -stepX;
                    normalY = 0;
                    normalZ = 0;
                }
                else
                {
                    z += stepZ;
                    travelled = tMaxZ;
                    tMaxZ += tDeltaZ;
                    normalX = 0;
                    normalY = 0;
                    normalZ = -stepZ;
                }
            }
            else
            {
                if (tMaxY < tMaxZ)
                {
                    y += stepY;
                    travelled = tMaxY;
                    tMaxY += tDeltaY;
                    normalX = 0;
                    normalY = -stepY;
                    normalZ = 0;
                }
                else
                {
                    z += stepZ;
                    travelled = tMaxZ;
                    tMaxZ += tDeltaZ;
                    normalX = 0;
                    normalY = 0;
                    normalZ = -stepZ;
                }
            }

            if (travelled > maxDistance)
            {
                break;
            }

            if ((uint)y >= Chunk.SizeY)
            {
                if (stepY >= 0 && y >= Chunk.SizeY)
                {
                    break;
                }

                if (y < 0)
                {
                    break;
                }

                continue;
            }

            BlockType block = world.GetBlock(x, y, z);
            if (block == BlockType.Air)
            {
                continue;
            }

            if (!hitLiquids && Blocks.IsLiquid(block))
            {
                continue;
            }

            return RaycastHit.Create(x, y, z, normalX, normalY, normalZ, block, travelled);
        }

        return RaycastHit.None;
    }
}
