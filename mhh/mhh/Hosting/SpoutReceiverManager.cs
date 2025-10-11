
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using Spout.Interop;

namespace mhh;

public class SpoutReceiverManager : StreamingReceiverBase
{
    private static readonly ILogger Logger = LogHelper.CreateLogger(nameof(SpoutReceiverManager));

    private SpoutReceiver Receiver;
    private bool Invert;

    public SpoutReceiverManager()
    {
        Logger?.LogTrace("Constructor");
    }

    /// <inheritdoc/>
    public override void Connect(string source, bool invert)
    {
        Logger?.LogTrace("Connect");
        Invert = invert;
        Receiver = new();
        Receiver.SetActiveSender(source);
    }

    /// <inheritdoc/>
    public override void UpdateTexture()
    {
        if (Texture is null) return;

        if (Receiver.ReceiveTexture())
        {
            _ = Receiver.IsUpdated;

            int width = (int)Receiver.SenderWidth;
            int height = (int)Receiver.SenderHeight;
            if (width == 0 || height == 0) return;

            GL.ActiveTexture(Texture.TextureUnit);
            GL.BindTexture(TextureTarget.Texture2D, Texture.TextureHandle);
            if (TextureWidth == 0 || TextureHeight == 0)
            {
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out TextureWidth);
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out TextureHeight);
            }
            if (TextureWidth > 0 && TextureHeight > 0 && (width != TextureWidth || height != TextureHeight))
            {
                // currently Spout can only receive into a sender-sized texture, create a blank one;
                // maintainer says next version will be able to blit into the receiver's texture size
                TextureWidth = width;
                TextureHeight = height;
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            }
            GL.BindTexture(TextureTarget.Texture2D, 0);

            Receiver.ReceiveTexture((uint)Texture.TextureHandle, (uint)TextureTarget.Texture2D, Invert, 0);
        }

    }

    public override void Dispose()
    {
        base.Dispose();

        if (IsDisposed) return;
        Logger?.LogTrace("Disposing");

        Receiver?.Dispose();

        IsDisposed = true;
        GC.SuppressFinalize(true);
    }
    private bool IsDisposed = false;
}
