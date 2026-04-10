
using System.Collections.Concurrent;
using Downloader;
using Microsoft.Extensions.Logging;
using StbImageSharp;
using StbImageResizeSharp;

namespace mhh;

// Technically an edge-case exists, nothing prevents multiple simultaneous downloads of the same
// URL, but a single viz/fx config shouldn't use two copies of the same texture. There are only
// two cases where this could happen: a viz and fx each use the same HTTP texture, or a viz or
// fx requests an HTTP texture exactly when the user issues --cache add for the same URL. However,
// the thread-safe tracking to avoid this isn't really worth the overhead.

/// <summary>
/// Downloads textures to a memory stream and resizes them if necessary (per config MaxDimension).
/// If HTTP caching is enabled, the result is stored in cache. If an active texture was provided,
/// HostWindow will invoke GetTextures to upload to the GPU. This can be invoked from Program.cs
/// for on-demand caching.
/// </summary>
public static class HttpDownloadManager
{
    // See Abort for thread-safe cancellation and replacement
    private static CancellationTokenSource CTS = new();

    // Thread-safe copy from Program.AppConfig.
    private static int maxDimension;
    
    private static DownloadConfiguration config = new()
    {
        MaxTryAgainOnFailure = 2,
        ChunkCount = 8,
        ParallelDownload = true,
        EnableAutoResumeDownload = false,
    };

    // Downloads which are ready to upload to a Texture (failed downloads are logged, and
    // interactive command-line downloads are only cached and have no target Texture)
    private static ConcurrentQueue<Download> downloadQueue = new();

    private static readonly ILogger Logger = LogHelper.CreateLogger(nameof(HttpDownloadManager));

