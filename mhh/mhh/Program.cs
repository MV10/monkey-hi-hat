
using CommandLineSwitchPipe;
using eyecandy;
using Microsoft.Extensions.Logging;
using OpenTK.Windowing.Desktop;
using Serilog;
using Serilog.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

/*
Program.Main primarily does two things:
-- sets up and runs the VisualizerHostWindow
-- processes switches / args recieved at runtime

There are no startup switches.

The last part is accomplished by my CommandLineSwitchPipe library. At startup it
tries to connect to an existing named pipe. If none is found, this instance becomes
the named pipe listener and the program starts, loading the window and the idle viz.

After that it's just waiting for the window to exit (or to receive a quit command over
the named pipe).

However, if a named pipe is found, any args are passed to the already-running program.
If a response is received, it is written to the console and the secondary instance ends.
For example, the frame rate of the running instance can be queried.

On Linux, spotifyd can run an arbitrary command on track change, so this feature could
be used to tell the program to load the next shader in the playlist with each new song.
*/

namespace mhh
{
    public class Program
    {
        static readonly string ConfigFilename = "mhh.conf";
        static readonly string DebugConfigFilename = "mhh.debug.conf";
        static readonly string SwitchPipeName = "monkey-hi-hat";
        static readonly Version OpenGLVersion = new(4, 6);
        static readonly string ConfigPathnameEnvironmentVariable = "monkey-hi-hat-config"; // set in Debug Launch Profile dialog

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

        // cancel this to terminate the switch server's named pipe.
        private static CancellationTokenSource ctsSwitchPipe;

        [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        static readonly int SW_HIDE = 0;
        static readonly int SW_SHOW = 5;
        private static bool ConsoleVisible = true;

        // Currently Windows Terminal will only minimize, not hide. Microsoft
        // is debating whether and how to fix that (not just about Powershell):
        // https://github.com/microsoft/terminal/issues/12464
        private static bool IsConsoleVisible
        {
            get => ConsoleVisible;
            set
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new InvalidOperationException("Console visibility can only be changed on Windows OS");
                ConsoleVisible = value;
                var hwnd = GetConsoleWindow();
                var flag = ConsoleVisible ? SW_SHOW : SW_HIDE;
                ShowWindow(hwnd, flag);
            }
        }

        internal static bool AppRunning = true; // the window can change this
        private static bool OnStandby = false;

        public static async Task Main(string[] args)
        {
            try
            {
                if(await InitializeAndWait(args))
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        IsConsoleVisible = !AppConfig.WindowsHideConsoleAtStartup || (AppConfig.StartInStandby && AppConfig.WindowsHideConsoleInStandby);
                    }

                    AppRunning = true;
                    OnStandby = AppConfig.StartInStandby;
                    while (AppRunning)
                    {
                        if (OnStandby)
                        {
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
                var e = ex;
                while (e != null)
                {
                    LogException($"{e.GetType().Name}: {e.Message}");
                    e = e.InnerException;
                }
                LogException(ex.StackTrace);
            }
            finally
            {
                // Stephen Cleary says CTS disposal is unnecessary as long as the token is cancelled
                ctsSwitchPipe?.Cancel();
                AppWindow?.Dispose();
                Log.CloseAndFlush();
            }

            // Give the sloooow console time to catch up...
            await Task.Delay(250);
        }

        public static string ProcessSwitches(string[] args)
        {
            if (args.Length == 0) return ShowHelp();

            LogHelper.Logger?.LogInformation($"Processing switches: {string.Join(" ", args)}");

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

                case "--jpg":
                case "--png":
                    if (OnStandby) return "ERR: Application is in standby";
                    if (args.Length > 2 || args.Length == 2 && !args[1].Equals("wait", StringComparison.InvariantCultureIgnoreCase)) return ShowHelp();
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
                    return GetShaderDetail(GetVisualizerPathname(args[1]));

                case "--nocache":
                    if (OnStandby) return "ERR: Application is in standby";
                    if (args.Length > 1) return ShowHelp();
                    return AppWindow.Command_DisableCaching();

                case "--console":
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        IsConsoleVisible = !IsConsoleVisible;
                        return "ACK";
                    }
                    else
                    {
                        return "ERR: Console visibility is only available on Windows OS";
                    }

                case "--quit":
                    if (args.Length > 1) return ShowHelp();
                    AppRunning = false;
                    if (OnStandby) return "ACK";
                    return AppWindow.Command_Quit();

                case "--standby":
                    AppWindow?.Command_Quit();
                    OnStandby = !OnStandby;
                    return "ACK";

                default:
                    return ShowHelp();
            }
        }

