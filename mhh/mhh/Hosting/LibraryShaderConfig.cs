
using OpenTK.Graphics.OpenGL;

namespace mhh;

/// <summary>
/// A simple container for shader library entries in other config files.
/// </summary>
public class LibraryShaderConfig
{
    /// <summary>
    /// The fully-qualified path to the shader source file.
    /// </summary>
    public readonly string Pathname;

    /// <summary>
    /// The program stage to link this shader into.
    /// </summary>
    public readonly ShaderType Type;

    public LibraryShaderConfig(string pathname, ShaderType type)
    {
        Pathname = pathname;
        Type = type;
    }
}
