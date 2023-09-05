
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

        public readonly List<string> AudioTextureUniformNames = new();

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
                foreach(var tex in ConfigSource.Content["audiotextures"])
                {
                    AudioTextureUniformNames.Add(tex.Value);
                }
            }
        }
    }
}
