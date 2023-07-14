using OpenTK.Windowing.Common;

// TODO - should IVisualizer include OnResize and OnUnload?

namespace mhh
{
    public interface IVisualizer : IDisposable
    {
        /// <summary>
        /// Indicates rendering is about to begin.
        /// </summary>
        public void Start(VisualizerHostWindow hostWindow);
        
        /// <summary>
        /// The window has loaded. Will always be called for a new visualizer in a
        /// window that already exists.
        /// </summary>
        public void OnLoad(VisualizerHostWindow hostWindow);
        
        /// <summary>
        /// Called after the window has already updated any audio textures and set
        /// their uniforms. The "resolution" and "time" uniforms are already set
        /// as well. After this call, the window will swap buffers and calculate FPS.
        /// </summary>
        public void OnRenderFrame(VisualizerHostWindow hostWindow, FrameEventArgs e);
        
        /// <summary>
        /// Called after the window checks for ESC keypress (which exits immediately).
        /// </summary>
        public void OnUpdateFrame(VisualizerHostWindow hostWindow, FrameEventArgs e);
        
        /// <summary>
        /// Indicates this visualizer is being replaced. Dispose is called after this.
        /// </summary>
        public void Stop(VisualizerHostWindow hostWindow);
    }
}
