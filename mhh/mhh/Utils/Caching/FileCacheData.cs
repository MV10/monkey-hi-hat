
namespace mhh;

/// <summary>
/// Details of a cached texture file (image or video).
/// </summary>
public class FileCacheData
{
    /// <summary>
    /// This is the filename of the data stored in the cache directory.
    /// </summary>
    public string GuidFilename { get; set; }

    /// <summary>
    /// Where the file was retrieved. Case-sensitivity is controlled
    /// by the FileCacheCaseSensitive setting in the app configuration.
    /// </summary>
    public string OriginURI { get; set; }

    /// <summary>
    /// The size of the file in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// The ContentType reported from where the file was retrieved.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// When the file was fetched from the OriginURL.
    /// </summary>
    public DateTime RetrievalTimestamp { get; set; }

    /// <summary>
    /// Returns the filename portion of the OriginURI for logging / debugging.
    /// </summary>
    public string GetFilename()
    {
        var parsed = new Uri(OriginURI);
        if (parsed.IsFile) return Path.GetFileName(parsed.AbsolutePath);
        return null;
    }

    /// <summary>
    /// Returns the physical location of the cached file.
    /// </summary>
    public string GetPathname() 
        => PathHelper.FindFile(Program.AppConfig.TexturePath, GuidFilename);
}
