
using eyecandy;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Spout.Interop;
using StbImageSharp;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace mhh;

/// <summary>
/// The window owns the visualizer definition and instance, the eyecandy
/// audio texture and capture processing, and supplies the "resolution"
/// and "time" uniforms. The visualizers do most of the other work.
/// </summary>
public class HostWindow : BaseWindow, IDisposable
{
    /// <summary>
    /// Handles all visualization rendering prep and execution.
    /// </summary>
    public RenderManager Renderer;

    /// <summary>
    /// Handles playlist content.
    /// </summary>
    public PlaylistManager Playlist;

    /// <summary>
    /// Handles test-mode content.
    /// </summary>
    public TestModeManager Tester;

    /// <summary>
    /// Audio and texture processing by the eyecandy library.
    /// </summary>
    public AudioTextureEngine Eyecandy;

    /// <summary>
    /// A random 0-1 float that will not change for the duration of the program. The
    /// uniform name is "randomseed".
    /// </summary>
    public float UniformRandomSeed;
    
    /// <summary>
    /// A random 0-1 float that is generated for each new frame. The uniform name
    /// is "randomnumber".
    /// </summary>
    public float UniformRandomNumber;

    /// <summary>
    /// The current date (year, month, date, seconds since midnight)
    /// </summary>
    public Vector4 UniformDate;

    /// <summary>
    /// The current time (hour, minute, seconds, UTC hour)
    /// </summary>
    public Vector4 UniformClockTime;

    /// <summary>
    /// Indicates whether an FX shader is running (0-no, 1-yes).
    /// </summary>
    public float UniformFXActive;

    /// <summary>
    /// Indicates whether the audio processor indicates silence (0-no, 1-yes).
    /// </summary>
    public float UniformSilenceDetected;

    private MethodInfo EyecandyEnableMethod;
    private MethodInfo EyecandyDisableMethod;
    // Example of how to invoke generic method
    //    // AudioTextureEngine.Create<TextureType>(uniform, assignedTextureUnit, multiplier, enabled)
    //    var method = EyecandyCreateMethod.MakeGenericMethod(TextureType);
    //    method.Invoke(Eyecandy, new object[]
    //    {
    //        uniformName,
    //        multiplier,
    //        defaultEnabled,
    //    });


    private CommandRequest CommandRequested = CommandRequest.None;
    private bool IsPaused = false;

    private bool OnResizeFired = false;
    private bool TrackingSilentPeriod = false;

    private object QueuedConfigLock = new();
    private VisualizerConfig QueuedVisualizerConfig = null;
    private FXConfig QueuedFXConfig = null;
    private string QueuedCrossfadePathname = string.Empty;

    private Random RNG = new();

    private const int SpotifyCheckMillisec = 500;
    private const string SpotifyProcessName = "SPOTIFY";
    private const string SpotifyUnavailableMessage = "Spotify information is not available";
    private const char MusicNote = (char)11;
    private DateTime NextSpotifyCheck = DateTime.MaxValue;
    private string SpotifyTrackInfo = SpotifyUnavailableMessage;

    private SpoutSender SpoutSender;
    private const string SpoutSenderName = "Monkey Hi Hat";

    private NDISenderManager NDISender;

    private static readonly ILogger Logger = LogHelper.CreateLogger(nameof(HostWindow));

    public HostWindow(EyeCandyWindowConfig windowConfig, EyeCandyCaptureConfig audioConfig)
        : base(windowConfig, createShaderFromConfig: false)
    {
        Logger?.LogTrace(nameof(HostWindow));

        Eyecandy = new(audioConfig);
        EyecandyEnableMethod = Eyecandy.GetType().GetMethod("Enable");
        EyecandyDisableMethod = Eyecandy.GetType().GetMethod("Disable");

        // TODO default these to enabled: false
        Eyecandy.Create<AudioTextureWaveHistory>("eyecandyWave", enabled: true);
        Eyecandy.Create<AudioTextureFrequencyDecibelHistory>("eyecandyFreqDB", enabled: true);
        Eyecandy.Create<AudioTextureFrequencyMagnitudeHistory>("eyecandyFreqMag", enabled: true);
        Eyecandy.Create<AudioTextureWebAudioHistory>("eyecandyWebAudio", enabled: true);
        Eyecandy.Create<AudioTextureShadertoy>("eyecandyShadertoy", enabled: true);
        Eyecandy.Create<AudioTexture4ChannelHistory>("eyecandy4Channel", enabled: true);
        Eyecandy.Create<AudioTextureVolumeHistory>("eyecandyVolume", enabled: true);
        Eyecandy.EvaluateRequirements();

        RenderingHelper.ClientSize = ClientSize;
        UniformRandomSeed = (float)RNG.NextDouble();

        InitializeCache();

        Playlist = new();
        Renderer = new();
    }

