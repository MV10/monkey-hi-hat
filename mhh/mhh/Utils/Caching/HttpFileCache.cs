
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace mhh;

public static class HttpFileCache
{
    private static readonly string CacheIndexPathname;
    
    // Keyed on FileCacheData OriginURI
    private static Dictionary<string, CachedFileData> CacheIndex = new();
    private static Dictionary<string, CachedFileRequest> QueuedURIs = new();

    // Total of all FileCacheData.Size values
    private static long CacheSpaceUsed;

    private static readonly HttpClient Downloader = new();

    static HttpFileCache()
    {
        if(Program.AppConfig is null)
        {
            Console.WriteLine($"{nameof(HttpFileCache)} was accessed before app configuration was loaded");
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
    /// The file consumer should call ReleaseURI when releasing texture resources.
    /// </summary>
    public static void RequestURI(string sourceUri, int fileHandle, Action<int, CachedFileData> callback = null)
    {
        var uri = ParseUri(sourceUri);
        if (uri is null)
        {
            callback?.Invoke(fileHandle, null);
            return;
        }

        // Immediate response if already cached; if file has expired, the
        // current version is still returned, but a new download is requested
        // and if the new copy is retrieved, the callback is invoked again.
        bool replacingExpiredFile = false;
        if(CacheIndex.TryGetValue(uri, out var fileData))
        {
            callback?.Invoke(fileHandle, fileData);

            // Mark it as in-use
            fileData.UsageCounter++;

            // Exit unless we also need to download a new copy
            var expires = fileData.RetrievalTimestamp.AddDays(Program.AppConfig.FileCacheMaxAge);
            if (Program.AppConfig.FileCacheMaxAge == 0 || DateTime.Now <= expires) return;

            replacingExpiredFile = true;
        }

        // Queue for retrieval
        var request = new CachedFileRequest
        {
            FileHandle = fileHandle,
            Callback = callback,
            ReplacingExpiredFile = replacingExpiredFile,
            Data = new CachedFileData
            {
                GuidFilename = new Guid().ToString().ToUpperInvariant(),
                OriginURI = uri
            }
        };
        QueuedURIs.Add(request.Data.OriginURI, request);

        _ = Task.Run(() => DownloadFile(request));
    }

    /// <summary>
    /// Texture consumers should call this when disposing texture resources.
    /// </summary>
    public static void ReleaseURI(string sourceUri)
    {
        var uri = ParseUri(sourceUri);
        if (uri is null) return;

        if (QueuedURIs.TryGetValue(uri, out var download))
        {
            download.CTS.Cancel();
            QueuedURIs.Remove(uri);
        }

        if(CacheIndex.TryGetValue(uri, out var file))
        {
            file.UsageCounter--;
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

    private static string ParseUri(string sourceUri)
    {
        var uri = (Program.AppConfig.FileCacheCaseSensitive) ? sourceUri : sourceUri.ToLowerInvariant();
        Uri parsedUri;
        try
        {
            parsedUri = new(sourceUri);
            if (!parsedUri.IsFile) throw new ArgumentException("URI must reference a file");
        }
        catch (Exception ex)
        {
            LogHelper.Logger?.LogError($"{nameof(HttpFileCache)} unable to parse URI {sourceUri}\n{ex.Message}");
            return null;
        }
        return parsedUri.AbsoluteUri;
    }

    private static void DownloadFile(CachedFileRequest request)
    {
        bool downloadFailed = false;

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

            request.Callback?.Invoke(request.FileHandle, request.Data);

            if(request.ReplacingExpiredFile)
            {
                File.Delete(CacheIndex[request.Data.OriginURI].GetPathname());
                CacheIndex[request.Data.OriginURI] = request.Data;
            }

            CacheSpaceUsed += request.Data.Size;
            PruneCache();
            WriteCacheIndex();
        }
        catch (OperationCanceledException)
        {
            downloadFailed = true;
            request.Callback?.Invoke(request.FileHandle, null);
        }
        catch (Exception)
        {
            downloadFailed = true;
            request.Callback?.Invoke(request.FileHandle, null);
        }
        finally
        {
            QueuedURIs.Remove(request.Data.OriginURI);
        }

        // clean up failed partial download
        if (downloadFailed && File.Exists(request.Data.GetPathname()))
        {
            File.Delete(request.Data.GetPathname());
        }
    }

    private static void PruneCache()
    {
        if (CacheSpaceUsed < Program.AppConfig.FileCacheMaxSize) return;
        var files = CacheIndex
                    .Where(kvp => kvp.Value.UsageCounter == 0)
                    .OrderBy(kvp => kvp.Value.RetrievalTimestamp)
                    .Select(kvp => kvp.Value)
                    .ToList();

        foreach(var file in files)
        {
            if(File.Exists(file.GetPathname()))
            {
                File.Delete(file.GetPathname());
                CacheSpaceUsed -= file.Size;
                CacheIndex.Remove(file.OriginURI);
            }
            if (CacheSpaceUsed < Program.AppConfig.FileCacheMaxSize) return;
        }
    }

    private static void ReadCacheIndex()
    {
        var json = File.ReadAllText(CacheIndexPathname);
        CacheIndex = JsonConvert.DeserializeObject<Dictionary<string, CachedFileData>>(json);
        CacheSpaceUsed = CacheIndex.Sum(kvp => kvp.Value.Size);
    }

    private static void WriteCacheIndex()
    {
        string json = JsonConvert.SerializeObject(CacheIndex, Formatting.Indented);
        File.WriteAllText(CacheIndexPathname, json);
        CacheSpaceUsed = CacheIndex.Sum(kvp => kvp.Value.Size);
    }
}
