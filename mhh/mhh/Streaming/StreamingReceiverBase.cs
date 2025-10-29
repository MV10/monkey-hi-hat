
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
            SenderWidth = 0;
            SenderHeight = 0;
            field = value;
        }
    }

    /// <summary>
    /// Controls whether the incoming texture is vertically flipped.
    /// </summary>
    public bool Invert { get; set; } = true;

    // Stores the most recently seen dimensions of the source image
    private protected int SenderWidth = 0;
    private protected int SenderHeight = 0;

    // Locally-allocated image dimensions
    private protected int LocalWidth = 0;
    private protected int LocalHeight = 0;

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

    /// <summary>
    /// Sets LocalWidth / LocalHeight based on on the Texture.ResizeMode
    /// versus either viewport diemsions or SenderWidth / SenderHeight.
    /// Returns true if the local dimensions changed.
    /// </summary>
    public bool UpdateLocalDimensions()
    {
        var prevWidth = LocalWidth;
        var prevHeight = LocalHeight;

        switch (Texture.ResizeMode)
        {
            case StreamingResizeContentMode.Viewport:
                LocalWidth = Program.AppWindow.ClientSize.X;
                LocalHeight = Program.AppWindow.ClientSize.Y;
                break;

            case StreamingResizeContentMode.Source:
                LocalWidth = SenderWidth;
                LocalHeight = SenderHeight;
                break;

            case StreamingResizeContentMode.Scaled:
                if (SenderWidth > SenderHeight)
                {
                    LocalWidth = Texture.ResizeMaxDimension;
                    LocalHeight = (int)((double)SenderHeight * ((double)Texture.ResizeMaxDimension / (double)SenderWidth));
                }
                else
                {
                    LocalHeight = Texture.ResizeMaxDimension;
                    LocalWidth = (int)((double)SenderWidth * ((double)Texture.ResizeMaxDimension / (double)SenderHeight));
                }
                break;
        }

        return (LocalWidth != prevWidth || LocalHeight != prevHeight);
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