    /// <summary>
    /// The host window's base class sets the background color, then the active visualizer is invoked.
    /// </summary>
    protected override void OnLoad()
    {
        base.OnLoad();
        GL.Enable(EnableCap.ProgramPointSize);
        Renderer.PrepareNewRenderer(Caching.IdleVisualizer);
        Eyecandy.BeginAudioProcessing();

        if (Program.AppConfig.SpoutSender)
        {
            SpoutSender = new();
            SpoutSender.SetSenderName(SpoutSenderName);
        }

        if (Program.AppConfig.NDISender)
        {
            NDISender = new NDISenderManager(Program.AppConfig.NDIDeviceName, Program.AppConfig.NDIGroupList);
        }

        if (Program.AppConfig.ShowSpotifyTrackPopups) NextSpotifyCheck = DateTime.Now.AddMilliseconds(SpotifyCheckMillisec);
    }

    /// <summary>
    /// The host window's base class clears the background, then the host window updates any audio
    /// textures and sets the audio texture uniforms, as well as the "resolution" and "time" uniforms,
    /// then invokes the active visualizer. Finally, buffers are swapped and FPS is calculated.
    /// </summary>
    protected override void OnRenderFrame(FrameEventArgs e)
    {
        if (Renderer.ActiveRenderer is null) return;
        base.OnRenderFrame(e);

        Eyecandy.UpdateTextures();

        UniformRandomNumber = (float)RNG.NextDouble();
        UniformDate = new(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, (float)DateTime.Now.TimeOfDay.TotalSeconds);
        UniformClockTime = new(DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.UtcNow.Hour);
        UniformFXActive = Renderer.FXActive();
        UniformSilenceDetected = Eyecandy.IsSilent ? 1 : 0;

        Renderer.RenderFrame();

        SwapBuffers();
        CalculateFPS();

        // All zeros means use default framebuffer and auto-detect size
        _ = SpoutSender?.SendFbo(0, 0, 0, true);

        NDISender?.SendVideoFrame();

        // Starts hidden to avoid a white flicker before the first frame is rendered.
        if (!IsVisible) IsVisible = true;
    }

    /// <summary>
    /// Processes keystrokes, pending requested commands, queued viz/fx configs, and
    /// polls for Spotify track changes.
    /// </summary>
    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        // On Windows, Update/Render events are suspended during resize operations, so
        // if this is true, resizing has been completed.
        if (OnResizeFired)
        {
            OnResizeFired = false;
            RenderingHelper.ClientSize = ClientSize;
            Renderer.OnResize();
            NDISender?.PrepareVideoFrame();
            return;
        }

        var input = KeyboardState;

        // ESC to quit
        if (input.IsKeyReleased(Keys.Escape))
        {
            // set the flag to ensure the render callback starts
            // short-circuiting before we start releasing stuff
            CommandRequested = CommandRequest.Quit;
            return;
        }

        // process pending command
        switch (CommandRequested)
        {
            case CommandRequest.Quit:
            {
                CommandRequested = CommandRequest.None;
                Close();
                return;
            }

            case CommandRequest.ToggleFullscreen:
            {
                switch(WindowState)
                {
                    case WindowState.Fullscreen:
                        Logger?.LogDebug($"OnUpdateFrame changing WindowState from {WindowState} to Normal");
                        WindowState = WindowState.Normal;
                        break;

                    default:
                        Logger?.LogDebug($"OnUpdateFrame changing WindowState from {WindowState} to Fullscreen");
                        WindowState = WindowState.Fullscreen;
                        break;
                }
                CommandRequested = CommandRequest.None;
                return;
            }

            case CommandRequest.SnapshotNowJpg:
            case CommandRequest.SnapshotNowPng:
            {
                Logger?.LogInformation("Saving screenshot");
                Renderer.ScreenshotHandler = new(CommandRequested);
                CommandRequested = CommandRequest.None;
                return;
            }

            case CommandRequest.SnapshotSpacebarJpg:
            case CommandRequest.SnapshotSpacebarPng:
            {
                if(input.IsKeyReleased(Keys.Space))
                {
                    Renderer.ScreenshotHandler = new(CommandRequested);
                    CommandRequested = CommandRequest.None;
                    return;
                }
                break;
            }

            default:
                CommandRequested = CommandRequest.None;
                break;
        }

