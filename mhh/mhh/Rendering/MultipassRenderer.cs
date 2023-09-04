
namespace mhh;

public class MultipassRenderer : IRenderer
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; set; }

    public MultipassRenderer(VisualizerConfig visualizerConfig)
    {
        //Config = visualizerConfig;
        //Filename = Path.GetFileNameWithoutExtension(Config.ConfigSource.Pathname);

        // TODO
    }

    public void RenderFrame()
    {

    }

    private bool IsDisposed = false;
    public void Dispose()
    {
        if (IsDisposed) return;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}
