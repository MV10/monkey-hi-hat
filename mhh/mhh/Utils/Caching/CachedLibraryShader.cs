
using eyecandy;

namespace mhh;

public class CachedLibraryShader : ShaderLibrary
{
    /// <summary>
    /// The unique key for this library shader (which is the pathname and type)
    /// </summary>
    public readonly LibraryShaderConfig Key;

    public CachedLibraryShader(LibraryShaderConfig libraryConfig)
        :base(libraryConfig.Pathname, libraryConfig.Type)
    {
        Key = libraryConfig;
    }
}
