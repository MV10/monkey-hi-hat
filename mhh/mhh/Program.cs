
using CommandLineSwitchPipe;
using eyecandy;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
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
        /// <summary>
        /// Content parsed from the mhh.conf configuration file and the
        /// default idle shader conf file.
        /// </summary>
        public static ApplicationConfiguration AppConfig;

        // cancel this to terminate the switch server's named pipe.
        private static CancellationTokenSource ctsSwitchPipe;

        // where the magic happens
        private static HostWindow win;

        static async Task Main(string[] args)
        {
            // Load the application configuration file (parsed later)
            var appConfigFile = new ConfigFile("mhh.conf");

            // Prepare logging and the switch server
            CommandLineSwitchServer.Options.PipeName = "monkey-hi-hat";
            var alreadyRunning = await CommandLineSwitchServer.TryConnect();
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
                // Parse the application configuration file
                AppConfig = new ApplicationConfiguration(appConfigFile, "InternalShaders/idle.conf");

                // Send args to an already-running instance?
                if (await CommandLineSwitchServer.TrySendArgs())
                {
                    Console.WriteLine(CommandLineSwitchServer.QueryResponse);
                    return;
                }
                // ...or continue running since we're the first instance

                Console.Clear();
                Console.WriteLine($"\nmonkey-hi-hat (PID {Environment.ProcessId})");

                // Start listening for commands
                ctsSwitchPipe = new();
                _ = Task.Run(() => CommandLineSwitchServer.StartServer(ProcessExecutionSwitches, ctsSwitchPipe.Token));

                // Prepare the window configurations
                ErrorLogging.Logger = LogHelper.Logger;

                var AudioConfig = new EyeCandyCaptureConfig();
                AudioConfig.DriverName = AppConfig.CaptureDriverName;
                AudioConfig.CaptureDeviceName = AppConfig.CaptureDeviceName;

                var WindowConfig = new EyeCandyWindowConfig();
                WindowConfig.OpenTKNativeWindowSettings.Title = "monkey-hi-hat";
                WindowConfig.OpenTKNativeWindowSettings.Size = (AppConfig.SizeX, AppConfig.SizeY);
                WindowConfig.StartFullScreen = AppConfig.StartFullScreen;
                WindowConfig.VertexShaderPathname = AppConfig.IdleVisualizer.VertexShaderPathname;
                WindowConfig.FragmentShaderPathname = AppConfig.IdleVisualizer.FragmentShaderPathname;

                // Spin up the window and get the show started
                win = new(WindowConfig, AudioConfig);
                win.Focus();
                win.Run(); // blocks
            }
            catch (OperationCanceledException)
            { } // normal, disregard
            catch (Exception ex)
            {
                var e = ex;
                while(e != null)
                {
                    LogHelper.Logger.LogError($"{e.GetType().Name}: {e.Message}");
                    e = e.InnerException;
                }
                LogHelper.Logger.LogError(ex.StackTrace);
            }
            finally
            {
                // Stephen Cleary says CTS disposal is unnecessary as long as the token is cancelled
                ctsSwitchPipe?.Cancel();
                win?.Dispose();
                Log.CloseAndFlush();
            }
        }

        private static string ProcessExecutionSwitches(string[] args)
        {
            // TODO - add --slideshow and --next switches

            if (args.Length == 0) return ShowHelp();

            switch (args[0].ToLowerInvariant())
            {
                case "--load":
                    if (args.Length != 2) return ShowHelp();

                    // if a path separator exists, just send the argument as-is...
                    if (args[1].Contains('/')) return win.Command_Load(args[1]);

                    //...otherwise prefix with the shader path
                    return win.Command_Load(Path.Combine(AppConfig.ShaderPath, args[1]));

                case "--help":
                    if (args.Length == 2 && args[1].ToLowerInvariant().Equals("viz")) return ShowVizHelp();
                    return ShowHelp();

                case "--quit":
                    if (args.Length > 1) return ShowHelp();
                    return win.Command_Quit();

                case "--info":
                    if (args.Length > 1) return ShowHelp();
                    return win.Command_Info();

                case "--fps":
                    if (args.Length > 1) return ShowHelp();
                    return $"{win.FramesPerSecond} FPS\n{win.AverageFramesPerSecond} average FPS over {win.AverageFPSTimeframeSeconds} seconds";

                case "--idle":
                    if (args.Length > 1) return ShowHelp();
                    return win.Command_Idle();

                case "--pause":
                    if (args.Length > 1) return ShowHelp();
                    return win.Command_Pause();

                case "--run":
                    if (args.Length > 1) return ShowHelp();
                    return win.Command_Run();

                case "--reload":
                    if (args.Length > 1) return ShowHelp();
                    return win.Command_Reload();

                case "--pid":
                    if (args.Length > 1) return ShowHelp();
                    return Environment.ProcessId.ToString();

                case "--log":
                    if (args.Length == 1) return $"current log level {LevelConvert.ToExtensionsLevel(LogHelper.LevelSwitch.MinimumLevel).ToString()}";
                    return $"setting log level {LogHelper.SetLogLevel(args[1])}";

                case "--viz":
                    if (args.Length != 3) return ShowHelp();
                    return win.Command_VizCommand(args[1], args[2]);

                default:
                    return ShowHelp();
            }
        }

        private static string ShowVizHelp()
        {
            var help = win.Command_VizHelp();
            if(help.Count == 0) return $"\n{win.ActiveVisualizer.VisualizerTypeName} does not accept runtime commands.";

            var sb = new StringBuilder();
            sb.AppendLine($"\nRuntime commands for {win.ActiveVisualizer.VisualizerTypeName}:\n");
            foreach(var cv in help)
            {
                sb.AppendLine($"--viz [{cv.command}] [{cv.value}]");
            }
            return sb.ToString();
        }

        private static string ShowHelp()
            =>
@"

mhh: monkey-hi-hat

There are no startup switches, the application always loads with the default ""idle"" shader.
All switches are passed to the already-running instance:

--help                      shows help (surprise!)
--load [shader]             loads [shader].conf from ShaderPath defined in mhh.conf
--load [path/shader]        must use forward slash; if present, loads [shader].conf from requested location
--quit                      ends the program
--info                      writes shader and execution details to the console
--fps                       writes FPS information to the console
--idle                      load the default/idle shader
--pause                     stops the current shader
--run                       executes the current shader
--reload                    unloads and reloads the current shader
--pid                       shows the current Process ID
--log [level]               shows or sets log-level (None, Trace, Debug, Information, Warning, Error, Critical)
--viz [command] [value]     send commands to the current visualizer (if supported; see below)
--help viz                  list --viz command/value options for the current visalizer, if any

";
    }
}
