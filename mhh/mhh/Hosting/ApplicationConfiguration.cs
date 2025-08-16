
using eyecandy;
using OpenTK.Windowing.Common;
using System.Runtime.InteropServices;

namespace mhh
{
    public class ApplicationConfiguration : IConfigSource
    {
        public static readonly string SectionOS = "windows"; // Linux support removed as of version 4.3.1
        public static readonly string InternalShaderPath = "./InternalShaders/";
        public static readonly string PassthroughVertexPathname = Path.Combine(InternalShaderPath, "passthrough.vert");
        public static readonly string PassthroughFragmentPathname = Path.Combine(InternalShaderPath, "passthrough.frag");

        public ConfigFile ConfigSource { get; private set; }

        public readonly string VisualizerPath = string.Empty;
        public readonly string PlaylistPath = string.Empty;
        public readonly string TexturePath = string.Empty;
        public readonly string FXPath = string.Empty;
        public readonly string FFmpegPath = string.Empty;

        public readonly bool StartFullScreen;
        public readonly int StartX;
        public readonly int StartY;
        public readonly int SizeX;
        public readonly int SizeY;
        public readonly int RenderResolutionLimit;
        public readonly bool HideMousePointer;
        public readonly bool HideWindowBorder;
        public readonly bool FullscreenMinimizeOnFocusChange;
        public readonly int ShaderCacheSize;
        public readonly int FXCacheSize;
        public readonly int LibraryCacheSize;
        public bool RandomizeCrossfade; // not readonly, can be disabled during initialization
        public readonly int CrossfadeSeconds;
        public readonly int FrameRateLimit;
        public readonly VSyncMode VSync;
        public readonly int UnsecuredPort;
        public readonly int TestingSkipVizCount;
        public readonly int TestingSkipFXCount;
        public readonly VideoFlipMode VideoFlip;

        public readonly bool StartInStandby;
        public readonly bool CloseToStandby;
        public readonly bool WindowsHideConsoleAtStartup;
        public readonly bool WindowsHideConsoleInStandby;

        public readonly string CaptureDriverName = string.Empty;
        public readonly string CaptureDeviceName = string.Empty;
        public readonly LoopbackApi LoopbackApi;

        public readonly int DetectSilenceSeconds = 0;
        public readonly double DetectSilenceMaxRMS = 1.5d;
        public readonly SilenceAction DetectSilenceAction = SilenceAction.None;

        public readonly double ReplaceSilenceAfterSeconds = 0;
        public readonly double SyntheticDataBPM = 120;
        public readonly double SyntheticDataBeatDuration = 0.1;
        public readonly double SyntheticDataBeatFrequency = 440;
        public readonly double SyntheticDataAmplitude = 0.5;
        public readonly float SyntheticDataMinimumLevel = 0.1f;
        public readonly SyntheticDataAlgorithm SyntheticAlgorithm = SyntheticDataAlgorithm.MetronomeBeat;
        public readonly float SyntheticDataPlaybackVolume = 0.0f;

        public bool ShowPlaylistPopups; // not readonly, can be toggled at runtime
        public readonly int PopupVisibilitySeconds;
        public readonly int PopupFadeMilliseconds;
        public readonly bool OverlayPermanent;
        public readonly int OverlayVisibilitySeconds;
        public readonly int OverlayUpdateMilliseconds;
        public readonly float OutlineWeight;
        public readonly int TextBufferX;
        public readonly int TextBufferY;
        public readonly string FontAtlasFilename = string.Empty;
        public readonly float CharacterSize;
        public readonly float PositionX;
        public readonly float PositionY;

        public readonly bool ShowSpotifyTrackPopups;