    /// <summary>
    /// Ensures the scheme (http/https) and host (FQDN) are lowercase and that the URL is well-formed.
    /// Returns an empty string if it can't be parsed for some reason, or the normalized version.
    /// </summary>
    public static string NormalizeUrl(string sourceUrl)
      => !Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) ? string.Empty : uri.AbsoluteUri;
    
    /// <summary>
    /// Downloads a texture for live usage. This is used when MHH is active by RenderingHelper. The
    /// HostWindow processing loop must periodically check HasCompletedDownloads and invoke GetTextures
    /// to actually upload the results to the GPU for shader usage.
    /// </summary>
    public static void Download(string sourceUrl, GLImageTexture texture)
    {
        Logger?.LogDebug($"{nameof(Download)} downloading: {sourceUrl}");
        
        // ensure we have a thread-safe copy for background-thread usage
        Interlocked.Exchange(ref maxDimension, Program.AppConfig.HttpCacheMaxDimension);

        var url = NormalizeUrl(sourceUrl);
        if (string.IsNullOrEmpty(url))
        {
            Logger?.LogError($"{nameof(Download)} can't parse URL {sourceUrl}");
            return;
        }

        Task.Run(() => BackgroundDownload(sourceUrl, Program.AppWindow.HttpCaching, texture));
    }

    /// <summary>
    /// Called by HostWindow to upload completed downloads to the GPU for shader usage.
    /// </summary>
    public unsafe static void GetTextures()
    {
        while (downloadQueue.TryDequeue(out var dl))
        {
            // Checking for -1 guards against a shader that ended before download completed.
            // We don't want to Abort when shaders end because caching the result will still be useful.
            if (dl.Texture.TextureHandle != -1)
            {
                fixed (void* ptr = dl.Image.Data)
                {
                    StbImage.stbi__vertical_flip(ptr, dl.Image.Width, dl.Image.Height, 4);
                }
                RenderingHelper.LoadFromImageResult(dl.Texture, dl.Image);
            }
        }
    }

    /// <summary>
    /// Aborts all in-flight downloads and creates a new CTS for later use.
    /// </summary>
    public static void Abort()
    {
        Logger?.LogDebug(nameof(Abort));
        var oldCTS = Interlocked.Exchange(ref CTS, new CancellationTokenSource());
        oldCTS.Cancel();
        oldCTS.Dispose();
    }

    /// <summary>
    /// Downloads a texture for caching. This is used from Program.cs in standby mode.
    /// </summary>
    public static async Task<string> InteractiveDownloadAsync(string sourceUrl, HttpCacheManager cacheManager)
    {
        Logger?.LogDebug($"{nameof(InteractiveDownloadAsync)} downloading for caching: {sourceUrl}");
        
        // ensure we have a thread-safe copy for background-thread usage
        Interlocked.Exchange(ref maxDimension, Program.AppConfig.HttpCacheMaxDimension);

        var url = NormalizeUrl(sourceUrl);
        if (string.IsNullOrEmpty(url))
        {
            Logger?.LogError($"{nameof(InteractiveDownloadAsync)} can't parse URL {sourceUrl}");
            return "ERR: Can't parse URL";
        }

        var dl = await BackgroundDownload(sourceUrl, cacheManager);
        if (dl.Image is null) return "ERR: Download failed";

        var item = cacheManager.SaveImage(dl);
        return $"Cached {dl.Image.Width}x{dl.Image.Height} image ({item.Bytes:N0} bytes, {(item.Bytes / 1024 / 1024):N0} MB)";
    }

    /// <summary>
    /// This runs the download operation on a background thread. If it completes normally, any MaxDimension setting
    /// is applied (ie. resizing). If a target Texture was identified, the download tracker is added to a queue for
    /// processing from the main thread.
    /// </summary>
    private static async Task<Download> BackgroundDownload(string sourceUrl, HttpCacheManager cacheManager, GLImageTexture texture = null)
    {
        Logger?.LogDebug($"{nameof(BackgroundDownload)} invoked for {sourceUrl}");

        var dl = new Download()
        {
            SourceUrl =  sourceUrl,
            Texture = texture, // storage only, main thread must copy Image to Texture
            DownloadJob = DownloadBuilder.New()
                .WithConfiguration(config)
                .WithUrl(sourceUrl)
                .Build(),
        };
        
        Stream stream = null;
        try
        {
            stream = await dl.DownloadJob.StartAsync(CTS.Token);
            var success = dl.DownloadJob.Status == DownloadStatus.Completed;
            if (!success) return dl;

            // debug: write stream to home directory file
            //stream.Position = 0;
            //File.WriteAllBytes("/home/mv10/test.jpg", (stream as MemoryStream).ToArray());
            
            dl.Image = ProcessImageStream(stream);
            if (dl.Image is not null) cacheManager?.SaveImage(dl);
            if (dl.Texture is not null) downloadQueue.Enqueue(dl);
        }
        catch (TaskCanceledException)
        {
            dl.Image = null;
        }
        catch (Exception e)
        {
            Logger?.LogError($"{nameof(BackgroundDownload)} for {dl.SourceUrl} failed: {e}");
            dl.Image = null;
        }
        finally
        {
            stream?.Dispose();
            dl.DownloadJob?.Stop();
            dl.DownloadJob?.Dispose();
        }

        return dl;
    }

    /// <summary>
    /// Turns the memory stream into an image buffer and resizes it if necessary.
    /// </summary>
    private static ImageResult ProcessImageStream(Stream stream)
    {
        ImageResult image = null;
        try
        {
            // DO NOT flip vertically, that's done elsewhere
            StbImage.stbi_set_flip_vertically_on_load(0); 
               
            stream.Position = 0;
            image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            // debug: write raw buffer to home directory file (Gwenview can read .raw files)
            //stream.Position = 0;
            //File.WriteAllBytes("/home/mv10/test.raw", (stream as MemoryStream).ToArray());

            if (maxDimension > 0)
            {
                int sourceWidth = image.Width;
                int sourceHeight = image.Height;
                int targetWidth, targetHeight;
                if (sourceWidth > maxDimension || sourceHeight > maxDimension)
                {
                    double ratio = (double)maxDimension / Math.Max(sourceWidth, sourceHeight);
                    targetWidth = (int)Math.Round(sourceWidth * ratio);
                    targetHeight = (int)Math.Round(sourceHeight * ratio);
                    var output = new byte[targetWidth * targetHeight * 4];
                    
                    Logger?.LogDebug($"{nameof(ProcessImageStream)}: resizing ({sourceWidth}x{sourceHeight}) to ({targetWidth}x{targetHeight})");
                    
                    _ = StbImageResize.stbir_resize_uint8(
                        image.Data,  // Input byte array
                        sourceWidth,    // Input width
                        sourceHeight,   // Input height
                        0,     // Input stride (0 for tightly packed)
                        output,                // Output byte array
                        targetWidth,   // Output width
                        targetHeight,  // Output height
                        0,    // Output stride (0 for tightly packed)
                        4          // Number of channels (RGBA = 4)
                    );
                    
                    image.Data = output;
                    image.Width = targetWidth;
                    image.Height = targetHeight;
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError($"{nameof(ProcessImageStream)} failed, re-throwing {ex}");
            throw;
        }

        return image;
    }
}
