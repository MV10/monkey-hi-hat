
using OpenTK.Mathematics;

namespace mhh;

public interface IRenderer : IConfigSource, IDisposable
{
    /// <summary>
    /// Indicates initialization was successful.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// When IsValid is false, indicates the reason why.
    /// </summary>
    public string InvalidReason { get; set; }

    /// <summary>
    /// The original visualizer or FX filename. Used by the PlaylistManager and shown
    /// in --info command output and with some text overlays and popups. Set this
    /// via Path.GetFilenameWithoutExtension against the ConfigSource.Pathname.
    /// </summary>
    public string Filename { get; }

    /// <summary>
    /// If available, the long-form description of the visualizer or FX shader. Used
    /// for text popups when new content is being loaded from a playlist.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Called to produce a new frame of output. If a ScreenshotWriter is provided,
    /// the output buffer should be provided to that object to generate a screenshot.
    /// </summary>
    public void RenderFrame(ScreenshotWriter screenshotWriter = null);

    /// <summary>
    /// Called by RenderManager.OnResize in response to AppWindow.OnResize.
    /// </summary>
    public void OnResize();

    /// <summary>
    /// The resource group that contains the output at the end of the frame. Returning
    /// null indicates output is rendered to the built-in OpenGL backbuffer rather than
    /// an internally-managed framebuffer texture.
    /// </summary>
    public GLResourceGroup OutputBuffers { get; }

    /// <summary>
    /// Width and height of the rendered frame data.
    /// </summary>
    public Vector2 Resolution { get; }

    /// <summary>
    /// When true, the implementation can disable any blitter calls 
    /// to the OpenGL-provided display backbuffer.
    /// </summary>
    public bool OutputIntercepted { set; }

    /// <summary>
    /// Renderers should maintain an internal Stopwatch and set a float uniform named "time".
    /// The renderer should not automatically start the clock; the RenderManager should control
    /// start / stop activity.
    /// </summary>
    public void StartClock();

    /// <summary>
    /// Renderers should maintain an internal Stopwatch and set a float uniform named "time".
    /// The renderer should not automatically start the clock; the RenderManager should control
    /// start / stop activity.
    /// </summary>
    public void StopClock();

    /// <summary>
    /// Returns the total elapsed seconds the renderer has been running. The "time"
    /// uniform may be altered by a random offset if specified in the visualizer config.
    /// </summary>
    public float TrueElapsedTime();

    /// <summary>
    /// Returns a list of the visualizers option uniforms for the specified FX, if any.
    /// Returns an empty collection if there aren't any matching the FX filename.
    /// </summary>
    public Dictionary<string, float> GetFXUniforms(string fxFilename);
}
