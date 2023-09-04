
using eyecandy;

namespace mhh
{
    public interface IVisualizer : IDisposable
    {
        /// <summary>
        /// Called before the visualizer will be executed.
        /// </summary>
        public void Initialize(VisualizerConfig config, Shader shader);

        /// <summary>
        /// Called after audio textures are updated, uniforms are set, and any
        /// framebuffers are prepared for drawing.
        /// </summary>
        public void RenderFrame(Shader shader);
    }
}
