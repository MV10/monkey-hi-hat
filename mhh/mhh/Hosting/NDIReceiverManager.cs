
using Microsoft.Extensions.Logging;
using NewTek;
using OpenTK.Graphics.OpenGL;
using StbImageSharp;
using StbImageResizeSharp;
using System.Runtime.InteropServices;

namespace mhh;

public class NDIReceiverManager : StreamingReceiverBase
{
    private string SenderName;
    private readonly string ReceiverName = "Monkey Hi Hat";

    private nint Receiver = nint.Zero;
    private nint ptrSenderName = nint.Zero;
    private nint ptrReceiverName = nint.Zero;

    private bool IsCapturing = false;
    private CancellationTokenSource CaptureCTS = new();
    private Task FrameCaptureTask;

    // for resizing VideoFrameBuffer within UpdateTexture
    private int FinalBufferWidth = 0;
    private int FinalBufferHeight = 0;

    // must be protected by a lock, used by foreground and background threads
    private object VideoFrameLock = new object();
    private int VideoFrameWidth = 0;
    private int VideoFrameHeight = 0;
    private byte[] VideoFrameBuffer;
    private long ForegroundPreviousTimecode = long.MinValue;
    private long VideoFrameTimecode;

    // used by the background thread
    private uint FRAME_WAIT_TIME_MS = 100;
    private NDIlib.video_frame_v2_t VideoFrameData;
    private long BackgroundPreviousTimecode = long.MinValue;
    private NDIlib.audio_frame_v3_t AudioFrameData = new();      // released immediately (not used)
    private NDIlib.metadata_frame_t MetadataFrameData = new();   // released immediately (not used)

    private static readonly ILogger Logger = LogHelper.CreateLogger(nameof(NDIReceiverManager));

    public NDIReceiverManager()
    {
        Logger?.LogTrace("Constructor");

    }

    /// <inheritdoc/>
    public override void Connect(string source)
    {
        Logger?.LogTrace("Connect");

        SenderName = source;

        ptrSenderName = Marshal.StringToHGlobalAnsi(SenderName);
        ptrReceiverName = Marshal.StringToHGlobalAnsi(ReceiverName);

        var config = new NDIlib.recv_create_v3_t
        {
            p_ndi_recv_name = ptrReceiverName,
            source_to_connect_to = new NDIlib.source_t { p_ndi_name = ptrSenderName },
            allow_video_fields = false, // NDI will de-interlace from interlaced sources
            bandwidth = NDIlib.recv_bandwidth_e.recv_bandwidth_highest,
            color_format = NDIlib.recv_color_format_e.recv_color_format_RGBX_RGBA
        };

        Receiver = NDIlib.recv_create_v3(ref config);

        // capture is a blocking operation, so run it on a background thread
        IsCapturing = true;
        FrameCaptureTask = Task.Run(() => ReceiveVideoFrames(CaptureCTS.Token));
    }

