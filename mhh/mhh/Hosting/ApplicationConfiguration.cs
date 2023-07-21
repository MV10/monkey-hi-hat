
using System.Runtime.InteropServices;

namespace mhh
{
    public class ApplicationConfiguration
    {
        public static readonly string SectionOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";

        public readonly string ShaderPath = string.Empty;
        public readonly string PlaylistPath = string.Empty;
        public readonly string PluginPath = string.Empty;

        public readonly bool StartFullScreen;
        public readonly int SizeX;
        public readonly int SizeY;

        public readonly string CaptureDriverName = string.Empty;
        public readonly string CaptureDeviceName = string.Empty;

        public readonly VisualizerConfig IdleVisualizer;

        public readonly ConfigFile Config;

        public ApplicationConfiguration(ConfigFile appConfigFile, string idleVisualizerConfigPathname)
        {
            Config = appConfigFile;

            StartFullScreen = Config.ReadValue("setup", "startfullscreen").ToBool(false);
            SizeX = Config.ReadValue("setup", "sizex").ToInt32(960);
            SizeY = Config.ReadValue("setup", "sizey").ToInt32(540);

            ShaderPath = Config.ReadValue(SectionOS, "shaderpath");
            PlaylistPath = Config.ReadValue(SectionOS, "playlistpath");
            PluginPath = Config.ReadValue(SectionOS, "pluginpath");

            CaptureDriverName = Config.ReadValue(SectionOS, "capturedrivername");
            CaptureDeviceName = Config.ReadValue(SectionOS, "capturedevicename");

            IdleVisualizer = new(idleVisualizerConfigPathname);
        }
    }
}
