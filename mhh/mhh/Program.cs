
using CommandLineSwitchPipe;
using eyecandy;
using FFMediaToolkit;
using Microsoft.Extensions.Logging;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Serilog.Extensions.Logging;
using StbImageSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

/*
Program.Main primarily does these things:
-- reads config, prepares logging, goes into standby or starts immediately
-- sets up and runs the VisualizerHostWindow
-- processes switches / args received at runtime

There are no startup switches. Only --help and --devices can be used without
another instance already running.

The last part is accomplished by my CommandLineSwitchPipe library. At startup it
tries to connect to an existing named pipe. If none is found, this instance becomes
the named pipe listener and the program starts, either in standby mode (waiting for
additional commands from another instance), or loading the window and the idle viz.

However, if a named pipe is found, any args are passed to the already-running program.
If a response is received, it is written to the console and the secondary instance ends.
For example, the frame rate of the running instance can be queried.
*/

namespace mhh;

public class Program
{
    static readonly string ConfigFilename = "mhh.conf";
    static readonly string DebugConfigFilename = "mhh.debug.conf";

    static readonly string SwitchPipeName = "monkey-hi-hat";

    // Previously MHH only supported core API v4.6, the "final" OpenGL, but Linux MESA
    // drivers apparently only support v4.5 (according to "glxinfo -B" from mesa-utils)
    // and 4.6 features aren't important to MHH, so post-3.1 was reverted to v4.5.
    // https://www.khronos.org/opengl/wiki/History_of_OpenGL#OpenGL_4.6_(2017)
    static readonly Version OpenGLVersion = new(4, 5);

    private static readonly string ConfigLocationEnvironmentVariable =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "monkey-hi-hat-config"
            : "MONKEY_HI_HAT_CONFIG";

    /// <summary>
    /// Version parsed from version.txt in the ConfigFiles directory
    /// </summary>
    public static string VersionNumber;

    /// <summary>
    /// Location of the config file being used
    /// </summary>
    public static string ConfigFilePathname;
    
    /// <summary>
    /// Content parsed from the mhh.conf configuration file and the
    /// default idle shader conf file.
    /// </summary>
    public static ApplicationConfiguration AppConfig;

    /// <summary>
    /// Where the magic happens.
    /// </summary>
    public static HostWindow AppWindow;

    /// <summary>
    /// Allows a set of command-line arguments to be queued while another
    /// set is being processed. This will be passed to ProcessSwitches by
    /// HostWindow's OnWindowUpdate event. The primary use-case is to allow
    /// the program to come out of standby then process a command like --load.
    /// </summary>
    public static string[] QueuedArgs;

    /// <summary>
    /// Provides OS-specific features.
    /// </summary>
    public static IOSInterop OSInterop;
    
    // these will be accepted when MHH is not running
    private static readonly string[] ImmediateOutputSwitches = { "--help", "--longhelp", "--devices", "--cache" };
    private static readonly string[] AutoStartSwitches = { "--load", "--playlist" };
    private static string[] stagedAutoStartSwitches = new string[0];

    // cancel this to terminate the switch server's named pipe.
    private static CancellationTokenSource ctsSwitchPipe;

    internal static bool AppRunning = true; // the window can change this
    private static bool OnStandby = false;

    // only valid after InitializeAndWait
    private static ILogger Logger;

