
using Microsoft.Extensions.Logging;

namespace mhh
{
    /// <summary>
    /// Container for MHH "conf" configuration files. All MHH conf files consist of
    /// [section] headers followed by key=value pairs. Blank lines and hashtag (#) prefixed
    /// content is discarded. Invalid content is discarded (key=value pairs before a [section]
    /// heading, lines not in the key=value format, or duplicate keys within a section). All
    /// [section] and key values are forced to lowercase for case-insensitive matching. Values
    /// retain their casing (important for things like Type-name-matching).
    /// </summary>
    public class ConfigFile
    {
        /// <summary>
        /// The source of the configuration data. If a relative pathname is provided
        /// to the constructor, this stores the fully-qualified equivalent.
        /// </summary>
        public readonly string Pathname = string.Empty;

        /// <summary>
        /// Contents of the configuration data excluding blank lines and comments.
        /// The nested dictionaries reflect the conf file as <section, <key, value>>.
        /// </summary>
        public readonly Dictionary<string, Dictionary<string, string>> Content = new();

        private Random RNG = new();

        public ConfigFile(string confPathname)
        {
            Pathname = Path.GetFullPath(confPathname);
            if (!Pathname.EndsWith(".conf", StringComparison.InvariantCultureIgnoreCase)) Pathname += ".conf";
            if (!File.Exists(Pathname))
            {
                var err = $"Configuration file not found: {Pathname}";
                if (LogHelper.Logger is null)
                {
                    Console.WriteLine(err);
                }
                else
                {
                    LogHelper.Logger.LogError(err);
                }
                return;
            }

            LogHelper.Logger?.LogTrace($"ConfigFile: {confPathname}");

            var section = string.Empty;
            int sectionID = 0;
            foreach (var line in File.ReadAllLines(Pathname))
            {
                if (!string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#"))
                {
                    var text = (line.Contains("#")) ? line.Split("#", Const.SplitOptions)[0].Trim() : line.Trim();
                    if (text.StartsWith("[") && text.EndsWith("]"))
                    {
                        section = text.Substring(1, text.Length - 2).ToLowerInvariant();
                        sectionID = 0;
                    }
                    else
                    {
                        if(!string.IsNullOrWhiteSpace(section))
                        {
                            var kvp = (text + " ").Split("=", 2, Const.SplitOptions);
                            if(kvp.Length > 0)
                            {
                                if (!Content.ContainsKey(section)) Content.Add(section, new());
                                var key = (kvp.Length == 2) ? kvp[0] : (sectionID++).ToString();
                                var value = (kvp.Length == 2) ? kvp[1] : kvp[0];
                                Content[section].Add(key.ToLowerInvariant(), value);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Helper function for .conf files that support a [uniforms] section.
        /// </summary>
        public Dictionary<string, float> ParseUniforms()
        {
            var uniforms = new Dictionary<string, float>();

            if (Content.ContainsKey("uniforms"))
            {
                foreach (var u in Content["uniforms"])
                {
                    if (uniforms.ContainsKey(u.Key)) continue;

                    var range = u.Value.Split(':', Const.SplitOptions);
                    if (range.Length == 1)
                    {
                        if (float.TryParse(range[0], out var f))
                        {
                            uniforms.Add(u.Key, f);
                        }
                    }
                    else
                    {
                        if (float.TryParse(range[0], out var f0) && float.TryParse(range[1], out var f1))
                        {
                            var hi = Math.Max(f0, f1);
                            var lo = Math.Min(f0, f1);
                            var span = hi - lo;
                            float val = (float)RNG.NextDouble() * span + lo;
                            uniforms.Add(u.Key, val);
                        }
                    }

                    if (!uniforms.ContainsKey(u.Key)) uniforms.Add(u.Key, 0f);
                }
            }

            return uniforms;
        }
    }
}