        // Test-mode
        if(Tester is not null && Tester.Mode != TestMode.None)
        {
            if (input.IsKeyReleased(Keys.Q)) Command_Test(TestMode.None);
            if (input.IsKeyReleased(Keys.R)) Tester.Reload();
            if (input.IsKeyReleased(Keys.KeyPadAdd) || input.IsKeyReleased(Keys.Equal)) Tester.Next();
            if (input.IsKeyReleased(Keys.KeyPadSubtract) || input.IsKeyReleased(Keys.Minus)) Tester.Previous();

            if (input.IsKeyReleased(Keys.Q)
                || input.IsKeyReleased(Keys.R)
                || input.IsKeyReleased(Keys.KeyPadAdd) 
                || input.IsKeyReleased(Keys.Equal)
                || input.IsKeyReleased(Keys.KeyPadSubtract)
                || input.IsKeyReleased(Keys.Minus))
                return;
        }

        // Text overlay text commands
        if (input.IsKeyReleased(Keys.V)) Command_Show("viz");
        if (input.IsKeyReleased(Keys.S)) Command_Show("stats");
        if (input.IsKeyReleased(Keys.D)) Command_Show("debug");
        if (input.IsKeyReleased(Keys.Comma)) Command_Show("toggle");
        if (input.IsKeyReleased(Keys.Period)) Command_Show("clear");
        if (input.IsKeyReleased(Keys.G)) Command_Show("grid");
        if (input.IsKeyReleased(Keys.P)) Command_Show("popups");
        if (input.IsKeyReleased(Keys.W)) Command_Show("what");
        if (input.IsKeyReleased(Keys.T)) Command_Show("track");

        // Backspace for immediate JPG screenshot
        if (input.IsKeyReleased(Keys.Backspace))
        {
            CommandRequested = CommandRequest.SnapshotNowJpg;
            return;
        }

        // Right-arrow for next in playlist
        if (input.IsKeyReleased(Keys.Right))
        {
            Command_PlaylistNext(temporarilyIgnoreSilence: true);
            return;
        }

        // Down-arrow for next FX during playlist
        if (input.IsKeyReleased(Keys.Down))
        {
            Command_PlaylistNextFX();
            return;
        }

        // Playlist extend run time by 1 minute
        if (input.IsKeyReleased(Keys.X))
        {
            Playlist?.AddOneMinute();
        }

        // Playlist extend run time indefinitely
        if (input.IsKeyReleased(Keys.A))
        {
            Playlist?.PauseAutoAdvance();
        }

        // Spacebar to toggle full-screen mode
        if (input.IsKeyReleased(Keys.Space))
        {
            CommandRequested = CommandRequest.ToggleFullscreen;
            return;
        }

        // Enter to change monitors in full-screen mode
        if (WindowState == WindowState.Fullscreen && input.IsKeyReleased(Keys.Enter))
        {
            var mon = Monitors.GetMonitors();
            if (mon.Count < 2) return;

            var cur = Monitors.GetMonitorFromWindow(Program.AppWindow);
            var idx = 0;
            
            for(int i = 0; i < mon.Count; i++)
            {
                if (cur.Handle.Pointer == mon[i].Handle.Pointer)
                {
                    idx = i + 1;
                    if (idx == mon.Count) idx = 0;
                    break;
                }
            }

            Logger?.LogInformation($"Switching to monitor {idx + 1} - {mon[idx].Name}");
            MakeFullscreen(mon[idx].Handle);
        }

        double duration = DetectSilence();
        if (Program.AppConfig.DetectSilenceSeconds > 0 && duration >= Program.AppConfig.DetectSilenceSeconds)
        {
            // long-duration silence switches to the lower-overhead Idle or Blank viz
            RespondToSilence(duration);
            return;
        }

        // playlists can be configured to advance after a short duration of silence
        Playlist.UpdateFrame(duration);

