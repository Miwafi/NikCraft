namespace NikCraft.Voxel;

public enum BlockType : byte
{
    Air = 0,
    Stone = 1,
    Cobblestone = 2,
    MossyCobblestone = 3,
    Dirt = 4,
    Grass = 5,
    Sand = 6,
    Sandstone = 7,
    Gravel = 8,
    Snow = 9,
    Water = 10,
    Ice = 11,
    OakLog = 12,
    OakLeaves = 13,
    BirchLog = 14,
    BirchLeaves = 15,
    Planks = 16,
    Glass = 17,
    Bricks = 18,
    CoalOre = 19,
    IronOre = 20,
    GoldOre = 21,
    DiamondOre = 22,
    Cactus = 23,
    Pumpkin = 24,
    Bedrock = 25,
}

/// <summary>Tile indices inside the 16x16 texture atlas. Must stay in sync with <see cref="Render.BlockAtlas"/>.</summary>
public static class Tile
{
    public const int GrassTop = 0;
    public const int GrassSide = 1;
    public const int Dirt = 2;
    public const int Stone = 3;
    public const int Cobblestone = 4;
    public const int Sand = 5;
    public const int Sandstone = 6;
    public const int Gravel = 7;
    public const int Snow = 8;
    public const int Water = 9;
    public const int Ice = 10;
    public const int OakLogSide = 11;
    public const int OakLogTop = 12;
    public const int OakLeaves = 13;
    public const int BirchLogSide = 14;
    public const int BirchLogTop = 15;
    public const int BirchLeaves = 16;
    public const int Planks = 17;
    public const int Glass = 18;
    public const int Bricks = 19;
    public const int CoalOre = 20;
    public const int IronOre = 21;
    public const int GoldOre = 22;
    public const int DiamondOre = 23;
    public const int MossyCobblestone = 24;
    public const int Bedrock = 25;
    public const int CactusSide = 26;
    public const int CactusTop = 27;
    public const int PumpkinSide = 28;
    public const int PumpkinTop = 29;
    public const int TileCount = 30;
}

public readonly struct BlockInfo
{
    public readonly BlockType Type;
    public readonly string Name;

    /// <summary>Blocks player movement.</summary>
    public readonly bool IsSolid;

    /// <summary>Completely hides the neighbouring face (used for face culling).</summary>
    public readonly bool IsOpaque;

    /// <summary>Rendered in the blended pass (water, glass, ice).</summary>
    public readonly bool IsTransparent;

    /// <summary>Needs the alpha-test (cutout) path in the opaque pass (leaves).</summary>
    public readonly bool UseAlphaTest;

    public readonly bool IsLiquid;
    public readonly int TileTop;
    public readonly int TileBottom;
    public readonly int TileSide;

    public BlockInfo(
        BlockType type,
        string name,
        bool isSolid,
        bool isOpaque,
        bool isTransparent,
        bool useAlphaTest,
        bool isLiquid,
        int tileTop,
        int tileBottom,
        int tileSide)
    {
        Type = type;
        Name = name;
        IsSolid = isSolid;
        IsOpaque = isOpaque;
        IsTransparent = isTransparent;
        UseAlphaTest = useAlphaTest;
        IsLiquid = isLiquid;
        TileTop = tileTop;
        TileBottom = tileBottom;
        TileSide = tileSide;
    }

    public int TileForFace(int faceIndex) => faceIndex switch
    {
        0 => TileTop,
        1 => TileBottom,
        _ => TileSide,
    };
}

public static class Blocks
{
    private const int TableSize = 256;
    private static readonly BlockInfo[] Table = new BlockInfo[TableSize];

