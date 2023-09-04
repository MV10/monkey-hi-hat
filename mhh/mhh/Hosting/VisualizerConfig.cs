
using OpenTK.Mathematics;

namespace mhh
{
    /// <summary>
    /// Represents the generic visualizer configuration details read from the
    /// conf file. Does not resolve the targeted visualizer type (and those
    /// implementations are responsible for reading their own extra settings
    /// from the ConfigFile data stored here).
    /// </summary>
    public class VisualizerConfig
    {
        public readonly ConfigFile ConfigSource;

        public readonly string Description;
        public readonly string VertexShaderPathname;
        public readonly string FragmentShaderPathname;

        public readonly Color4 BackgroundColor;

        public readonly string VisualizerTypeName;

        public readonly Dictionary<int, string> AudioTextureUniformNames = new();
        public readonly Dictionary<int, string> AudioTextureTypeNames = new();
        public readonly Dictionary<int, float> AudioTextureMultipliers = new();

        public VisualizerConfig(string pathname)
        {
            ConfigSource = new ConfigFile(pathname);

            Description = ConfigSource.ReadValue("shader", "description");

            var configFileLocation = Path.GetDirectoryName(ConfigSource.Pathname);
            VertexShaderPathname = Path.Combine(configFileLocation, ConfigSource.ReadValue("shader", "vertexshaderfilename"));
            FragmentShaderPathname = Path.Combine(configFileLocation, ConfigSource.ReadValue("shader", "fragmentshaderfilename"));

            var rgb = ConfigSource.ReadValue("shader", "backgroundfloatrgb").Split(",");
            if(rgb.Length == 3)
            {
                BackgroundColor = new(rgb[0].ToFloat(0), rgb[1].ToFloat(0), rgb[2].ToFloat(0), 1f);
            }
            else
            {
                BackgroundColor = new(0f, 0f, 0f, 1f);
            }

            VisualizerTypeName = ConfigSource.ReadValue("shader", "visualizertypename");

            if(ConfigSource.Content.ContainsKey("audiotextures"))
            {
                // Each entry is "unit#=uniform TypeName"
                foreach(var tex in ConfigSource.Content["audiotextures"])
                {
                    var id = tex.Key.ToInt32(-1);
                    var definition = tex.Value.Split(' ');
                    if(id >-1 && id <32 && definition.Length == 2 && !AudioTextureTypeNames.ContainsKey(id))
                    {
                        AudioTextureUniformNames.Add(id, definition[0]);
                        AudioTextureTypeNames.Add(id, definition[1]);
                        AudioTextureMultipliers.Add(id, 1.0f);
                    }
                }
            }

            if(ConfigSource.Content.ContainsKey("audiotexturemultipliers"))
            {
                foreach (var tex in ConfigSource.Content["audiotexturemultipliers"])
                {
                    var id = tex.Key.ToInt32(-1);
                    var mult = tex.Value.ToFloat(float.MinValue);
                    if (mult > float.MinValue && AudioTextureMultipliers.ContainsKey(id))
                    {
                        AudioTextureMultipliers[id] = mult;
                    }
                }
            }
        }
    }
}