        // if the CommandLineSwitchPipe thread processed a request
        // to use a different shader, process here where we know
        // the GLFW thread won't be trying to use the Shader object
        lock (QueuedConfigLock)
        {
            bool exit = false;
            if(QueuedVisualizerConfig is not null)
            {
                Renderer.PrepareNewRenderer(QueuedVisualizerConfig, QueuedCrossfadePathname);
                QueuedVisualizerConfig = null;
                QueuedCrossfadePathname = string.Empty;
                exit = true;

                if (Program.AppConfig.ShowPlaylistPopups 
                    && Playlist?.ActivePlaylist is not null
                    && QueuedFXConfig is null) 
                    RenderManager.TextManager.SetPopupText(Renderer.GetPopupText());
            }

            if (QueuedFXConfig is not null && Renderer.ApplyFX(QueuedFXConfig))
            {
                QueuedFXConfig = null;
                exit = true;

                if (Program.AppConfig.ShowPlaylistPopups
                    && Playlist?.ActivePlaylist is not null)
                    RenderManager.TextManager.SetPopupText(Renderer.GetPopupText());
            }

            if (exit) return;
        }

        // was a secondary command queued?
        if(Program.QueuedArgs?.Length > 0)
        {
            Program.ProcessSwitches(Program.QueuedArgs);
            Program.QueuedArgs = null;
            return;
        }

