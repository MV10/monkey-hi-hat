
using Microsoft.Extensions.Logging;
using OpenTK.Mathematics;

namespace mhh
{
    /// <summary>
    /// Represents the generic visualizer configuration details read from the
    /// conf file. Does not resolve the targeted visualizer type (and those
    /// implementations are responsible for reading their own extra settings
    /// from the ConfigFile data stored here).
    /// </summary>
    public class VisualizerConfig : IConfigSource
    {
        public ConfigFile ConfigSource { get; private set; }

        public readonly string Description;
        public readonly string VertexShaderPathname;
        public readonly string FragmentShaderPathname;

        public readonly Color4 BackgroundColor;
        public readonly int RenderResolutionLimit;
        public readonly int RandomTimeOffset;

        public readonly string VertexSourceTypeName;

        public readonly List<string> AudioTextureUniformNames = new();

        public readonly VizPlaylistTimeHint SwitchTimeHint;
        public readonly int FXAddStartPercent;
        public readonly List<string> FXBlacklist = new();

        public readonly Dictionary<string, float> Uniforms;

        public VisualizerConfig(string pathname)
        {
            ConfigSource = new ConfigFile(pathname);

            Description = ConfigSource.ReadValue("shader", "description");

            var configFileLocation = Path.GetDirectoryName(ConfigSource.Pathname);

            var shader = ConfigSource.ReadValue("shader", "vertexshaderfilename").DefaultString("*");
            VertexShaderPathname = shader.Equals("*")
                ? Path.Combine(ApplicationConfiguration.InternalShaderPath, "passthrough.vert")
                : Path.Combine(configFileLocation, shader);

            shader = ConfigSource.ReadValue("shader", "fragmentshaderfilename").DefaultString("*");
            FragmentShaderPathname = shader.Equals("*")
                ? Path.Combine(ApplicationConfiguration.InternalShaderPath, "passthrough.frag")
                : Path.Combine(configFileLocation, shader);

            var rgb = ConfigSource.ReadValue("shader", "backgroundfloatrgb").Split(",", Const.SplitOptions);
            if(rgb.Length == 3)
            {
                BackgroundColor = new(rgb[0].ToFloat(0), rgb[1].ToFloat(0), rgb[2].ToFloat(0), 1f);
            }
            else
            {
                BackgroundColor = new(0f, 0f, 0f, 1f);
            }

            RenderResolutionLimit = ConfigSource.ReadValue("shader", "renderresolutionlimit").ToInt32(Program.AppConfig.RenderResolutionLimit);

            RandomTimeOffset = ConfigSource.ReadValue("shader", "randomtimeoffset").ToInt32(0);

            VertexSourceTypeName = ConfigSource.ReadValue("shader", "vertexsourcetypename");

            if(ConfigSource.Content.ContainsKey("audiotextures"))
            {
                foreach(var tex in ConfigSource.Content["audiotextures"])
                {
                    AudioTextureUniformNames.Add(tex.Value);
                }
            }

            SwitchTimeHint = ConfigSource.ReadValue("playlist", "switchtimehint").ToEnum(VizPlaylistTimeHint.None);
            FXAddStartPercent = ConfigSource.ReadValue("playlist", "fxaddstartpercent").ToInt32(0);

            if (ConfigSource.Content.ContainsKey("fx-blacklist"))
            {
                foreach (var fx in ConfigSource.Content["fx-blacklist"])
                {
                    FXBlacklist.Add(fx.Value);
                }
            }

            Uniforms = ConfigSource.ParseUniforms();

            // shader pathnames and vertex source type names are validated in RenderingHelper

            var err = $"Error in {ConfigSource.Pathname}: ";
            if (RenderResolutionLimit < 256 && RenderResolutionLimit != 0) throw new ArgumentException($"{err} RenderResolutionLimit must be 256 or greater (default is 0 to disable)");

            if (Program.AppConfig.RenderResolutionLimit > 0 && RenderResolutionLimit > Program.AppConfig.RenderResolutionLimit)
            {
                LogHelper.Logger?.LogWarning($"Clamping visualizer to global RenderResolutionLimit {Program.AppConfig.RenderResolutionLimit} instead of {RenderResolutionLimit} in conf.");
                RenderResolutionLimit = Program.AppConfig.RenderResolutionLimit;
            }

            if (FXAddStartPercent < -50 || FXAddStartPercent > 50) throw new ArgumentException($"{err} FXAddStartPercent must be within -50 to +50 (default is 0)");
        }
    }
}
