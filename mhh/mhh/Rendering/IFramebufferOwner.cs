
using mhh.Utils;

namespace mhh;

public interface IFramebufferOwner
{
    /// <summary>
    /// The resource group that contains the output at the end of the frame.
    /// </summary>
    public GLResources OutputBuffers { get; }

    /// <summary>
    /// When true, the implementation can disable any blitter calls 
    /// to the OpenGL-provided display backbuffer.
    /// </summary>
    public bool OutputIntercepted { set; }
}