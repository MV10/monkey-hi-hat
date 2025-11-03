namespace mhh;

/// <summary>
/// OS-specific feature support.
/// </summary>
public interface IOSInterop
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
    /// Current media track formatted for rendering (ie. with music-note symbol)
    /// </summary>
    public string GetMediaTrackForDisplay { get; }
    
    /// <summary>
    /// Checks to see if the media has changed (Spotify track on Windows,
    /// or the DBUS media metadata on Linux).
    /// </summary>
    public void UpdateMediaTrackInfo();
}
