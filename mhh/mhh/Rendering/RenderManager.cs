
using mhh.Utils;

namespace mhh;

// RenderManager can execute simple stand-alone visualizations, but it can also orchestrate
// multi-pass rendering operations, including cross-fade, visualizations defined as multi-pass
// and "attached" post-procesing effects (including "upgrading" a stand-alone to multi-pass).

public class RenderManager : IDisposable
{
    /// <summary>
    /// Multi-pass renderers use this to create and destroy framebuffers.
    /// </summary>
    public static GLResourceManager ResourceManager = GLResourceManager.GetInstanceForRenderManager();

    /// <summary>
    /// The renderer currently generating output, or in a cross-fade scenario, the old
    /// visualizer that is becoming transparent.
    /// </summary>
    public IRenderer ActiveRenderer { get; private set; }
    
    /// <summary>
    /// The renderer to become active either on the next render call, or in a cross-fade
    /// scenario, the visualizer that is becoming visible.
    /// </summary>
    public IRenderer NewRenderer { get; private set; }

    /// <summary>
    /// Queues up a renderer to run the visualization as the next active renderer. May
    /// employ a cross-fade effect before the new one is running exclusively.
    /// </summary>
    public IRenderer PrepareNewRenderer(VisualizerConfig visualizerConfig)
    {
        IRenderer renderer;
        if (visualizerConfig.ConfigSource.Content.ContainsKey("multipass"))
        {
            renderer = new MultipassRenderer(visualizerConfig);
        }
        else
        {
            renderer = new SingleVisualizerRenderer(visualizerConfig);
        }

        if (!renderer.IsValid) return renderer;

        if (ActiveRenderer is null)
        {
            ActiveRenderer = renderer;
        }
        else
        {
            if (NewRenderer is not null) NewRenderer.Dispose();
            NewRenderer = renderer;
        }

        return renderer;
    }

    /// <summary>
    /// Called by AppWindow.OnRenderFrame.
    /// </summary>
    public void RenderFrame()
    {
        if(NewRenderer is not null)
        {
            ActiveRenderer.Dispose();
            ActiveRenderer = NewRenderer;
            NewRenderer = null;
        }

        ActiveRenderer?.RenderFrame();
    }

    /// <summary>
    /// Called by AppWindow.OnResizeWindow
    /// </summary>
    public void ViewportResized(int viewportWidth, int viewportHeight)
    {
        ResourceManager.ResizeTextures(viewportWidth, viewportHeight);
    }

    /// <summary>
    /// Visualization / renderer information for the --info command.
    /// </summary>
    public string GetInfo()
    {
        return "TODO";
    }

    /// <summary/>
    public void Dispose()
    {
        if (IsDisposed) return;

        NewRenderer?.Dispose();
        NewRenderer = null;

        ActiveRenderer?.Dispose();
        ActiveRenderer = null;

        ResourceManager?.Dispose();

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
