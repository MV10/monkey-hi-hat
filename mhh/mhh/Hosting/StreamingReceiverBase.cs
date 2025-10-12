
using Microsoft.Extensions.Logging;
using StbImageSharp;

namespace mhh;

public abstract class StreamingReceiverBase : IDisposable
{
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
            StoredWidth = 0;
            StoredHeight = 0;
            field = value;
        }
    }

    /// <summary>
    /// Controls whether the incoming texture is vertically flipped.
    /// </summary>
    public bool Invert { get; set; } = true;

    private protected int StoredWidth = 0;
    private protected int StoredHeight = 0;

    private static readonly ILogger Logger = LogHelper.CreateLogger(nameof(StreamingReceiverBase));

    /// <summary>
    /// Connect to the stream source and begin receiving data.
    /// </summary>
    public abstract void Connect(string source);

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

    /// <summary>
    /// Checks the current visualizer and FX for a streaming texture and attaches to that.
    /// </summary>
    public void FindStreamingTexture()
    {
        Texture = Program.AppWindow.Renderer.NewRenderer?.GetStreamingTexture() ?? Program.AppWindow.Renderer.ActiveRenderer?.GetStreamingTexture();
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
