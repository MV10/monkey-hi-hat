
using Microsoft.Extensions.Logging;
using NewTek;
using OpenTK.Graphics.OpenGL;
using StbImageSharp;
using System.Runtime.InteropServices;

namespace mhh;

internal class NDISenderManager : IDisposable
{
    private nint sender = nint.Zero;
    private NDIlib.video_frame_v2_t videoFrameDescriptor;
    private byte[] videoFrameData;

    private static readonly ILogger Logger = LogHelper.CreateLogger(nameof(NDISenderManager));

    public NDISenderManager(string name, string groups)
    {
        Logger?.LogTrace("Constructor");

        nint ptrName = nint.Zero;
        nint ptrGroups = nint.Zero;

        try
        {
            ptrName = Marshal.StringToHGlobalAnsi(name);
            ptrGroups = Marshal.StringToHGlobalAnsi(groups);
            var config = new NDIlib.send_create_t
            {
                p_ndi_name = ptrName,
                p_groups = ptrGroups,
                clock_audio = false,
                clock_video = true,
            };
            sender = NDIlib.send_create(ref config);
        }
        finally
        {
            if (ptrName != nint.Zero) Marshal.FreeHGlobal(ptrName);
            if (ptrGroups != nint.Zero) Marshal.FreeHGlobal(ptrGroups);
        }

        PrepareVideoFrame();
    }

    public bool IsValid 
        => sender != nint.Zero;

    public void PrepareVideoFrame()
    {
        if (!IsValid) return;
        videoFrameDescriptor = new()
        {
            xres = Program.AppWindow.ClientSize.X,
            yres = Program.AppWindow.ClientSize.Y,
            FourCC = NDIlib.FourCC_type_e.FourCC_type_RGBA,
            frame_rate_N = 60000,
            frame_rate_D = 1001,
            picture_aspect_ratio = (float)Program.AppWindow.ClientSize.X / Program.AppWindow.ClientSize.Y,
            frame_format_type = NDIlib.frame_format_type_e.frame_format_type_progressive,
            timecode = NDIlib.send_timecode_synthesize,
            p_data = nint.Zero,
            line_stride_in_bytes = Program.AppWindow.ClientSize.X * 4,
        };
    }

    public unsafe void SendVideoFrame()
    {
        if (!IsValid) return;
        var bufferSize = Program.AppWindow.ClientSize.X * 4 * Program.AppWindow.ClientSize.Y;
        if (videoFrameData is null || videoFrameData.Length != bufferSize) videoFrameData = new byte[bufferSize];
        fixed (byte* ptrFrameData = videoFrameData)
        {
            GL.ReadPixels(0, 0, Program.AppWindow.ClientSize.X, Program.AppWindow.ClientSize.Y, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)ptrFrameData);
            StbImage.stbi__vertical_flip(ptrFrameData, Program.AppWindow.ClientSize.X, Program.AppWindow.ClientSize.Y, 4);
            videoFrameDescriptor.p_data = (nint)ptrFrameData;
            NDIlib.send_send_video_v2(sender, ref videoFrameDescriptor);
        }
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        Logger?.LogTrace("Disposing");

        if (IsValid)
        {
            NDIlib.send_destroy(sender);
            sender = nint.Zero;
        }

        IsDisposed = true;
        GC.SuppressFinalize(true);
    }
    private bool IsDisposed = false;
}