    /// <inheritdoc/>
    public override unsafe void UpdateTexture()
    {
        if (Receiver == nint.Zero || Texture is null) return;
        if (VideoFrameBuffer is null || VideoFrameBuffer.Length == 0) return;
        if (ForegroundPreviousTimecode >= VideoFrameTimecode) return;

        ForegroundPreviousTimecode = VideoFrameTimecode;

        // did the video frame dimensions change?
        if (StoredWidth != VideoFrameWidth || StoredHeight != VideoFrameHeight)
        {
            StoredWidth = VideoFrameWidth;
            StoredHeight = VideoFrameHeight;

            switch (Texture.ResizeMode)
            {
                case StreamingResizeContentMode.Viewport:
                    FinalBufferWidth = Program.AppWindow.ClientSize.X;
                    FinalBufferHeight = Program.AppWindow.ClientSize.Y;
                    break;

                case StreamingResizeContentMode.Source:
                    FinalBufferWidth = VideoFrameWidth;
                    FinalBufferHeight = VideoFrameHeight;
                    break;

                case StreamingResizeContentMode.Scaled:
                    if(VideoFrameWidth > VideoFrameHeight)
                    {
                        FinalBufferWidth = Texture.ResizeMaxDimension;
                        FinalBufferHeight = (int)((double)VideoFrameHeight * ((double)Texture.ResizeMaxDimension / (double)VideoFrameWidth));
                    }
                    else
                    {
                        FinalBufferHeight = Texture.ResizeMaxDimension;
                        FinalBufferWidth = (int)((double)VideoFrameWidth * ((double)Texture.ResizeMaxDimension / (double)VideoFrameHeight));
                    }
                    break;
            }
        }

        // resize required?
        var buffer = (FinalBufferWidth != VideoFrameWidth || FinalBufferHeight != VideoFrameHeight) 
            ? ResizeBuffer(VideoFrameBuffer, VideoFrameWidth, VideoFrameHeight, FinalBufferWidth, FinalBufferHeight)
            : VideoFrameBuffer;

        GL.ActiveTexture(Texture.TextureUnit);
        GL.BindTexture(TextureTarget.Texture2D, Texture.TextureHandle);
        lock (VideoFrameLock)
        {
            fixed (byte* ptr = buffer)
            {
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, FinalBufferWidth, FinalBufferHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, buffer);
            }
        }
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    private byte[] ResizeBuffer(byte[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        var output = new byte[targetWidth * targetHeight * 4];

        _ = StbImageResize.stbir_resize_uint8(
            source,             // Input byte array
            sourceWidth,        // Input width
            sourceHeight,       // Input height
            0,                  // Input stride (0 for tightly packed)
            output,             // Output byte array
            targetWidth,        // Output width
            targetHeight,       // Output height
            0,                  // Output stride (0 for tightly packed)
            4                   // Number of channels (RGBA = 4)
        );

        return output;
    }

    // this runs on a background thread
    private unsafe void ReceiveVideoFrames(CancellationToken cancellationToken)
    {
        Logger?.LogTrace("Receive thread started");

        var previousFrameType = (NDIlib.frame_type_e)int.MinValue;

        while (!cancellationToken.IsCancellationRequested)
        {
            var frameType = NDIlib.recv_capture_v3(Receiver, ref VideoFrameData, ref AudioFrameData, ref MetadataFrameData, FRAME_WAIT_TIME_MS);

            //if (frameType != previousFrameType)
            //{
            //    Console.WriteLine($"\n{DateTime.Now} new frame type {frameType}");
            //    if (frameType == NDIlib.frame_type_e.frame_type_video)
            //    {
            //        Console.WriteLine($"  timecode: {VideoFrameData.timecode}");
            //        Console.WriteLine($"  format:   {VideoFrameData.FourCC}");
            //        Console.WriteLine($"  size:     {VideoFrameData.xres} x {VideoFrameData.yres}");
            //        Console.WriteLine($"  FPS:      {VideoFrameData.frame_rate_D}");
            //    }
            //}

            switch (frameType)
            {
                case NDIlib.frame_type_e.frame_type_video:
                    // OpenGL can't be used from a background thread, so we copy
                    // the NDI video buffer to another buffer that OpenGL can move
                    // into a texture when the main thread invokes UpdateTexture
                    if (BackgroundPreviousTimecode < VideoFrameData.timecode)
                    {
                        BackgroundPreviousTimecode = VideoFrameData.timecode;
                        lock (VideoFrameLock)
                        {
                            VideoFrameWidth = VideoFrameData.xres;
                            VideoFrameHeight = VideoFrameData.yres;
                            var bufferLen = VideoFrameData.xres * VideoFrameData.yres * 4;
                            if (VideoFrameBuffer is null || VideoFrameBuffer.Length != bufferLen) VideoFrameBuffer = new byte[bufferLen];
                            if (Invert) StbImage.stbi__vertical_flip((void*)VideoFrameData.p_data, VideoFrameData.xres, VideoFrameData.yres, 4);
                            Marshal.Copy(VideoFrameData.p_data, VideoFrameBuffer, 0, bufferLen);
                            Interlocked.Exchange(ref VideoFrameTimecode, VideoFrameData.timecode);
                        }
                    }
                    NDIlib.recv_free_video_v2(Receiver, ref VideoFrameData);
                    break;

                case NDIlib.frame_type_e.frame_type_audio: // not used
                    NDIlib.recv_free_audio_v3(Receiver, ref AudioFrameData);
                    break;

                case NDIlib.frame_type_e.frame_type_metadata: // not used
                    NDIlib.recv_free_metadata(Receiver, ref MetadataFrameData);
                    break;

                case NDIlib.frame_type_e.frame_type_status_change: // not used
                    break;

                case NDIlib.frame_type_e.frame_type_error:
                    // apparently there isn't any more info available
                    break;

                // "source changed" ... 101 is newer than the bindings package
                case (NDIlib.frame_type_e)101:
                    break;

                default: // frame type "none"
                    break;
            }

            previousFrameType = frameType;
        }

        Logger?.LogTrace("Receive thread exiting");
    }

    public override void Dispose()
    {
        base.Dispose();

        if (IsDisposed) return;
        Logger?.LogTrace("Disposing");

        if (IsCapturing)
        {
            IsCapturing = false;
            CaptureCTS.Cancel();
            // receiving frame data is a blocking operation,
            // allow it time to finsh and check the token
            Thread.Sleep((int)FRAME_WAIT_TIME_MS * 2);
        }

        if (ptrSenderName != nint.Zero) Marshal.FreeHGlobal(ptrSenderName);

        if (ptrReceiverName != nint.Zero) Marshal.FreeHGlobal(ptrReceiverName);

        if (Receiver != nint.Zero)
        {
            NDIlib.recv_destroy(Receiver);
            Receiver = nint.Zero;
        }

        IsDisposed = true;
        GC.SuppressFinalize(true);
    }
    private bool IsDisposed = false;
}
