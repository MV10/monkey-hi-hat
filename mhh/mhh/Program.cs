
using CommandLineSwitchPipe;
using eyecandy;
using Microsoft.Extensions.Logging;
using OpenTK.Windowing.Desktop;
using Serilog;
using Serilog.Extensions.Logging;
using System.Diagnostics;
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

        /// <summary>
        /// Content parsed from the mhh.conf configuration file and the
        /// default idle shader conf file.
        /// </summary>
        public static ApplicationConfiguration AppConfig;

        /// <summary>
        /// Where the magic happens.
        /// </summary>
        public static HostWindow AppWindow;

        // cancel this to terminate the switch server's named pipe.
        private static CancellationTokenSource ctsSwitchPipe;

        static async Task Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine($"\nmonkey-hi-hat (PID {Environment.ProcessId})");

            // Load the application configuration file (parsed later)
            var appConfigFile = new ConfigFile(Debugger.IsAttached ? DebugConfigFilename : ConfigFilename);

            // Prepare logging and the switch server
            CommandLineSwitchServer.Options.PipeName = SwitchPipeName;
            var alreadyRunning = await CommandLineSwitchServer.TryConnect().ConfigureAwait(false);
            LogHelper.Initialize(appConfigFile, alreadyRunning);
            CommandLineSwitchServer.Options.Logger = LogHelper.Logger;

            // Show help if requested, or if it's already running but no args were provided
            if((args.Length == 1 && args[0].ToLowerInvariant().Equals("--help"))
                || (args.Length == 0 && alreadyRunning))
            {
                Console.WriteLine(ShowHelp());
                Environment.Exit(0);
            }

            try
            {
                LogHelper.Logger?.LogInformation($"Starting (PID {Environment.ProcessId})");

                // Send args to an already-running instance?
                if (await CommandLineSwitchServer.TrySendArgs().ConfigureAwait(false))
                {
                    LogHelper.Logger?.LogDebug($"Sending switch: {args[0]}");
                    Console.WriteLine(CommandLineSwitchServer.QueryResponse);
                    return;
                }
                // ...or continue running since we're the first instance

                // Parse the application configuration file and internal shaders
                AppConfig = new ApplicationConfiguration(appConfigFile);

                // Start listening for commands
                ctsSwitchPipe = new();
                _ = Task.Run(() => CommandLineSwitchServer.StartServer(ProcessExecutionSwitches, ctsSwitchPipe.Token, AppConfig.UnsecuredPort));

                // Prepare the eycandy library
                ErrorLogging.Logger = LogHelper.Logger;

                var AudioConfig = new EyeCandyCaptureConfig()
                {
                    //DriverName = AppConfig.CaptureDriverName, // unnecessary for capture as of eyecandy 1.0.81
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
                WindowConfig.OpenTKGameWindowSettings.UpdateFrequency = AppConfig.FrameRateLock;
                // OpenTK 4.8 obsoleted the separate RenderFrequency setting :(

                // Spin up the window and get the show started
                AppWindow = new(WindowConfig, AudioConfig);
                AppWindow.Focus();
                AppWindow.Run(); // blocks
                LogHelper.Logger?.LogTrace("Program.Main AppWindow.Run has exited ----------------------------");
            }
            catch (OperationCanceledException)
            { } // normal, disregard
            catch (Exception ex)
            {
                var e = ex;
                while(e != null)
                {
                    LogException($"{e.GetType().Name}: {e.Message}");
                    e = e.InnerException;
                }
                LogException(ex.StackTrace);
            }
            finally
            {
                // Stephen Cleary says CTS disposal is unnecessary as long as the token is cancelled
                LogHelper.Logger?.LogTrace("  Program.Main cancelling CommandLineSwitchPipe token");
                ctsSwitchPipe?.Cancel();

                LogHelper.Logger?.LogTrace("  Program.Main disposing AppWindow");
                AppWindow?.Dispose();

                LogHelper.Logger?.LogInformation($"Exiting (PID {Environment.ProcessId})");
                Log.CloseAndFlush();
            }

            // Give the sloooow console time to catch up...
            await Task.Delay(250);
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

        private static string ProcessExecutionSwitches(string[] args)
        {
            if (args.Length == 0) return ShowHelp();

            LogHelper.Logger?.LogInformation($"Processing switches: {string.Join(" ", args)}");

            switch (args[0].ToLowerInvariant())
            {
                case "--load":
                    if (args.Length != 2) return ShowHelp();
                    var shaderPathname = GetShaderPathname(args[1]);
                    if (shaderPathname is null) return "ERR: Shader not found.";
                    return AppWindow.Command_Load(shaderPathname);

                case "--playlist":
                    if (args.Length != 2) return ShowHelp();
                    var playlistPathname = GetPlaylistPathname(args[1]);
                    if (playlistPathname is null) return "ERR: Playlist not found.";
                    return AppWindow.Command_Playlist(playlistPathname);

                case "--next":
                    return AppWindow.Command_PlaylistNext();

                case "--list":
                case "--md.list":
                    var readable = args[0].ToLowerInvariant().Equals("--list");
                    var separator =  readable ? "\n" : CommandLineSwitchServer.Options.Advanced.SeparatorControlCode;
                    
                    if (args.Length == 2 && args[1].ToLowerInvariant().Equals("viz"))
                    {
                        if (string.IsNullOrEmpty(AppConfig.VisualizerPath)) return "ERR: ShaderPath not defined in mhh.conf.";
                        return GetConfigFiles(AppConfig.VisualizerPath, separator);
                    }

                    if (args.Length == 2 && args[1].ToLowerInvariant().Equals("playlists"))
                    {
                        if (string.IsNullOrEmpty(AppConfig.PlaylistPath)) return "ERR: PlaylistPath not defined in mhh.conf.";
                        return GetConfigFiles(AppConfig.PlaylistPath, separator);
                    }

                    return readable ? ShowHelp() : string.Empty;

                case "--quit":
                    if (args.Length > 1) return ShowHelp();
                    return AppWindow.Command_Quit();

                case "--info":
                    if (args.Length > 1) return ShowHelp();
                    return AppWindow.Command_Info();

                case "--fps":
                    if (args.Length > 2) return ShowHelp();
                    if(args.Length == 2)
                    {
                        if (!int.TryParse(args[1], out var fpsTarget) || fpsTarget < 0 || fpsTarget > 9999) return ShowHelp();
                        AppWindow.UpdateFrequency = fpsTarget;
                        return (fpsTarget == 0) ? "FPS target disabled (max possible FPS)" : $"FPS target set to {fpsTarget}";
                    }
                    else
                    {
                        return $"{AppWindow.FramesPerSecond} FPS" +
                            $"\n{AppWindow.AverageFramesPerSecond} average FPS over past {AppWindow.AverageFPSTimeframeSeconds} seconds" +
                            $"\nFPS target is {(AppWindow.UpdateFrequency == 0 ? "not locked (max FPS)" : $"locked to {AppWindow.UpdateFrequency} FPS" )}";
                    }

                case "--fullscreen":
                    if (args.Length > 1) return ShowHelp();
                    return AppWindow.Command_FullScreen();

                case "--idle":
                    if (args.Length > 1) return ShowHelp();
                    return AppWindow.Command_Idle();

                case "--pause":
                    if (args.Length > 1) return ShowHelp();
                    return AppWindow.Command_Pause();

                case "--run":
                    if (args.Length > 1) return ShowHelp();
                    return AppWindow.Command_Run();

                case "--reload":
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
                    return GetShaderDetail(GetShaderPathname(args[1]));

                default:
                    return ShowHelp();
            }
        }

        private static string GetPlaylistPathname(string fromArg)
            => HasPathSeparators(fromArg) ? fromArg : PathHelper.FindConfigFile(AppConfig.PlaylistPath, fromArg);

        private static string GetShaderPathname(string fromArg)
            => HasPathSeparators(fromArg) ? fromArg : PathHelper.FindConfigFile(AppConfig.VisualizerPath, fromArg);

        private static bool HasPathSeparators(string fromArg)
            => fromArg.Contains(Path.DirectorySeparatorChar) || fromArg.Contains(Path.AltDirectorySeparatorChar);

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

        private static string GetConfigFiles(string pathspec, string separator)
        {
            var sb = new StringBuilder();
            var paths = pathspec.Split(';', Const.SplitOptions);
            foreach(var path in paths)
            {
                foreach (var filename in Directory.EnumerateFiles(path, "*.conf"))
                {
                    if (sb.Length > 0) sb.Append(separator);
                    sb.Append(Path.GetFileNameWithoutExtension(filename));
                }
            }

            return (sb.Length > 0) ? sb.ToString() : "ERR: No conf files available.";
        }

        private static string ShowHelp()
            =>
@"

mhh: monkey-hi-hat

There are no startup switches, the application always loads with the default ""idle"" shader.
All switches are passed to the already-running instance:

--help                      shows help (surprise!)
--quit                      ends the program

--load [viz]                loads [viz].conf from VisualizationPath defined in mhh.conf
--load [path\viz]           if present, loads [viz].conf from requested path (use platform-specific separator)
--list [viz|playlists]      shows visualization confs or playlists in the default storage locations
--idle                      load the default/idle shader
--reload                    unloads and reloads the current shader

--playlist [file]           loads [file].conf from PlaylistPath defined in mhh.conf
--playlist [path/file]      must use forward slash; if present, loads [file].conf from requested location
--next                      when a playlist is active, advances to the next shader (according to the Order)

--info                      writes shader and execution details to the console
--fullscreen                toggle between windowed and full-screen state
--fps                       returns instantaneous FPS and average FPS over past 10 seconds
--fps [0-9999]              sets a frame rate lock (FPS target), or 0 to disable (max possible FPS)

--pause                     stops the current shader
--run                       executes the current shader
--pid                       shows the current Process ID
--log [level]               shows or sets log-level (None, Trace, Debug, Information, Warning, Error, Critical)

";
    }
}
