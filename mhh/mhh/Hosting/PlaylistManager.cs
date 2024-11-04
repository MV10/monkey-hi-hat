
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Compression;

namespace mhh;

public class PlaylistManager
{
    public PlaylistConfig ActivePlaylist;
    public bool IsFXActive;

    private int PlaylistPointer = 0;
    private DateTime PlaylistAdvanceAt = DateTime.MaxValue;
    private DateTime PlaylistIgnoreSilenceUntil = DateTime.MinValue;
    private Random RNG = new();

    private string NextFXPathname = null;
    private DateTime FXStartTime = DateTime.MaxValue;
    private bool ForceStartFX;
    private int FXAddStartPercent;

    public string StartNewPlaylist(string playlistConfPathname)
    {
        ActivePlaylist = null;

        var cfg = new PlaylistConfig(playlistConfPathname);
        string err = null;
        if (cfg.Playlist.Length < 2) err = "Invalid playlist configuration file, one or zero visualizations loaded, aborted";
        if (cfg.Order == PlaylistOrder.RandomFavorites && cfg.Favorites.Count == 0) err = "RandomWeighted playlist requires Favorites visualizations, aborted";
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

        // This only immediately loads an FX if the playlist entry is written
        // that way (specifies a viz and the fx in the playlist file). The
        // NextFXPathname approach isn't used, the viz and fx pathnames are
        // both passed to the window Command_Load at the end of the function.

        NextFXPathname = null;
        FXStartTime = DateTime.MaxValue;
        IsFXActive = false;
        ForceStartFX = false;

        string fadeFile;
        string vizFile;
        string fxFile;
        if (ActivePlaylist.Order == PlaylistOrder.RandomFavorites)
        {
            do
            {
                if (RNG.Next(101) < 50 || RNG.Next(101) < ActivePlaylist.FavoritesPct)
                {
                    (fadeFile, vizFile, fxFile) = FilenameSplitter(ActivePlaylist.Favorites[RNG.Next(ActivePlaylist.Favorites.Count)]);
                }
                else
                {
                    (fadeFile, vizFile, fxFile) = FilenameSplitter(ActivePlaylist.Visualizations[RNG.Next(ActivePlaylist.Visualizations.Count)]);
                }
            } while (vizFile.Equals(Program.AppWindow.Renderer.ActiveRenderer?.Filename ?? string.Empty));
        }
        else
        {
            (fadeFile, vizFile, fxFile) = FilenameSplitter(ActivePlaylist.Playlist[PlaylistPointer++]);
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

        if(!string.IsNullOrEmpty(fadeFile))
        {
            if (!fadeFile.StartsWith("crossfade_", StringComparison.InvariantCultureIgnoreCase)) fadeFile = $"crossfade_{fadeFile}";
            var fadePathname = PathHelper.FindFile(Program.AppConfig.VisualizerPath, PathHelper.MakeFragFilename(fadeFile));
            if(!string.IsNullOrEmpty(fadePathname))
            {
                LogHelper.Logger?.LogTrace($"Playlist queuing crossfade {fadeFile}");
                Program.AppWindow.Command_QueueCrossfade(fadePathname);
            }
            else
            {
                LogHelper.Logger?.LogWarning($"Playlist crossfade not found: {fadeFile}");
            }
        }

        var vizPathname = PathHelper.FindConfigFile(Program.AppConfig.VisualizerPath, vizFile);
        if (vizPathname is null) return $"ERR: {vizFile} not found in visualizer path(s)";

        var fxPathname = string.Empty;
        if (!string.IsNullOrWhiteSpace(fxFile))
        {
            fxPathname = PathHelper.FindConfigFile(Program.AppConfig.FXPath, fxFile);
            if (fxPathname is null) return $"ERR: {fxFile} not found in FX path(s)";
        }

        LogHelper.Logger?.LogTrace($"Playlist queuing viz {Path.GetFileNameWithoutExtension(vizPathname)} with FX {fxPathname}");

        var msg = Program.AppWindow.Command_Load(vizPathname, fxPathname, terminatesPlaylist: false);
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

        FXAddStartPercent = visualizerConfig.FXAddStartPercent;

        if(NextFXPathname is null)
        {
            if (ActivePlaylist.FXPercent > 0 && RNG.Next(1, 101) <= ActivePlaylist.FXPercent) ChooseNextFX(visualizerConfig.ConfigSource);
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
                || (visualizerConfig.SwitchTimeHint == VizPlaylistTimeHint.DoubleFX && NextFXPathname is not null))
            {
                PlaylistAdvanceAt.AddSeconds(ActivePlaylist.SwitchSeconds);
            }
        }

        PlaylistAdvanceAt.AddSeconds(Program.AppConfig.CrossfadeSeconds);
    }

