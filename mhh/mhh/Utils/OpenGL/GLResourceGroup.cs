
using OpenTK.Graphics.OpenGL;

namespace mhh;

public class GLResourceGroup
{
    /// <summary>
    /// Identifies the object which requested the allocation.
    /// </summary>
    public string OwnerName;

    /// <summary>
    /// The 0-based index number the owner should use to find the right
    /// buffer handle to bind for drawing, and/or texture handle to set
    /// as a shader input uniform. These correspond to the buffer numbers
    /// specified in the visualizer configuration [multipass] section,
    /// for example. For backbuffer resource collections, these must be
    /// remapped by the resource owner to match the correct frontbuffer
    /// index.
    /// </summary>
    public int DrawPassIndex;

    /// <summary>
    /// The resource owner should set this. Note this can change when
    /// the owner is swapping draw-buffers and back-buffers.
    /// </summary>
    public string UniformName;

    /// <summary>
    /// Handle to the OpenGL FramebufferObject.
    /// </summary>
    public int FramebufferHandle;

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
