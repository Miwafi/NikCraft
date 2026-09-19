using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace NikCraft.Core;

/// <summary>Frame based keyboard/mouse state: "is down" plus "went down this frame".</summary>
public sealed class InputState
{
    private readonly HashSet<Keys> _keysDown = new();
    private readonly HashSet<Keys> _keysPressed = new();
    private readonly HashSet<Keys> _keysReleased = new();
    private readonly HashSet<MouseButton> _mouseDown = new();
    private readonly HashSet<MouseButton> _mousePressed = new();

    public float MouseDeltaX { get; private set; }
    public float MouseDeltaY { get; private set; }
    public float ScrollDelta { get; private set; }

    public void OnKeyDown(Keys key)
    {
        if (_keysDown.Add(key))
        {
            _keysPressed.Add(key);
        }
    }

    public void OnKeyUp(Keys key)
    {
        if (_keysDown.Remove(key))
        {
            _keysReleased.Add(key);
        }
    }

    public void OnMouseDown(MouseButton button)
    {
        if (_mouseDown.Add(button))
        {
            _mousePressed.Add(button);
        }
    }

    public void OnMouseUp(MouseButton button) => _mouseDown.Remove(button);

    /// <summary>Upper bound per event; GLFW can report one enormous jump when the cursor first gets locked.</summary>
    private const float MaxDeltaPerEvent = 160f;

    public void OnMouseMove(float dx, float dy)
    {
        MouseDeltaX += Math.Clamp(dx, -MaxDeltaPerEvent, MaxDeltaPerEvent);
        MouseDeltaY += Math.Clamp(dy, -MaxDeltaPerEvent, MaxDeltaPerEvent);
    }

    public void OnScroll(float delta) => ScrollDelta += delta;

    public bool IsKeyDown(Keys key) => _keysDown.Contains(key);
    public bool WasKeyPressed(Keys key) => _keysPressed.Contains(key);
    public bool WasKeyReleased(Keys key) => _keysReleased.Contains(key);
    public bool IsMouseDown(MouseButton button) => _mouseDown.Contains(button);
    public bool WasMousePressed(MouseButton button) => _mousePressed.Contains(button);

    /// <summary>Called at the very end of a frame to clear the per-frame edges.</summary>
    public void EndFrame()
    {
        _keysPressed.Clear();
        _keysReleased.Clear();
        _mousePressed.Clear();
        MouseDeltaX = 0f;
        MouseDeltaY = 0f;
        ScrollDelta = 0f;
    }

    public void Reset()
    {
        _keysDown.Clear();
        _mouseDown.Clear();
        EndFrame();
    }
}