    public string ApplyFX()
    {
        if (ActivePlaylist is null) return "ERR: No playlist is active";
        if (IsFXActive) return "ERR: A post-processing FX is already active";
        if (ActivePlaylist.FX.Count == 0) return "ERR: FX disabled or no configurations were found";
        if (Program.AppWindow.Renderer.ActiveRenderer is CrossfadeRenderer) return "ERR: FX can't be applied during crossfade";

        if (NextFXPathname is null) ChooseNextFX(Program.AppWindow.Renderer.ActiveRenderer.ConfigSource);
        if (NextFXPathname is null) return "ERR: A selected FX config could not be found";

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
        if (ForceStartFX || DateTime.Now > FXStartTime && RNG.Next(1, 101) <= (50 + FXAddStartPercent))
        {
            FXStartTime = DateTime.MaxValue;
            IsFXActive = true;
            ForceStartFX = false;
            Program.AppWindow.Command_ApplyFX(NextFXPathname);
            NextFXPathname = null;
        }
    }

    public string GetInfo()
    {
        if (ActivePlaylist is null) return "(none)";
        return Path.GetFileNameWithoutExtension(ActivePlaylist.ConfigSource.Pathname).Replace("_", " ");
    }

    private void ChooseNextFX(ConfigFile visualizerConfig)
    {
        NextFXPathname = null;
        FXStartTime = DateTime.MaxValue;
        ForceStartFX = false;

        var fxList = ActivePlaylist.FX;
        if(visualizerConfig.Content.ContainsKey("fx-blacklist"))
        {
            var blacklist = visualizerConfig.Content["fx-blacklist"];
            if (fxList.Count == blacklist.Count) return;
            fxList = fxList.Where(fx => !blacklist.Any(bfx => bfx.Value.Equals(fx, Const.CompareFlags))).ToList();
            if (fxList.Count == 0) return;
        }

        NextFXPathname = PathHelper.FindConfigFile(Program.AppConfig.FXPath, fxList[RNG.Next(0, fxList.Count)]);
        if (NextFXPathname is null) return;

        FXStartTime = DateTime.Now.AddSeconds(ActivePlaylist.FXDelaySeconds + Program.AppConfig.CrossfadeSeconds);
        ForceStartFX = (RNG.Next(1, 101) <= ActivePlaylist.InstantFXPercent);
    }

    private (string fade, string viz, string fx) FilenameSplitter(string entry)
    {
        var items = entry.Split(' ', Const.SplitOptions);

        // viz only
        if (items.Length == 1) return (string.Empty, items[0], string.Empty);

        // first item is crossfade?
        var fade = (items[0].StartsWith(">")) ? items[0].Substring(1) : string.Empty;

        // crossfade, viz, fx
        if (items.Length == 3) return (fade, items[1], items[2]);

        // length 2 = viz, fx
        if (string.IsNullOrEmpty(fade)) return (string.Empty, items[0], items[1]);

        // length 2 = crossfade, viz
        return (fade, items[1], string.Empty);
    }
}
