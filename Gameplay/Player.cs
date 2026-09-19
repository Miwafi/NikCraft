using NikCraft.Voxel;
using OpenTK.Mathematics;

namespace NikCraft.Gameplay;

public struct PlayerInput
{
    public float Forward;
    public float Strafe;
    public bool Jump;
    public bool Sneak;
    public bool Sprint;
}

/// <summary>First person player with an axis-separated AABB collision solver.</summary>
public sealed class Player
{
    public const float HalfWidth = 0.3f;
    public const float Height = 1.8f;
    public const float EyeHeight = 1.62f;

    private const float Gravity = 28f;
    private const float JumpSpeed = 8.6f;
    private const float WalkSpeed = 4.317f;
    private const float SprintSpeed = 5.612f;
    private const float SneakSpeed = 1.35f;
    private const float FlySpeed = 11.5f;
    private const float SwimSpeed = 3.4f;
    private const float WaterGravity = 5.5f;
    private const float MaxFallSpeed = 60f;
    private const float MoveStep = 0.05f;

    public Vector3 Position;
    public Vector3 Velocity;
    public float Yaw;
    public float Pitch;

    public bool OnGround;
    public bool Flying;
    public bool InWater;
    public bool Sprinting;

    public Vector3 EyePosition => Position + new Vector3(0f, EyeHeight, 0f);

    public Vector3 LookDirection { get; private set; } = -Vector3.UnitZ;
    public Vector3 ForwardFlat { get; private set; } = -Vector3.UnitZ;
    public Vector3 RightFlat { get; private set; } = Vector3.UnitX;

    public void UpdateLook()
    {
        float yawRad = MathHelper.DegreesToRadians(Yaw);
        float pitchRad = MathHelper.DegreesToRadians(Math.Clamp(Pitch, -89.5f, 89.5f));

        float cosPitch = MathF.Cos(pitchRad);
        LookDirection = Vector3.Normalize(new Vector3(
            cosPitch * MathF.Sin(yawRad),
            MathF.Sin(pitchRad),
            -cosPitch * MathF.Cos(yawRad)));

        ForwardFlat = Vector3.Normalize(new Vector3(MathF.Sin(yawRad), 0f, -MathF.Cos(yawRad)));
        RightFlat = new Vector3(MathF.Cos(yawRad), 0f, MathF.Sin(yawRad));
    }

    public void Update(World world, in PlayerInput input, float deltaTime)
    {
        UpdateLook();

        InWater = IsInsideLiquid(world);

        Vector3 wish = (ForwardFlat * input.Forward) + (RightFlat * input.Strafe);
        if (wish.LengthSquared > 1e-6f)
        {
            wish = Vector3.Normalize(wish);
        }

        Sprinting = input.Sprint && input.Forward > 0.1f && !input.Sneak;

        if (Flying)
        {
            UpdateFlight(input, wish, deltaTime);
        }
        else
        {
            UpdateWalking(world, input, wish, deltaTime);
        }
    }

    private void UpdateFlight(in PlayerInput input, Vector3 wish, float deltaTime)
    {
        float speed = FlySpeed * (input.Sprint ? 2f : 1f);
        Vector3 desired = wish * speed;

        if (input.Jump)
        {
            desired.Y += speed;
        }

        if (input.Sneak)
        {
            desired.Y -= speed;
        }

        Velocity = Vector3.Lerp(Velocity, desired, Math.Clamp(deltaTime * 12f, 0f, 1f));
        OnGround = false;
    }

    private void UpdateWalking(World world, in PlayerInput input, Vector3 wish, float deltaTime)
    {
        float speed = input.Sneak ? SneakSpeed : Sprinting ? SprintSpeed : WalkSpeed;

        if (InWater)
        {
            speed = SwimSpeed * (Sprinting ? 1.35f : 1f);
        }

        float acceleration = OnGround || InWater ? 14f : 4f;

        Vector3 horizontal = new(Velocity.X, 0f, Velocity.Z);
        Vector3 target = wish * speed;
        horizontal = Vector3.Lerp(horizontal, target, Math.Clamp(deltaTime * acceleration, 0f, 1f));

        Velocity.X = horizontal.X;
        Velocity.Z = horizontal.Z;

        if (InWater)
        {
            Velocity.Y -= WaterGravity * deltaTime;

            if (input.Jump)
            {
                Velocity.Y = SwimSpeed * 0.75f;
            }

            Velocity.Y = Math.Clamp(Velocity.Y, -6f, 6f);
            Velocity.Y *= 1f - Math.Clamp(deltaTime * 3.2f, 0f, 0.9f);
        }
        else
        {
            if (input.Jump && OnGround)
            {
                Velocity.Y = JumpSpeed;
                OnGround = false;
            }

            Velocity.Y -= Gravity * deltaTime;
            Velocity.Y = MathF.Max(Velocity.Y, -MaxFallSpeed);
        }
    }

