
namespace mhh;

/// <summary>
/// Represents a resource being downloaded.
/// </summary>
public class FileCacheRequest
{
    /// <summary>
    /// If this is zero, it is assumed this request is from a command-line
    /// --filecache command, and FileCacheManager will assign a unique
    /// negative number (since this is a key value for queued download 
    /// requests, and the command-line callback is a NOP).
    /// </summary>
    public int TextureHandle;

    public Action<int, FileCacheData> Callback;
    
    public CancellationTokenSource CTS = new();
    
    public bool ReplacingExpiredFile = false;

    public FileCacheData Data;
}
