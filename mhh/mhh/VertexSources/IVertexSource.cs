
using eyecandy;

namespace mhh;

public interface IVertexSource : IDisposable
{
    /// <summary>
    /// Called before the visualizer will be executed.
    /// </summary>
    public void Initialize(IConfigSource config, Shader shader);

    /// <summary>
    /// Called whenever the viewport changes via AppWindow.OnResize events.
    /// </summary>
    public void BindBuffers(Shader shader);

    /// <summary>
    /// Called after textures are updated, uniforms are set, and any framebuffers 
    /// are prepared for drawing.
    /// </summary>
    public void RenderFrame(Shader shader);
}