    /// <summary>Applies gravity/momentum, stepping through blocks one axis at a time.</summary>
    public void Move(World world, float deltaTime)
    {
        bool startBlocked = Collides(world, Position);

        OnGround = false;
        MoveAxis(world, 0, Velocity.X * deltaTime, startBlocked);
        MoveAxis(world, 2, Velocity.Z * deltaTime, startBlocked);
        MoveAxis(world, 1, Velocity.Y * deltaTime, startBlocked);

        // Safety net: never let the player fall out of the world.
        if (Position.Y < -24f)
        {
            Position = new Vector3(Position.X, 96f, Position.Z);
            Velocity = Vector3.Zero;
        }
    }

    private void MoveAxis(World world, int axis, float amount, bool ignoreCollisions)
    {
        if (MathF.Abs(amount) < 1e-5f)
        {
            return;
        }

        float remaining = amount;

        while (MathF.Abs(remaining) > 1e-5f)
        {
            float step = Math.Clamp(remaining, -MoveStep, MoveStep);
            remaining -= step;

            Vector3 candidate = Position;
            switch (axis)
            {
                case 0: candidate.X += step; break;
                case 1: candidate.Y += step; break;
                default: candidate.Z += step; break;
            }

            if (!ignoreCollisions && Collides(world, candidate))
            {
                if (axis == 1)
                {
                    if (step < 0f)
                    {
                        OnGround = true;
                    }

                    Velocity.Y = 0f;
                }
                else if (axis == 0)
                {
                    Velocity.X = 0f;
                }
                else
                {
                    Velocity.Z = 0f;
                }

                return;
            }

            Position = candidate;
        }
    }

    public bool Collides(World world, Vector3 position)
    {
        float minX = position.X - HalfWidth;
        float maxX = position.X + HalfWidth;
        float minY = position.Y;
        float maxY = position.Y + Height;
        float minZ = position.Z - HalfWidth;
        float maxZ = position.Z + HalfWidth;

        const float epsilon = 1e-4f;

        int x0 = (int)MathF.Floor(minX);
        int x1 = (int)MathF.Floor(maxX - epsilon);
        int y0 = (int)MathF.Floor(minY);
        int y1 = (int)MathF.Floor(maxY - epsilon);
        int z0 = (int)MathF.Floor(minZ);
        int z1 = (int)MathF.Floor(maxZ - epsilon);

        for (int y = y0; y <= y1; y++)
        {
            if ((uint)y >= Chunk.SizeY)
            {
                continue;
            }

            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (Blocks.IsSolid(world.GetBlock(x, y, z)))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public bool IsInsideLiquid(World world)
    {
        Vector3 feet = Position + new Vector3(0f, 0.1f, 0f);
        Vector3 chest = Position + new Vector3(0f, 1.1f, 0f);

        return Blocks.IsLiquid(world.GetBlock((int)MathF.Floor(feet.X), (int)MathF.Floor(feet.Y), (int)MathF.Floor(feet.Z)))
            || Blocks.IsLiquid(world.GetBlock((int)MathF.Floor(chest.X), (int)MathF.Floor(chest.Y), (int)MathF.Floor(chest.Z)));
    }

    /// <summary>Returns true when a block placed at this position would intersect the player.</summary>
    public bool IntersectsBlock(int blockX, int blockY, int blockZ)
    {
        float minX = Position.X - HalfWidth;
        float maxX = Position.X + HalfWidth;
        float minY = Position.Y;
        float maxY = Position.Y + Height;
        float minZ = Position.Z - HalfWidth;
        float maxZ = Position.Z + HalfWidth;

        return blockX + 1 > minX && blockX < maxX
            && blockY + 1 > minY && blockY < maxY
            && blockZ + 1 > minZ && blockZ < maxZ;
    }
}
