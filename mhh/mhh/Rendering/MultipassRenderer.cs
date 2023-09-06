
using mhh.Utils;

namespace mhh;

public class MultipassRenderer : IRenderer, IFramebufferOwner
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; set; }

    private Guid OwnerName = Guid.NewGuid();
    private IReadOnlyList<GLResources> Resources;
    private int OutputFramebuffer = -1;

    public MultipassRenderer(VisualizerConfig visualizerConfig)
    {
        //Config = visualizerConfig;
        //Filename = Path.GetFileNameWithoutExtension(Config.ConfigSource.Pathname);

        // TODO
    }

    public void RenderFrame()
    {

    }

    public GLResources GetFinalDrawTargetResource()
        => (OutputFramebuffer == -1) ? null : Resources?[OutputFramebuffer] ?? null;

    public void Dispose()
    {
        if (IsDisposed) return;

        if(Resources?.Count > 0) RenderManager.ResourceManager.DestroyResources(OwnerName);
        Resources = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    private bool IsDisposed = false;
}
