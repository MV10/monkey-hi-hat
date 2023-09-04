
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
}
