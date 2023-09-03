
using eyecandy;
using System.Numerics;

namespace mhh.Utils
{
    public static class Caching
    {
        /// <summary>
        /// All built-in EyeCandy AudioTexture implementations. Plugin DLLs may define others not listed here.
        /// </summary>
        public static readonly IReadOnlyList<Type> KnownAudioTextures = typeof(AudioTexture).GetAllDerivedTypes();

        /// <summary>
        /// All built-in MHH IVisualizer implementations. Plugin DLLs may define others not listed here.
        /// </summary>
        public static readonly IReadOnlyList<Type> KnownVisualizers = typeof(IVisualizer).GetAllDerivedTypes();

        /// <summary>
        /// Compiled shader programs. The key is a murmur3 hash of the combined vert and frag pathnames.
        /// </summary>
        public static CacheLRU<BigInteger, CachedShader> Shaders;
    }
}
