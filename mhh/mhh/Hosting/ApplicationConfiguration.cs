
using System.Runtime.InteropServices;

namespace mhh
{
    public class ApplicationConfiguration
    {
        public static readonly string SectionOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
        public static readonly string InternalShaderPath = "./InternalShaders/";

        public readonly string ShaderPath = string.Empty;
        public readonly string PlaylistPath = string.Empty;
        public readonly string PluginPath = string.Empty;

        public readonly bool StartFullScreen;
        public readonly int SizeX;
        public readonly int SizeY;
        public readonly bool HideMousePointer;
        public readonly int ShaderCacheSize;
        public readonly int CrossfadeSeconds;
        public readonly int UnsecuredPort;

        public readonly string CaptureDriverName = string.Empty;
        public readonly string CaptureDeviceName = string.Empty;

        public readonly int DetectSilenceSeconds = 0;
        public readonly double DetectSilenceMaxRMS = 1.5d;
        public readonly SilenceAction DetectSilenceAction = SilenceAction.None;

        public readonly ConfigFile Config;

        public ApplicationConfiguration(ConfigFile appConfigFile)
        {
            Config = appConfigFile;

            StartFullScreen = Config.ReadValue("setup", "startfullscreen").ToBool(true);
            SizeX = Config.ReadValue("setup", "sizex").ToInt32(960);
            SizeY = Config.ReadValue("setup", "sizey").ToInt32(540);
            HideMousePointer = Config.ReadValue("setup", "hidemousepointer").ToBool(true);
            ShaderCacheSize = Config.ReadValue("setup", "shadercachesize").ToInt32(50);
            CrossfadeSeconds = Config.ReadValue("setup", "crossfadeseconds").ToInt32(2);
            UnsecuredPort = Config.ReadValue("setup", "unsecuredport").ToInt32(0);

            ShaderPath = Config.ReadValue(SectionOS, "shaderpath");
            PlaylistPath = Config.ReadValue(SectionOS, "playlistpath");
            PluginPath = Config.ReadValue(SectionOS, "pluginpath");

            DetectSilenceSeconds = Config.ReadValue("setup", "detectsilenceseconds").ToInt32(0);
            DetectSilenceMaxRMS = Config.ReadValue("setup", "detectsilencemaxrms").ToDouble(1.5d);
            DetectSilenceAction = Config.ReadValue("setup", "detectsilenceaction").ToEnum(SilenceAction.Blank);

            CaptureDriverName = Config.ReadValue(SectionOS, "capturedrivername");
            CaptureDeviceName = Config.ReadValue(SectionOS, "capturedevicename");
        }
    }
}
