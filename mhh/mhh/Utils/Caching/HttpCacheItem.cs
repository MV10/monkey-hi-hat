
namespace mhh;

public class HttpCacheItem
{
    /// <summary>
    /// Filename in the cache directory. Normally generated when HttpCacheManager writes
    /// the completed downloaded-in-memory stream to disk.
    /// </summary>
    public string LocalName { get; set; }

    /// <summary>
    /// Where the file was retrieved from (case-sensitive).
    /// </summary>
    public string SourceUrl { get; set; }
    
    /// <summary>
    /// When the file was retrieved.
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Size of the file.
    /// </summary>
    public long Bytes { get; set; }
    
    /// <summary>
    /// Produces a unique filename (unformatted GUID).
    /// </summary>
    public void GenerateLocalName()
        => LocalName = Guid.NewGuid().ToString("N");
}

