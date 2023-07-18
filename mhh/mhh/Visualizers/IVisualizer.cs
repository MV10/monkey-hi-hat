using OpenTK.Windowing.Common;

namespace mhh
{
    public interface IVisualizer : IDisposable
    {
        /// <summary>
        /// Indicates rendering is about to begin.
        /// </summary>
        public void Start(HostWindow hostWindow);
        
        /// <summary>
        /// The window has loaded. Will always be called for a new visualizer in a
        /// window that already exists.
        /// </summary>
        public void OnLoad(HostWindow hostWindow);
        
        /// <summary>
        /// Called after the window has already updated any audio textures and set
        /// their uniforms. The "resolution" and "time" uniforms are already set
        /// as well. After this call, the window will swap buffers and calculate FPS.
        /// </summary>
        public void OnRenderFrame(HostWindow hostWindow, FrameEventArgs e);

        // TODO - public void OnResize(HostWindow hostWindow, ResizeEventArgs e);

        // TODO - public void OnUnload(HostWindow hostWindow);
        
        /// <summary>
        /// Called after the window checks for ESC keypress (which exits immediately).
        /// </summary>
        public void OnUpdateFrame(HostWindow hostWindow, FrameEventArgs e);
        
        /// <summary>
        /// Indicates this visualizer is being replaced. Dispose is called after this.
        /// </summary>
        public void Stop(HostWindow hostWindow);

        /// <summary>
        /// Called when the user issues the --viz switch to a running instance. The visualizer
        /// defines the acceptable "command" and "value" arguments, if any. If the command
        /// doesn't imply any specific output, return "ACK".
        /// </summary>
        public string CommandLineArgument(HostWindow hostWindow, string command, string value);

        /// <summary>
        /// Output should list all command/value pairs supported by CommandLineArgument, if
        /// any. Formatting is controlled by the Program help functions which call this method.
        /// </summary>
        public List<(string command, string value)> CommandLineArgumentHelp();
    }
}
