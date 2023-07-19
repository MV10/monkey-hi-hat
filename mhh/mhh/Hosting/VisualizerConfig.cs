
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
        public readonly ConfigFile Config;

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
            Config = new ConfigFile(pathname);

            Description = Config.ReadValue("shader", "description");

            var configFileLocation = Path.GetDirectoryName(Config.Pathname);
            VertexShaderPathname = Path.Combine(configFileLocation, Config.ReadValue("shader", "vertexshaderfilename"));
            FragmentShaderPathname = Path.Combine(configFileLocation, Config.ReadValue("shader", "fragmentshaderfilename"));

            var rgb = Config.ReadValue("shader", "backgroundfloatrgb").Split(",");
            if(rgb.Length == 3)
            {
                BackgroundColor = new(rgb[0].ToFloat(0), rgb[1].ToFloat(0), rgb[2].ToFloat(0), 1f);
            }
            else
            {
                BackgroundColor = new(0f, 0f, 0f, 1f);
            }

            VisualizerTypeName = Config.ReadValue("shader", "visualizertypename");

            if(Config.Content.ContainsKey("audiotextures"))
            {
                // Each entry is "unit#=uniform TypeName"
                foreach(var tex in Config.Content["audiotextures"])
                {
                    var unit = tex.Key.ToInt32(-1);
                    var definition = tex.Value.Split(' ');
                    if(unit >-1 && unit <32 && definition.Length == 2 && !AudioTextureTypeNames.ContainsKey(unit))
                    {
                        AudioTextureUniformNames.Add(unit, definition[0]);
                        AudioTextureTypeNames.Add(unit, definition[1]);
                        AudioTextureMultipliers.Add(unit, 1.0f);
                    }
                }
            }

            if(Config.Content.ContainsKey("audiotexturemultipliers"))
            {
                foreach (var tex in Config.Content["audiotexturemultipliers"])
                {
                    var unit = tex.Key.ToInt32(-1);
                    var mult = tex.Value.ToFloat(float.MinValue);
                    if (mult > float.MinValue && AudioTextureMultipliers.ContainsKey(unit))
                    {
                        AudioTextureMultipliers[unit] = mult;
                    }
                }
            }
        }
    }
}
