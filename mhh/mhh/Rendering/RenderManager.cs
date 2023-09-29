
using Microsoft.Extensions.Logging;

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
    /// When true, any renderer clocks (which normally sets a "time" uniform) are stopped.
    /// The default value is false (clocks are running upon renderer initialization).
    /// </summary>
    public bool TimePaused
    {
        get => IsTimePaused;
        set
        {
            IsTimePaused = value;
            if (IsTimePaused)
            {
                ActiveRenderer?.StopClock();
            }
            else
            {
                ActiveRenderer?.StartClock();
            }
        }
    }
    private bool IsTimePaused = false;

    /// <summary>
    /// Queues up a renderer to run the visualization as the next active renderer. May
    /// employ a cross-fade effect before the new one is running exclusively.
    /// </summary>
    public void PrepareNewRenderer(VisualizerConfig visualizerConfig)
    {
        IRenderer renderer;
        if (visualizerConfig.ConfigSource.Content.ContainsKey("multipass"))
        {
            renderer = new MultipassRenderer(visualizerConfig);
        }
        else
        {
            renderer = new SimpleRenderer(visualizerConfig);
        }

        if (!renderer.IsValid)
        {
            renderer.Dispose();
            LogHelper.Logger?.LogError(renderer.InvalidReason);
            return;
        }

        if (ActiveRenderer is CrossfadeRenderer)
        {
            ActiveRenderer.Dispose();
            ActiveRenderer = null;
        }

        Program.AppWindow.Playlist?.StartingNextVisualization(visualizerConfig);

        if (ActiveRenderer is null)
        {
            ActiveRenderer = renderer;
            if (!IsTimePaused) ActiveRenderer.StartClock();
            return;
        }

        NewRenderer?.Dispose();
        NewRenderer = renderer;
    }

    /// <summary>
    /// Converts the active renderer to an FXRenderer. Ignored if crossfade
    /// is active, another FX is already active, or a NewRenderer is pending. The
    /// return value indicates whether the request was ignored.
    /// </summary>
    public bool ApplyFX(FXConfig fxConfig)
    {
        if (ActiveRenderer is null) return false;

        var primaryRenderer = 
            (ActiveRenderer is CrossfadeRenderer)
                ? (ActiveRenderer as CrossfadeRenderer).NewRenderer
                : (NewRenderer is null) ? ActiveRenderer : NewRenderer;
        if (primaryRenderer is FXRenderer) return false;

        var fxRenderer = new FXRenderer(fxConfig, primaryRenderer);
        if (!fxRenderer.IsValid)
        {
            fxRenderer.Dispose();
            LogHelper.Logger?.LogError(fxRenderer.InvalidReason);
            return false;
        }

        if(NewRenderer is null)
        {
            if(ActiveRenderer is CrossfadeRenderer)
            {
                (ActiveRenderer as CrossfadeRenderer).NewRenderer = fxRenderer;
            }
            else
            {
                ActiveRenderer = fxRenderer;
            }
            if (!IsTimePaused) fxRenderer.StartClock();
        }
        else
        {
            NewRenderer = fxRenderer;
        }

        return true;
    }

    /// <summary>
    /// Called by AppWindow.OnRenderFrame.
    /// </summary>
    public void RenderFrame()
    {
        if(NewRenderer is not null)
        {
            if(Program.AppConfig.CrossfadeSeconds == 0)
            {
                // Crossfade disabled, just do the switch
                ActiveRenderer.Dispose();
                ActiveRenderer = NewRenderer;
                NewRenderer = null;
                if (!IsTimePaused) ActiveRenderer.StartClock();
            }
            else
            {
                // Crossfade enabled, hand off control and make the crossfader active
                var oldRenderer = ActiveRenderer;
                ActiveRenderer = new CrossfadeRenderer(oldRenderer, NewRenderer, CrossfadeCompleted);
                NewRenderer = null;
                if (!IsTimePaused) ActiveRenderer.StartClock();
            }

        }

        ActiveRenderer?.RenderFrame();
    }

    /// <summary>
    /// Called by AppWindow.OnResize
    /// </summary>
    public void OnResize()
    {
        ActiveRenderer?.OnResize();
        NewRenderer?.OnResize();
    }

    /// <summary>
    /// Visualization / renderer information for the --info command.
    /// </summary>
    public string GetInfo()
        => $"elapsed sec: {ActiveRenderer?.TrueElapsedTime() ?? 0}\nvisualizer : {ActiveRenderer?.Filename ?? "(none)"}";

    private void CrossfadeCompleted()
    {
        // Upon completion, re-take control of the new one, clean up the
        // old one and the crossfader, and make the new one active
        var crossfader = ActiveRenderer as CrossfadeRenderer;
        var newRenderer = crossfader.NewRenderer;
        crossfader.OldRenderer.Dispose();
        crossfader.Dispose();
        ActiveRenderer = newRenderer;
    }

    /// <summary/>
    public void Dispose()
    {
        if (IsDisposed) return;
        LogHelper.Logger?.LogTrace($"{GetType()}.Dispose() ----------------------------");

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() NewRenderer");
        NewRenderer?.Dispose();
        NewRenderer = null;

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() ActiveRenderer");
        ActiveRenderer?.Dispose();
        ActiveRenderer = null;

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() ResourceManager");
        ResourceManager?.Dispose();

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
