
using OpenTK.Mathematics;

namespace mhh;

public interface IRenderer : IDisposable
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
    /// The original visualizer filename. Used by the PlaylistManager. Set this
    /// via Path.GetFilenameWithoutExtension against the ConfigSource.Pathname.
    /// </summary>
    public string Filename { get; set; }

    /// <summary>
    /// Called after audio textures are updated, uniforms are set, and any
    /// framebuffers are prepared for drawing.
    /// </summary>
    public void RenderFrame();

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
    /// Returns the total elapsed seconds used for the shader "time" uniform.
    /// </summary>
    public float ElapsedTime();
}