        if(Program.AppConfig.ShowSpotifyTrackPopups && DateTime.Now >= NextSpotifyCheck)
        {
            var p = Process.GetProcessesByName(SpotifyProcessName);
            if(p.Length > 0)
            {
                if (p[0].MainWindowTitle != SpotifyTrackInfo)
                {
                    SpotifyTrackInfo = p[0].MainWindowTitle;
                    if (SpotifyTrackInfo.StartsWith("Spotify"))
                    {
                        SpotifyTrackInfo = SpotifyUnavailableMessage;
                    }
                    else
                    {
                        RenderManager.TextManager.SetPopupText(GetTrackForDisplay());
                    }
                }
            }
            else
            {
                SpotifyTrackInfo = SpotifyUnavailableMessage;
            }
            NextSpotifyCheck = DateTime.Now.AddMilliseconds(SpotifyCheckMillisec);
        }
    }

    /// <summary>
    /// Only sets a flag, as this will fire continuously as the user drags the
    /// window border. During resize, OnUpdateFrame is suspended, so the actual
    /// resize response happens in that event when the flag is set.
    /// </summary>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        //Console.WriteLine($"OnResize ClientSize {ClientSize.X},{ClientSize.Y}  WindowState {WindowState}");
        OnResizeFired = true;
    }

    /// <summary>
    /// Handler for the --load command-line switch.
    /// </summary>
    public string Command_Load(string visualizerConfPathname, string fxConfPathname = "", bool terminatesPlaylist = true)
    {
        Logger?.LogTrace($"{nameof(Command_Load)} viz {visualizerConfPathname} fx {fxConfPathname}");

        var newViz = new VisualizerConfig(visualizerConfPathname);
        if (newViz.ConfigSource.Content.Count == 0)
        {
            var err = $"Unable to load visualizer configuration {newViz.ConfigSource.Pathname}";
            Logger?.LogError(err);
            return $"ERR: {err}";
        }

        if (terminatesPlaylist) Playlist.TerminatePlaylist();

        QueueVisualization(newViz);

        if (!string.IsNullOrWhiteSpace(fxConfPathname))
        {
            var fx = Command_ApplyFX(fxConfPathname);
            if (fx.StartsWith("ERR:")) fxConfPathname = null;
        }

        var msg = $"Requested visualizer {newViz.ConfigSource.Pathname}{(string.IsNullOrWhiteSpace(fxConfPathname) ? "" : $" with FX {fxConfPathname}")}";
        Logger?.LogInformation(msg);
        return msg;
    }

    /// <summary>
    /// Handler for the --fx command-line switch.
    /// </summary>
    public string Command_ApplyFX(string fxConfPathname)
    {
        Logger?.LogTrace($"{nameof(Command_ApplyFX)} fx {fxConfPathname}");

        var fx = new FXConfig(fxConfPathname);
        if(fx.ConfigSource.Content.Count == 0)
        {
            var err = $"Unable to load FX configuration {fx.ConfigSource.Pathname}";
            Logger?.LogError(err);
            return $"ERR: {err}";
        }
        QueueFX(fx);
        var msg = $"Requested FX {fx.ConfigSource.Pathname}";
        Logger?.LogInformation(msg);
        return msg;
    }

    /// <summary>
    /// Handler for the --fade command-line switch.
    /// </summary>
    public string Command_QueueCrossfade(string fadeFragPathname)
    {
        Logger?.LogTrace($"{nameof(Command_QueueCrossfade)} frag {fadeFragPathname}");

        QueuedCrossfadePathname = fadeFragPathname;
        return $"Queued crossfader {fadeFragPathname}";
    }

    /// <summary>
    /// Loads and begins using a playlist.
    /// </summary>
    public string Command_Playlist(string playlistConfPathname)
    {
        Logger?.LogTrace($"{nameof(Command_Playlist)} {playlistConfPathname}");

        return Playlist.StartNewPlaylist(playlistConfPathname);
    }

    /// <summary>
    /// Advances to the next visualization when a playlist is active.
    /// </summary>
    public string Command_PlaylistNext(bool temporarilyIgnoreSilence = false)
    {
        Logger?.LogTrace($"{nameof(Command_PlaylistNext)}");

        return Playlist.NextVisualization(temporarilyIgnoreSilence);
    }

    /// <summary>
    /// Applies a post-processing FX even if one wasn't planned.
    /// </summary>
    public string Command_PlaylistNextFX()
    {
        Logger?.LogTrace($"{nameof(Command_PlaylistNextFX)}");

        return Playlist.ApplyFX();
    }

    /// <summary>
    /// Handler for the --quit command-line switch.
    /// </summary>
    public string Command_Quit()
    {
        Logger?.LogTrace($"{nameof(Command_Quit)}");

        CommandRequested = CommandRequest.Quit;
        return "ACK";
    }

    /// <summary>
    /// Handler for the --info command-line switch.
    /// </summary>
    public string Command_Info()
    {
        Logger?.LogTrace($"{nameof(Command_Info)}");

        var msg =
$@"{GetStatistics()}
{Renderer.GetInfo()}
playlist   : {Playlist.GetInfo()}";

        Logger?.LogInformation(msg);
        return msg;
    }

    /// <summary>
    /// Handler for the --fullscreen command-line switch.
    /// </summary>
    public string Command_FullScreen()
    {
        Logger?.LogTrace($"{nameof(Command_FullScreen)}");

        CommandRequested = CommandRequest.ToggleFullscreen;
        return "ACK";
    }

    /// <summary>
    /// Handler for the --jpg and --png command-line switches.
    /// </summary>
    public string Command_Screenshot(CommandRequest commandRequest)
    {
        Logger?.LogTrace($"{nameof(Command_Screenshot)} {commandRequest}");

        if (CommandRequested != CommandRequest.None) return "ERR: A command is already pending";

        CommandRequested = commandRequest;

        if (commandRequest == CommandRequest.SnapshotSpacebarJpg || commandRequest == CommandRequest.SnapshotSpacebarPng)
        {
            Program.AppWindow.Focus();
            return "ACK - press spacebar to save image";
        }

        return "ACK - saving image";
    }

    /// <summary>
    /// Handler for the --idle command-line switch.
    /// </summary>
    public string Command_Idle()
    {
        Logger?.LogTrace($"{nameof(Command_Idle)}");

        Playlist.TerminatePlaylist();
        QueueVisualization(Caching.IdleVisualizer);
        return "ACK";
    }

    /// <summary>
    /// Handler for the --pause command-line switch.
    /// </summary>
    public string Command_Pause()
    {
        Logger?.LogTrace($"{nameof(Command_Pause)}");

        if (IsPaused) return "already paused; use --run to resume";
        Renderer.TimePaused = true;
        IsPaused = true;
        return "ACK";
    }

    /// <summary>
    /// Handler for the --run command-line switch.
    /// </summary>
    public string Command_Run()
    {
        Logger?.LogTrace($"{nameof(Command_Run)}");

        if (!IsPaused) return "already running; use --pause to suspend";
        Renderer.TimePaused = false;
        IsPaused = false;
        return "ACK";
    }

    /// <summary>
    /// Handler for the --reload command-line switch.
    /// </summary>
    public string Command_Reload()
    {
        Logger?.LogTrace($"{nameof(Command_Reload)}");

        if (Renderer.ActiveRenderer is CrossfadeRenderer || Renderer.ActiveRenderer is FXRenderer) return "ERR - Crossfade or FX is active";

        var filename = Renderer.ActiveRenderer.Filename;
        var pathname = PathHelper.FindConfigFile(Program.AppConfig.VisualizerPath, filename);
        if (pathname is null) return $"ERR - {filename} not found in shader path(s)";

        var newViz = new VisualizerConfig(pathname);
        if (newViz.ConfigSource.Content.Count == 0)
        {
            var err = $"Unable to load visualizer configuration {newViz.ConfigSource.Pathname}";
            Logger?.LogError(err);
            return $"ERR: {err}";
        }

        QueueVisualization(newViz, replaceCachedShader: true);
        var msg = $"Reloading {newViz.ConfigSource.Pathname}";
        Logger?.LogInformation(msg);
        return msg;
    }

    /// <summary>
    /// Handler for the --nocache command-line switch
    /// </summary>
    public string Command_DisableCaching()
    {
        Logger?.LogTrace($"{nameof(Command_DisableCaching)}");

        Caching.VisualizerShaders.CachingDisabled = true;
        Caching.FXShaders.CachingDisabled = true;
        Caching.LibraryShaders.CachingDisabled = true;
        return "ACK";
    }

    /// <summary>
    /// Handler for the --show command-line switches
    /// </summary>
    public string Command_Show(string flag)
    {
        Logger?.LogTrace($"{nameof(Command_Show)} {flag}");

        switch (flag.ToLowerInvariant())
        {
            case "viz":
                RenderManager.TextManager.SetOverlayText(Renderer.GetPopupText);
                break;

            case "stats":
                RenderManager.TextManager.SetOverlayText(GetStatistics);
                break;

            case "grid":
                RenderManager.TextManager.SetOverlayText(() =>
@"LINE 01---20xxx5xxxx30xxx5xxxx40xxx5xxxx50xxx5xxxx60xxx5xxxx70xxx5xxxx80xxx5xxxx90xxx5xxxx
LINE 02
LINE 03
LINE 04
LINE 05
LINE 06
LINE 07
LINE 08
LINE 09
LINE 10
LINE 11
LINE 12
LINE 13
LINE 14
LINE 15");
                break;

            case "track":
                RenderManager.TextManager.SetPopupText(GetTrackForDisplay());
                break;

            case "popups":
                Program.AppConfig.ShowPlaylistPopups = !Program.AppConfig.ShowPlaylistPopups;
                return $"ACK (popups {(Program.AppConfig.ShowPlaylistPopups ? "will be shown" : "are disabled")})";

            case "what":
                RenderManager.TextManager.SetPopupText(Renderer.GetPopupText());
                break;

            case "debug":
                RenderManager.TextManager.SetPopupText(Renderer.GetPopupText());
                break;

            case "toggle":
                return RenderManager.TextManager.TogglePermanence();

            case "clear":
                RenderManager.TextManager.Reset();
                break;

            default:
                return "ERR: Unrecognized argument.";
        }
        return "ACK";
    }

    /// <summary>
    /// Handler for the --display command-line switch
    /// </summary>
    public string Command_Display()
    {
        Logger?.LogTrace($"{nameof(Command_Display)}");

        var mon = Monitors.GetMonitors();
        var cur = Monitors.GetMonitorFromWindow(Program.AppWindow);
        var idx = 0;

        StringBuilder msg = new("Monitors:\n");
        for(int i = 0; i < mon.Count; i++)
        {
            msg.AppendLine($"  {i + 1} name: {mon[i].Name}");
            msg.AppendLine($"  {i + 1} area: {mon[i].ClientArea}");
            if (mon[i].Handle.Pointer == cur.Handle.Pointer) idx = i;
        }
        msg.AppendLine($"Window:");
        msg.AppendLine($"   state: {WindowState}");
        msg.AppendLine($"   coord: ({Location.X},{Location.Y}) - ({Size.X},{Size.Y})");
        msg.AppendLine($"  screen: {idx} - {cur.Name}");

        Logger?.LogInformation(msg.ToString());
        return msg.ToString();
    }

    /// <summary>
    /// Handler for the --test and --endtest command-line switches
    /// </summary>
    public string Command_Test(TestMode mode, string filename = "")
    {
        Logger?.LogTrace($"{nameof(Command_Test)} {mode} {filename}");

        if (Tester is not null)
        {
            Tester.EndTest();
            Tester.Dispose();
            Tester = null;
        }
        if (mode == TestMode.None) return "ACK";
        var validation = TestModeManager.Validate(mode, filename);
        if (!string.IsNullOrEmpty(validation)) return validation;
        Tester = new TestModeManager(mode, filename);
        if(Tester.Mode == TestMode.None)
        {
            Tester.Dispose();
            Tester = null;
            return "ERR: TestModeManager did not start correctly";
        }
        return "ACK";
    }

    /// <summary>
    /// Clears the running-instance console.
    /// </summary>
    public string Command_CLS()
    {
        Console.Clear();
        return "ACK";
    }

    /// <summary>
    /// Queues a new visualizer to send to the RenderManager on the next OnUpdateFrame pass.
    /// </summary>
    private void QueueVisualization(VisualizerConfig newVisualizerConfig, bool replaceCachedShader = false)
    {
        Logger?.LogTrace($"{nameof(QueueVisualization)} {newVisualizerConfig.ConfigSource.Pathname} replaceCachedShader {replaceCachedShader}");

        lock (QueuedConfigLock)
        {
            // CommandLineSwitchPipe invokes this from another thread;
            // actual update occurs in OnUpdateFrame which is "safe"
            // because it won't be busy doing things like using the
            // current Shader object in an OnRenderFrame call.
            QueuedVisualizerConfig = newVisualizerConfig;

            // When the --reload command has been issued we want to compile a fresh copy.
            RenderingHelper.ReplaceCachedShader = replaceCachedShader;

            // Wipe this out, although it may be set again for viz+fx scenarios
            QueuedFXConfig = null;
        }
    }

    /// <summary>
    /// Queues a new FX to send to the RenderManager on the next OnUpdateFrame pass.
    /// </summary>
    private void QueueFX(FXConfig fxConfig)
    {
        Logger?.LogTrace($"{nameof(QueueFX)} {fxConfig.ConfigSource.Pathname}");

        lock (QueuedConfigLock)
        {
            // CommandLineSwitchPipe invokes this from another thread;
            // actual update occurs in OnUpdateFrame which is "safe"
            // because it won't be busy doing things like using the
            // current Shader object in an OnRenderFrame call.
            QueuedFXConfig = fxConfig;

            // This is never invoked with the --reload command.
            RenderingHelper.ReplaceCachedShader = false;
        }
    }

    private double DetectSilence()
    {
        if (Eyecandy.IsSilent)
        {
            if (!TrackingSilentPeriod)
            {
                TrackingSilentPeriod = true;
            }
            else
            {
                return DateTime.Now.Subtract(Eyecandy.SilenceStarted).TotalSeconds;
            }
        }
        else
        {
            if (TrackingSilentPeriod)
            {
                TrackingSilentPeriod = false;
                return Eyecandy.SilenceEnded.Subtract(Eyecandy.SilenceStarted).TotalSeconds;
            }
        }

        return 0;
    }

    /// <summary>
    /// Implements the configured action when silence is detected by OnUpdateFrame
    /// </summary>
    private void RespondToSilence(double duration)
    {
        Logger?.LogDebug($"Long-term silence detected (duration: {duration:0.####} sec");

        Playlist.TerminatePlaylist();

        lock (QueuedConfigLock)
        {
#pragma warning disable CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
            QueuedVisualizerConfig = Program.AppConfig.DetectSilenceAction switch
            {
                SilenceAction.Blank => Caching.BlankVisualizer,
                SilenceAction.Idle => Caching.IdleVisualizer,
            };
#pragma warning restore CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
        }
    }

    private void InitializeCache()
    {
        Caching.VisualizerShaders = new(Program.AppConfig.ShaderCacheSize);
        Caching.FXShaders = new(Program.AppConfig.FXCacheSize);
        Caching.LibraryShaders = new(Program.AppConfig.LibraryCacheSize);

        Caching.IdleVisualizer = new(Path.Combine(ApplicationConfiguration.InternalShaderPath, "idle.conf"));
        Caching.BlankVisualizer = new(Path.Combine(ApplicationConfiguration.InternalShaderPath, "blank.conf"));

        Caching.InternalCrossfadeShader = new(
            ApplicationConfiguration.PassthroughVertexPathname,
            Path.Combine(ApplicationConfiguration.InternalShaderPath, "crossfade.frag"));

        if (!Caching.InternalCrossfadeShader.IsValid)
        {
            Logger?.LogCritical("Internal crossfade shader was not found or failed to compile.");
            Console.WriteLine("\n\nFATAL ERROR: Internal crossfade shader was not found or failed to compile.\n\n");
            Thread.Sleep(250);
            Environment.Exit(-1);
        }

        Caching.TextShader = new(
            ApplicationConfiguration.PassthroughVertexPathname,
            Path.Combine(ApplicationConfiguration.InternalShaderPath, "text.frag"));

        if (!Caching.TextShader.IsValid)
        {
            Logger?.LogCritical("Internal crossfade shader was not found or failed to compile.");
            Console.WriteLine("\n\nFATAL ERROR: Internal text shader was not found or failed to compile.\n\n");
            Thread.Sleep(250);
            Environment.Exit(-1);
        }

        if(Program.AppConfig.RandomizeCrossfade)
        {
            // in v5.0.0 and earlier, crossfades were stored in the viz path with a crossfade_ prefix
            //var files = PathHelper.GetWildcardFiles(Program.AppConfig.VisualizerPath, "crossfade_*.frag", returnFullPathname: true);

            var files = PathHelper.GetWildcardFiles(Program.AppConfig.CrossfadePath, "*.frag", returnFullPathname: true);
            if (files.Count > 0)
            {
                Caching.CrossfadeShaders = new(files.Count);
                foreach(var pathname in files)
                {
                    var shader = new CachedShader(ApplicationConfiguration.PassthroughVertexPathname, pathname);
                    if (shader.IsValid) Caching.CrossfadeShaders.Add(shader);
                }
            }

            if(files.Count == 0 || Caching.CrossfadeShaders?.Count == 0)
            {
                Logger?.LogWarning("No crossfade shaders found, or none compiled successfully; disabling RandomizeCrossfade setting.");
                Program.AppConfig.RandomizeCrossfade = false;
            }
        }

        using var stream = File.OpenRead(Path.Combine(ApplicationConfiguration.InternalShaderPath, "badtexture.jpg"));
        StbImage.stbi_set_flip_vertically_on_load(1); // OpenGL origin is bottom left instead of top left
        Caching.BadTexturePlaceholder = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        // see MaxAvailableTextureUnit property comments for an explanation
        GL.GetInteger(GetPName.MaxCombinedTextureImageUnits, out var maxTU);
        Caching.MaxAvailableTextureUnit = maxTU - 1 - Caching.KnownAudioTextures.Count;
        Logger?.LogInformation($"This GPU supports a combined maximum of {maxTU} TextureUnits.");
    }

    private string GetStatistics() =>
$@"frame rate : {FramesPerSecond}
average fps: {AverageFramesPerSecond} (past {AverageFPSTimeframeSeconds} sec)
target fps : {(UpdateFrequency == 0 ? "unlimited" : UpdateFrequency)}
display res: {ClientSize.X} x {ClientSize.Y}";

    private string GetTrackForDisplay()
        => $"{MusicNote} {SpotifyTrackInfo.Replace(" - ", $"\n{MusicNote} ")}";

    public new void Dispose()
    {
        if (IsDisposed) return;
        Logger?.LogTrace("Disposing");

        base.Dispose();

        SpoutSender?.Dispose();

        NDISender?.Dispose();

        var success = Eyecandy?.EndAudioProcessing();
        Logger?.LogTrace($"Dispose Eyecandy.EndAudioProcessing success: {success}");

        Tester?.Dispose();

        Eyecandy?.Dispose();

        Caching.VisualizerShaders.Dispose();
        Caching.FXShaders.Dispose();
        Caching.LibraryShaders.Dispose();
        Caching.InternalCrossfadeShader.Dispose();
        if (Caching.CrossfadeShaders?.Count > 0)
        {
            foreach(var s in Caching.CrossfadeShaders)
            {
                s.Dispose();
            }
            Caching.CrossfadeShaders = null;
        }
        Caching.TextShader.Dispose();

        Renderer?.Dispose();

        IsDisposed = true;
        GC.SuppressFinalize(true);
    }
    private bool IsDisposed = false;
}
