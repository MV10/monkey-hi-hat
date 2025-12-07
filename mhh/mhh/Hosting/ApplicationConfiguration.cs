
using eyecandy;
using OpenTK.Windowing.Common;
using System.Runtime.InteropServices;

namespace mhh;

public class ApplicationConfiguration : IConfigSource
{
    //
    // Not all mhh.conf settings are represented.
    // Some such as LogLevel are read before config is parsed into this class.
    //

    public static readonly string SectionOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
    public static readonly string InternalShaderPath = "./InternalShaders/";
    public static readonly string PassthroughVertexPathname = Path.Combine(InternalShaderPath, "passthrough.vert");
    public static readonly string PassthroughFragmentPathname = Path.Combine(InternalShaderPath, "passthrough.frag");

    public ConfigFile ConfigSource { get; private set; }

    // When implementing a new pathspec, also update the --paths command
    public readonly string VisualizerPath = string.Empty;
    public readonly string PlaylistPath = string.Empty;
    public readonly string TexturePath = string.Empty;
    public readonly string FXPath = string.Empty;
    public readonly string CrossfadePath = string.Empty;
    public readonly string FFmpegPath = string.Empty;
    public readonly string ScreenshotPath = string.Empty;

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
    public readonly bool HideConsoleAtStartup;
    public readonly bool HideConsoleInStandby;
    public readonly bool LinuxSkipX11Check = false;
    
    public readonly OpenGLErrorLogFlags OpenGLErrorLogging;
    public readonly bool OpenGLErrorBreakpoint;
    public readonly long OpenGLErrorThrottle;

    public readonly string OpenALContextDeviceName = string.Empty;
    public readonly string CaptureDeviceName = string.Empty;
    public readonly LoopbackApi LoopbackApi;

    public readonly int DetectSilenceSeconds = 0;
    public readonly double DetectSilenceMaxRMS = 1.5d;
    public readonly SilenceAction DetectSilenceAction = SilenceAction.None;

    public readonly double MinimumSilenceSeconds = 0.25;
    public readonly double ReplaceSilenceAfterSeconds = 0;
    public readonly double SyntheticDataBPM = 120;
    public readonly double SyntheticDataBeatDuration = 0.1;
    public readonly double SyntheticDataBeatFrequency = 440;
    public readonly double SyntheticDataAmplitude = 0.5;
    public readonly float SyntheticDataMinimumLevel = 0.1f;
    public readonly SyntheticDataAlgorithm SyntheticAlgorithm = SyntheticDataAlgorithm.MetronomeBeat;

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

    public readonly bool WindowsSpotifyTrackPopups;
    public readonly bool LinuxMediaPopups;
    public readonly string LinuxMediaService = string.Empty;

    public readonly bool NDISender;
    public readonly string NDIDeviceName = string.Empty;
    public readonly string NDIGroupList = string.Empty;
    public readonly string NDIReceiveFrom = string.Empty;
    public readonly bool NDIReceiveInvert = true;

    public readonly bool SpoutSender;
    public readonly string SpoutReceiveFrom = string.Empty;
    public readonly bool SpoutReceiveInvert = true;

