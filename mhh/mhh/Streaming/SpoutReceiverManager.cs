
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using Spout.Interop;

namespace mhh;

public class SpoutReceiverManager : StreamingReceiverBase
{
    private static readonly ILogger Logger = LogHelper.CreateLogger(nameof(SpoutReceiverManager));

    private SpoutReceiver Receiver;

    public SpoutReceiverManager()
    {
        Logger?.LogTrace("Constructor");
    }

    /// <inheritdoc/>
    public override void Connect(string source)
    {
        Logger?.LogTrace("Connect");
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
            if (SenderWidth == 0 || SenderHeight == 0)
            {
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out SenderWidth);
                GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out SenderHeight);
            }
            if (SenderWidth > 0 && SenderHeight > 0 && (width != SenderWidth || height != SenderHeight))
            {
                SenderWidth = width;
                SenderHeight = height;
            }

            if (UpdateLocalDimensions())
            {
                // resize the buffer, Spout will FBO blit to match
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, LocalWidth, LocalHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
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
