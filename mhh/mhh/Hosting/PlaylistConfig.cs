
namespace mhh
{
    public class PlaylistConfig
    {
        public readonly ConfigFile ConfigSource;

        public readonly PlaylistOrder Order;
        public readonly int FavoritesPct;

        public readonly PlaylistSwitchModes SwitchMode;
        public readonly double SwitchSeconds;
        public readonly double SwitchCooldownSeconds;
        public readonly double MaxRunSeconds;

        /// <summary>
        /// Pre-sorted/randomized combination of Visualizations
        /// and Favorites entries. Call GeneratePlaylist to update
        /// (ie. randomize again at end of playlist).
        /// </summary>
        public readonly string[] Playlist;

        public readonly List<string> Visualizations;
        public readonly List<string> Favorites;

        private readonly Random rand = new();
        
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

            Visualizations = LoadNames("visualizations");
            Favorites = LoadNames("favorites");

            Playlist = new string[Math.Max(1, Visualizations.Count + Favorites.Count)];
            GeneratePlaylist();

            // TODO implement [Collections] section (reading from other playlists)
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
                            if(faves.Count == 0 || rand.Next(100) < 50)
                            {
                                int v = rand.Next(viz.Count);
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
                                int f = rand.Next(faves.Count);
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

        private List<string> LoadNames(string section)
        {
            if (!ConfigSource.Content.ContainsKey(section) || ConfigSource.Content[section].Values.Count == 0) return new(1);

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
