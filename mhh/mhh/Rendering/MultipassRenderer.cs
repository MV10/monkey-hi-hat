
using mhh.Utils;

namespace mhh;

public class MultipassRenderer : IRenderer, IGLResourceOwner
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; set; }

    private Guid FramebufferName = Guid.NewGuid();
    private IReadOnlyList<GLResources> Framebuffers;
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

    public Guid GetFramebufferOwnerName()
        => FramebufferName;

    public int GetFramebufferCount()
        => Framebuffers?.Count ?? 0;

    public GLResources GetOutputFramebuffer()
        => (OutputFramebuffer == -1) ? null : Framebuffers?[OutputFramebuffer] ?? null;

    public void Dispose()
    {
        if (IsDisposed) return;

        if(Framebuffers?.Count > 0) RenderManager.ResourceManager.DestroyFramebuffers(FramebufferName);
        Framebuffers = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    private bool IsDisposed = false;
}