    static Blocks()
    {
        // Air is the "everything false" default, but we still give it a name.
        Table[(byte)BlockType.Air] = new BlockInfo(BlockType.Air, "AIR", false, false, false, false, false, 0, 0, 0);

        Register(BlockType.Stone, "STONE", solid: true, opaque: true, top: Tile.Stone, bottom: Tile.Stone, side: Tile.Stone);
        Register(BlockType.Cobblestone, "COBBLESTONE", solid: true, opaque: true, top: Tile.Cobblestone, bottom: Tile.Cobblestone, side: Tile.Cobblestone);
        Register(BlockType.MossyCobblestone, "MOSSY COBBLE", solid: true, opaque: true, top: Tile.MossyCobblestone, bottom: Tile.MossyCobblestone, side: Tile.MossyCobblestone);
        Register(BlockType.Dirt, "DIRT", solid: true, opaque: true, top: Tile.Dirt, bottom: Tile.Dirt, side: Tile.Dirt);
        Register(BlockType.Grass, "GRASS", solid: true, opaque: true, top: Tile.GrassTop, bottom: Tile.Dirt, side: Tile.GrassSide);
        Register(BlockType.Sand, "SAND", solid: true, opaque: true, top: Tile.Sand, bottom: Tile.Sand, side: Tile.Sand);
        Register(BlockType.Sandstone, "SANDSTONE", solid: true, opaque: true, top: Tile.Sandstone, bottom: Tile.Sandstone, side: Tile.Sandstone);
        Register(BlockType.Gravel, "GRAVEL", solid: true, opaque: true, top: Tile.Gravel, bottom: Tile.Gravel, side: Tile.Gravel);
        Register(BlockType.Snow, "SNOW", solid: true, opaque: true, top: Tile.Snow, bottom: Tile.Snow, side: Tile.Snow);
        Register(BlockType.Bedrock, "BEDROCK", solid: true, opaque: true, top: Tile.Bedrock, bottom: Tile.Bedrock, side: Tile.Bedrock);

        Register(BlockType.OakLog, "OAK LOG", solid: true, opaque: true, top: Tile.OakLogTop, bottom: Tile.OakLogTop, side: Tile.OakLogSide);
        Register(BlockType.BirchLog, "BIRCH LOG", solid: true, opaque: true, top: Tile.BirchLogTop, bottom: Tile.BirchLogTop, side: Tile.BirchLogSide);
        Register(BlockType.Planks, "PLANKS", solid: true, opaque: true, top: Tile.Planks, bottom: Tile.Planks, side: Tile.Planks);
        Register(BlockType.Bricks, "BRICKS", solid: true, opaque: true, top: Tile.Bricks, bottom: Tile.Bricks, side: Tile.Bricks);
        Register(BlockType.Cactus, "CACTUS", solid: true, opaque: true, top: Tile.CactusTop, bottom: Tile.CactusTop, side: Tile.CactusSide);
        Register(BlockType.Pumpkin, "PUMPKIN", solid: true, opaque: true, top: Tile.PumpkinTop, bottom: Tile.PumpkinTop, side: Tile.PumpkinSide);

        Register(BlockType.CoalOre, "COAL ORE", solid: true, opaque: true, top: Tile.CoalOre, bottom: Tile.CoalOre, side: Tile.CoalOre);
        Register(BlockType.IronOre, "IRON ORE", solid: true, opaque: true, top: Tile.IronOre, bottom: Tile.IronOre, side: Tile.IronOre);
        Register(BlockType.GoldOre, "GOLD ORE", solid: true, opaque: true, top: Tile.GoldOre, bottom: Tile.GoldOre, side: Tile.GoldOre);
        Register(BlockType.DiamondOre, "DIAMOND ORE", solid: true, opaque: true, top: Tile.DiamondOre, bottom: Tile.DiamondOre, side: Tile.DiamondOre);

        // Cutout foliage: not opaque (so it does not hide the terrain behind it) but drawn in the depth-writing pass.
        Register(BlockType.OakLeaves, "OAK LEAVES", solid: true, opaque: false, top: Tile.OakLeaves, bottom: Tile.OakLeaves, side: Tile.OakLeaves, alphaTest: true);
        Register(BlockType.BirchLeaves, "BIRCH LEAVES", solid: true, opaque: false, top: Tile.BirchLeaves, bottom: Tile.BirchLeaves, side: Tile.BirchLeaves, alphaTest: true);

        // Blended blocks.
        RegisterLiquid(BlockType.Water, "WATER", Tile.Water);
        RegisterTransparent(BlockType.Glass, "GLASS", Tile.Glass);
        RegisterTransparent(BlockType.Ice, "ICE", Tile.Ice);
    }

    private static void Register(
        BlockType type,
        string name,
        bool solid,
        bool opaque,
        int top,
        int bottom,
        int side,
        bool alphaTest = false)
    {
        Table[(byte)type] = new BlockInfo(type, name, solid, opaque, false, alphaTest, false, top, bottom, side);
    }

    private static void RegisterTransparent(BlockType type, string name, int tile)
    {
        Table[(byte)type] = new BlockInfo(type, name, true, false, true, false, false, tile, tile, tile);
    }

    private static void RegisterLiquid(BlockType type, string name, int tile)
    {
        Table[(byte)type] = new BlockInfo(type, name, false, false, true, false, true, tile, tile, tile);
    }

    public static BlockInfo Get(BlockType type) => Table[(byte)type];

    public static string NameOf(BlockType type) => Table[(byte)type].Name;

    public static bool IsAir(BlockType type) => type == BlockType.Air;

    public static bool IsSolid(BlockType type) => Table[(byte)type].IsSolid;

    public static bool IsOpaque(BlockType type) => Table[(byte)type].IsOpaque;

    public static bool IsLiquid(BlockType type) => Table[(byte)type].IsLiquid;

    public static bool IsTransparent(BlockType type) => Table[(byte)type].IsTransparent;

    /// <summary>Blocks that can be picked from the hotbar / placed by the player.</summary>
    public static readonly BlockType[] PlaceableBlocks =
    {
        BlockType.Grass,
        BlockType.Dirt,
        BlockType.Stone,
        BlockType.Cobblestone,
        BlockType.Planks,
        BlockType.OakLog,
        BlockType.OakLeaves,
        BlockType.Sand,
        BlockType.Bricks,
        BlockType.Glass,
        BlockType.Snow,
        BlockType.Sandstone,
        BlockType.MossyCobblestone,
        BlockType.BirchLog,
        BlockType.BirchLeaves,
        BlockType.Gravel,
        BlockType.Ice,
        BlockType.Cactus,
        BlockType.Pumpkin,
        BlockType.CoalOre,
        BlockType.IronOre,
        BlockType.GoldOre,
        BlockType.DiamondOre,
        BlockType.Water,
    };
}
