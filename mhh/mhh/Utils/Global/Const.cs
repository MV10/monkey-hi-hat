
namespace mhh;

public static class Const
{
    /// <summary>
    /// Combines the TrimEntries and RemoveEmptyEntries flags
    /// </summary>
    public static readonly StringSplitOptions SplitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

    /// <summary>
    /// Shorter reference to StringComparison.InvariantCultureIgnoreCase
    /// </summary>
    public static readonly StringComparison CompareFlags = StringComparison.InvariantCultureIgnoreCase;
    
    /// <summary>
    /// Placeholder text when the media track info can't be retrieved
    /// </summary>
    public static readonly string MediaTrackUnavailable = "Media information is not available";
    
    /// <summary>
    /// Music-note symbol in the standard Shadertoy font texture
    /// </summary>
    public static readonly char MusicNote = (char)11;

    /// <summary>
    /// Linux DBus location of MPRIS MediaPlayer2 services.
    /// </summary>
    public static readonly string DBusMediaPlayer2Path = "/org/mpris/MediaPlayer2";

    /// <summary>
    /// Linux DBus MPRIS2 MediaPlayer2 service name prefix.
    /// </summary>
    public static readonly string DBusMediaPlayer2Prefix = "org.mpris.MediaPlayer2.";

    /// <summary>
    /// Linux DBus MPRIS2 MediaPlayer2 metadata keys.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DBusMediaPlayer2MetadataKeys = new Dictionary<string, string>()
    {
        {"title", "xesam:title"},
        {"album", "xesam:album"},
        {"artist", "xesam:artist"},
    };
}
