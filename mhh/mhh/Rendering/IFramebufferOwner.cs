
using mhh.Utils;

namespace mhh;

public interface IFramebufferOwner
{
    /// <summary>
    /// Returns the GLResources object that defines the 
    /// final framebuffer the renderer draws into.
    /// </summary>
    public GLResources GetFinalDrawTargetResource();
}