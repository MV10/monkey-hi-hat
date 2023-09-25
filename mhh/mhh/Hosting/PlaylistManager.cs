
using Microsoft.Extensions.Logging;

namespace mhh;

public class PlaylistManager
{
    public PlaylistConfig ActivePlaylist;
    public bool IsFXActive;

    private int PlaylistPointer = 0;
    private DateTime PlaylistAdvanceAt = DateTime.MaxValue;
    private DateTime PlaylistIgnoreSilenceUntil = DateTime.MinValue;
    private Random RNG = new();

    private string NextFXConfig = null;
    private DateTime FXStartTime = DateTime.MaxValue;
    private bool ForceStartFX;

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

        NextFXConfig = null;
        FXStartTime = DateTime.MaxValue;
        IsFXActive = false;
        ForceStartFX = false;

        string filename;
        if (ActivePlaylist.Order == PlaylistOrder.RandomWeighted)
        {
            do
            {
                if (RNG.Next(101) < 50 || RNG.Next(101) < ActivePlaylist.FavoritesPct)
                {
                    filename = ActivePlaylist.Favorites[RNG.Next(ActivePlaylist.Favorites.Count)];
                }
                else
                {
                    filename = ActivePlaylist.Visualizations[RNG.Next(ActivePlaylist.Visualizations.Count)];
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

        var pathname = PathHelper.FindConfigFile(Program.AppConfig.VisualizerPath, filename);
        if (pathname is null) return $"ERR: {filename} not found in shader path(s)";

        if (ActivePlaylist.FXPercent > 0 && RNG.Next(1, 101) <= ActivePlaylist.FXPercent) ChooseNextFX();

        LogHelper.Logger?.LogTrace($" Playlist queuing viz {Path.GetFileNameWithoutExtension(pathname)} with FX {NextFXConfig ?? "(none)"}");

        var msg = Program.AppWindow.Command_Load(pathname, terminatesPlaylist: false);
        return msg;
    }

    public void StartingNextVisualization(VisualizerConfig visualizerConfig)
    {
        // RenderManager calls this (whether a playlist is running or not) when a
        // new visualizer is prepared for loading. Visualizer playlist settings can
        // be applied here (in StartNewPlaylist, only the visualizer filename was
        // known, the config was not loaded yet; similarly only a pending FX name
        // is known here, if any; the FX config hasn't been loaded yet).

        if (ActivePlaylist is null) return;

        if(NextFXConfig is not null)
        {
            var fxFilename = Path.GetFileNameWithoutExtension(NextFXConfig);
            if(visualizerConfig.FXBlacklist.Any(f => f.Equals(fxFilename, StringComparison.InvariantCultureIgnoreCase)))
            {
                NextFXConfig = null;
                FXStartTime = DateTime.MaxValue;
            }
        }

        if(ActivePlaylist.SwitchMode == PlaylistSwitchModes.Time && visualizerConfig.SwitchTimeHint != VizPlaylistTimeHint.None)
        {
            if (visualizerConfig.SwitchTimeHint == VizPlaylistTimeHint.Half)
            {
                var seconds = PlaylistAdvanceAt.Subtract(DateTime.Now).TotalSeconds;
                seconds = Math.Min(5, seconds * 0.5);
                PlaylistAdvanceAt = DateTime.Now.AddSeconds(seconds);
            }

            if(visualizerConfig.SwitchTimeHint == VizPlaylistTimeHint.Double
                || (visualizerConfig.SwitchTimeHint == VizPlaylistTimeHint.DoubleFX && NextFXConfig is not null))
            {
                PlaylistAdvanceAt.AddSeconds(ActivePlaylist.SwitchSeconds);
            }
        }
    }

    public string ApplyFX()
    {
        if (ActivePlaylist is null) return "ERR: No playlist is active";
        if (IsFXActive) return "ERR: A post-processing FX is already active";
        if (ActivePlaylist.FX.Count == 0) return "ERR: FX disabled or no configurations were found";

        if (NextFXConfig is null) ChooseNextFX();
        if (NextFXConfig is null) return "ERR: A selected FX config could not be found";

        ForceStartFX = true;

        return "ACK";
    }

    public void UpdateFrame(double silenceDuration)
    {
        if (ActivePlaylist is null || Program.AppWindow.Renderer.TimePaused) return;

        // advance to next visualization?
        if (
            // short-duration silence for playlist track-change viz-advancement
            (ActivePlaylist?.SwitchMode == PlaylistSwitchModes.Silence
            && DateTime.Now >= PlaylistIgnoreSilenceUntil
            && silenceDuration >= ActivePlaylist.SwitchSeconds)

            // playlist viz-advancement by time
            || DateTime.Now >= PlaylistAdvanceAt
            )
        {
            Program.AppWindow.Command_PlaylistNext(temporarilyIgnoreSilence: true);
            return;
        }

        // apply FX?
        if (ForceStartFX || DateTime.Now > FXStartTime && RNG.Next(1, 101) > 50)
        {
            FXStartTime = DateTime.MaxValue;
            IsFXActive = true;
            ForceStartFX = false;
            Program.AppWindow.Command_ApplyFX(NextFXConfig);
            NextFXConfig = null;
        }
    }

    public string GetInfo()
        => ActivePlaylist?.ConfigSource.Pathname ?? "(none)";

    private void ChooseNextFX()
    {
        NextFXConfig = PathHelper.FindConfigFile(Program.AppConfig.FXPath, ActivePlaylist.FX[RNG.Next(0, ActivePlaylist.FX.Count)]);
        if (NextFXConfig is not null) FXStartTime = DateTime.Now.AddSeconds(ActivePlaylist.FXDelaySeconds + Program.AppConfig.CrossfadeSeconds);
    }
}
