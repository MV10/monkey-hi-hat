
using OpenTK.Graphics.OpenGL;

namespace mhh;

/// <summary>
/// Represents an FX file describing post-processing shader passes.
/// </summary>
public class FXConfig : IConfigSource
{
    public ConfigFile ConfigSource { get; private set; }

    public readonly string Description;
    public readonly int RenderResolutionLimit;
    public readonly bool ApplyPrimaryResolutionLimit;
    public readonly bool Crossfade;

    public readonly Dictionary<string, float> Uniforms;

    public readonly List<string> AudioTextureUniformNames = new();

    public readonly List<LibraryShaderConfig> LibraryConfigs;

    public FXConfig(string pathname)
    {
        // FXRenderer handles loading the [textures] section, if present

        ConfigSource = new(pathname);

        Description = ConfigSource.ReadValue("fx", "description");

        RenderResolutionLimit = ConfigSource.ReadValue("fx", "renderresolutionlimit").ToInt32(Program.AppConfig.RenderResolutionLimit);
        ApplyPrimaryResolutionLimit = ConfigSource.ReadValue("fx", "applyprimaryresolutionlimit").ToBool(false);
        Crossfade = (Program.AppConfig.CrossfadeSeconds == 0) ? false : ConfigSource.ReadValue("fx", "crossfade").ToBool(true);

        Uniforms = ConfigSource.ParseUniforms();

        LibraryConfigs = ConfigSource.ParseLibraryPathnames(Program.AppConfig.FXPath);

        if (ConfigSource.Content.ContainsKey("audiotextures"))
        {
            foreach (var tex in ConfigSource.Content["audiotextures"])
            {
                AudioTextureUniformNames.Add(tex.Value);
            }
        }
    }
}
