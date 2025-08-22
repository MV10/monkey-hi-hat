
using eyecandy;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using StbImageSharp;

namespace mhh;

/// <summary>
/// This manages FFMediaToolkit decoding of video files and blitting frame
/// data to a previously-allocated OpenGL texture buffer. This works in a
/// synchronous fashion, the render loop should invoke UpdateTextures.
/// </summary>
public class VideoMediaProcessor : IDisposable
{
    protected readonly List<GLImageTexture> VideoTextures;

    // Video file playback involves frequently updating the texture with new frames.
    // Since OpenGL is not thread-safe, this mutex prevents overlapping calls with
    // the eyecandy background thread that updates audio texture data.
    private static readonly Mutex GLTextureLockMutex = new(false, AudioTextureEngine.GLTextureMutexName);

    public VideoMediaProcessor(IReadOnlyList<GLImageTexture> textures)
    {
        VideoTextures = textures.Where(t => t.Loaded && t.VideoData is not null).ToList();
    }

    public void UpdateTextures()
    {
        foreach (var tex in VideoTextures)
        {
            if (Program.AppWindow.Renderer.TimePaused)
            {
                if (!tex.VideoData.IsPaused)
                {
                    tex.VideoData.IsPaused = true;
                    tex.VideoData.Clock.Stop();
                }
            }
            else
            {
                if (tex.VideoData.IsPaused)
                {
                    tex.VideoData.IsPaused = false;
                    tex.VideoData.Clock.Start();
                }

                DecodeVideoFrame(tex);
            }
        }
    }

    private static void DecodeVideoFrame(GLImageTexture tex)
    {
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

        try
        {
            var frame = tex.VideoData.Stream.GetFrame(tex.VideoData.Clock.Elapsed);

            tex.VideoData.LastUpdateTime = tex.VideoData.Clock.Elapsed;

            if (!frame.Data.IsEmpty && tex.VideoData.Stream.Position != tex.VideoData.LastStreamPosition)
            {
                tex.VideoData.LastStreamPosition = tex.VideoData.Stream.Position;

                GLTextureLockMutex.WaitOne();
                try
                {
                    GL.ActiveTexture(tex.TextureUnit);
                    GL.BindTexture(TextureTarget.Texture2D, tex.TextureHandle);
                    var buffer = frame.Data;
                    unsafe
                    {
                        fixed (void* ptr = frame.Data)
                        {
                            if (Program.AppConfig.VideoFlip == VideoFlipMode.Internal) StbImage.stbi__vertical_flip(ptr, tex.VideoData.Width, tex.VideoData.Height, 4);
                            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, tex.VideoData.Width, tex.VideoData.Height, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)(byte*)ptr);
                        }
                    }
                    GL.BindTexture(TextureTarget.Texture2D, 0);
                }
                finally
                {
                    GLTextureLockMutex.ReleaseMutex();
                }
            }
        }
        catch (EndOfStreamException)
        {
            // If we reach the end of the stream, restart the clock to loop the video and abort
            tex.VideoData.Clock.Restart();
            return;
        }
        catch (Exception ex)
        {
            LogHelper.Logger?.LogError(ex, $"Error decoding video stream {tex.Filename}");
        }
    }

    public virtual void Dispose()
    {
        if(IsDisposed) return;
        LogHelper.Logger?.LogTrace($"{GetType()}.Dispose() ----------------------------");

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() Releasing video resources");
        foreach(var tex in VideoTextures)
        {
            if (!tex.Loaded || tex.VideoData is null) continue;
            tex.VideoData.Stream.Dispose();
            tex.VideoData.Stream = null;
            tex.VideoData.File.Dispose();
            tex.VideoData.File = null;
        }

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
