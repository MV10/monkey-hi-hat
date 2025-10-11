
using Microsoft.Extensions.Logging;
using StbImageSharp;

namespace mhh;

public abstract class StreamingReceiverBase : IDisposable
{
    /////////////////////////////////////////////////////////////////
    // Properties initialized by RenderingHelper.GetTextures

    /// <summary>
    /// Sizing behavior for the texture.
    /// Set by the Renderer when a streaming viz/FX is loaded.
    /// </summary>
    public ResizeContentMode ResizeMode { get; set; }

    /// <summary>
    /// Maximum dimension size for scaling the texture.
    /// Set by the Renderer when a streaming viz/FX is loaded.
    /// </summary>
    public int MaxSize { get; set; }

    /// <summary>
    /// The Texture object to update when ReceiveTexture is called.
    /// When null, simply return from ReceiveTexture.
    /// Set by the Renderer when a streaming viz/FX is loaded.
    /// Reset to null when a viz/FX is disposed.
    /// </summary>
    public GLImageTexture Texture
    {
        get;
        set
        {
            TextureWidth = 0;
            TextureHeight = 0;
            field = value;
        }
    }

    private protected int TextureWidth = 0;
    private protected int TextureHeight = 0;

    private static readonly ILogger Logger = LogHelper.CreateLogger(nameof(StreamingReceiverBase));

    /// <summary>
    /// Connect to the stream source and begin receiving data.
    /// </summary>
    public abstract void Connect(string source, bool invert);

    /// <summary>
    /// Updates the Texture property with the latest frame data
    /// (or StandbyTexture if no response has been received yet.)
    /// </summary>
    public abstract void UpdateTexture();

    /// <summary>
    /// Set Texture object to null if Texture is in textureList by
    /// verifying this TextureHandle
    /// Renderers own the Texture object, so when they are disposed
    /// they should release this reference, but multiple instances
    /// of a given Renderer class can be active (such as crossfade)
    /// so this ensures the correct Renderer releases the texture.
    /// </summary>
    public void TryDetachTexture(IReadOnlyList<GLImageTexture> textureList)
    {
        if (Texture is null || textureList is null) return;
        if (textureList.Any(t => t.TextureHandle == Texture.TextureHandle)) Texture = null;
    }

    public virtual void Dispose()
    {
        if (IsDisposed) return;
        Logger?.LogTrace("Disposing");

        Texture = null;

        IsDisposed = true;
        GC.SuppressFinalize(true);
    }
    private bool IsDisposed = false;
}
