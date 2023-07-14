using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace mhh
{
    /// <summary>
    /// Represents the contents of a [shadername].conf definition file.
    /// The defaults are for startup using idle.vert and idle.frag.
    /// </summary>
    public class VisualizerDefinition
    {
        /// <summary>
        /// Display-name of the shader.
        /// </summary>
        public string Description = "MHH Idle Defaults";

        /// <summary>
        /// Defines how the application feeds data to the shader. Refer to the
        /// help on individual VizMode options for details.
        /// </summary>
        public VizMode VisualizationMode = Defaults.VisualizationMode;

        /// <summary>
        /// Location of the vertex shader. Bear in mind Linux is case-sensitive and
        /// requires forward-slash separators. Forward-slahes work here for Windows, too.
        /// </summary>
        public string VertexShaderPathame = Defaults.IdleVertexShaderPathname;

        /// <summary>
        /// Location of the fragment shader. Bear in mind Linux is case-sensitive and
        /// requires forward-slash separators. Forward-slahes work here for Windows, too.
        /// </summary>
        public string FragmentShaderPathname = Defaults.IdleFragmentShaderPathname;

        /// <summary>
        /// The application automatically clears the background on every render pass.
        /// </summary>
        public Color4 BackgroundColor = new(0f, 0f, 0f, 1f);

        /// <summary>
        /// Defines how the output data is drawn.
        /// </summary>
        public ArrayDrawingMode DrawingMode = Defaults.DrawingMode;

        /// <summary>
        /// For VizMode.VertexIntegerArray, defines how many sequential integers
        /// are provided as input (single-dimension array, 0 to n-1).
        /// </summary>
        public int VertexIntegerCount = Defaults.VertexIntegerCount;

        /// <summary>
        /// If audio textures are used, each entry should be the uniform name and the EyeCandy
        /// AudioTexture class name separated by a space (ex. "sound AudioTextureWaveHistory")
        /// </summary>
        public List<string> AudioTextures = new();

        // TODO - how to set audio texture features like the multiplier?

        /// <summary>
        /// Casts the MHH DrawingMode as an OpenGL PrimitiveType for GL.DrawArray() calls.
        /// </summary>
        public PrimitiveType GLDrawingMode()
            => (PrimitiveType)DrawingMode;
    }
}
