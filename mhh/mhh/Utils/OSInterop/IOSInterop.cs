
namespace mhh;

public interface IOSInterop : IDisposable
{
    /// <summary>
    /// Indicates whether the app's console window is currently visible.
    /// Under Windows Terminal this may only reflect minimized state until
    /// Microsoft fixes Terminal's ShowWindow support.
    /// </summary>
    public bool IsConsoleVisible { get; set; }

    /// <summary>
    /// Writes audio device information to the console. Called by Program class.
    /// </summary>
    public void ListAudioDevices();
    
    /// <summary>
    /// Current media track formatted for rendering (ie. with music-note symbol).
    /// On Windows this will be two lines (Spotify artist and track).
    /// On Linux this will be up to three lines (artist, album, and track).
    /// </summary>
    public string GetMediaTrackForDisplay { get; }
    
    /// <summary>
    /// Checks to see if the media has changed (Spotify track on Windows,
    /// or the DBUS media metadata on Linux).
    /// </summary>
    public void UpdateMediaTrackInfo();

    /// <summary>
    /// Interop objects are created and used before logging is ready for use,
    /// so this is called when LogHelper.CreateLogger can be invoked.
    /// </summary>
    public void CreateLogger();
}
