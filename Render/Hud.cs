using NikCraft.Core;
using NikCraft.Voxel;
using OpenTK.Mathematics;

namespace NikCraft.Render;

/// <summary>Crosshair, hotbar, item label and the F3 debug overlay.</summary>
public sealed class Hud : IDisposable
{
    private const float SlotSize = 46f;
    private const float SlotMargin = 14f;

    private readonly UIRenderer _ui;
    private readonly BitmapFont _font;
    private readonly Texture2D _atlas;
    private readonly List<string> _debugLines = new(24);
    private bool _disposed;

    /// <summary>Set by the game right after the hotbar is scrolled; counts down in seconds.</summary>
    public float ItemLabelTimer;

    public Hud(UIRenderer ui, BitmapFont font, Texture2D atlas)
    {
        _ui = ui;
        _font = font;
        _atlas = atlas;
    }

    public void Render(
        float screenWidth,
        float screenHeight,
        IReadOnlyList<BlockType> hotbar,
        int selectedIndex,
        string itemLabel,
        IReadOnlyList<string>? debugLines,
        string? toast)
    {
        _ui.Begin(screenWidth, screenHeight);

        DrawCrosshair(screenWidth, screenHeight);
        DrawHotbar(screenWidth, screenHeight, hotbar, selectedIndex);

        if (ItemLabelTimer > 0f && !string.IsNullOrEmpty(itemLabel))
        {
            DrawItemLabel(screenWidth, screenHeight, itemLabel);
        }

        if (debugLines is { Count: > 0 })
        {
            DrawDebugPanel(debugLines);
        }

        if (!string.IsNullOrEmpty(toast))
        {
            DrawToast(screenWidth, screenHeight, toast);
        }

        _ui.End();
    }

    private void DrawCrosshair(float width, float height)
    {
        float cx = MathF.Floor(width * 0.5f);
        float cy = MathF.Floor(height * 0.5f);

        var shadow = new Vector4(0f, 0f, 0f, 0.45f);
        var white = new Vector4(1f, 1f, 1f, 0.88f);

        for (int pass = 0; pass < 2; pass++)
        {
            Vector4 color = pass == 0 ? shadow : white;
            float offset = pass == 0 ? 1f : 0f;

            _ui.DrawQuad(cx - 11f + offset, cy - 1f + offset, 8f, 2f, color);
            _ui.DrawQuad(cx + 3f + offset, cy - 1f + offset, 8f, 2f, color);
            _ui.DrawQuad(cx - 1f + offset, cy - 11f + offset, 2f, 8f, color);
            _ui.DrawQuad(cx - 1f + offset, cy + 3f + offset, 2f, 8f, color);
        }
    }

    private void DrawHotbar(float width, float height, IReadOnlyList<BlockType> hotbar, int selectedIndex)
    {
        int count = hotbar.Count;
        float totalWidth = count * SlotSize;
        float startX = MathF.Floor((width - totalWidth) * 0.5f);
        float y = height - SlotSize - SlotMargin;

        var background = new Vector4(0f, 0f, 0f, 0.42f);
        var border = new Vector4(1f, 1f, 1f, 0.18f);

        _ui.DrawQuad(startX - 3f, y - 3f, totalWidth + 6f, SlotSize + 6f, new Vector4(0f, 0f, 0f, 0.28f));

        for (int i = 0; i < count; i++)
        {
            float x = startX + (i * SlotSize);

            if (i == selectedIndex)
            {
                _ui.DrawQuad(x - 2f, y - 2f, SlotSize + 4f, SlotSize + 4f, new Vector4(1f, 1f, 1f, 0.85f));
            }

            _ui.DrawQuad(x, y, SlotSize, SlotSize, i == selectedIndex ? new Vector4(0f, 0f, 0f, 0.30f) : background);
            _ui.DrawQuad(x + 0.5f, y + 0.5f, SlotSize - 1f, SlotSize - 1f, border);

            BlockType block = hotbar[i];
            if (block == BlockType.Air)
            {
                continue;
            }

            int tile = Blocks.Get(block).TileSide;
            BlockAtlas.GetTileUvBounds(tile, out Vector2 min, out Vector2 max);

            _ui.DrawTexturedQuad(
                x + 5f,
                y + 5f,
                SlotSize - 10f,
                SlotSize - 10f,
                min.X,
                max.Y,
                max.X,
                min.Y,
                Vector4.One,
                _atlas.Handle);
        }

        // Slot numbers, wrapped every nine slots.
        for (int i = 0; i < count; i++)
        {
            if (i >= 9)
            {
                break;
            }

            float x = startX + (i * SlotSize) + 4f;
            _ui.DrawText(_font, (i + 1).ToString(), x, y + 3f, 1f, new Vector4(1f, 1f, 1f, 0.75f));
        }
    }

    private void DrawItemLabel(float width, float height, string label)
    {
        float alpha = Math.Clamp(ItemLabelTimer, 0f, 1f);
        float scale = 2f;
        float textWidth = BitmapFont.MeasureWidth(label, scale);
        float x = MathF.Floor((width - textWidth) * 0.5f);
        float y = height - SlotSize - SlotMargin - 34f;

        _ui.DrawText(_font, label, x, y, scale, new Vector4(1f, 1f, 1f, alpha));
    }

    private void DrawDebugPanel(IReadOnlyList<string> lines)
    {
        const float scale = 2f;
        float lineHeight = (BitmapFont.GlyphHeight * scale) + 4f;

        float maxWidth = 0f;
        foreach (string line in lines)
        {
            maxWidth = MathF.Max(maxWidth, BitmapFont.MeasureWidth(line, scale));
        }

        float panelWidth = maxWidth + 16f;
        float panelHeight = (lines.Count * lineHeight) + 12f;

        _ui.DrawQuad(6f, 6f, panelWidth, panelHeight, new Vector4(0f, 0f, 0f, 0.38f));

        float y = 12f;
        foreach (string line in lines)
        {
            _ui.DrawText(_font, line, 14f, y, scale, new Vector4(0.95f, 0.98f, 1f, 0.95f));
            y += lineHeight;
        }
    }

    private void DrawToast(float width, float height, string toast)
    {
        const float scale = 2f;
        float textWidth = BitmapFont.MeasureWidth(toast, scale);
        float x = MathF.Floor((width - textWidth) * 0.5f);

        _ui.DrawText(_font, toast, x, height * 0.18f, scale, new Vector4(1f, 0.95f, 0.6f, 0.95f));
    }

    public void SetDebugLines(IEnumerable<string> lines)
    {
        _debugLines.Clear();
        _debugLines.AddRange(lines);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ui.Dispose();
        _font.Dispose();
    }
}
