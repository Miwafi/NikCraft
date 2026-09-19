using System.Runtime.InteropServices;
using NikCraft;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace NikCraft;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            Run();
        }
        catch (Exception ex)
        {
            ReportFatal(ex);
        }
    }

    private static void Run()
    {
        int autoScreenshotFrame = -1;
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 1; i < arguments.Length - 1; i++)
        {
            if (arguments[i] == "--shot" && int.TryParse(arguments[i + 1], out int frame))
            {
                autoScreenshotFrame = frame;
            }
        }

        var nativeSettings = new NativeWindowSettings
        {
            ClientSize = new Vector2i(1440, 810),
            Title = "NikCraft  |  C# + OpenTK + OpenGL 3.3",
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible,
            WindowState = WindowState.Normal,
            WindowBorder = WindowBorder.Resizable,
            NumberOfSamples = 0,
        };

        var gameSettings = new GameWindowSettings
        {
            UpdateFrequency = 0.0,
        };

        using var game = new Game(gameSettings, nativeSettings, autoScreenshotFrame);
        game.Run();
    }

    private static void ReportFatal(Exception ex)
    {
        string logPath = Path.Combine(AppContext.BaseDirectory, "crash.log");
        try
        {
            File.WriteAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]{Environment.NewLine}{ex}");
        }
        catch
        {
            // ignored - we still try to show a message box
        }

        NativeMessageBox(
            IntPtr.Zero,
            ex.Message + "\n\n" + ex.StackTrace + "\n\n日志已写入:\n" + logPath,
            "NikCraft 启动失败",
            0x00010000 /* MB_ICONERROR */);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int NativeMessageBox(IntPtr hWnd, string text, string caption, uint type);
}
