
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
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
        public readonly int FXResolutionLimit;
        public readonly int RandomTimeOffset;

        public readonly string VertexSourceTypeName;

        public readonly List<string> AudioTextureUniformNames = new();

        public readonly VizPlaylistTimeHint SwitchTimeHint;
        public readonly int FXAddStartPercent;
        public readonly List<string> FXBlacklist = new();

        public readonly Dictionary<string, float> Uniforms;
        public readonly Dictionary<string, Dictionary<string, float>> FXUniforms;

        public readonly List<LibraryShaderConfig> LibraryConfigs;

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
            FXResolutionLimit = ConfigSource.ReadValue("shader", "fxresolutionlimit").ToInt32(Program.AppConfig.RenderResolutionLimit);

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
            FXUniforms = ConfigSource.ParseFXUniforms();

            LibraryConfigs = ConfigSource.ParseLibraryPathnames(Program.AppConfig.VisualizerPath);

            // shader pathnames and vertex source type names are validated in RenderingHelper

            var err = $"Error in {ConfigSource.Pathname}: ";
            if (RenderResolutionLimit < 256 && RenderResolutionLimit != 0) throw new ArgumentException($"{err} RenderResolutionLimit must be 256 or greater (default is 0 to disable)");
            if (FXResolutionLimit < 256 && FXResolutionLimit != 0) throw new ArgumentException($"{err} FXResolutionLimit must be 256 or greater (default is 0 to disable)");

            if (Program.AppConfig.RenderResolutionLimit > 0 && RenderResolutionLimit > Program.AppConfig.RenderResolutionLimit)
            {
                LogHelper.Logger?.LogWarning($"Clamping visualizer to global RenderResolutionLimit {Program.AppConfig.RenderResolutionLimit} instead of {RenderResolutionLimit} in conf.");
                RenderResolutionLimit = Program.AppConfig.RenderResolutionLimit;
            }

            if (Program.AppConfig.RenderResolutionLimit > 0 && FXResolutionLimit > Program.AppConfig.RenderResolutionLimit)
            {
                LogHelper.Logger?.LogWarning($"Clamping visualizer FXResolutionLimit to global limit {Program.AppConfig.RenderResolutionLimit} instead of {FXResolutionLimit} in conf.");
                FXResolutionLimit = Program.AppConfig.RenderResolutionLimit;
            }

            if (FXResolutionLimit > 0 && RenderResolutionLimit > 0 && RenderResolutionLimit < FXResolutionLimit)
            {
                LogHelper.Logger?.LogWarning($"Clamping visualizer FXResolutionLimit {FXResolutionLimit} to RenderResolutionLimit {RenderResolutionLimit}.");
                FXResolutionLimit = RenderResolutionLimit;
            }

            if (FXAddStartPercent < -50 || FXAddStartPercent > 50) throw new ArgumentException($"{err} FXAddStartPercent must be within -50 to +50 (default is 0)");
        }
    }
}
