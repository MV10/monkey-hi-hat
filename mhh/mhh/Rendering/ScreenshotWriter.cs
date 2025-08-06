
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using StbImageWriteSharp;
using System.Runtime.InteropServices;

namespace mhh;

public class ScreenshotWriter
{
    private const int BytesPerPixel = 4;
    private const int JpegQuality = 90;

    // false is PNG
    private readonly bool IsJpeg;

    public ScreenshotWriter(CommandRequest cmd)
    {
        IsJpeg = (cmd == CommandRequest.SnapshotNowJpg || cmd == CommandRequest.SnapshotSpacebarJpg);
    }

    public void SaveFramebuffer(int width, int height, int framebufferHandle = 0)
    {
        var ext = IsJpeg ? "jpg" : "png";
        var filename = $"monkey-hi-hat-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-ffff}.{ext}";
        var pathname = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), filename);

        try
        {
            if(framebufferHandle > 0) GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebufferHandle);
            var buffer = ReadFramebufferPixels(width, height);
            if (framebufferHandle > 0) GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            using var stream = File.OpenWrite(pathname);
            var writer = new ImageWriter();
            if (IsJpeg)
            {
                writer.WriteJpg(buffer, width, height, ColorComponents.RedGreenBlueAlpha, stream, JpegQuality);
            }
            else
            {
                writer.WritePng(buffer, width, height, ColorComponents.RedGreenBlueAlpha, stream);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Logger?.LogError(ex, "Unable to save screenshot");
        }

    }

    unsafe private byte[] ReadFramebufferPixels(int width, int height)
    {
        var bufferSize = width * BytesPerPixel * height;
        var buffer = new byte[bufferSize];
        fixed (byte* pb = buffer)
        {
            var ptr = (IntPtr)pb;
            //GL.ReadPixels(0, 0, width, height, PixelFormat.Bgr, PixelType.UnsignedByte, ptr);
            GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }
        return buffer;
    }
}
