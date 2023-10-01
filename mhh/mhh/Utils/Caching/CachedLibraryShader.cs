
using eyecandy;
using OpenTK.Graphics.OpenGL;

namespace mhh;

public class CachedLibraryShader : ShaderLibrary
{
    /// <summary>
    /// The unique key for this library shader (which is the pathname)
    /// </summary>
    public readonly string Key;

    public CachedLibraryShader(string pathname, ShaderType type = ShaderType.FragmentShader)
        :base(pathname, type)
    {
        Key = pathname;
    }
}
