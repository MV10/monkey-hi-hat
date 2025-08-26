
using OpenTK.Graphics.OpenGL;

namespace mhh;

public class GLImageTexture
{
    /// <summary>
    /// Identifies the object which requested the allocation.
    /// </summary>
    public string OwnerName;

    /// <summary>
    /// The resource owner should set this.
    /// </summary>
    public string Filename;

    /// <summary>
    /// The resource owner should set this.
    /// </summary>
    public string UniformName;

    /// <summary>
    /// Indicates whether the target file was successfully loaded.
    /// </summary>
    public bool Loaded;

    /// <summary>
    /// The handle of the buffer where the image is stored.
    /// </summary>
    public int TextureHandle;

    /// <summary>
    /// The assigned texture unit as a plain integer;
    /// </summary>
    public int TextureUnitOrdinal;

    /// <summary>
    /// Controls how out-of-bounds sampling is handled.
    /// </summary>
    public TextureWrapMode WrapMode = TextureWrapMode.Repeat;

    /// <summary>
    /// The assigned texture unit as an enum (required by some API calls).
    /// </summary>
    public TextureUnit TextureUnit
        => TextureUnit.Texture0 + TextureUnitOrdinal;

    /// <summary>
    /// If the texture is a video, this object stores the FFMediaToolkit references needed for playback.
    /// </summary>
    public VideoMediaData VideoData;
}
