
using mhh.Utils;

namespace mhh;

// Crossfade doesn't implement IFramebufferOwner because it isn't expected
// to be "interrogated" by any other renderer, it should always be the last
// in any rendering sequence. If another renderer doesn't implement the
// IFramebufferOwner interface, it simply renders to whatever is bound before
// RenderFrame is called, which means Crossfade can use an internally-owned
// Framebuffer.

public class CrossfadeRenderer : IRenderer
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;

    private Guid OwnerName = Guid.NewGuid();
    private IReadOnlyList<GLResources> Resources;

    public CrossfadeRenderer()
    {
        Resources = RenderManager.ResourceManager.CreateFramebuffers(OwnerName, 2);
    }

    public void RenderFrame()
    {

    }

    public void Dispose()
    {
        if (IsDisposed) return;

        if (Resources?.Count > 0) RenderManager.ResourceManager.DestroyFramebuffers(OwnerName);
        Resources = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
