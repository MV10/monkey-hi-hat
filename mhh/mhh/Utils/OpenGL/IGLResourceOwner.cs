
namespace mhh.Utils;

public interface IGLResourceOwner
{
    public Guid GetFramebufferOwnerName();

    public int GetFramebufferCount();

    public GLResources GetOutputFramebuffer();
}