        // Return "false" to tell main() to exit after init (another instance is running)
        private static async Task<bool> InitializeAndWait(string[] args)
        {
            Console.Clear();
            Console.WriteLine($"\nMonkey Hi Hat (PID {Environment.ProcessId})");

            // Find and load the application configuration file (parsed later)
            var configPathname = Path.GetFullPath(Debugger.IsAttached ? DebugConfigFilename : ConfigFilename);
            if (!File.Exists(configPathname))
            {
                configPathname = Environment.GetEnvironmentVariable(ConfigPathnameEnvironmentVariable);
                if (configPathname is null || !File.Exists(configPathname))
                {
                    Console.WriteLine($"\n\nUnable to locate the configuration file.\n  The default is to read \"{ConfigFilename}\" from the application directory.\n  When a debugger is attached, \"{DebugConfigFilename}\" is expected.\n  Alternately, a \"{ConfigPathnameEnvironmentVariable}\" environment variable can provide the full pathname.");
                    Thread.Sleep(250); // slow-ass console
                    return false;
                }
                Console.WriteLine($"Loading configuration via \"{ConfigPathnameEnvironmentVariable}\" environment variable:\n  {configPathname}");
            }
            var appConfigFile = new ConfigFile(configPathname);

            // Prepare logging and the switch server
            CommandLineSwitchServer.Options.PipeName = SwitchPipeName;
            var alreadyRunning = await CommandLineSwitchServer.TryConnect().ConfigureAwait(false);
            LogHelper.Initialize(appConfigFile, alreadyRunning);
            CommandLineSwitchServer.Options.Logger = LogHelper.Logger;

            // Show help if requested, or if it's already running but no args were provided
            if ((args.Length == 1 && args[0].ToLowerInvariant().Equals("--help"))
                || (args.Length == 0 && alreadyRunning))
            {
                Console.WriteLine(ShowHelp());
                return false;
            }

            LogHelper.Logger?.LogInformation($"Starting (PID {Environment.ProcessId})");

            // Send args to an already-running instance?
            if (await CommandLineSwitchServer.TrySendArgs().ConfigureAwait(false))
            {
                LogHelper.Logger?.LogDebug($"Sending switch: {args[0]}");
                Console.WriteLine(CommandLineSwitchServer.QueryResponse);
                return false; // end program
            }
            // ...or continue running since we're the first instance

            // Parse the application configuration file and internal shaders
            AppConfig = new ApplicationConfiguration(appConfigFile);

            // Start listening for commands
            ctsSwitchPipe = new();
            _ = Task.Run(() => CommandLineSwitchServer.StartServer(ProcessSwitches, ctsSwitchPipe.Token, AppConfig.UnsecuredPort));

            // Prepare the eycandy library
            ErrorLogging.Logger = LogHelper.Logger;

            return true; // continue running
        }

