using eyecandy;

namespace mhh
{
    /// <summary>
    /// Mostly consists of values used during application startup.
    /// </summary>
    public static class Defaults
    {
        /// <summary>
        /// All built-in EyeCandy AudioTexture implementations. Plugin DLLs may define others not listed here.
        /// </summary>
        public static readonly IReadOnlyList<Type> AudioTextureTypes = typeof(AudioTexture).GetAllDerivedTypes();

        /// <summary>
        /// Initializes an EyeCandyWindowConfig field.
        /// </summary>
        public static readonly string IdleVertexShaderPathname = "InternalShaders/idle.vert";

        /// <summary>
        /// Initializes an EyeCandyWindowConfig field.
        /// </summary>
        public static readonly string IdleFragmentShaderPathname = "InternalShaders/idle.frag";

        /// <summary>
        /// Initializes a VisualizerDefinition field.
        /// </summary>
        public static readonly VizMode VisualizationMode = VizMode.FragmentQuad;

        /// <summary>
        /// Initializes a VisualizerDefinition field.
        /// </summary>
        public static readonly ArrayDrawingMode DrawingMode = ArrayDrawingMode.Points;

        /// <summary>
        /// Initializes a VisualizerDefinition field.
        /// </summary>
        public static readonly int VertexIntegerCount = 64;
    }
}
