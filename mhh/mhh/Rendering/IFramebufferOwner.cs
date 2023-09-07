
using mhh.Utils;

namespace mhh;

public interface IFramebufferOwner
{
    /// <summary>
    /// Returns the GLResources object that defines the final draw-state
    /// framebuffer which a multi-pass renderer outputs. When interceptActive
    /// is true, this implies the final framebuffer is used as input elsewhere
    /// (like crossfade) and any final blit-to-backbuffer can be skipped. When
    /// interceptActive is false, the renderer should assume it controls final
    /// output and blit to the backbuffer.
    /// </summary>
    public GLResources GetFinalDrawTargetResource(bool interceptActive);
}