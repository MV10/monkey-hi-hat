
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace mhh;

// TODO manage total cache size

public static class FileCacheManager
{
    private static readonly string CacheIndexPathname;
    
    // Keyed on FileCacheData OriginURI
    private static Dictionary<string, FileCacheData> CacheIndex = new();

    // Keyed on textureHandle by RequestURI
    private static Dictionary<int, FileCacheRequest> QueuedURIs = new();

    // Negatively incremented when FileCacheRequest.TextureHandle is zero, so
    // that command-line --filecache requests can still be keyed in the queue.
    private static int CommandLineIDs = 0;

    private static readonly HttpClient Downloader = new();

    static FileCacheManager()
    {
        if(Program.AppConfig is null)
        {
            Console.WriteLine($"{nameof(FileCacheManager)} was accessed before app configuration was loaded");
            Thread.Sleep(250);
            Environment.Exit(1);
        }

        CacheIndexPathname = Path.Combine(Program.AppConfig.FileCachePath, "index.json");
        if (File.Exists(CacheIndexPathname)) ReadCacheIndex();
    }

    /// <summary>
    /// Retrieves a file from the cache or queues it for download. In either case
    /// the response is a callback with the FileCacheData describing the file, or
    /// a null. The assigned texture handle is used to identify the requested content.
    /// Command-line requests from --filecache can omit textureHandle and callback.
    /// </summary>
    public static void RequestURI(string targetUri, int textureHandle = 0, Action<int, FileCacheData> callback = null)
    {
        // Validation
        var uri = (Program.AppConfig.FileCacheCaseSensitive) ? targetUri : targetUri.ToLowerInvariant();
        Uri parsedUri;
        try
        {
            parsedUri = new(targetUri);
            if (!parsedUri.IsFile) throw new ArgumentException("URI must reference a file");
        }
        catch (Exception ex)
        {
            LogHelper.Logger?.LogError($"{nameof(FileCacheManager)} unable to parse URI {targetUri}\n{ex.Message}");
            callback?.Invoke(textureHandle, null);
            return;
        }
        uri = parsedUri.AbsoluteUri;

        // Immediate response if already cached; if file has expired, the
        // current version is still returned, but a new download is requested
        // and if the new copy is retrieved, the callback is invoked again.
        bool replacingExpiredFile = false;
        if(CacheIndex.TryGetValue(uri, out var fileData))
        {
            callback?.Invoke(textureHandle, fileData);

            // Exit unless we also need to download a new copy
            var expires = fileData.RetrievalTimestamp.AddDays(Program.AppConfig.FileCacheMaxAge);
            if (Program.AppConfig.FileCacheMaxAge == 0 || DateTime.Now <= expires) return;

            replacingExpiredFile = true;
        }

        // Queue for retrieval
        var request = new FileCacheRequest
        {
            TextureHandle = (textureHandle > 0) ? textureHandle : --CommandLineIDs,
            Callback = callback,
            ReplacingExpiredFile = replacingExpiredFile,
            Data = new FileCacheData
            {
                GuidFilename = new Guid().ToString().ToUpperInvariant(),
                OriginURI = uri
            }
        };
        QueuedURIs.Add(textureHandle, request);

        DownloadFile(request);
    }

    /// <summary>
    /// Call this for any textureHandle being released (low overhead).
    /// </summary>
    public static void CancelDownload(int textureHandle)
    {
        if(QueuedURIs.TryGetValue(textureHandle, out var download))
        {
            download.CTS.Cancel();
            QueuedURIs.Remove(textureHandle);
        }
    }

    /// <summary>
    /// Although static classes cannot implement IDisposable, this should be called upon
    /// application shutdown to end any download activity in progress.
    /// </summary>
    public static void Dispose()
    {
        foreach(var kvp in QueuedURIs)
        {
            kvp.Value.CTS.Cancel();
        }
        QueuedURIs.Clear();
    }

    private static void DownloadFile(FileCacheRequest request)
    {
        try
        {
            request.CTS.Token.ThrowIfCancellationRequested();

            var requestMessage = new HttpRequestMessage(HttpMethod.Get, request.Data.OriginURI);
            var response = Downloader.Send(requestMessage, request.CTS.Token);
            if (response.IsSuccessStatusCode)
            {
                using var stream = response.Content.ReadAsStream();
                using var fileStream = new FileStream(request.Data.GetPathname(), FileMode.Create, FileAccess.Write);
                byte[] buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    request.CTS.Token.ThrowIfCancellationRequested();
                    fileStream.Write(buffer, 0, bytesRead);
                    request.Data.Size += bytesRead;
                }
            }

            request.Data.RetrievalTimestamp = DateTime.Now;
            request.Data.ContentType = response.Content.Headers.ContentType?.ToString();

            request.Callback?.Invoke(request.TextureHandle, request.Data);

            if(request.ReplacingExpiredFile)
            {
                File.Delete(CacheIndex[request.Data.OriginURI].GetPathname());
                CacheIndex[request.Data.OriginURI] = request.Data;
            }

            WriteCacheIndex();
        }
        catch (OperationCanceledException)
        {
            request.Callback?.Invoke(request.TextureHandle, null);
        }
        catch (Exception)
        {
            request.Callback?.Invoke(request.TextureHandle, null);
        }
        finally
        {
            QueuedURIs.Remove(request.TextureHandle);
        }

        // TODO clean up failed partial download?
    }

    private static void ReadCacheIndex()
    {
        var json = File.ReadAllText(CacheIndexPathname);
        CacheIndex = JsonConvert.DeserializeObject<Dictionary<string, FileCacheData>>(json);
    }

    private static void WriteCacheIndex()
    {
        string json = JsonConvert.SerializeObject(CacheIndex, Formatting.Indented);
        File.WriteAllText(CacheIndexPathname, json);
    }

}
