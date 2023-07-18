
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

        public ConfigFile(string confPathname)
        {
            Pathname = Path.GetFullPath(confPathname);
            if (!Pathname.EndsWith(".conf", StringComparison.InvariantCultureIgnoreCase)) Pathname += ".conf";
            if (!File.Exists(Pathname)) return;

            var section = string.Empty;
            foreach (var line in File.ReadAllLines(Pathname))
            {
                if (!string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#"))
                {
                    var text = (line.Contains("#")) ? line.Split("#")[0].Trim() : line.Trim();
                    if (text.StartsWith("[") && text.EndsWith("]"))
                    {
                        section = text.Substring(1, text.Length - 2).ToLowerInvariant();
                    }
                    else
                    {
                        var kvp = (text + " ").Split("=", 2, StringSplitOptions.TrimEntries);
                        if (kvp.Length == 2 && !string.IsNullOrWhiteSpace(section))
                        {
                            if (!Content.ContainsKey(section)) Content.Add(section, new());
                            Content[section].Add(kvp[0].ToLowerInvariant(), kvp[1]);
                        }
                    }
                }
            }
        }
    }
}
