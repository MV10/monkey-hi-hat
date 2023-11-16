
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
    public static GLResourceManager ResourceManager;

    /// <summary>
    /// Handles all final-pass text overlay rendering and exposes utility functions for
    /// manipulating the text buffer.
    /// </summary>
    public static TextManager TextManager;

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
    /// When this is set, the final output buffer is handed off for saving, then
    /// this reference is set to null again.
    /// </summary>
    public ScreenshotWriter ScreenshotHandler { get; set; } = null;

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

    public RenderManager()
    {
        ResourceManager = new();
        TextManager = new();
    }

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
        TextManager.BeforeRenderFrame();

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

        if(ActiveRenderer is CrossfadeRenderer)
        {
            ActiveRenderer.RenderFrame(null);
        }
        else
        {
            ActiveRenderer?.RenderFrame(ScreenshotHandler);
            ScreenshotHandler = null;
        }

        TextManager.Renderer.RenderFrame();
    }

    /// <summary>
    /// Called by AppWindow.OnResize
    /// </summary>
    public void OnResize()
    {
        ActiveRenderer?.OnResize();
        NewRenderer?.OnResize();
        TextManager.Renderer.OnResize();
    }

    /// <summary>
    /// Visualization / renderer information for the --info command.
    /// </summary>
    public string GetInfo()
    {
        var viz = ActiveRenderer?.Filename.Replace("_", " ") ?? "(none)";
        var rez = (ActiveRenderer is null ? "n/a" : $"{ActiveRenderer.Resolution.X} x {ActiveRenderer.Resolution.Y}");

        if (ActiveRenderer is CrossfadeRenderer)
        {
            var cf = ActiveRenderer as CrossfadeRenderer;
            var ro = $"{cf.OldRenderer.Resolution.X} x {cf.OldRenderer.Resolution.Y}";
            var rn = $"{cf.NewRenderer.Resolution.X} x {cf.NewRenderer.Resolution.Y}";
            if (cf.OldRenderer is FXRenderer) (_, ro) = GetFXInfo(cf.OldRenderer as FXRenderer);
            if (cf.NewRenderer is FXRenderer) (_, rn) = GetFXInfo(cf.NewRenderer as FXRenderer);
            viz = "Crossfading...";
            rez = $"{ro} and {rn}";
        }

        if (ActiveRenderer is FXRenderer) (viz, rez) = GetFXInfo(ActiveRenderer as FXRenderer);

        return
$@"elapsed sec: {ActiveRenderer?.TrueElapsedTime() ?? 0}
visualizer : {viz}
render res : {rez}";
    }

    private (string viz, string rez) GetFXInfo(FXRenderer fx)
    {
        var viz = $"{fx.PrimaryRenderer.Filename.Replace("_", " ")} with FX {fx.Filename.Replace("_", " ")}";
        var rez = $"{fx.PrimaryRenderer.Resolution.X} x {fx.PrimaryRenderer.Resolution.Y}";
        return (viz, rez);
    }

    public string GetPopupText()
    {
        var primary = (ActiveRenderer is CrossfadeRenderer)
            ? (ActiveRenderer as CrossfadeRenderer).NewRenderer
            : (NewRenderer is null) ? ActiveRenderer : NewRenderer;

        if (primary is FXRenderer)
        {
            var fx = primary as FXRenderer;
            return
$@"{fx.PrimaryRenderer.Filename.Replace("_", " ")}
{fx.PrimaryRenderer.Description}

FX {fx.Filename.Replace("_", " ")}
{fx.Description}";
        }

        return
$@"{primary.Filename.Replace("_", " ")}
{primary.Description}";
    }

    private void CrossfadeCompleted()
    {
        // Upon completion, re-take control of the new one, clean up the
        // old one and the crossfader, and make the new one active
        var crossfader = ActiveRenderer as CrossfadeRenderer;
        var newRenderer = crossfader.NewRenderer;
        crossfader.OldRenderer.Dispose();
        crossfader.Dispose();
        ActiveRenderer = newRenderer;

        // Undo any Crossfade-driven application of FXResolutionLimit
        ActiveRenderer.OnResize();
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

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() TextManager");
        TextManager?.Dispose();

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
