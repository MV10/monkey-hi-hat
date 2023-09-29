
namespace mhh;

/// <summary>
/// Represents an FX file describing post-processing shader passes.
/// </summary>
public class FXConfig : IConfigSource
{
    public ConfigFile ConfigSource { get; private set; }

    public readonly string Description;
    public readonly FXPrimaryDrawMode PrimaryDrawMode;
    public readonly int RenderResolutionLimit;
    public readonly int PrimaryResolutionLimit;
    public readonly bool Crossfade;

    public readonly Dictionary<string, float> Uniforms;

    public readonly List<string> AudioTextureUniformNames = new();

    public FXConfig(string pathname)
    {
        // FXRenderer handles loading the [textures] section, if present

        ConfigSource = new(pathname);

        Description = ConfigSource.ReadValue("fx", "description");

        PrimaryDrawMode = ConfigSource.ReadValue("fx", "primarydrawmode").ToEnum(FXPrimaryDrawMode.Active);
        if (PrimaryDrawMode == FXPrimaryDrawMode.Random) PrimaryDrawMode = (new Random().Next(101) < 50) ? FXPrimaryDrawMode.Active : FXPrimaryDrawMode.Snapshot;

        RenderResolutionLimit = ConfigSource.ReadValue("fx", "renderresolutionlimit").ToInt32(Program.AppConfig.RenderResolutionLimit);
        PrimaryResolutionLimit = ConfigSource.ReadValue("fx", "primaryresolutionlimit").ToInt32(Program.AppConfig.RenderResolutionLimit);

        Crossfade = (Program.AppConfig.CrossfadeSeconds == 0) ? false : ConfigSource.ReadValue("fx", "crossfade").ToBool(true);

        Uniforms = ConfigSource.ParseUniforms();

        if (ConfigSource.Content.ContainsKey("audiotextures"))
        {
            foreach (var tex in ConfigSource.Content["audiotextures"])
            {
                AudioTextureUniformNames.Add(tex.Value);
            }
        }
    }
}
