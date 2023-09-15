
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
        public readonly int RenderResolutionLimit;

        public readonly string VisualizerTypeName;

        public readonly List<string> AudioTextureUniformNames = new();

        public VisualizerConfig(string pathname)
        {
            ConfigSource = new ConfigFile(pathname);

            Description = ConfigSource.ReadValue("shader", "description");

            var configFileLocation = Path.GetDirectoryName(ConfigSource.Pathname);
            VertexShaderPathname = Path.Combine(configFileLocation, ConfigSource.ReadValue("shader", "vertexshaderfilename"));
            FragmentShaderPathname = Path.Combine(configFileLocation, ConfigSource.ReadValue("shader", "fragmentshaderfilename"));

            var rgb = ConfigSource.ReadValue("shader", "backgroundfloatrgb").Split(",", Const.SplitOptions);
            if(rgb.Length == 3)
            {
                BackgroundColor = new(rgb[0].ToFloat(0), rgb[1].ToFloat(0), rgb[2].ToFloat(0), 1f);
            }
            else
            {
                BackgroundColor = new(0f, 0f, 0f, 1f);
            }

            RenderResolutionLimit = ConfigSource.ReadValue("shader", "renderresolutionlimit").ToInt32(0);

            VisualizerTypeName = ConfigSource.ReadValue("shader", "visualizertypename");

            if(ConfigSource.Content.ContainsKey("audiotextures"))
            {
                foreach(var tex in ConfigSource.Content["audiotextures"])
                {
                    AudioTextureUniformNames.Add(tex.Value);
                }
            }

            // shader pathnames and visualizer type names are validated in RenderingHelper
            var err = $"Error in {ConfigSource.Pathname}: ";
            if (RenderResolutionLimit < 256 && RenderResolutionLimit != 0) throw new ArgumentException($"{err} RenderResolutionLimit must be 256 or greater (default is 0 to disable)");
        }
    }
}
