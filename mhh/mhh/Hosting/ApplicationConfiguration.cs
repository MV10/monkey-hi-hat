
using System.Runtime.InteropServices;

namespace mhh
{
    public class ApplicationConfiguration
    {
        public readonly string PlaylistPath = string.Empty;
        public readonly string ShaderPath = string.Empty;
        public readonly string PluginPath = string.Empty;

        public readonly bool StartFullScreen;
        public readonly int SizeX;
        public readonly int SizeY;

        public readonly string CaptureDriverName = string.Empty;
        public readonly string CaptureDeviceName = string.Empty;

        public readonly VisualizerConfig IdleVisualizer;

        public ApplicationConfiguration(string appConfigPathname, string idleVisualizerConfigPathname)
        {
            var conf = new ConfigFile(appConfigPathname);
            
            PlaylistPath = conf.ReadValue("setup", "playlistpath");
            ShaderPath = conf.ReadValue("setup", "shaderpath");
            PluginPath = conf.ReadValue("setup", "pluginpath");

            StartFullScreen = conf.ReadValue("setup", "startfullscreen").ToBool(false);
            SizeX = conf.ReadValue("setup", "sizex").ToInt32(960);
            SizeY = conf.ReadValue("setup", "sizey").ToInt32(540);

            var osSection = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
            CaptureDriverName = conf.ReadValue(osSection, "capturedrivername");
            CaptureDeviceName = conf.ReadValue(osSection, "capturedevicename");

            IdleVisualizer = new(idleVisualizerConfigPathname);
        }
    }
}
