
using OpenTK.Graphics.OpenGL;

namespace mhh.Utils;

public class GLResources
{
    /// <summary>
    /// Identifies the object which requested the allocation.
    /// </summary>
    public Guid OwnerName;

    /// <summary>
    /// The 0-based index number the owner should use to find the right
    /// buffer handle to bind for drawing, and/or texture handle to set
    /// as a shader input uniform. These correspond to the buffer numbers
    /// specified in the visualizer configuration [multipass] section,
    /// for example.
    /// </summary>
    public int Index;

    /// <summary>
    /// Handle to the OpenGL FramebufferObject.
    /// </summary>
    public int BufferHandle;

    /// <summary>
    /// Handle to the Texture attached to the framebuffer.
    /// </summary>
    public int TextureHandle;

    /// <summary>
    /// The assigned texture unit as a plain integer;
    /// </summary>
    public int TextureUnitOrdinal;

    /// <summary>
    /// The assigned texture unit as an enum (required by some API calls).
    /// </summary>
    public TextureUnit TextureUnit 
        => TextureUnit.Texture0 + TextureUnitOrdinal;
}
