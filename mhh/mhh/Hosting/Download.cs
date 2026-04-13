
using Downloader;
using StbImageSharp;

namespace mhh;

/// <summary>
/// Represents an in-flight download.
/// </summary>
public class Download
{
    /// <summary>
    /// Where the image file comes from.
    /// </summary>
    public string SourceUrl { get; set; }
    
    /// <summary>
    /// Where the image buffer should be loaded upon completion. This must be set from
    /// the main thread (polling will check the Completed property and Image validity).
    /// </summary>
    public GLImageTexture Texture { get; set; }
    
    /// <summary>
    /// The Downloader object that manages retrieval.
    /// </summary>
    public IDownload DownloadJob { get; set; }
    
    /// <summary>
    /// The end result, if successful, or null if unsuccessful.
    /// HttpCacheMaxDimension will be applied (resizing).
    /// </summary>
    public ImageResult Image { get; set; } = null;
}

