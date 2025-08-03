
using OpenTK.Graphics.OpenGL;

namespace mhh;

public static class VideoRenderingHelper
{
    /// <summary>
    /// Called by IVertexSource RenderFrame to update any video textures that are currently playing.
    /// </summary>
    public static void Render(IReadOnlyList<GLImageTexture> textures)
    {
        if (textures is null || textures.Count == 0) return;

        bool paused = Program.AppWindow.Renderer.TimePaused;

        foreach (var tex in textures)
        {
            if (tex.VideoData is not null && tex.Loaded)
            {
                if (paused)
                {
                    if (!tex.VideoData.IsPaused)
                    {
                        tex.VideoData.IsPaused = true;
                        tex.VideoData.Clock.Stop();
                    }
                    continue; // don't update video frames when paused
                }
                else 
                {
                    if (tex.VideoData.IsPaused)
                    {
                        tex.VideoData.IsPaused = false;
                        tex.VideoData.Clock.Start();
                    }
                }

                UpdateTexture(tex);
            }
        }
    }

    /// <summary>
    /// Called by GLResourceManagers's Dispose/Destroy methods.
    /// </summary>
    public static void DestroyVideoObjects(GLImageTexture tex)
    {
        tex.VideoData.File?.Dispose();
        tex.VideoData = null;
    }

    private static void UpdateTexture(GLImageTexture tex)
    {
        if (tex.VideoData is null || !tex.Loaded || tex.VideoData.Stream is null) return;

        if (!tex.VideoData.Clock.IsRunning)
        {
            if (tex.VideoData.IsPaused) return;
            tex.VideoData.Clock.Start();
        }

        if (tex.VideoData.Clock.Elapsed == tex.VideoData.LastUpdateTime) return; // abort if time hasn't changed

        if (tex.VideoData.Clock.Elapsed > tex.VideoData.Duration)
        {
            tex.VideoData.Clock.Restart(); // always loop
        }

        /*
        Performance Considerations: For sequential playback without seeking, consider switching to 
        TryGetNextFrame(out ImageData frame) which returns a boolean indicating success. The current 
        implementation uses GetFrame for flexible time-based updates, suitable for looping or non-linear playback.

        Error Handling: In production, add try-catch around GetFrame as it may throw exceptions on severe
        errors (e.g., corrupt files). The empty check handles most end-of-stream cases gracefully.
        */
        try
        {
            var frame = tex.VideoData.Stream.GetFrame(tex.VideoData.Clock.Elapsed);

            tex.VideoData.LastUpdateTime = tex.VideoData.Clock.Elapsed;

            if (!frame.Data.IsEmpty && tex.VideoData.Stream.Position != tex.VideoData.LastStreamPosition)
            {
                tex.VideoData.LastStreamPosition = tex.VideoData.Stream.Position;

                if(Program.AppConfig.VideoFlip == VideoFlipMode.Internal)
                {
                    int rowBytes = tex.VideoData.Width * 4; // 4 bytes per pixel for RGBA
                    byte[] flippedData = new byte[frame.Data.Length];
                    for (int y = 0; y < tex.VideoData.Height; y++)
                    {
                        int sourceOffset = y * frame.Stride;
                        int destOffset = (tex.VideoData.Height - 1 - y) * rowBytes;
                        frame.Data.Slice(sourceOffset, rowBytes).CopyTo(flippedData.AsSpan(destOffset, rowBytes));
                    }

                    GL.BindTexture(TextureTarget.Texture2D, tex.TextureHandle);
                    unsafe
                    {
                        fixed (byte* ptr = flippedData)
                        {
                            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, tex.VideoData.Width, tex.VideoData.Height, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)ptr);
                        }
                    }
                    GL.BindTexture(TextureTarget.Texture2D, 0);
                }
                else
                {
                    GL.BindTexture(TextureTarget.Texture2D, tex.TextureHandle);
                    unsafe
                    {
                        fixed (byte* ptr = frame.Data)
                        {
                            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, tex.VideoData.Width, tex.VideoData.Height, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)ptr);
                        }
                    }
                    GL.BindTexture(TextureTarget.Texture2D, 0);
                }
            }
        }
        catch (EndOfStreamException e)
        {
            // If we reach the end of the stream, restart the clock to loop the video and abort
            tex.VideoData.Clock.Restart();
            return;
        }
        catch (Exception ex)
        {
            // TODO better error handling
        }
    }
}
