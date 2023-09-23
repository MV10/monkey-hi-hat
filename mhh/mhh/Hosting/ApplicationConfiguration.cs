﻿
using System.Runtime.InteropServices;

namespace mhh
{
    public class ApplicationConfiguration : IConfigSource
    {
        public static readonly string SectionOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
        public static readonly string InternalShaderPath = "./InternalShaders/";

        public ConfigFile ConfigSource { get; private set; }

        public readonly string VisualizerPath = string.Empty;
        public readonly string PlaylistPath = string.Empty;
        public readonly string TexturePath = string.Empty;
        public readonly string FXPath = string.Empty;

        public readonly bool StartFullScreen;
        public readonly int SizeX;
        public readonly int SizeY;
        public readonly int RenderResolutionLimit;
        public readonly bool HideMousePointer;
        public readonly int ShaderCacheSize;
        public readonly int CrossfadeSeconds;
        public readonly int FrameRateLimit;
        public readonly int UnsecuredPort;

        public readonly string CaptureDriverName = string.Empty;
        public readonly string CaptureDeviceName = string.Empty;

        public readonly int DetectSilenceSeconds = 0;
        public readonly double DetectSilenceMaxRMS = 1.5d;
        public readonly SilenceAction DetectSilenceAction = SilenceAction.None;

        public ApplicationConfiguration(ConfigFile appConfigFile)
        {
            ConfigSource = appConfigFile;

            StartFullScreen = ConfigSource.ReadValue("setup", "startfullscreen").ToBool(true);
            SizeX = ConfigSource.ReadValue("setup", "sizex").ToInt32(960);
            SizeY = ConfigSource.ReadValue("setup", "sizey").ToInt32(540);
            RenderResolutionLimit = ConfigSource.ReadValue("setup", "renderresolutionlimit").ToInt32(0);
            HideMousePointer = ConfigSource.ReadValue("setup", "hidemousepointer").ToBool(true);
            ShaderCacheSize = ConfigSource.ReadValue("setup", "shadercachesize").ToInt32(50);
            CrossfadeSeconds = ConfigSource.ReadValue("setup", "crossfadeseconds").ToInt32(2);
            FrameRateLimit = ConfigSource.ReadValue("setup", "FrameRateLimit").ToInt32(60);
            UnsecuredPort = ConfigSource.ReadValue("setup", "unsecuredport").ToInt32(0);

            VisualizerPath = ConfigSource.ReadValue(SectionOS, "visualizerpath");
            PlaylistPath = ConfigSource.ReadValue(SectionOS, "playlistpath");
            TexturePath = ConfigSource.ReadValue(SectionOS, "texturepath");
            FXPath = ConfigSource.ReadValue(SectionOS, "fxpath");

            DetectSilenceSeconds = ConfigSource.ReadValue("setup", "detectsilenceseconds").ToInt32(0);
            DetectSilenceMaxRMS = ConfigSource.ReadValue("setup", "detectsilencemaxrms").ToDouble(1.5d);
            DetectSilenceAction = ConfigSource.ReadValue("setup", "detectsilenceaction").ToEnum(SilenceAction.Blank);

            CaptureDriverName = ConfigSource.ReadValue(SectionOS, "capturedrivername");
            CaptureDeviceName = ConfigSource.ReadValue(SectionOS, "capturedevicename");

            // validation
            if (RenderResolutionLimit < 256 && RenderResolutionLimit !=0) ConfError("RenderResolutionLimit must be 256 or greater (default is 0 to disable).");
            if (ShaderCacheSize < 1) ConfError("ShaderCacheSize must be 1 or greater. Default is 50 when omitted.");
            if (CrossfadeSeconds < 0) ConfError("CrossfadeSeconds must be 0 or greater. Default is 2, use 0 to disable.");
            if (FrameRateLimit < 0 || FrameRateLimit > 9999) ConfError("FrameRateLimit must be 0 to 9999. Default is 60. Set to 0 for no limit (may break some shaders).");
            if (UnsecuredPort < 0 || UnsecuredPort > 65534) ConfError("UnsecuredPort must be 0 to 65534, recommended range is 49152 or higher, use 0 to disable.");
            if (DetectSilenceSeconds < 0) ConfError("DetectSilenceSeconds must be 0 or greater.");
            if (DetectSilenceMaxRMS < 0) ConfError("DetectSilienceMaxRMS must be 0 or greater.");

            if (string.IsNullOrWhiteSpace(VisualizerPath)) ConfError("VisualizerPath is required.");
            PathValidation(VisualizerPath);
            PathValidation(PlaylistPath);
            PathValidation(TexturePath);
            PathValidation(FXPath);
        }

        private void PathValidation(string pathspec)
        {
            if (string.IsNullOrWhiteSpace(pathspec)) return;

            var paths = PathHelper.GetIndividualPaths(pathspec);
            foreach(var path in paths)
            {
                if (!Path.IsPathFullyQualified(path)) ConfError($"Path not fully-qualified: {path}");
                if (!Directory.Exists(path)) ConfError($"Path not found: {path}");
            }
        }

        private void ConfError(string message)
            => throw new ArgumentException($"Error in mhh.conf: {message}");
    }
}