    public ApplicationConfiguration(ConfigFile appConfigFile)
    {
        ConfigSource = appConfigFile;

        StartFullScreen = ConfigSource.ReadValue("setup", "startfullscreen").ToBool(true);
        StartInStandby = ConfigSource.ReadValue("setup", "startinstandby").ToBool(false);
        CloseToStandby = ConfigSource.ReadValue("setup", "closetostandby").ToBool(false);
        HideConsoleAtStartup = ConfigSource.ReadValue(SectionOS, "HideConsoleAtStartup").ToBool(false);
        HideConsoleInStandby = ConfigSource.ReadValue(SectionOS, "HideConsoleInStandby").ToBool(false);
        LinuxSkipX11Check = ConfigSource.ReadValue("linux", "skipx11check").ToBool(false);

        OpenGLErrorLogging = ConfigSource.ReadValue("setup", "openglerrorlogging").ToEnum(OpenGLErrorLogFlags.Normal);
        OpenGLErrorBreakpoint = ConfigSource.ReadValue("setup", "openglerrorbreakpoint").ToBool(false);
        OpenGLErrorThrottle = ConfigSource.ReadValue("setup", "openglerrorthrottle").ToLong(60000);

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
        ScreenshotPath = ConfigSource.ReadValue(SectionOS, "screenshotpath");
        CrossfadePath = ConfigSource.ReadValue(SectionOS, "CrossfadePath");

        DetectSilenceSeconds = ConfigSource.ReadValue("setup", "detectsilenceseconds").ToInt32(0);
        DetectSilenceMaxRMS = ConfigSource.ReadValue("setup", "detectsilencemaxrms").ToDouble(1.5d);
        DetectSilenceAction = ConfigSource.ReadValue("setup", "detectsilenceaction").ToEnum(SilenceAction.Blank);

        MinimumSilenceSeconds = ConfigSource.ReadValue("setup", "minimumsilenceseconds").ToDouble(0.25);
        ReplaceSilenceAfterSeconds = ConfigSource.ReadValue("setup", "replacesilenceafterseconds").ToDouble(2.0);
        SyntheticDataBPM = ConfigSource.ReadValue("setup", "syntheticdatabpm").ToDouble(120);
        SyntheticDataBeatDuration = ConfigSource.ReadValue("setup", "syntheticdatabeatduration").ToDouble(0.1);
        SyntheticDataBeatFrequency = ConfigSource.ReadValue("setup", "syntheticdatabeatfrequency").ToDouble(440);
        SyntheticDataAmplitude = ConfigSource.ReadValue("setup", "syntheticdataamplitude").ToDouble(0.5);
        SyntheticDataMinimumLevel = ConfigSource.ReadValue("setup", "syntheticdataminimumlevel").ToFloat(0.1f);
        SyntheticAlgorithm = ConfigSource.ReadValue("setup", "syntheticalgorithm").ToEnum(SyntheticDataAlgorithm.MetronomeBeat);

        LoopbackApi = ConfigSource.ReadValue(SectionOS, "loopbackapi").ToEnum(LoopbackApi.WindowsInternal);
        OpenALContextDeviceName = ConfigSource.ReadValue(SectionOS, "OpenALContextDeviceName");
        CaptureDeviceName = ConfigSource.ReadValue(SectionOS, "capturedevicename");

        NDISender = ConfigSource.ReadValue("ndi", "ndisender").ToBool(false);
        NDIDeviceName = ConfigSource.ReadValue("ndi", "ndidevicename");
        NDIGroupList = ConfigSource.ReadValue("ndi", "ndigroupList");
        NDIReceiveFrom = ConfigSource.ReadValue("ndi", "NDIReceiveFrom");
        NDIReceiveInvert = ConfigSource.ReadValue("ndi", "NDIReceiveInvert").ToBool(true);

        SpoutSender = ConfigSource.ReadValue("windows", "spoutsender").ToBool(false);
        SpoutReceiveFrom = ConfigSource.ReadValue("windows", "SpoutReceiveFrom");
        SpoutReceiveInvert = ConfigSource.ReadValue("windows", "SpoutReceiveInvert").ToBool(true);

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

        WindowsSpotifyTrackPopups = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && 
                                    ConfigSource.ReadValue("windows", "showspotifytrackpopups").ToBool(false);

        LinuxMediaPopups = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                           ConfigSource.ReadValue("linux", "showmediapopups").ToBool(false);

        LinuxMediaService = ConfigSource.ReadValue("linux", "LinuxMediaService");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            PathHelper.ExpandLinuxHomeDirectory(ref VisualizerPath);
            PathHelper.ExpandLinuxHomeDirectory(ref PlaylistPath);
            PathHelper.ExpandLinuxHomeDirectory(ref TexturePath);
            PathHelper.ExpandLinuxHomeDirectory(ref FXPath);
            PathHelper.ExpandLinuxHomeDirectory(ref FFmpegPath);
            PathHelper.ExpandLinuxHomeDirectory(ref ScreenshotPath);
            PathHelper.ExpandLinuxHomeDirectory(ref CrossfadePath);
        }
        
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
        if (DetectSilenceMaxRMS < 0) ConfError("DetectSilenceMaxRMS must be 0 or greater.");
        if (SectionOS == "linux" && LoopbackApi == LoopbackApi.WindowsInternal) ConfError("LoopbackApi WindowsInternal is not valid for Linux.");

        if (string.IsNullOrWhiteSpace(VisualizerPath)) ConfError("VisualizerPath is required.");
        PathValidation(VisualizerPath);
        PathValidation(PlaylistPath);
        PathValidation(TexturePath);
        PathValidation(FXPath);
        PathValidation(CrossfadePath);

        if (string.IsNullOrWhiteSpace(ScreenshotPath)) ScreenshotPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (PathHelper.GetIndividualPaths(ScreenshotPath).Length > 1) ConfError("Exactly one path is required for ScreenshotPath.");
        PathValidation(ScreenshotPath);

        if (PathHelper.GetIndividualPaths(FFmpegPath).Length > 1) ConfError("Exactly one path is required for FFmpegPath.");
        PathValidation(FFmpegPath);

        if (!string.IsNullOrWhiteSpace(NDIReceiveFrom) && !string.IsNullOrWhiteSpace(SpoutReceiveFrom)) ConfError("Only one streaming source can be specified (SpoutReceiveFrom or NDIReceiveFrom)");

        if (string.IsNullOrWhiteSpace(NDIDeviceName)) NDIDeviceName = "Monkey Hi Hat";
        if (!string.IsNullOrWhiteSpace(NDIReceiveFrom) && (NDIReceiveFrom.IndexOf('(') == -1 || NDIReceiveFrom.IndexOf(')') == -1)) ConfError("NDIReceiveFrom must be in the format MACHINE_NAME (SENDER NAME)");
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