    public static async Task Main(string[] args)
    {
        OSInterop = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? OSInteropWindows.Create()
            : await OSInteropLinux.CreateAsync();
        
        VersionNumber = (await File.ReadAllTextAsync(Path.Combine(".", "ConfigFiles", "version.txt"))).Trim('\n');
        
        try
        {
            if(await InitializeAndWait(args))
            {
                OSInterop.IsConsoleVisible = !AppConfig.HideConsoleAtStartup || (AppConfig.StartInStandby && AppConfig.HideConsoleInStandby);

                AppRunning = true;
                OnStandby = AppConfig.StartInStandby;
                while (AppRunning)
                {
                    if (OnStandby)
                    {
                        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                        {
                            AppRunning = false;
                        }
                        Thread.Yield();
                    }
                    else
                    {
                        RunWindow(); // blocks
                        if (AppRunning && !OnStandby)
                        {
                            if(AppConfig.CloseToStandby)
                            {
                                OnStandby = true;
                                ShowAppInfo();
                            }
                            else
                            {
                                AppRunning = false;
                            }
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        { } // normal, disregard
        catch (Exception ex)
        {
            LogExceptionMessage(ex);
        }
        finally
        {
            // Stephen Cleary says CTS disposal is unnecessary as long as the token is cancelled
            ctsSwitchPipe?.Cancel();
            AppWindow?.Dispose();
            LogHelper.Dispose();
            OSInterop.Dispose();
        }

        // Give the sloooow console time to catch up...
        await Task.Delay(250);
    }

    public static async Task ProcessNonRunningSwitches(string[] args)
    {
        Logger?.LogInformation($"Processing switches: {string.Join(" ", args)}");
        switch (args[0].ToLowerInvariant())
        {
            case "--devices":
                OSInterop.ListAudioDevices();
                break;
            
            case "--cache":
                if (args.Length == 1 || args.Length > 3)
                {
                    Console.WriteLine(ShowLongHelp());
                    break;
                }
                await ProcessCacheSwitches(args);
                break;
            
            case "--longhelp":
                Console.WriteLine(ShowLongHelp());
                break;
            
            default:
                Console.WriteLine(ShowHelp());
                break;
        }
    }

    public static string ProcessSwitches(string[] args)
    {
        if (args.Length == 0) return ShowHelp();

        Logger?.LogInformation($"Processing switches: {string.Join(" ", args)}");

        switch (args[0].ToLowerInvariant())
        {
            case "--load":
                if (OnStandby) return QueueAndExitStandby(args);
                if (args.Length > 3) return ShowHelp();
                var vizPathname = GetVisualizerPathname(args[1]);
                if (vizPathname is null) return "ERR: Visualizer not found.";
                if (args.Length == 2) return AppWindow.Command_Load(vizPathname);
                var vizfxPathname = GetFxPathname(args[2]);
                if (vizfxPathname is null) return "ERR: FX not found.";
                return AppWindow.Command_Load(vizPathname, vizfxPathname);

            case "--playlist":
                if (args.Length != 2) return ShowHelp();
                var playlistPathname = GetPlaylistPathname(args[1]);
                if (playlistPathname is null) return "ERR: Playlist not found.";
                if (OnStandby) return QueueAndExitStandby(args);
                return AppWindow.Command_Playlist(playlistPathname);

            case "--fx":
                if (OnStandby) return "ERR: Application is in standby";
                if (args.Length != 2) return ShowHelp();
                var fxPathname = GetFxPathname(args[1]);
                if (fxPathname is null) return "ERR: FX not found.";
                return AppWindow.Command_ApplyFX(fxPathname);

            case "--fade":
                if (OnStandby) return "ERR: Application is in standby";
                if (args.Length != 2) return ShowHelp();
                var fadePathname = GetFadePathname(args[1]);
                if (fadePathname is null) return "ERR: Crossfade not found.";
                return AppWindow.Command_QueueCrossfade(fadePathname);

            case "--next":
                if (OnStandby) return "ERR: Application is in standby";
                if (args.Length > 2) return ShowHelp();
                if (args.Length == 2 && args[1].ToLowerInvariant().Equals("fx")) return AppWindow.Command_PlaylistNextFX();
                if (args.Length == 2) return ShowHelp();
                return AppWindow.Command_PlaylistNext();

            case "--list":
            case "--md.list":
                var readable = args[0].ToLowerInvariant().Equals("--list");
                var separator = readable ? "\n" : CommandLineSwitchServer.Options.Advanced.SeparatorControlCode;

                if (args.Length == 2 && args[1].ToLowerInvariant().Equals("viz"))
                {
                    if (string.IsNullOrEmpty(AppConfig.VisualizerPath)) return "ERR: VisualizerPath not defined in mhh.conf.";
                    return GetConfigFiles(AppConfig.VisualizerPath, separator);
                }

                if (args.Length == 2 && args[1].ToLowerInvariant().Equals("playlists"))
                {
                    if (string.IsNullOrEmpty(AppConfig.PlaylistPath)) return "ERR: PlaylistPath not defined in mhh.conf.";
                    return GetConfigFiles(AppConfig.PlaylistPath, separator);
                }

                if (args.Length == 2 && args[1].ToLowerInvariant().Equals("fx"))
                {
                    if (string.IsNullOrEmpty(AppConfig.FXPath)) return "ERR: FXPath not defined in mhh.conf.";
                    return GetConfigFiles(AppConfig.FXPath, separator);
                }

                return readable ? ShowHelp() : string.Empty;

            case "--info":
                if (OnStandby) return "Application is in standby mode, use --standby command to toggle";
                if (args.Length > 1) return ShowHelp();
                return AppWindow.Command_Info();

            case "--show":
                if (OnStandby) return "Application is in standby mode, use --standby command to toggle";
                if (args.Length != 2) return ShowHelp();
                return AppWindow.Command_Show(args[1]);

            case "--fps":
                if (OnStandby) return "ERR: Application is in standby";
                if (args.Length > 2) return ShowHelp();
                if (args.Length == 2)
                {
                    if (!int.TryParse(args[1], out var fpsTarget) || fpsTarget < 0 || fpsTarget > 9999) return ShowHelp();
                    AppWindow.UpdateFrequency = fpsTarget;
                    return (fpsTarget == 0) ? "FPS target disabled (max possible FPS)" : $"FPS target set to {fpsTarget}";
                }
                else
                {
                    return $"{AppWindow.FramesPerSecond} FPS" +
                        $"\n{AppWindow.AverageFramesPerSecond} average FPS over past {AppWindow.AverageFPSTimeframeSeconds} seconds" +
                        $"\nFPS target is {(AppWindow.UpdateFrequency == 0 ? "not locked (max FPS)" : $"locked to {AppWindow.UpdateFrequency} FPS")}";
                }

            case "--display":
                if (OnStandby) return "ERR: Application is in standby";
                if (args.Length > 1) return ShowHelp();
                return AppWindow.Command_Display();

            case "--jpg":
            case "--png":
                if (OnStandby) return "ERR: Application is in standby";
                if (args.Length > 2 || args.Length == 2 && !args[1].Equals("wait", Const.CompareFlags)) return ShowHelp();
                if(args.Length == 1)
                {
                    return (args[0].Equals("--jpg"))
                        ? AppWindow.Command_Screenshot(CommandRequest.SnapshotNowJpg)
                        : AppWindow.Command_Screenshot(CommandRequest.SnapshotNowPng);
                }
                else
                {
                    return (args[0].Equals("--jpg"))
                        ? AppWindow.Command_Screenshot(CommandRequest.SnapshotSpacebarJpg)
                        : AppWindow.Command_Screenshot(CommandRequest.SnapshotSpacebarPng);
                }

            case "--fullscreen":
                if (OnStandby) return "ERR: Application is in standby";
                if (args.Length > 1) return ShowHelp();
                return AppWindow.Command_FullScreen();

            case "--idle":
                if (OnStandby) return "ERR: Application is in standby";
                if (args.Length > 1) return ShowHelp();
                return AppWindow.Command_Idle();

            case "--pause":
                if (OnStandby) return "ERR: Application is in standby";
                if (args.Length > 1) return ShowHelp();
                return AppWindow.Command_Pause();

            case "--run":
                if (OnStandby) return "ERR: Application is in standby";
                if (args.Length > 1) return ShowHelp();
                return AppWindow.Command_Run();

            case "--reload":
                if (OnStandby) return "ERR: Application is in standby";
                if (args.Length > 1) return ShowHelp();
                return AppWindow.Command_Reload();

            case "--pid":
                if (args.Length > 1) return ShowHelp();
                return Environment.ProcessId.ToString();

            case "--log":
                if (args.Length == 1) return $"Current log level {LevelConvert.ToExtensionsLevel(LogHelper.LevelSwitch.MinimumLevel).ToString()}";
                return $"Setting log level {LogHelper.SetLogLevel(args[1])}";

            case "--md.detail":
                if (args.Length != 2) return "ERR: Visualizer name or pathname required.";
                return GetShaderDetail(GetVisualizerPathname(args[1]), "shader");

            case "--md.detailfx":
                if (args.Length != 2) return "ERR: FX name or pathname required.";
                return GetShaderDetail(GetFxPathname(args[1]), "fx");

            case "--nocache":
                if (OnStandby) return "ERR: Application is in standby";
                if (args.Length > 1) return ShowHelp();
                return AppWindow.Command_DisableCaching();

            case "--test":
                if (OnStandby) return "ERR: Application is in standby";
                if (args.Length != 3) return ShowHelp();
                if (!Enum.TryParse<TestMode>(args[1], ignoreCase: true, out var testmode)) return "ERR: Must specify viz, fx, or fade mode";
                if (testmode == TestMode.None) return "ERR: Use --endtest to terminate testing mode";
                return AppWindow.Command_Test(testmode, args[2]);

            case "--endtest":
                return AppWindow.Command_Test(TestMode.None);

            case "--console":
                 OSInterop.IsConsoleVisible = !OSInterop.IsConsoleVisible;
                 return "ACK";

            case "--paths":
                if (args.Length > 1) return ShowHelp();
                return $"\nConfigured paths\n\nVizualization shaders:\n{AppConfig.VisualizerPath.Replace(';','\n')}\n\nPost-processing FX shaders:\n{AppConfig.FXPath.Replace(';', '\n')}\n\nTexure and video files:\n{AppConfig.TexturePath.Replace(';', '\n')}\n\nPlaylists:\n{AppConfig.PlaylistPath.Replace(';', '\n')}\n\nCrossfades:\n{AppConfig.CrossfadePath}\n\nScreenshots:\n{AppConfig.ScreenshotPath}";

            case "--cls":
                return AppWindow.Command_CLS();

            case "--quit":
                if (args.Length > 1) return ShowHelp();
                AppRunning = false;
                if (OnStandby) return "ACK";
                return AppWindow.Command_Quit();

            case "--standby":
                AppWindow?.Command_Quit();
                OnStandby = !OnStandby;
                return "ACK";

            case "--streaming":
                if (OnStandby) return "ERR: Application is in standby";
                return AppWindow?.Command_Streaming(args);

            case "--help":
                return ShowHelp();
            
            case "--longhelp":
                return ShowLongHelp();
            
            default:
                return $"ERR: Switch {args[0].ToLowerInvariant()} unknown, try --help";
        }
    }

    // Return "false" to tell main() to exit after init (another instance is running)
    private static async Task<bool> InitializeAndWait(string[] args)
    {
        Console.Clear();
        Console.WriteLine($"Monkey Hi Hat {VersionNumber}");

        var appConfigFile = FindAppConfig();
        if(appConfigFile is null)
        {
            Console.WriteLine($"\nUnable to locate the \"{ConfigFilename}\" configuration file (or \"{DebugConfigFilename}\" if running with a debugger attached).\n Search sequence is the \"{ConfigLocationEnvironmentVariable}\" environment variable, if defined, then the app directory, then the \"ConfigFile\" app subdirectory.");
            Thread.Sleep(250); // slow-ass console
            return false;
        }

        // Start the switch server and look for another instance
        CommandLineSwitchServer.Options.PipeName = SwitchPipeName;
        var alreadyRunning = await CommandLineSwitchServer.TryConnect().ConfigureAwait(false);

        // Did it fail with an exception?
        if (CommandLineSwitchServer.TryException is not null)
        {
            LogExceptionMessage(CommandLineSwitchServer.TryException);
            Thread.Sleep(250);
            return false;
        }

        // Initialize logging (including setting LoggerFactory in libraries)
        LogHelper.Initialize(appConfigFile, alreadyRunning);
        Logger = LogHelper.CreateLogger(nameof(Program));
        OSInterop.CreateLogger();

        // Show help if running but no switches provided
        if(args.Length == 0 && alreadyRunning)
        {
            Console.WriteLine(ShowHelp());
            return false; // end program
        }

        // Parse the application configuration file
        AppConfig = new ApplicationConfiguration(appConfigFile);

        if (args.Length > 0)
        {
            // Save auto-start switches for later
            if (Array.Exists(AutoStartSwitches, cmd => cmd.Equals(args[0].ToLowerInvariant()))) stagedAutoStartSwitches = args;

            // Immediately process these and exit
            if (Array.Exists(ImmediateOutputSwitches, cmd => cmd.Equals(args[0].ToLowerInvariant())))
            {
                await ProcessNonRunningSwitches(args);
                return false; // end program
            }
        }

        // Currently GLFW is only compatible with X11.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !AppConfig.LinuxSkipX11Check)
        {
            var desktop = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? string.Empty;
            if (desktop.ToLowerInvariant() != "x11")
            {
                throw new GLFWException("Monkey Hi Hat on Linux requires X11. This check can be disabled in config.");
            }
        }

        // Disallow other switches at startup of first instance
        if (!alreadyRunning && stagedAutoStartSwitches.Length == 0 && args.Length > 0)
        {
            Console.WriteLine($"\nOnly these switches are valid when the program is not already running:\n  {string.Join("\n  ", ImmediateOutputSwitches)}\n  {string.Join("\n  ", AutoStartSwitches)}");
            return false; // end program
        }

        Logger?.LogInformation($"Starting (PID {Environment.ProcessId})");

        // Try sending args to an already-running instance...
        if (await CommandLineSwitchServer.TrySendArgs().ConfigureAwait(false))
        {
            Logger?.LogDebug($"Sending switch: {args[0]}");
            Console.WriteLine(CommandLineSwitchServer.QueryResponse);
            return false; // end program
        }

        // ...did it fail with an exception?
        if (CommandLineSwitchServer.TryException is not null)
        {
            LogExceptionMessage(CommandLineSwitchServer.TryException);
            Thread.Sleep(250);
            return false;
        }

        // ...or continue running since we're the first instance (send failed)

        // Start listening for commands
        ctsSwitchPipe = new();
        _ = Task.Run(() => CommandLineSwitchServer.StartServer(ProcessSwitches, ctsSwitchPipe.Token, AppConfig.UnsecuredPort));

        // Prepare video-related settings
        if (!string.IsNullOrWhiteSpace(AppConfig.FFmpegPath))
        {
            FFmpegLoader.FFmpegPath = AppConfig.FFmpegPath;
            RenderingHelper.VideoMediaOptions.FlipVertically = (AppConfig.VideoFlip == VideoFlipMode.FFmpeg);
        }

        ShowAppInfo();
        return true; // continue running
    }

    private static void RunWindow()
    {
        try
        {
            var AudioConfig = new EyeCandyCaptureConfig()
            {
                LoopbackApi = AppConfig.LoopbackApi,
                OpenALContextDeviceName = AppConfig.OpenALContextDeviceName,
                CaptureDeviceName = AppConfig.CaptureDeviceName,
                
                DetectSilence = true, // always detect, playlists may need it
                MaximumSilenceRMS = AppConfig.DetectSilenceMaxRMS,

                MinimumSilenceSeconds = AppConfig.MinimumSilenceSeconds,
                ReplaceSilenceAfterSeconds = AppConfig.ReplaceSilenceAfterSeconds,
                SyntheticDataBPM = AppConfig.SyntheticDataBPM,
                SyntheticDataBeatDuration = AppConfig.SyntheticDataBeatDuration,
                SyntheticDataBeatFrequency = AppConfig.SyntheticDataBeatFrequency,
                SyntheticDataAmplitude = AppConfig.SyntheticDataAmplitude,
                SyntheticDataMinimumLevel = AppConfig.SyntheticDataMinimumLevel,
                SyntheticAlgorithm = AppConfig.SyntheticAlgorithm,
            };

            // Since console programs don't have a SynchronizationContext, the use of await prior to
            // this point means that we're most likely not on the main thread (managed thread ID #1),
            // and GLFW (or the OpenTK wrapper) complains about this. However, they also provided this
            // to disable that check, and I'm not seeing any negative repercussions, so this is easier
            // than something like https://github.com/StephenCleary/AsyncEx/wiki#asynccontext. Needed
            // at this point because the GameWindow constructor checks the thread ID by default.
            GLFWProvider.CheckForMainThread = false;

            var WindowConfig = new EyeCandyWindowConfig()
            {
                StartFullScreen = AppConfig.StartFullScreen,
                HideMousePointer = AppConfig.HideMousePointer,
                OpenGLErrorLogging = AppConfig.OpenGLErrorLogging,
                OpenGLErrorBreakpoint = AppConfig.OpenGLErrorBreakpoint,
                OpenGLErrorThrottle = AppConfig.OpenGLErrorThrottle,
            };
            WindowConfig.OpenTKNativeWindowSettings.Title = "monkey-hi-hat";
            WindowConfig.OpenTKNativeWindowSettings.Location = (AppConfig.StartX, AppConfig.StartY);
            WindowConfig.OpenTKNativeWindowSettings.ClientSize = (AppConfig.SizeX, AppConfig.SizeY);
            WindowConfig.OpenTKNativeWindowSettings.APIVersion = OpenGLVersion;
            WindowConfig.OpenTKGameWindowSettings.UpdateFrequency = AppConfig.FrameRateLimit;
            WindowConfig.OpenTKNativeWindowSettings.Vsync = AppConfig.VSync;
            WindowConfig.OpenTKNativeWindowSettings.AutoIconify = AppConfig.FullscreenMinimizeOnFocusChange;
            WindowConfig.OpenTKNativeWindowSettings.WindowBorder = (AppConfig.HideWindowBorder) ? WindowBorder.Hidden : WindowBorder.Resizable;

            try
            {
                using var stream = File.OpenRead("mhh-icon.png");
                var png = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                var icon = new OpenTK.Windowing.Common.Input.Image(png.Width, png.Height, png.Data);
                WindowConfig.OpenTKNativeWindowSettings.Icon = new WindowIcon(icon);
            }
            catch { }
            
            // Starts hidden to avoid a white flicker before the first frame is rendered.
            // Window is made visible by OnRenderFrame.
            WindowConfig.OpenTKNativeWindowSettings.StartVisible = false;

            // Spin up the window and get the show started
            AppWindow = new(WindowConfig, AudioConfig);
            
            // Prime the pump with any auto-start switches (return value ignored; worst case we just go to idle)
            if (stagedAutoStartSwitches.Length > 0) ProcessSwitches(stagedAutoStartSwitches);
            
            AppWindow.Focus();
            AppWindow.Run(); // blocks
        }
        finally
        {
            AppWindow?.Dispose();
            AppWindow = null;
        }
    }

    private static void ShowAppInfo()
    {
        var sampleCommands = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "  cd \\Program Files\\mhh\n  mhh --help\n  mhh --playlist variety"
            : "  cd ~/monkeyhihat\n  ./mhh --help\n  ./mhh --playlist variety";
        
        var tcp = (AppConfig.UnsecuredPort == 0) ? "disabled" : AppConfig.UnsecuredPort.ToString();
        Console.Clear();
        Console.WriteLine($"\nMonkey Hi Hat {VersionNumber}\n");
        Console.WriteLine($"Process ID {Environment.ProcessId}");
        Console.WriteLine($"Listening on TCP port {tcp}");
        Console.WriteLine(@$"
What Now?
Monkey Hi Hat is running which means it's waiting for commands.
There are several options to send commands to the program.

PC (Windows/Linux) or Android device
Download and run the Monkey Droid remote control GUI from the Release page.

Command Line
Open a new terminal / console window, or connect via SSH, and send commands:

{sampleCommands}

Documentation
Find walk-throughs and troubleshooting docs at https://www.monkeyhihat.com/

Support / Questions
Please open an Issue at https://github.com/MV10/monkey-hi-hat and ask!
");
    }

    private static void LogExceptionMessage(Exception ex)
    {
        var e = ex;
        while (e != null)
        {
            LogExceptionMessage($"{e.GetType()}: {e.Message}");
            e = e.InnerException;
        }
        LogExceptionMessage(ex.StackTrace);
    }

    private static void LogExceptionMessage(string message)
    {
        if(Logger is null)
        {
            Console.WriteLine($"[no logger] {message}");
        }
        else
        {
            Logger.LogError(message);
        }
    }

    private static string GetPlaylistPathname(string fromArg)
        => PathHelper.HasPathSeparators(fromArg) ? fromArg : PathHelper.FindConfigFile(AppConfig.PlaylistPath, fromArg);

    private static string GetVisualizerPathname(string fromArg)
        => PathHelper.HasPathSeparators(fromArg) ? fromArg : PathHelper.FindConfigFile(AppConfig.VisualizerPath, fromArg);

    private static string GetFxPathname(string fromArg)
        => PathHelper.HasPathSeparators(fromArg) ? fromArg : PathHelper.FindConfigFile(AppConfig.FXPath, fromArg);

    private static string GetFadePathname(string fromArg)
        => PathHelper.HasPathSeparators(fromArg) ? fromArg : PathHelper.FindFile(AppConfig.CrossfadePath, PathHelper.MakeFragFilename(fromArg));

    // used for both visualizers and FX
    private static string GetShaderDetail(string pathname, string descriptionSection)
    {
        // returns 0/1 for uses music, followed by shader:description entry (or fx:description)
        var cfg = new ConfigFile(pathname);
        var usesAudio = cfg.Content.ContainsKey("audiotextures") ? "1" : "0";
        var description = cfg.Content.TryGetValue(descriptionSection, out var shaderInfo)
            ? shaderInfo.TryGetValue("description", out var desc)
                ? desc
                : "(No description)"
            : "(No description)";
        return $"{usesAudio}{description}";
    }

    private static string GetConfigFiles(string pathspec, string responseSeparator)
    {
        var files = PathHelper.GetConfigFiles(pathspec);
        var sb = new StringBuilder();
        foreach (var filename in files)
        {
            if (sb.Length > 0) sb.Append(responseSeparator);
            sb.Append(filename);
        }
        return (sb.Length > 0) ? sb.ToString() : "ERR: No conf files available.";
    }

    private static string QueueAndExitStandby(string[] args)
    {
        OnStandby = false;
        QueuedArgs = args;
        return "ACK (command queued, exiting standby)";
    }

    // Find and load (but don't parse) the application configuration file
    private static ConfigFile FindAppConfig()
    {
        var filename = Debugger.IsAttached ? DebugConfigFilename : ConfigFilename;

        // Path search sequence:
        // 1. Environment variable (must be complete pathname)
        // 2. App directory (preferred location)
        // 3. ConfigFiles subdirectory (might be an invalid default config; ie. invalid pathspecs)

        ConfigFilePathname = Environment.GetEnvironmentVariable(ConfigLocationEnvironmentVariable);
        if(!string.IsNullOrEmpty(ConfigFilePathname))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) PathHelper.ExpandLinuxHomeDirectory(ref ConfigFilePathname);
            ConfigFilePathname = Path.GetFullPath(ConfigFilePathname);
            if (!File.Exists(ConfigFilePathname) && Directory.Exists(ConfigFilePathname)) ConfigFilePathname = Path.Combine(ConfigFilePathname, filename);
            if (File.Exists(ConfigFilePathname))
            {
                Console.WriteLine($"Loading configuration via \"{ConfigLocationEnvironmentVariable}\" environment variable:\n  {ConfigFilePathname}");
                return new(ConfigFilePathname);
            }
        }
        
        ConfigFilePathname = Path.GetFullPath(Path.Combine($".{Path.DirectorySeparatorChar}", filename));
        if(File.Exists(ConfigFilePathname))
        {
            Console.WriteLine($"Loading configuration from application directory:\n  {ConfigFilePathname}\n");
            return new(ConfigFilePathname);
        }

        ConfigFilePathname = Path.GetFullPath(Path.Combine($".{Path.DirectorySeparatorChar}ConfigFiles", filename));
        if (File.Exists(ConfigFilePathname))
        {
            Console.WriteLine($"WARNING:\nLoading DEFAULT CONFIGURATION from ConfigFiles sub-directory:\n  {ConfigFilePathname}\n");
            return new(ConfigFilePathname);
        }

        return null;
    }

    private static async Task ProcessCacheSwitches(string[] args)
    {
        HttpCacheManager.ValidateHttpCaching();
        if (!AppConfig.HttpCacheEnabled)
        {
            Console.WriteLine("ERR: Caching is disabled or cache init failed");
            return;
        }
        var cacheManager = new HttpCacheManager();

        switch (args[1].ToLowerInvariant())
        {
            case "purge":
                if (Caching.HttpCacheIndex.Count == 0)
                {
                    Console.WriteLine("ERR: Cache is empty");
                    break;
                }
                Console.WriteLine($"Purging {Caching.HttpCacheIndex.Count} files");
                while(Caching.HttpCacheIndex.Count > 0) cacheManager.RemoveItem(Caching.HttpCacheIndex[0]);
                cacheManager.SaveIndex();
                break;
            
            case "info":
                var sizeMB = Caching.HttpCacheIndex.Sum(i => i.Bytes) / 1024 / 1024;
                Console.WriteLine($"Cache location: {AppConfig.HttpCachePath}");
                Console.WriteLine($"Cache contains {Caching.HttpCacheIndex.Count} files occupying approx {sizeMB:N0} MB");
                Console.WriteLine($"Maximum file count is {(AppConfig.HttpCacheMaxFileCount > 0 ? AppConfig.HttpCacheMaxFileCount : "unlimited")}");
                Console.WriteLine($"Maximum total size is {(AppConfig.HttpCacheMaxTotalMB > 0 ? AppConfig.HttpCacheMaxTotalMB : "unlimited")} MB");
                Console.WriteLine($"Maximum retrieval age is {(AppConfig.HttpCacheMaxAgeDays > 0 ? AppConfig.HttpCacheMaxAgeDays : "unlimited")} days");
                break;
            
            case "add":
                if (args.Length != 3)
                {
                    Console.WriteLine(ShowLongHelp());
                    break;
                }

                var addUrl = HttpDownloadManager.NormalizeUrl(args[2]);
                if (string.IsNullOrEmpty(addUrl))
                {
                    Console.WriteLine("ERR: Failed to parse URL");
                    break;
                }
                
                Console.WriteLine("Downloading...");
                await DownloadToCache(addUrl);
                break;
            
            case "find":
                if (args.Length != 3)
                {
                    Console.WriteLine(ShowLongHelp());
                    break;
                }

                var findUrl = HttpDownloadManager.NormalizeUrl(args[2]);
                if (string.IsNullOrEmpty(findUrl))
                {
                    Console.WriteLine("ERR: Failed to parse URL");
                    break;
                }
                
                var foundItem = cacheManager.GetItem(findUrl);
                if (foundItem == null)
                {
                    Console.WriteLine("ERR: File is not cached");
                }
                else
                {
                    Console.WriteLine($"Cached file is {foundItem.Bytes:N0} bytes, {(foundItem.Bytes / 1024 / 1024):N0} MB");
                }
                break;

            case "list":
                if (Caching.HttpCacheIndex.Count == 0)
                {
                    Console.WriteLine("ERR: Cache is empty");
                    break;
                }

                var content = Caching.HttpCacheIndex.OrderBy(i => i.Timestamp).ToList();
                foreach (var listedItem in content)
                {
                    Console.WriteLine($"{listedItem.SourceUrl}\n   timestamp {listedItem.Timestamp}, stored {listedItem.Bytes:N0} bytes, {(listedItem.Bytes / 1024 / 1024):N0} MB\n");
                }
                break;
            
            case "prefetch":
                var pathnames = PathHelper.GetConfigFiles(AppConfig.VisualizerPath, true);
                pathnames.AddRange(PathHelper.GetConfigFiles(AppConfig.FXPath, true));
                if (pathnames.Count == 0)
                {
                    Console.WriteLine("ERR: No viz/fx configs found");
                    break;
                }
                Console.WriteLine($"Parsing {pathnames.Count} viz/fx configs");
                if(AppConfig.HttpCacheMaxFileCount > 0 && pathnames.Count > AppConfig.HttpCacheMaxFileCount) Console.WriteLine($"(Total exceeds cache file count setting of {AppConfig.HttpCacheMaxFileCount})");
                var urls = new List<string>();
                foreach (var pathname in pathnames) urls.AddRange(CollectUrls(pathname));
                Console.WriteLine($"Found {urls.Count} HTTP texture references");
                if (urls.Count == 0) break;
                foreach (var url in urls)
                {
                    Console.WriteLine($"Downloading {url}");
                    await DownloadToCache(url);
                }
                break;
            
            default:
                Console.WriteLine(ShowLongHelp());
                break;
        }

        HttpDownloadManager.Abort();
        
        async Task DownloadToCache(string url)
        {
            long size = 0;
            long max = AppConfig.HttpCacheMaxTotalMB * 1024 * 1024;
            if (max == 0) size = -1;
            await HttpDownloadManager.InteractiveDownloadAsync(url, cacheManager);
            var addedItem = cacheManager.GetItem(url);
            if (addedItem == null)
            {
                Console.WriteLine("  ERR: File was not added to cache");
            }
            else
            {
                Console.WriteLine($"  Cached {addedItem.Bytes:N0} bytes, {(addedItem.Bytes / 1024 / 1024):N0} MB");
                if (size > -1)
                {
                    size += addedItem.Bytes;
                    if (size > max)
                    {
                        Console.WriteLine($"(Total has exceeded cache size setting of {AppConfig.HttpCacheMaxTotalMB:N0} MB)");
                        size = -1;
                    }
                }
            }
        }
        
        List<string> CollectUrls(string pathname)
        {
            var urls = new List<string>();
            var conf = new ConfigFile(pathname);
            ParseSection("textures");
            ParseSection("cubemaps");

            void ParseSection(string section)
            {
                if (conf.Content.TryGetValue(section, out var items))
                {
                    foreach (var item in items)
                    {
                        var parts = item.Value.Split(':', 2, Const.SplitOptions);
                        if (parts.Length == 2 && PathHelper.IsHttpTextureFilename(parts[1]))
                        {
                            urls.Add(parts[1].StartsWith('!') ? parts[1].Substring(1) : parts[1]);
                        }
                    }
                }
            }
        
            return urls;
        }
    }

    private static string ShowHelp()
        =>
@$"

mhh: Monkey Hi Hat

By default, the application always loads with the default ""idle"" shader and all other switches are
are passed to the already-running instance. Only ""--help"", ""--display"", ""--load"", or ""--playlist""
switches can be used if an instance is not already running.

--help                      shows the most commonly-used switches (this help)
--longhelp                  shows all available switches
--standby                   toggles between standby mode and active mode
--quit                      ends the program

--list [viz|playlists|fx]   shows config files (*.conf) from all defined paths for the requested file type

--idle                      loads the default startup shader
--reload                    unloads and reloads the current shader (unavailable after an FX shader loads)
--load [file]               loads [file].conf from VisualizationPath defined in mhh.conf
--load [viz] [fx]           loads a visualization and immediately applies FX; must use search paths
--fx [file]                 loads [file].conf from FXPath defined in mhh.conf
--fade [file]               queues a specific crossfade shader for the next visualizer change

--playlist [file]           loads [file].conf from PlaylistPath defined in mhh.conf
--next                      when a playlist is active, advances to the next viz (using the Order setting)
--next fx                   when a playlist is active, applies a post-processing FX (if one isn't running)

--jpg [wait]                JPG screenshot (saves to desktop); ""wait"" watches for spacebar
--png [wait]                PNG screenshot (saves to desktop); ""wait"" watches for spacebar
";
    
    private static string ShowLongHelp()
        =>
@$"

mhh: Monkey Hi Hat

By default, the application always loads with the default ""idle"" shader and all other switches are
are passed to the already-running instance. Only ""--help"", ""--display"", ""--load"", or ""--playlist""
switches can be used if an instance is not already running.

--help                      shows the most commonly-used switches
--longhelp                  shows all available switches (this help)
--standby                   toggles between standby mode and active mode
--quit                      ends the program

--list [viz|playlists|fx]   shows config files (*.conf) from all defined paths for the requested file type

--idle                      loads the default startup shader
--reload                    unloads and reloads the current shader (unavailable after an FX shader loads)
--load [file]               loads [file].conf from VisualizationPath defined in mhh.conf
--load [viz] [fx]           loads a visualization and immediately applies FX; must use search paths
--fx [file]                 loads [file].conf from FXPath defined in mhh.conf
--fade [file]               queues a specific crossfade shader for the next visualizer change
--load [path{Path.DirectorySeparatorChar}file]          if present, loads [file].conf from requested path
--fx [path{Path.DirectorySeparatorChar}file]            if present, loads [file].conf from requested path
--fade [path{Path.DirectorySeparatorChar}file]          if present, queues crossfade from requested path

--playlist [file]           loads [file].conf from PlaylistPath defined in mhh.conf
--playlist [path{Path.DirectorySeparatorChar}file]      if present, loads [file].conf from requested path
--next                      when a playlist is active, advances to the next viz (using the Order setting)
--next fx                   when a playlist is active, applies a post-processing FX (if one isn't running)

--jpg [wait]                JPG screenshot (saves to desktop); ""wait"" watches for spacebar
--png [wait]                PNG screenshot (saves to desktop); ""wait"" watches for spacebar

--show [viz|stats]          Text overlay for 10 seconds (unless ""toggle"" command is used)
--show [toggle|clear]       Switches text overlays from 10 seconds to permanent, ""clear"" removes overlay
--show [popups|what]        ""what"" shows viz and FX names and ""popups"" toggles playlist auto-popups
--show track                On Windows, displays most recent Spotify track info (if available)
--show grid                 Displays a character grid for adjusting text settings in app config

--info                      writes shader and execution details to the console
--display                   lists monitor details and the window state and coordinates
--fullscreen                toggle between windowed and full-screen state
--fps                       returns instantaneous FPS and average FPS over past 10 seconds
--fps [0|1-9999]            sets a frame rate (FPS) target, or 0 to disable (some shaders may require 60 FPS)
--nocache                   disables shader viz/FX caching for the remainder of the session (good for testing)

--test [viz|fx|fade] [file] Enters test mode, +/- cycles through content, Q to quit, R to reload
--endtest                   Exits test mode (loads the idle visualizer)

--standby                   toggles between standby mode and active mode
--pause                     stops the current shader
--run                       resumes the current shader
--pid                       shows the current Process ID
--log [level]               shows or sets log-level (None, Trace, Debug, Information, Warning, Error, Critical)
--paths                     shows the configured content paths (viz, FX, etc.)

--console                   toggles the console window visibility
--cls                       clears the console window of the running instance (useful during debug)

--streaming                 streaming commands control Spout / NDI; refer to the docs for details
--streaming status
--streaming send spout|ndi [""sender name""]
--streaming receive spout ""source name""
--streaming receive ndi ""machine (source name)"" [""group1,group2,...groupN""]
--streaming stop send|receive

The following switches are only accepted when the program is not already running:

--devices                   list audio device names

--cache purge               removes all cached content
--cache info                shows cache statistics (counts, size)
--cache add [url]           retrieves and caches a texture
--cache find [url]          shows details if URL is already cached
--cache list                shows all cached files and details
--cache prefetch            pre-fetches the cache for all viz/FX (subject to count/size limits)
";
}
