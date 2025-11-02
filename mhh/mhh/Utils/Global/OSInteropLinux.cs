namespace mhh;

/// <inheritdocs/>
public class OSInteropLinux :IOSInterop
{
    // TODO implement Linux terminal visibility control
    /// <inheritdocs/>
    public bool IsConsoleVisible { get; set; } = true;

    /// <inheritdocs/>
    public string GetMediaTrackForDisplay
    {
        get => Const.MediaTrackUnavailable;
    }

    /// <inheritdocs/>
    public void UpdateMediaTrackInfo()
    {
        // TODO read DBUS media metadata
    }
}