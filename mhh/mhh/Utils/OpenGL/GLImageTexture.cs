
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
    /// Indicates whether the target file was successfully loaded. When
    /// false, the program attempts to load the internal missing-texture
    /// image cached from the InternalShaders directory, so content still
    /// exists providing a visual clue that a problem exists.
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
    /// The type of texture being stored.
    /// </summary>
    public TextureTarget TextureTarget = TextureTarget.Texture2D;

    /// <summary>
    /// For streaming content, determines if or how to resize incoming frames.
    /// </summary>
    public StreamingResizeContentMode ResizeMode = StreamingResizeContentMode.NotStreaming;

    /// <summary>
    /// When streaming resize mode is Scaled, this specifies the largest dimension.
    /// </summary>
    public int ResizeMaxDimension;

    /// <summary>
    /// If the texture is a video, this object stores the FFMediaToolkit references needed for playback.
    /// </summary>
    public VideoMediaData VideoData;
}
