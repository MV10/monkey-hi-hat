
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
}
