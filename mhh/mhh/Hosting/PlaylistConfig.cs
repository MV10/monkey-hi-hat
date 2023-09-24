
using Microsoft.Extensions.Logging;

namespace mhh
{
    public class PlaylistConfig : IConfigSource
    {
        public ConfigFile ConfigSource { get; private set; }

        public readonly PlaylistOrder Order;
        public readonly int FavoritesPct;

        public readonly PlaylistSwitchModes SwitchMode;
        public readonly double SwitchSeconds;
        public readonly double SwitchCooldownSeconds;
        public readonly double MaxRunSeconds;

        public readonly int FXPercent;
        public readonly int FXDelaySeconds;
        public readonly IReadOnlyList<string> FX;

        /// <summary>
        /// Pre-sorted/randomized combination of Visualizations
        /// and Favorites entries. Call GeneratePlaylist to update
        /// (ie. randomize again at end of playlist).
        /// </summary>
        public readonly string[] Playlist;

        public readonly IReadOnlyList<string> Visualizations;
        public readonly IReadOnlyList<string> Favorites;

        private readonly Random RNG = new();

        public PlaylistConfig(string pathname)
        {
            ConfigSource = new ConfigFile(pathname);

            Order = ConfigSource.ReadValue("setup", "order").ToEnum(PlaylistOrder.RandomWeighted);
            FavoritesPct = ConfigSource.ReadValue("setup", "favoritespct").ToInt32(20);

            SwitchMode = ConfigSource.ReadValue("setup", "switch").ToEnum(PlaylistSwitchModes.Time);
            SwitchCooldownSeconds = ConfigSource.ReadValue("setup", "switchcooldownseconds").ToDouble(60d);
            MaxRunSeconds = ConfigSource.ReadValue("setup", "maxrunseconds").ToDouble(420d);
            SwitchSeconds = (SwitchMode == PlaylistSwitchModes.Time)
                ? ConfigSource.ReadValue("setup", "switchseconds").ToDouble(120d)
                : ConfigSource.ReadValue("setup", "switchseconds").ToDouble(0.5d);

            FavoritesPct = ConfigSource.ReadValue("setup", "favoritespct").ToInt32(20);

            FXPercent = ConfigSource.ReadValue("setup", "fxpercent").ToInt32(0);
            FXDelaySeconds = ConfigSource.ReadValue("setup", "fxdelayseconds").ToInt32(0);

            Visualizations = LoadNames("visualizations");
            Favorites = LoadNames("favorites");

            if (FXPercent > 0 && FXPercent < 101)
            {
                if (string.IsNullOrWhiteSpace(Program.AppConfig.FXPath))
                {
                    Warning("FX settings ignored, app configuration does not define FXPath.");
                    FXPercent = 0;
                }

                FX = LoadNames("fx");
                if (FX.Count == 0)
                {
                    FX = PathHelper.GetConfigFiles(Program.AppConfig.FXPath);
                    if (FX.Count == 0)
                    {
                        Warning("FX settings ignored, no .conf files in defined FXPath.");
                        FXPercent = 0;
                    }
                }
            }

            Playlist = new string[Math.Max(1, Visualizations.Count + Favorites.Count)];
            GeneratePlaylist();

            // TODO implement [Collections] section (reading from other playlists)

            if (FXPercent < 0 || FXPercent > 100 || FXDelaySeconds < 0)
            {
                Warning("FX settings are invalid and will be ignored.");
                FXPercent = 0;
            }

        }

        public void GeneratePlaylist()
        {
            int i = 0;
            switch (Order)
            {
                case PlaylistOrder.Sequential:
                    {
                        foreach (var viz in Favorites) Playlist[i++] = viz;
                        foreach (var viz in Visualizations) Playlist[i++] = viz;
                    }
                    break;

                case PlaylistOrder.Alternating:
                    {
                        int v = 0;
                        int f = 0;
                        while (v < Visualizations.Count || f < Favorites.Count)
                        {
                            if (v < Visualizations.Count) Playlist[i++] = Visualizations[v++];
                            if (f < Favorites.Count) Playlist[i++] = Favorites[f++];
                        }
                    }
                    break;

                case PlaylistOrder.Random:
                    {
                        var viz = new List<string>(Visualizations);
                        var faves = new List<string>(Favorites);
                        while(viz.Count > 0 || faves.Count > 0)
                        {
                            if(faves.Count == 0 || RNG.Next(101) < 50)
                            {
                                int v = RNG.Next(viz.Count);
                                Playlist[i++] = viz[v];
                                viz.RemoveAt(v);
                                if(viz.Count == 0)
                                {
                                    foreach (var x in faves) Playlist[i++] = x;
                                    faves.Clear();
                                }
                            }
                            else
                            {
                                int f = RNG.Next(faves.Count);
                                Playlist[i++] = faves[f];
                                faves.RemoveAt(f);
                                if(faves.Count == 0)
                                {
                                    foreach (var x in viz) Playlist[i++] = x;
                                    viz.Clear();
                                }
                            }
                        }
                    }
                    break;

                case PlaylistOrder.RandomWeighted:
                    {
                        // fully random with weightning, no pre-defined list
                    }
                    break;
            }
        }

        private void Warning(string message)
            => LogHelper.Logger?.LogWarning($"Playlist {ConfigSource.Pathname}: {message}");

        private IReadOnlyList<string> LoadNames(string section)
        {
            if (!ConfigSource.Content.ContainsKey(section) || ConfigSource.Content[section].Values.Count == 0) return new List<string>(1);

            var list = ConfigSource.Content[section].Values.ToList();

            for(int i = 0; i < list.Count; i++)
            {
                if (list[i].EndsWith(".conf", StringComparison.InvariantCultureIgnoreCase))
                {
                    list[i] = list[i].Substring(0, list[i].Length - 5);
                }
            }

            return list;
        }
    }
}
