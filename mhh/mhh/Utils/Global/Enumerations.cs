using OpenTK.Graphics.OpenGL;

namespace mhh
{
    /// <summary>
    /// Defines how the output data is drawn. This list comprises all of the
    /// supported standard OpenGL PritimiveType flags (although when plugin
    /// support is available, they are free to use others). A visualizer
    /// implementation is free to ignore this and use a specific mode.
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

    /// <summary>
    /// When silence-detection is active, action to take upon detection.
    /// </summary>
    public enum SilenceAction
    {
        None = 0,
        Idle = 1,
        Blank = 2,
    }

    /// <summary>
    /// How the visualizers in a playlist are processed
    /// </summary>
    public enum PlaylistOrder
    {
        Sequential = 0,
        Alternating = 1,
        Random = 2,
        RandomWeighted = 3,
    }

    /// <summary>
    /// Trigger condition to advance to next visualizer in the playlist
    /// </summary>
    public enum PlaylistSwitchModes
    {
        Silence = 0,
        Time = 1,
        External = 2,
    }
}