        private static void RunWindow()
        {
            try
            {
                var AudioConfig = new EyeCandyCaptureConfig()
                {
                    CaptureDeviceName = AppConfig.CaptureDeviceName,
                    DetectSilence = true, // always detect, playlists may need it
                    MaximumSilenceRMS = AppConfig.DetectSilenceMaxRMS,
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
                };
                WindowConfig.OpenTKNativeWindowSettings.Title = "monkey-hi-hat";
                WindowConfig.OpenTKNativeWindowSettings.Size = (AppConfig.SizeX, AppConfig.SizeY);
                WindowConfig.OpenTKNativeWindowSettings.APIVersion = OpenGLVersion;
                WindowConfig.OpenTKGameWindowSettings.UpdateFrequency = AppConfig.FrameRateLimit;
                // OpenTK 4.8 obsoleted the separate RenderFrequency setting :(

                // Spin up the window and get the show started
                AppWindow = new(WindowConfig, AudioConfig);
                AppWindow.Focus();
                AppWindow.Run(); // blocks
            }
            finally
            {
                AppWindow?.Dispose();
                AppWindow = null;
            }
        }

        private static void LogException(string message)
        {
            if(LogHelper.Logger is null)
            {
                Console.WriteLine($"[no logger] {message}");
            }
            else
            {
                LogHelper.Logger.LogError(message);
            }
        }

        private static string GetPlaylistPathname(string fromArg)
            => PathHelper.HasPathSeparators(fromArg) ? fromArg : PathHelper.FindConfigFile(AppConfig.PlaylistPath, fromArg);

        private static string GetVisualizerPathname(string fromArg)
            => PathHelper.HasPathSeparators(fromArg) ? fromArg : PathHelper.FindConfigFile(AppConfig.VisualizerPath, fromArg);

        private static string GetFxPathname(string fromArg)
            => PathHelper.HasPathSeparators(fromArg) ? fromArg : PathHelper.FindConfigFile(AppConfig.FXPath, fromArg);

        private static string GetShaderDetail(string pathname)
        {
            // returns 0/1 for uses music, followed by shader:description entry
            var cfg = new ConfigFile(pathname);
            var usesAudio = cfg.Content.ContainsKey("audiotextures") ? "1" : "0";
            var description = cfg.Content.TryGetValue("shader", out var shaderInfo)
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

        private static string ShowHelp()
            =>
@$"

mhh: Monkey Hi Hat

There are no startup switches, and the application always loads with the default ""idle"" shader.
All switches are passed to the already-running instance:

--help                      shows help (surprise!)
--quit                      ends the program

--list [viz|playlists|fx]   shows config files (*.conf) from all defined paths for the requested file type

--load [file]               loads [file].conf from VisualizationPath defined in mhh.conf
--load [path{Path.DirectorySeparatorChar}file]          if present, loads [file].conf from requested path
--idle                      loads the default startup shader
--reload                    unloads and reloads the current shader (unavailable after an FX shader loads)
--fx [file]                 loads [file].conf from FXPath defined in mhh.conf
--fx [path{Path.DirectorySeparatorChar}file]            if present, loads [file].conf from requested path
--load [viz] [fx]           loads a visualization and immediately applies FX; must use search paths

--playlist [file]           loads [file].conf from PlaylistPath defined in mhh.conf
--playlist [path{Path.DirectorySeparatorChar}file]      if present, loads [file].conf from requested path
--next                      when a playlist is active, advances to the next viz (using the Order setting)
--next fx                   when a playlist is active, applies a post-processing FX (if one isn't running)

--jpg [wait]                JPG screenshot; Win: desktop, Linux: app path; ""wait"" watches for spacebar
--png [wait]                PNG screenshot; Win: desktop, Linux: app path; ""wait"" watches for spacebar

--info                      writes shader and execution details to the console
--fullscreen                toggle between windowed and full-screen state
--fps                       returns instantaneous FPS and average FPS over past 10 seconds
--fps [0|1-9999]            sets a frame rate lock (FPS target), or 0 to disable (max possible FPS)
--nocache                   disables caching for the remainder of the session (good for testing)

--standby                   toggles between standby mode and active mode
--pause                     stops the current shader
--run                       resumse the current shader
--pid                       shows the current Process ID
--log [level]               shows or sets log-level (None, Trace, Debug, Information, Warning, Error, Critical)

--console                   Windows: toggles the visibility of the console window (only minimizes Terminal)
";
    }
}
