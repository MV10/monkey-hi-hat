
namespace mhh.Hosting
{
    public class PlaylistConfig
    {
        public readonly ConfigFile Config;

        public readonly PlaylistOrder Order;
        public readonly int FavoritesPct;

        public readonly PlaylistSwitchModes SwitchMode;
        public readonly double SwitchSeconds;
        public readonly double SwitchCooldownSeconds;

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
            Config = new ConfigFile(pathname);

            Order = Config.ReadValue("setup", "order").ToEnum(PlaylistOrder.RandomWeighted);
            FavoritesPct = Config.ReadValue("setup", "favoritespct").ToInt32(20);

            SwitchMode = Config.ReadValue("setup", "switch").ToEnum(PlaylistSwitchModes.Time);
            SwitchCooldownSeconds = Config.ReadValue("setup", "switchcooldownseconds").ToDouble(60d);
            SwitchSeconds = (SwitchMode == PlaylistSwitchModes.Time)
                ? Config.ReadValue("setup", "switchseconds").ToDouble(120d)
                : Config.ReadValue("setup", "switchseconds").ToDouble(0.5d);

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
                            if(rand.Next(100) < 50)
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
            if (!Config.Content.ContainsKey(section) || Config.Content[section].Values.Count == 0) return new(1);
            return Config.Content[section].Values.ToList();
        }
    }
}
