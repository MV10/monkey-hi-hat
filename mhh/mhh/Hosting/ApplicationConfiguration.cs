
using System.Runtime.InteropServices;

namespace mhh
{
    public class ApplicationConfiguration
    {
        public static readonly string SectionOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
        public static readonly string InternalShaderPath = "./InternalShaders/";

        public readonly string VisualizerPath = string.Empty;
        public readonly string PlaylistPath = string.Empty;
        public readonly string FXPath = string.Empty;
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

            VisualizerPath = Config.ReadValue(SectionOS, "visualizerpath");
            PlaylistPath = Config.ReadValue(SectionOS, "playlistpath");
            FXPath = Config.ReadValue(SectionOS, "fxpath");
            PluginPath = Config.ReadValue(SectionOS, "pluginpath");

            DetectSilenceSeconds = Config.ReadValue("setup", "detectsilenceseconds").ToInt32(0);
            DetectSilenceMaxRMS = Config.ReadValue("setup", "detectsilencemaxrms").ToDouble(1.5d);
            DetectSilenceAction = Config.ReadValue("setup", "detectsilenceaction").ToEnum(SilenceAction.Blank);

            CaptureDriverName = Config.ReadValue(SectionOS, "capturedrivername");
            CaptureDeviceName = Config.ReadValue(SectionOS, "capturedevicename");

            // validation
            if (string.IsNullOrWhiteSpace(VisualizerPath)) ConfError("VisualizerPath is required.");
            if (ShaderCacheSize < 1) ConfError("ShaderCacheSize must be 1 or greater. Default is 50 when omitted.");
            if (CrossfadeSeconds < 0) ConfError("CrossfadeSeconds must be 0 or greater. Default is 2, use 0 to disable.");
            if (UnsecuredPort < 0 || UnsecuredPort > 65534) ConfError("UnsecuredPort must be 0 to 65534, recommended range is 49152 or higher, use 0 to disable.");
            if (DetectSilenceSeconds < 0) ConfError("DetectSilenceSeconds must be 0 or greater.");
            if (DetectSilenceMaxRMS < 0) ConfError("DetectSilienceMaxRMS must be 0 or greater.");
        }

        private void ConfError(string message)
            => throw new ArgumentException($"Error in mhh.conf: {message}");
    }
}
