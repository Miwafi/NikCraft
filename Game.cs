using NikCraft.Core;
using NikCraft.Gameplay;
using NikCraft.Render;
using NikCraft.Voxel;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace NikCraft;

public sealed class Game : GameWindow
{
    private const float FieldOfView = 72f;
    private const float ReachDistance = 5.5f;
    private const float InteractionCooldown = 0.22f;
    private const float MouseSensitivity = 0.12f;

    private readonly InputState _input = new();
    private readonly BlockType[] _hotbar = new BlockType[9];
    private readonly List<string> _debugLines = new(16);

    private World _world = null!;
    private Player _player = null!;
    private WorldRenderer _worldRenderer = null!;
    private SkyRenderer _skyRenderer = null!;
    private SelectionRenderer _selectionRenderer = null!;
    private UIRenderer _uiRenderer = null!;
    private BitmapFont _font = null!;
    private Hud _hud = null!;

    private readonly int _autoScreenshotFrame;
    private int _frameIndex;

    private int _hotbarIndex;
    private string _toast = string.Empty;
    private float _toastTimer;
    private float _timeOfDay = 0.32f;
    private bool _advanceTime = true;
    private bool _showDebug = true;
    private bool _wireframe;
    private bool _mouseCaptured = true;
    private bool _spawnResolved;
    private int _mouseGraceFrames;
    private float _interactionCooldown;
    private float _fps = 60f;
    private double _fpsAccumulator;
    private int _fpsFrames;

    /// <param name="autoScreenshotFrame">
    /// Debug helper: when &gt;= 0 the game saves a screenshot once that frame index is reached and exits.
    /// </param>
    public Game(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings, int autoScreenshotFrame = -1)
        : base(gameWindowSettings, nativeWindowSettings)
    {
        _autoScreenshotFrame = autoScreenshotFrame;
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.ClearColor(0.45f, 0.66f, 0.95f, 1f);

        Log.Write($"OpenGL {GL.GetString(StringName.Version)} | {GL.GetString(StringName.Renderer)}");
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Back);
        GL.FrontFace(FrontFaceDirection.Ccw);

        int seed = Random.Shared.Next(int.MinValue, int.MaxValue);

        _world = new World(seed, renderDistance: 8);
        _player = new Player
        {
            Position = new Vector3(8.5f, 96f, 8.5f),
            Yaw = -32f,
            Pitch = -12f,
        };
        _player.UpdateLook();

        _worldRenderer = new WorldRenderer();
        _skyRenderer = new SkyRenderer();
        _selectionRenderer = new SelectionRenderer();
        _uiRenderer = new UIRenderer();
        _font = new BitmapFont();
        _hud = new Hud(_uiRenderer, _font, _worldRenderer.Atlas);

        for (int i = 0; i < _hotbar.Length; i++)
        {
            _hotbar[i] = Blocks.PlaceableBlocks[i % Blocks.PlaceableBlocks.Length];
        }

        SetMouseCaptured(true);
        VSync = VSyncMode.On;

        ShowToast("NIKCRAFT - 正在生成世界...");

