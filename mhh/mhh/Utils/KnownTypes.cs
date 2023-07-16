using eyecandy;

namespace mhh
{
    public static class KnownTypes
    {
        /// <summary>
        /// All built-in EyeCandy AudioTexture implementations. Plugin DLLs may define others not listed here.
        /// </summary>
        public static readonly IReadOnlyList<Type> AudioTextureTypes = typeof(AudioTexture).GetAllDerivedTypes();

        /// <summary>
        /// All built-in MHH IVisualizer implementations. Plugin DLLs may define others not listed here.
        /// </summary>
        public static readonly IReadOnlyList<Type> Visualizers = typeof(IVisualizer).GetAllDerivedTypes();
    }
}
