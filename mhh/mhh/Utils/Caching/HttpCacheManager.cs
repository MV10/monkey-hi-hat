
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StbImageWriteSharp;

namespace mhh;

/// <summary>
/// Handles storage of cached textures. Note this can be created by Program.cs from standby mode.
/// </summary>
public class HttpCacheManager
{
    private readonly string cacheIndexPathname;
    
    private static readonly ILogger Logger = LogHelper.CreateLogger(nameof(HttpCacheManager));

    public static void ValidateHttpCaching()
    {
        if (Program.AppConfig.HttpCacheEnabled && !Path.Exists(Program.AppConfig.HttpCachePath))
        {
            Logger?.LogInformation($"Creating HTTP cache directory {Program.AppConfig.HttpCachePath}");
            try
            {
                Directory.CreateDirectory(Program.AppConfig.HttpCachePath);    
            }
            catch (Exception e)
            {
                Logger?.LogError($"HTTP cache disabled, failed to create directory {e}");
                Program.AppConfig.HttpCacheEnabled = false;
            }
        }
    }
    
    public HttpCacheManager()
    {
        cacheIndexPathname = Path.Combine(Program.AppConfig.HttpCachePath, "cacheindex.json");
        LoadIndex();
    }

    /// <summary>
    /// Populates Caching.HttpCacheIndex with a list of details about currently cached files.
    /// </summary>
    public void LoadIndex()
    {
        if (File.Exists(cacheIndexPathname))
        {
            var json = File.ReadAllText(cacheIndexPathname);
            Caching.HttpCacheIndex = JsonSerializer.Deserialize<List<HttpCacheItem>>(json);
            PruneByAge();
        }
        else
        {
            Caching.HttpCacheIndex.Clear();
        }
        
        Logger?.LogInformation($"Cache index loaded {Caching.HttpCacheIndex.Count} items");
    }

    /// <summary>
    /// Writes contents of Caching.HttpCacheIndex to storage.
    /// </summary>
    public void SaveIndex()
    {
        File.WriteAllText(cacheIndexPathname, JsonSerializer.Serialize(Caching.HttpCacheIndex));
    }
    
    /// <summary>
    /// Removes cached files and details older than HttpCacheMaxAgeDays setting. Updates the index on disk.
    /// </summary>
    public void PruneByAge()
    {
        if (Caching.HttpCacheIndex.Count == 0 || Program.AppConfig.HttpCacheMaxAgeDays == 0) return;
        var evictions = Caching.HttpCacheIndex.Where(i => DateTime.Now >= i.Timestamp.AddDays(Program.AppConfig.HttpCacheMaxAgeDays)).ToList();
        if (evictions.Count == 0) return;
        foreach(var eviction in evictions) RemoveItem(eviction);
        SaveIndex();
        Logger?.LogInformation($"Cache index pruned {evictions.Count} items by timestamp");
    }

    /// <summary>
    /// Removes oldest cached files if total count or total size limits exceeded. Updates the index on disk.
    /// Return value indicates whether changes were made.
    /// </summary>
    public bool PruneByMetrics()
    {
        if (Caching.HttpCacheIndex.Count == 0 || (Program.AppConfig.HttpCacheMaxFileCount == 0 && Program.AppConfig.HttpCacheMaxTotalMB == 0)) return false;
        var initial = Caching.HttpCacheIndex.Count;
        var list = Caching.HttpCacheIndex.OrderByDescending(i => i.Timestamp).ToList();
        var totalBytes = list.Sum(i => i.Bytes);
        var maxCount = Program.AppConfig.HttpCacheMaxFileCount > 0 ? Program.AppConfig.HttpCacheMaxFileCount : int.MaxValue;
        var maxBytes = Program.AppConfig.HttpCacheMaxTotalMB > 0 ? Program.AppConfig.HttpCacheMaxTotalMB * 1024 * 1024 : int.MaxValue;

        var changed = false;
        while (list.Count > 0 && (list.Count > maxCount || totalBytes > maxBytes))
        {
            changed = true;
            totalBytes -= list[0].Bytes;
            RemoveItem(list[0]);
            list.RemoveAt(0);
        }

        if (changed)
        {
            SaveIndex();
            Logger?.LogInformation($"Cache index pruned {initial - Caching.HttpCacheIndex.Count} items by metrics");
        }
        return changed;
    }

    /// <summary>
    /// Returns data about a cached item or null if it isn't stored.
    /// </summary>
    public HttpCacheItem GetItem(string sourceUrl)
    {
        var url = HttpDownloadManager.NormalizeUrl(sourceUrl);
        return Caching.HttpCacheIndex.FirstOrDefault(i => i.SourceUrl == url);
    }
    
    /// <summary>
    /// Returns the local storage pathname to the requested file, or an empty string.
    /// </summary>
    public string GetPathname(string sourceUrl)
    {
        var item = GetItem(sourceUrl);
        if (item == null || string.IsNullOrEmpty(item.LocalName)) return string.Empty;
        return Path.Combine(Program.AppConfig.HttpCachePath, item.LocalName);
    }
    
    /// <summary>
    /// Writes Download's ImageResult to cache storage as PNG, updates the cache index (collection and on disk) if necessary,
    /// prunes by metrics. This generates the local filename for new items, and updates the stored file size and timestamp.
    /// The caller must dispose of the input stream.
    /// </summary>
    public HttpCacheItem SaveImage(Download dl)
    {
        var item = GetItem(dl.SourceUrl);
        var exists = item != null;
        if (!exists)
        {
            item = new() { SourceUrl = dl.SourceUrl, };
            item.GenerateLocalName();
        }
        var pathname = Path.Combine(Program.AppConfig.HttpCachePath, item.LocalName);

        using var stream = File.OpenWrite(pathname);
        var writer = new ImageWriter();
        writer.WritePng(dl.Image.Data, dl.Image.Width, dl.Image.Height, ColorComponents.RedGreenBlueAlpha, stream);
        
        item.Bytes = stream.Length;
        item.Timestamp = DateTime.Now;
        if (!exists) Caching.HttpCacheIndex.Add(item);
        if (!PruneByMetrics()) SaveIndex();
        
        Logger?.LogInformation($"Cache storing {item.Bytes} bytes as {item.LocalName} from {item.SourceUrl}");

        return item;
    }

    /// <summary>
    /// Removes a specific file from the cache and details from the index collection. Does not update the index file on disk.
    /// </summary>
    public void RemoveItem(HttpCacheItem item)
    {
        Logger?.LogInformation($"Cache removing {item.Bytes} bytes as {item.LocalName} from {item.SourceUrl}");

        var pathname = Path.Combine(Program.AppConfig.HttpCachePath, item.LocalName);
        if (!string.IsNullOrEmpty(item.LocalName) && File.Exists(pathname)) File.Delete(pathname);
        Caching.HttpCacheIndex.Remove(item); // validation is automatic within Remove
    }
}
