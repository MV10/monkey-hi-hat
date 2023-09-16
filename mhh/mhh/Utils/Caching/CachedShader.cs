
using eyecandy;

namespace mhh;

public class CachedShader : Shader
{
    /// <summary>
    /// Produces a cache key from a set of shader pathnames.
    /// </summary>
    public static string KeyFrom(string vertexPathname, string fragmentPathname)
        => string.Concat(vertexPathname, "*", fragmentPathname);

    /// <summary>
    /// The unique key for this shader (concats the vert and frag pathnames).
    /// </summary>
    public readonly string Key;

    public CachedShader(string vertexPathname, string fragmentPathname) 
        : base(vertexPathname, fragmentPathname)
    {
        Key = KeyFrom(vertexPathname, fragmentPathname);
    }
}
