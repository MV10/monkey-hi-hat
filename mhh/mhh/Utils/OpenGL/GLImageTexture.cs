
using OpenTK.Graphics.OpenGL;

namespace mhh.Utils;

public class GLImageTexture
{
    /// <summary>
    /// Identifies the object which requested the allocation.
    /// </summary>
    public Guid OwnerName;

    /// <summary>
    /// The resource owner should set this.
    /// </summary>
    public string Filename;

    /// <summary>
    /// The resource owner should set this.
    /// </summary>
    public string UniformName;

    /// <summary>
    /// Indicates whether the filename was successfully loaded.
    /// </summary>
    public bool ImageLoaded;

    /// <summary>
    /// The handle of the buffer where the image is stored.
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
