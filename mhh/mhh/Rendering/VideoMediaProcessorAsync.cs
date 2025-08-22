
using FFMediaToolkit.Decoding;
using Microsoft.Extensions.Logging;

namespace mhh;

/// <summary>
/// NOT CURRENTLY WORKING.
/// This is the asynchronous version of the VideoMediaProcessor class
/// which manages FFMediaToolkit decoding of video files and blitting frame
/// data to a previously-allocated OpenGL texture buffer. The caller should
/// invoke BeginProcessing before rendering starts, and EndProcessing before
/// the renderer is destroyed. During rendering the OpenGL textures will be
/// updated automatically on a background thread.
/// </summary>
public class VideoMediaProcessorAsync : VideoMediaProcessor
{
    public bool IsProcessing { get => (ProcessingFlag == 1); }
    private int ProcessingFlag = 0;
    private CancellationTokenSource cts;

    public VideoMediaProcessorAsync(IReadOnlyList<GLImageTexture> textures)
    : base(textures)
    {
        // These must be re-opened on the background thread after BeginProcessing is called.
        foreach (var tex in VideoTextures)
        {
            tex.VideoData.Stream.Dispose();
            tex.VideoData.Stream = null;
            tex.VideoData.File.Dispose();
            tex.VideoData.File = null;
        }
    }

    public void BeginProcessing()
    {
        if (IsDisposed || IsProcessing || (cts?.IsCancellationRequested ?? false)) return;
        Interlocked.Exchange(ref ProcessingFlag, 1);
        cts = new();
        _ = Task.Run(() => { ProcessingLoop(cts.Token); });
    }

    public void EndProcessing()
    {
        if (IsDisposed || !IsProcessing || (cts?.IsCancellationRequested ?? true)) return;
        cts.Cancel();
    }

    public Task ProcessingLoop(CancellationToken cancellationToken)
    {
        // These operations already succeeded in RenderingHelper.LoadVideoFile
        foreach (var tex in VideoTextures)
        {
            tex.VideoData.File = MediaFile.Open(tex.VideoData.Pathname, RenderingHelper.VideoMediaOptions);
            tex.VideoData.Stream = tex.VideoData.File.Video;
        }

        while (!cts.IsCancellationRequested && !IsDisposed)
        {
            base.UpdateTextures();
            Thread.Sleep(0); // lowest overhead; see eyecandy OpenAL processor comments for details
        }

        Interlocked.Exchange(ref ProcessingFlag, 0);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        LogHelper.Logger?.LogTrace($"{GetType()}.Dispose() ----------------------------");

        if (IsProcessing)
        {
            LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() Canceling async thread");
            EndProcessing();
            var timeout = DateTime.Now.AddSeconds(10);
            while (IsProcessing && DateTime.Now < timeout) Thread.Sleep(0);
        }

        base.Dispose();

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
