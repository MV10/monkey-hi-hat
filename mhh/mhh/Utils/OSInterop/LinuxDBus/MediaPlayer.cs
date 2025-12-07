
using Mpris.DBus;
using Tmds.DBus.Protocol;

namespace mhh;

/// <summary>
/// A wrapper exposing MPRIS.DBus functionality needed by MHH.
/// </summary>
internal class MediaPlayer
{
    private readonly MprisService Service;
    private readonly MprisPlayer Player;

    private Dictionary<string, VariantValue> Metadata;

    public MediaPlayer(MprisService service)
    {
        Service = service;
        Player = Service.CreatePlayer(Const.DBusMediaPlayer2Path);
    }

    /// <summary>
    /// Returns a dictionary of the standard MPRIS metadata values. 
    /// </summary>
    public async Task<Dictionary<string, VariantValue>> GetMetadata()
    {
        Metadata = await Player.GetMetadataAsync().ConfigureAwait(false);
        return Metadata;
    }

    /// <summary>
    /// Retrieves an MPRIS metadata value with optional refresh. If the return
    /// data is a string array (as is the standard for artist data), the first
    /// string value is returned. If the key is not present or the data type
    /// is not a string or string array, the return is an empty string. 
    /// </summary>
    public async Task<string> GetMetadata(string key, bool refresh = false)
    {
        if (refresh || Metadata is null || Metadata.Count == 0 || !Metadata.ContainsKey(key)) await GetMetadata().ConfigureAwait(false);
        if (!Metadata.TryGetValue(key, out var value)) return string.Empty;
        if (value.Type == VariantValueType.Array && value.ItemType == VariantValueType.String)
        {
            var a = value.GetArray<string>();
            if (a.Length == 0) return string.Empty;
            return a[0];
        }
        if (value.Type == VariantValueType.String) return value.GetString();
        return string.Empty;
    }

    /// <summary>
    /// Retrieves title, album, and artist information from the player (if available).
    /// Any missing data will return an empty string. 
    /// </summary>
    public async Task<(string title, string album, string artist)> GetPlaybackDetails(bool refresh = false)
    {
        var title = await GetMetadata(Const.DBusMediaPlayer2MetadataKeys["title"], refresh);
        var album = await GetMetadata(Const.DBusMediaPlayer2MetadataKeys["album"]);
        var artist = await GetMetadata(Const.DBusMediaPlayer2MetadataKeys["artist"]);
        return (title, album, artist);
    }
    
    /// <summary>
    /// Returns Playing, Paused, or Stopped (the MPRIS standard does
    /// not permit any other values). 
    /// </summary>
    public async Task<string> GetPlaybackStatus()
        => await Player.GetPlaybackStatusAsync().ConfigureAwait(false);
}