        Log.Write($"世界种子: {seed}");
    }

    protected override void OnUnload()
    {
        _hud?.Dispose();
        _selectionRenderer?.Dispose();
        _skyRenderer?.Dispose();
        _worldRenderer?.Dispose();
        _world?.Dispose();

        base.OnUnload();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, Math.Max(e.Width, 1), Math.Max(e.Height, 1));
    }

    // ------------------------------------------------------------------ input events

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsFocused)
        {
            _input.OnKeyDown(e.Key);
        }
    }

    protected override void OnKeyUp(KeyboardKeyEventArgs e)
    {
        base.OnKeyUp(e);
        _input.OnKeyUp(e.Key);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (IsFocused && _mouseCaptured)
        {
            _input.OnMouseDown(e.Button);
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        _input.OnMouseUp(e.Button);
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsFocused && _mouseCaptured && _mouseGraceFrames <= 0)
        {
            _input.OnMouseMove(e.Delta.X, e.Delta.Y);
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (IsFocused && _mouseCaptured)
        {
            _input.OnScroll(e.OffsetY);
        }
    }

    // ------------------------------------------------------------------ update

    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        float deltaTime = (float)Math.Min(args.Time, 0.05);

        if (_input.WasKeyPressed(Keys.Escape))
        {
            SetMouseCaptured(!_mouseCaptured);
        }

        HandleHotkeys();
        UpdateClock(deltaTime);

        if (_mouseCaptured)
        {
            ApplyMouseLook();
        }

        PlayerInput playerInput = _mouseCaptured ? BuildPlayerInput() : default;
        _player.Update(_world, playerInput, deltaTime);
        _player.Move(_world, deltaTime);

        _world.Update(_player.EyePosition);
        ResolveSpawn();

        if (_mouseCaptured)
        {
            HandleBlockInteraction(deltaTime);
        }

        _interactionCooldown = MathF.Max(0f, _interactionCooldown - deltaTime);
        _toastTimer = MathF.Max(0f, _toastTimer - deltaTime);
        if (_toastTimer <= 0f)
        {
            _toast = string.Empty;
        }

        _hud.ItemLabelTimer = MathF.Max(0f, _hud.ItemLabelTimer - deltaTime);

        UpdateFpsCounter(args.Time);

        _input.EndFrame();
    }

    private void ApplyMouseLook()
    {
        if (_mouseGraceFrames > 0)
        {
            _mouseGraceFrames--;
            return;
        }

        if (_input.MouseDeltaX == 0f && _input.MouseDeltaY == 0f)
        {
            return;
        }

        _player.Yaw += _input.MouseDeltaX * MouseSensitivity;
        _player.Pitch -= _input.MouseDeltaY * MouseSensitivity;
        _player.Pitch = Math.Clamp(_player.Pitch, -89.5f, 89.5f);
        _player.Yaw = ((_player.Yaw % 360f) + 360f) % 360f;
    }

    private PlayerInput BuildPlayerInput()
    {
        PlayerInput input = default;

        if (_input.IsKeyDown(Keys.W)) input.Forward += 1f;
        if (_input.IsKeyDown(Keys.S)) input.Forward -= 1f;
        if (_input.IsKeyDown(Keys.D)) input.Strafe += 1f;
        if (_input.IsKeyDown(Keys.A)) input.Strafe -= 1f;

        input.Jump = _input.IsKeyDown(Keys.Space);
        input.Sneak = _input.IsKeyDown(Keys.LeftShift) || _input.IsKeyDown(Keys.RightShift);
        input.Sprint = _input.IsKeyDown(Keys.LeftControl) || _input.IsKeyDown(Keys.RightControl);

        return input;
    }

    private void HandleHotkeys()
    {
        if (_input.WasKeyPressed(Keys.F3))
        {
            _showDebug = !_showDebug;
        }

        if (_input.WasKeyPressed(Keys.F))
        {
            _player.Flying = !_player.Flying;
            _player.Velocity = Vector3.Zero;
            ShowToast(_player.Flying ? "飞行模式: 开" : "飞行模式: 关");
        }

        if (_input.WasKeyPressed(Keys.G))
        {
            _wireframe = !_wireframe;
            ShowToast(_wireframe ? "线框模式: 开" : "线框模式: 关");
        }

        if (_input.WasKeyPressed(Keys.T))
        {
            _advanceTime = !_advanceTime;
            ShowToast(_advanceTime ? "时间流动: 开" : "时间流动: 关");
        }

        if (_input.WasKeyPressed(Keys.F11))
        {
            WindowState = WindowState == WindowState.Fullscreen ? WindowState.Normal : WindowState.Fullscreen;
        }

        if (_input.WasKeyPressed(Keys.F2))
        {
            CaptureScreenshot();
        }

        if (_input.WasKeyPressed(Keys.Equal))
        {
            _world.RenderDistance = Math.Clamp(_world.RenderDistance + 1, 2, 24);
            ShowToast($"视距: {_world.RenderDistance} 区块");
        }

        if (_input.WasKeyPressed(Keys.Minus))
        {
            _world.RenderDistance = Math.Clamp(_world.RenderDistance - 1, 2, 24);
            ShowToast($"视距: {_world.RenderDistance} 区块");
        }

        for (int i = 0; i < 9; i++)
        {
            if (_input.WasKeyPressed((Keys)((int)Keys.D1 + i)))
            {
                _hotbarIndex = i;
                _hud.ItemLabelTimer = 2f;
            }
        }

        if (_input.ScrollDelta != 0f)
        {
            int direction = _input.ScrollDelta > 0f ? -1 : 1;
            _hotbarIndex = ((_hotbarIndex + direction) % _hotbar.Length + _hotbar.Length) % _hotbar.Length;
            _hud.ItemLabelTimer = 2f;
        }
    }

    private void HandleBlockInteraction(float deltaTime)
    {
        RaycastHit hit = VoxelRaycast.Cast(_world, _player.EyePosition, _player.LookDirection, ReachDistance);

        if (_interactionCooldown > 0f || !hit.Hit)
        {
            return;
        }

        if (_input.IsMouseDown(MouseButton.Left))
        {
            _interactionCooldown = InteractionCooldown;

            if (hit.Block != BlockType.Bedrock)
            {
                _world.SetBlock(hit.X, hit.Y, hit.Z, BlockType.Air);
            }
        }
        else if (_input.IsMouseDown(MouseButton.Right))
        {
            _interactionCooldown = InteractionCooldown;

            Vector3i target = hit.Neighbour;
            if ((uint)target.Y >= Chunk.SizeY)
            {
                return;
            }

            BlockType existing = _world.GetBlock(target.X, target.Y, target.Z);
            bool replaceable = existing == BlockType.Air || Blocks.IsLiquid(existing);

            if (!replaceable || _player.IntersectsBlock(target.X, target.Y, target.Z))
            {
                return;
            }

            _world.SetBlock(target.X, target.Y, target.Z, _hotbar[_hotbarIndex]);
        }
        else if (_input.WasMousePressed(MouseButton.Middle))
        {
            _interactionCooldown = InteractionCooldown;

            int index = Array.IndexOf(_hotbar, hit.Block);
            if (index < 0)
            {
                index = Array.FindIndex(Blocks.PlaceableBlocks, b => b == hit.Block);
                if (index < 0)
                {
                    return;
                }

                index %= _hotbar.Length;
                _hotbar[index] = hit.Block;
            }

            _hotbarIndex = index;
            _hud.ItemLabelTimer = 2f;
        }
    }

    private void ResolveSpawn()
    {
        if (_spawnResolved)
        {
            return;
        }

        Chunk? chunk = _world.GetChunk(0, 0);
        if (chunk is null || !chunk.IsGenerated)
        {
            return;
        }

        int surface = 0;
        for (int y = Chunk.SizeY - 1; y >= 0; y--)
        {
            BlockType block = _world.GetBlock(8, y, 8);
            if (block == BlockType.Air || Blocks.IsLiquid(block))
            {
                continue;
            }

            if (block == BlockType.OakLeaves || block == BlockType.BirchLeaves)
            {
                continue;
            }

            surface = y;
            break;
        }

        _player.Position = new Vector3(8.5f, surface + 1.05f, 8.5f);
        _player.Velocity = Vector3.Zero;
        _spawnResolved = true;

        ShowToast("已到达出生点 - 祝你好运!");
    }

    private void UpdateClock(float deltaTime)
    {
        if (!_advanceTime)
        {
            return;
        }

        // A full day/night cycle takes roughly eight minutes.
        _timeOfDay = (_timeOfDay + (deltaTime * 0.0021f)) % 1f;
    }

    private void UpdateFpsCounter(double frameTime)
    {
        _fpsAccumulator += frameTime;
        _fpsFrames++;

        if (_fpsAccumulator >= 0.5)
        {
            _fps = (float)(_fpsFrames / _fpsAccumulator);
            _fpsAccumulator = 0d;
            _fpsFrames = 0;

            Title = $"NikCraft  |  {_fps:0} FPS  |  区块 {_world.LoadedChunkCount}  |  视距 {_world.RenderDistance}";
        }
    }

    private void ShowToast(string message)
    {
        _toast = message;
        _toastTimer = 2.6f;
    }

    private void SetMouseCaptured(bool captured)
    {
        _mouseCaptured = captured;
        CursorState = captured ? CursorState.Grabbed : CursorState.Normal;

        // Swallow a few frames of mouse input: the first events after grabbing the cursor are bogus.
        _mouseGraceFrames = 8;
        _input.Reset();
    }

    // ------------------------------------------------------------------ render

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        int width = Math.Max(Size.X, 1);
        int height = Math.Max(Size.Y, 1);

        GL.Viewport(0, 0, width, height);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        Vector3 eye = _player.EyePosition;
        Vector3 sunDirection = ComputeSunDirection(out Vector3 skyTop, out Vector3 skyHorizon, out Vector3 sunColor, out float ambient);

        float aspect = width / (float)height;
        float farPlane = (_world.RenderDistance * 16f) + 64f;

        Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(FieldOfView),
            aspect,
            0.08f,
            farPlane);

        Matrix4 view = Matrix4.LookAt(eye, eye + _player.LookDirection, Vector3.UnitY);

        // OpenTK matrices are the transpose of their GLSL counterparts, so combining them in
        // OpenTK order (view * projection) yields exactly what the shader needs once uploaded.
        Matrix4 viewProjection = view * projection;

        _skyRenderer.Render(viewProjection, eye, sunDirection, skyTop, skyHorizon, sunColor);

        float fogEnd = MathF.Max((_world.RenderDistance * 16f) - 8f, 24f);
        float fogStart = MathF.Max(fogEnd - 40f, 8f);

        _worldRenderer.Render(
            _world,
            viewProjection,
            eye,
            skyHorizon,
            fogStart,
            fogEnd,
            ambient,
            _wireframe,
            drawTransparentPass: true,
            drawOpaquePass: true);

        RaycastHit hit = VoxelRaycast.Cast(_world, eye, _player.LookDirection, ReachDistance);
        if (hit.Hit)
        {
            _selectionRenderer.Render(viewProjection, new Vector3(hit.X, hit.Y, hit.Z), new Vector4(0f, 0f, 0f, 0.55f));
        }

        RenderHud(width, height);

        if (_autoScreenshotFrame >= 0 && _frameIndex >= _autoScreenshotFrame)
        {
            string path = CaptureScreenshot();
            Log.Write($"自动截图: {path}");
            Close();
        }

        _frameIndex++;

        SwapBuffers();
    }

    private string CaptureScreenshot()
    {
        string path = Screenshot.Capture(Size.X, Size.Y);
        ShowToast($"截图已保存: {Path.GetFileName(path)}");
        Log.Write($"截图: {path}");
        return path;
    }

    private Vector3 ComputeSunDirection(out Vector3 skyTop, out Vector3 skyHorizon, out Vector3 sunColor, out float ambient)
    {
        float angle = _timeOfDay * MathF.Tau;
        Vector3 sunDirection = Vector3.Normalize(new Vector3(MathF.Cos(angle) * 0.72f, MathF.Sin(angle), 0.38f));
        float sunHeight = sunDirection.Y;

        float dayFactor = Math.Clamp((sunHeight * 2.4f) + 0.32f, 0f, 1f);
        float twilight = Math.Clamp(1f - (MathF.Abs(sunHeight) * 3.2f), 0f, 1f);

        Vector3 nightTop = new(0.020f, 0.030f, 0.090f);
        Vector3 nightHorizon = new(0.052f, 0.072f, 0.150f);
        Vector3 dayTop = new(0.295f, 0.520f, 0.945f);
        Vector3 dayHorizon = new(0.660f, 0.820f, 0.975f);
        Vector3 duskTop = new(0.270f, 0.250f, 0.540f);
        Vector3 duskHorizon = new(0.940f, 0.470f, 0.245f);

        skyTop = Vector3.Lerp(nightTop, dayTop, dayFactor);
        skyHorizon = Vector3.Lerp(nightHorizon, dayHorizon, dayFactor);

        skyTop = Vector3.Lerp(skyTop, duskTop, twilight * 0.55f);
        skyHorizon = Vector3.Lerp(skyHorizon, duskHorizon, twilight * 0.68f);

        sunColor = Vector3.Lerp(new Vector3(1f, 0.52f, 0.20f), new Vector3(1f, 0.96f, 0.86f), dayFactor);
        ambient = 0.34f + (dayFactor * 0.66f);

        return sunDirection;
    }

    private void RenderHud(float width, float height)
    {
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        IReadOnlyList<string>? debugLines = _showDebug ? BuildDebugLines() : null;

        _hud.Render(
            width,
            height,
            _hotbar,
            _hotbarIndex,
            Blocks.NameOf(_hotbar[_hotbarIndex]),
            debugLines,
            _toast);

        GL.Disable(EnableCap.Blend);
        GL.Enable(EnableCap.CullFace);
        GL.Enable(EnableCap.DepthTest);

    }

    private List<string> BuildDebugLines()
    {
        _debugLines.Clear();

        Vector3 position = _player.Position;
        int chunkX = (int)MathF.Floor(position.X) >> 4;
        int chunkZ = (int)MathF.Floor(position.Z) >> 4;
        Biome biome = _world.GetBiomeAt((int)MathF.Floor(position.X), (int)MathF.Floor(position.Z));

        int hours = (int)(((_timeOfDay + 0.25f) % 1f) * 24f);
        int minutes = (int)((((_timeOfDay + 0.25f) % 1f) * 24f * 60f) % 60f);

        _debugLines.Add($"NIKCRAFT   {_fps:0} FPS");
        _debugLines.Add($"XYZ  {position.X:0.0} / {position.Y:0.0} / {position.Z:0.0}");
        _debugLines.Add($"CHUNK  {chunkX} {chunkZ}   BIOME  {BiomeName(biome)}");
        _debugLines.Add($"FACING  {CompassDirection()}   PITCH {_player.Pitch:0}");
        _debugLines.Add($"CHUNKS  {_world.LoadedChunkCount} LOADED  {_world.PendingWorkCount} PENDING");
        _debugLines.Add($"MESH  {_worldRenderer.DrawnChunks} CHUNKS  {_worldRenderer.DrawnTriangles} TRIS");
        _debugLines.Add($"TIME  {hours:00}:{minutes:00}   FLY {(_player.Flying ? "ON" : "OFF")}   GROUND {(_player.OnGround ? "YES" : "NO")}   WATER {(_player.InWater ? "YES" : "NO")}");
        _debugLines.Add("WASD MOVE   SPACE JUMP   CTRL SPRINT   SHIFT SNEAK   F FLY   G WIRE");
        _debugLines.Add("LMB BREAK   RMB PLACE   MMB PICK   1-9 / WHEEL SELECT   +/- VIEW");
        _debugLines.Add("F3 DEBUG   T TIME   F11 FULLSCREEN   ESC RELEASE MOUSE");

        return _debugLines;
    }

    private static string BiomeName(Biome biome) => biome switch
    {
        Biome.Desert => "DESERT",
        Biome.Forest => "FOREST",
        Biome.Snowy => "SNOWY",
        _ => "PLAINS",
    };

    private string CompassDirection()
    {
        string[] labels = { "SOUTH", "SOUTH WEST", "WEST", "NORTH WEST", "NORTH", "NORTH EAST", "EAST", "SOUTH EAST" };
        int index = (int)MathF.Round(_player.Yaw / 45f) % 8;
        if (index < 0)
        {
            index += 8;
        }

        return labels[index];
    }
}
