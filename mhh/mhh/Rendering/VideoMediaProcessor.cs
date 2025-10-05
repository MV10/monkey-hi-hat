
using eyecandy;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using StbImageSharp;

namespace mhh;

// MHH temporarily supported background-thread processing for the decode
// portion, but the overhead of locking the buffer for threadsafe buffer
// usage between the main thread (OpenGL texture updates) and the decode
// thread (buffer writes) was less smooth than synchronous updating.

/// <summary>
/// This manages FFMediaToolkit decoding of video files and blitting frame
/// data to a previously-allocated OpenGL texture buffer. This works in a
/// synchronous fashion, the renderer should invoke UpdateTextures in the
/// PreRenderFrame function.
/// </summary>
public class VideoMediaProcessor : IDisposable
{
    protected readonly List<GLImageTexture> VideoTextures;

    // Video file playback involves frequently updating the texture with new frames.
    // Since OpenGL is not thread-safe, this mutex prevents overlapping calls with
    // the eyecandy background thread that updates audio texture data.
    private static readonly Mutex GLTextureLockMutex = new(false, AudioTextureEngine.GLTextureMutexName);

    private static readonly ILogger Logger = LogHelper.CreateLogger(nameof(VideoMediaProcessor));

    public VideoMediaProcessor(IReadOnlyList<GLImageTexture> textures)
    {
        VideoTextures = textures.Where(t => t.Loaded && t.VideoData is not null).ToList();
        Logger?.LogTrace($"Constructor found {VideoTextures.Count} video textures");
    }

    /// <summary>
    /// This should be invoked from the renferer's PreRenderFrame method.
    /// </summary>
    public void UpdateTextures()
    {
        foreach (var tex in VideoTextures)
        {
            CheckPauseState(tex);
            if (tex.VideoData.IsPaused) continue;
            DecodeVideoFrame(tex);
        }
    }

    private void CheckPauseState(GLImageTexture tex)
    {
        if (Program.AppWindow.Renderer.TimePaused)
        {
            if (!tex.VideoData.IsPaused)
            {
                tex.VideoData.IsPaused = true;
                tex.VideoData.Clock.Stop();
                Logger?.LogTrace($"Paused video {tex.VideoData.Pathname}");
            }
        }
        else
        {
            if (tex.VideoData.IsPaused)
            {
                tex.VideoData.IsPaused = false;
                tex.VideoData.Clock.Start();
                Logger?.LogTrace($"Unpaused video {tex.VideoData.Pathname}");
            }

        }
    }

    protected void DecodeVideoFrame(GLImageTexture tex)
    {
        if (!tex.VideoData.Clock.IsRunning)
        {
            if (tex.VideoData.IsPaused) return;
            tex.VideoData.Clock.Start();
            Logger?.LogTrace($"Clock started on video {tex.Filename}");
        }

        if (tex.VideoData.Clock.Elapsed == tex.VideoData.LastUpdateTime) return; // abort if time hasn't changed

        if (tex.VideoData.Clock.Elapsed > tex.VideoData.Duration)
        {
            Logger?.LogTrace($"Looped video {tex.Filename} at {tex.VideoData.Clock.Elapsed.TotalSeconds:F2}");
            tex.VideoData.Clock.Restart(); // always loop
        }

        try
        {
            using var frame = tex.VideoData.Stream.GetFrame(tex.VideoData.Clock.Elapsed);
            tex.VideoData.LastUpdateTime = tex.VideoData.Clock.Elapsed;
            if (!frame.Data.IsEmpty && tex.VideoData.Stream.Position != tex.VideoData.LastStreamPosition)
            {
                tex.VideoData.LastStreamPosition = tex.VideoData.Stream.Position;
                unsafe
                {
                    GL.ActiveTexture(tex.TextureUnit);
                    GL.BindTexture(TextureTarget.Texture2D, tex.TextureHandle);
                    fixed (void* ptr = frame.Data)
                    {
                        if (Program.AppConfig.VideoFlip == VideoFlipMode.Internal) StbImage.stbi__vertical_flip(ptr, tex.VideoData.Width, tex.VideoData.Height, 4);
                        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, tex.VideoData.Width, tex.VideoData.Height, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)(byte*)ptr);
                    }
                    GL.BindTexture(TextureTarget.Texture2D, 0);
                }
            }
        }
        catch (EndOfStreamException)
        {
            Logger?.LogTrace($"Looped video {tex.Filename} at {tex.VideoData.Clock.Elapsed.TotalSeconds:F2}");
            tex.VideoData.Clock.Restart();
            return;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, $"Error decoding video stream {tex.Filename}");
        }
    }

    public virtual void Dispose()
    {
        if(IsDisposed) return;
        Logger?.LogTrace("Disposing");

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
