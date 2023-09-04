
namespace mhh;

public class CrossfadeRenderer : IRenderer
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;

    public CrossfadeRenderer()
    {

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