        public ApplicationConfiguration(ConfigFile appConfigFile)
        {
            ConfigSource = appConfigFile;

            StartFullScreen = ConfigSource.ReadValue("setup", "startfullscreen").ToBool(true);
            StartInStandby = ConfigSource.ReadValue("setup", "startinstandby").ToBool(false);
            CloseToStandby = ConfigSource.ReadValue("setup", "closetostandby").ToBool(false);
            WindowsHideConsoleAtStartup = ConfigSource.ReadValue("windows", "hideconsoleatstartup").ToBool(false);
            WindowsHideConsoleInStandby = ConfigSource.ReadValue("windows", "hideconsoleinstandby").ToBool(true);
            StartX = ConfigSource.ReadValue("setup", "startx").ToInt32(100);
            StartY = ConfigSource.ReadValue("setup", "starty").ToInt32(100);
            SizeX = ConfigSource.ReadValue("setup", "sizex").ToInt32(960);
            SizeY = ConfigSource.ReadValue("setup", "sizey").ToInt32(540);
            RenderResolutionLimit = ConfigSource.ReadValue("setup", "renderresolutionlimit").ToInt32(0);
            HideMousePointer = ConfigSource.ReadValue("setup", "hidemousepointer").ToBool(true);
            HideWindowBorder = ConfigSource.ReadValue("setup", "hidewindowborder").ToBool(true);
            FullscreenMinimizeOnFocusChange = ConfigSource.ReadValue("setup", "fullscreenminimizeonfocuschange").ToBool(true);
            ShaderCacheSize = ConfigSource.ReadValue("setup", "shadercachesize").ToInt32(150);
            FXCacheSize = ConfigSource.ReadValue("setup", "fxcachesize").ToInt32(50);
            LibraryCacheSize = ConfigSource.ReadValue("setup", "librarycachesize").ToInt32(10);
            RandomizeCrossfade = ConfigSource.ReadValue("setup", "randomizecrossfade").ToBool(false);
            CrossfadeSeconds = ConfigSource.ReadValue("setup", "crossfadeseconds").ToInt32(2);
            FrameRateLimit = ConfigSource.ReadValue("setup", "FrameRateLimit").ToInt32(60);
            VSync = ConfigSource.ReadValue("setup", "vsync").ToEnum(VSyncMode.Off);
            UnsecuredPort = ConfigSource.ReadValue("setup", "unsecuredport").ToInt32(0);
            TestingSkipVizCount = ConfigSource.ReadValue("setup", "testingskipvizcount").ToInt32(0);
            TestingSkipFXCount = ConfigSource.ReadValue("setup", "testingskipfxcount").ToInt32(0);
            VideoFlip = ConfigSource.ReadValue("setup", "videoflip").ToEnum(VideoFlipMode.Internal);

            VisualizerPath = ConfigSource.ReadValue(SectionOS, "visualizerpath");
            PlaylistPath = ConfigSource.ReadValue(SectionOS, "playlistpath");
            TexturePath = ConfigSource.ReadValue(SectionOS, "texturepath");
            FXPath = ConfigSource.ReadValue(SectionOS, "fxpath");
            FFmpegPath = ConfigSource.ReadValue(SectionOS, "ffmpegpath");

            DetectSilenceSeconds = ConfigSource.ReadValue("setup", "detectsilenceseconds").ToInt32(0);
            DetectSilenceMaxRMS = ConfigSource.ReadValue("setup", "detectsilencemaxrms").ToDouble(1.5d);
            DetectSilenceAction = ConfigSource.ReadValue("setup", "detectsilenceaction").ToEnum(SilenceAction.Blank);

            ReplaceSilenceAfterSeconds = ConfigSource.ReadValue("setup", "replacesilenceafterseconds").ToDouble(2.0);
            SyntheticDataBPM = ConfigSource.ReadValue("setup", "syntheticdatabpm").ToDouble(120);
            SyntheticDataBeatDuration = ConfigSource.ReadValue("setup", "syntheticdatabeatduration").ToDouble(0.1);
            SyntheticDataBeatFrequency = ConfigSource.ReadValue("setup", "syntheticdatabeatfrequency").ToDouble(440);
            SyntheticDataAmplitude = ConfigSource.ReadValue("setup", "syntheticdataamplitude").ToDouble(0.5);
            SyntheticDataMinimumLevel = ConfigSource.ReadValue("setup", "syntheticdataminimumlevel").ToFloat(0.1f);
            SyntheticAlgorithm = ConfigSource.ReadValue("setup", "syntheticalgorithm").ToEnum(SyntheticDataAlgorithm.MetronomeBeat);
            SyntheticDataPlaybackVolume = ConfigSource.ReadValue("setup", "syntheticdataplaybackvolume").ToFloat(0.0f);

            CaptureDriverName = ConfigSource.ReadValue(SectionOS, "capturedrivername");
            CaptureDeviceName = ConfigSource.ReadValue(SectionOS, "capturedevicename");

            LoopbackApi = ConfigSource.ReadValue("windows", "loopbackapi").ToEnum(LoopbackApi.WindowsInternal);

            ShowPlaylistPopups = ConfigSource.ReadValue("text", "ShowPlaylistPopups").ToBool(true);
            PopupVisibilitySeconds = ConfigSource.ReadValue("text", "PopupVisibilitySeconds").ToInt32(5);
            PopupFadeMilliseconds = ConfigSource.ReadValue("text", "PopupFadeMilliseconds").ToInt32(1000);
            OverlayPermanent = ConfigSource.ReadValue("text", "OverlayPermanent").ToBool(false);
            OverlayVisibilitySeconds = ConfigSource.ReadValue("text", "OverlayVisibilitySeconds").ToInt32(10);
            OverlayUpdateMilliseconds = ConfigSource.ReadValue("text", "OverlayUpdateMilliseconds").ToInt32(500);
            OutlineWeight = ConfigSource.ReadValue("text", "OutlineWeight").ToFloat(0.55f);
            TextBufferX = ConfigSource.ReadValue("text", "TextBufferX").ToInt32(100);
            TextBufferY = ConfigSource.ReadValue("text", "TextBufferY").ToInt32(10);
            FontAtlasFilename = ConfigSource.ReadValue("text", "FontAtlasFilename");
            CharacterSize = ConfigSource.ReadValue("text", "CharacterSize").ToFloat(0.02f);
            PositionX = ConfigSource.ReadValue("text", "PositionX").ToFloat(-0.96f);
            PositionY = ConfigSource.ReadValue("text", "PositionY").ToFloat(0.52f);

            ShowSpotifyTrackPopups = ConfigSource.ReadValue(SectionOS, "showspotifytrackpopups").ToBool(false);

            // validation
            // TODO validate [text] section settings
            if (RenderResolutionLimit < 256 && RenderResolutionLimit !=0) ConfError("RenderResolutionLimit must be 256 or greater (default is 0 to disable).");
            if (ShaderCacheSize < 0) ConfError("ShaderCacheSize must be 0 or greater. Default is 150 when omitted, 0 disables caching.");
            if (FXCacheSize < 0) ConfError("FXCacheSize must be 0 or greater. Default is 50 when omitted, 0 disables caching.");
            if (LibraryCacheSize < 0) ConfError("LibraryCacheSize must be 0 or greater. Default is 10 when omitted, 0 disables caching.");
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

            if (PathHelper.GetIndividualPaths(FFmpegPath).Length > 1) ConfError("Exactly one path is required for FFmpegPath.");
            PathValidation(FFmpegPath);
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
