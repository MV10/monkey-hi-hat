using OpenTK.Graphics.OpenGL;

namespace mhh;

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
    RandomFavorites = 3,
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

/// <summary>
/// Requests resulting from command-line input (which is a separate thread).
/// </summary>
public enum CommandRequest
{
    None = 0,
    Quit = 1,
    ToggleFullscreen = 2,
    SnapshotNowJpg = 3,
    SnapshotNowPng = 4,
    SnapshotSpacebarJpg = 5,
    SnapshotSpacebarPng = 6,
}

/// <summary>
/// Controls visualizer playback time in a playlist.
/// </summary>
public enum VizPlaylistTimeHint
{
    None = 0,
    Half = 1,
    Double = 2,
    DoubleFX = 3,
}

/// <summary>
/// Determines the type of content being handled by TestModeManager.
/// </summary>
public enum TestMode
{
    None = 0,
    Viz = 1,
    FX = 2,
    Fade = 3,
}

/// <summary>
/// Specifies how or if video frames are vertically flipped.
/// </summary>
public enum VideoFlipMode
{
    None = 0,
    Internal = 1,
    FFmpeg = 2,
}
