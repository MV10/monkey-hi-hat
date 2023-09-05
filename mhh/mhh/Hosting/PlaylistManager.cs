
using mhh.Utils;
using Microsoft.Extensions.Logging;

namespace mhh;

public class PlaylistManager
{
    public PlaylistConfig ActivePlaylist;

    private int PlaylistPointer = 0;
    private DateTime PlaylistAdvanceAt = DateTime.MaxValue;
    private DateTime PlaylistIgnoreSilenceUntil = DateTime.MinValue;
    private Random rnd = new();

    public string StartNewPlaylist(string playlistConfPathname)
    {
        ActivePlaylist = null;

        var cfg = new PlaylistConfig(playlistConfPathname);
        string err = null;
        if (cfg.Playlist.Length < 2) err = "Invalid playlist configuration file, one or zero visualizations loaded, aborted";
        if (cfg.Order == PlaylistOrder.RandomWeighted && cfg.Favorites.Count == 0) err = "RandomWeighted playlist requires Favorites visualizations, aborted";
        if (err is not null)
        {
            LogHelper.Logger?.LogError(err);
            return $"ERR: {err}";
        }

        PlaylistPointer = 0;
        ActivePlaylist = cfg;
        return NextVisualization();
    }

    public void TerminatePlaylist()
    {
        ActivePlaylist = null;
    }

    public string NextVisualization(bool temporarilyIgnoreSilence = false)
    {
        if (ActivePlaylist is null) return "ERR: No playlist is active";

        string filename;
        if (ActivePlaylist.Order == PlaylistOrder.RandomWeighted)
        {
            do
            {
                if (rnd.Next(100) < 50 || rnd.Next(100) < ActivePlaylist.FavoritesPct)
                {
                    filename = ActivePlaylist.Favorites[rnd.Next(ActivePlaylist.Favorites.Count)];
                }
                else
                {
                    filename = ActivePlaylist.Visualizations[rnd.Next(ActivePlaylist.Visualizations.Count)];
                }
            } while (filename.Equals(Program.AppWindow.Renderer.ActiveRenderer?.Filename ?? string.Empty));
        }
        else
        {
            filename = ActivePlaylist.Playlist[PlaylistPointer++];
            if (PlaylistPointer == ActivePlaylist.Playlist.Length)
            {
                PlaylistPointer = 0;
                ActivePlaylist.GeneratePlaylist();
            }
        }

        PlaylistAdvanceAt = (ActivePlaylist.SwitchMode == PlaylistSwitchModes.Time)
            ? DateTime.Now.AddSeconds(ActivePlaylist.SwitchSeconds)
            : (ActivePlaylist.SwitchMode == PlaylistSwitchModes.Silence)
                ? DateTime.Now.AddSeconds(ActivePlaylist.MaxRunSeconds)
                : DateTime.MaxValue;

        PlaylistIgnoreSilenceUntil = (temporarilyIgnoreSilence && ActivePlaylist.SwitchMode == PlaylistSwitchModes.Silence)
            ? DateTime.Now.AddSeconds(ActivePlaylist.SwitchCooldownSeconds)
            : DateTime.MinValue;

        var pathname = PathHelper.FindConfigFile(Program.AppConfig.ShaderPath, filename);
        if (pathname is not null)
        {
            var msg = Program.AppWindow.Command_Load(pathname, terminatesPlaylist: false);
            // TODO handle ERR message
            return msg;
        }

        return $"ERR - {filename} not found in shader path(s)";
    }

    public void UpdateFrame(double silenceDuration)
    {
        if (
            // short-duration silence for playlist track-change viz-advancement
            (ActivePlaylist?.SwitchMode == PlaylistSwitchModes.Silence 
            && DateTime.Now >= PlaylistIgnoreSilenceUntil 
            && silenceDuration >= ActivePlaylist.SwitchSeconds)

            // playlist viz-advancement by time
            || DateTime.Now >= PlaylistAdvanceAt
            )
            Program.AppWindow.Command_PlaylistNext(temporarilyIgnoreSilence: true);
    }

    public string GetInfo()
        => (ActivePlaylist is null)
            ? "(none)"
            : ActivePlaylist.ConfigSource.Pathname;
}
