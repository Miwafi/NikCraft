using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace NikCraft.Core;

/// <summary>Thin wrapper around a linked GLSL program with cached uniform lookups.</summary>
public sealed class Shader : IDisposable
{
    private readonly Dictionary<string, int> _uniformCache = new(StringComparer.Ordinal);
    private bool _disposed;

    public int Handle { get; }

    public Shader(string vertexSource, string fragmentSource, string debugName = "shader")
    {
        int vs = Compile(ShaderType.VertexShader, vertexSource, debugName + ".vert");
        int fs = Compile(ShaderType.FragmentShader, fragmentSource, debugName + ".frag");

        Handle = GL.CreateProgram();
        GL.AttachShader(Handle, vs);
        GL.AttachShader(Handle, fs);
        GL.LinkProgram(Handle);
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = GL.GetProgramInfoLog(Handle);
            GL.DeleteProgram(Handle);
            throw new InvalidOperationException($"链接 {debugName} 失败: {log}");
        }

        GL.DetachShader(Handle, vs);
        GL.DetachShader(Handle, fs);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);
    }

    private static int Compile(ShaderType type, string source, string label)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
        {
            string log = GL.GetShaderInfoLog(shader);
            GL.DeleteShader(shader);
            throw new InvalidOperationException($"编译 {label} 失败: {log}");
        }

        return shader;
    }

    public void Use() => GL.UseProgram(Handle);

    /// <summary>Diagnostic helper.</summary>
    public int GetLocation(string name) => Location(name);

    private int Location(string name)
    {
        if (_uniformCache.TryGetValue(name, out int cached))
        {
            return cached;
        }

        int location = GL.GetUniformLocation(Handle, name);
        _uniformCache[name] = location;
        return location;
    }

    public void SetMatrix4(string name, Matrix4 value)
    {
        int location = Location(name);
        if (location >= 0)
        {
            GL.ProgramUniformMatrix4(Handle, location, false, ref value);
        }
    }

    public void SetVector3(string name, Vector3 value)
    {
        int location = Location(name);
        if (location >= 0)
        {
            GL.ProgramUniform3(Handle, location, value);
        }
    }

    public void SetVector2(string name, Vector2 value)
    {
        int location = Location(name);
        if (location >= 0)
        {
            GL.ProgramUniform2(Handle, location, value);
        }
    }

    public void SetVector4(string name, Vector4 value)
    {
        int location = Location(name);
        if (location >= 0)
        {
            GL.ProgramUniform4(Handle, location, value);
        }
    }

    public void SetFloat(string name, float value)
    {
        int location = Location(name);
        if (location >= 0)
        {
            GL.ProgramUniform1(Handle, location, value);
        }
    }

    public void SetInt(string name, int value)
    {
        int location = Location(name);
        if (location >= 0)
        {
            GL.ProgramUniform1(Handle, location, value);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GL.DeleteProgram(Handle);
    }
}
