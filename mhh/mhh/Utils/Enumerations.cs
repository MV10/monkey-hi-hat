using OpenTK.Graphics.OpenGL;

namespace mhh
{
    /// <summary>
    /// Defines the type of data fed to the vertex and fragment shaders.
    /// </summary>
    public enum VizMode
    {
        /// <summary>
        /// Passes an array of sequential integers to the vertex shader, as
        /// well as the array size. This works similarly to VertexShaderArt
        /// projects. See the eyecandy repository's README for a discussion
        /// of the differences.
        /// </summary>
        VertexIntegerArray = 0,
        
        /// <summary>
        /// Passes a full-screen quad to the fragment (aka pixel) shader.
        /// This works similarly to Shadertoy projects. See the eyecandy 
        /// repository's README for a discussion of the differences.
        /// </summary>
        FragmentQuad = 1,

        // TODO - plugin support
    }

    /// <summary>
    /// Defines how the output data is drawn. This list comprises all of the
    /// supported standard OpenGL PritimiveType flags (although when plugin
    /// support is available, they are free to use others).
    /// </summary>
    public enum ArrayDrawingMode
    {
        Points = PrimitiveType.Points,
        Lines = PrimitiveType.Lines,
        LineStrip = PrimitiveType.LineStrip,
        LineLoop = PrimitiveType.LineLoop,
        Triangles = PrimitiveType.Triangles,
        TriangleStrip = PrimitiveType.TriangleStrip,
        TriangleFan = PrimitiveType.TriangleFan,
    }
}
