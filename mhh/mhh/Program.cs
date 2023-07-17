
using CommandLineSwitchPipe;
using eyecandy;

/*
Main does two things:
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
            try
            {
                CommandLineSwitchServer.Options.PipeName = "monkey-hi-hat";

                // Send args to an already-running instance?
                if (await CommandLineSwitchServer.TrySendArgs())
                {
                    Console.WriteLine(CommandLineSwitchServer.QueryResponse);
                    return;
                }
                // ...or continue running since we're the first instance

                Console.Clear();
                Console.WriteLine($"\nmonkey-hi-hat (PID {Environment.ProcessId})");

                AppConfig = new ApplicationConfiguration("mhh.conf", "InternalShaders/idle.conf");

                var AudioConfig = new EyeCandyCaptureConfig();
                AudioConfig.DriverName = AppConfig.CaptureDriverName;
                AudioConfig.CaptureDeviceName = AppConfig.CaptureDeviceName;

                var WindowConfig = new EyeCandyWindowConfig();
                WindowConfig.OpenTKNativeWindowSettings.Title = "monkey-hi-hat";
                WindowConfig.OpenTKNativeWindowSettings.Size = (AppConfig.SizeX, AppConfig.SizeY);
                WindowConfig.StartFullScreen = AppConfig.StartFullScreen;
                WindowConfig.VertexShaderPathname = AppConfig.IdleVisualizer.VertexShaderPathname;
                WindowConfig.FragmentShaderPathname = AppConfig.IdleVisualizer.FragmentShaderPathname;

                // Start listening for commands
                ctsSwitchPipe = new();
                _ = Task.Run(() => CommandLineSwitchServer.StartServer(ProcessExecutionSwitches, ctsSwitchPipe.Token));

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
                    Console.WriteLine($"\n{e.GetType().Name}: {e.Message}");
                    e = e.InnerException;
                }
                Console.WriteLine($"\n{ex.StackTrace}");
            }
            finally
            {
                // Stephen Cleary says CTS disposal is unnecessary as long as the token is cancelled
                ctsSwitchPipe?.Cancel();
                win?.Dispose();
            }
        }

        private static string ProcessExecutionSwitches(string[] args)
        {
            // TODO - add --slideshow and --next switches

            if (args.Length == 0 || args[0].LowercaseEquals("--help")) return ShowHelp();

            if (args[0].LowercaseEquals("--load"))
            {
                if (args.Length != 2) return ShowHelp();
                
                // if a path separator exists, just send the argument as-is...
                if (args[1].Contains('/')) return win.Command_Load(args[1]);

                //...otherwise prefix with the shader path
                return win.Command_Load(Path.Combine(AppConfig.ShaderPath, args[1]));
            }

            if (args.Length > 1) return ShowHelp();

            switch(args[0].ToLowerInvariant())
            {
                case "--quit":
                    return win.Command_Quit();

                case "--info":
                    return win.Command_Info();

                case "--fps":
                    return win.FramesPerSecond.ToString();

                case "--idle":
                    return win.Command_Idle();

                case "--pause":
                    return win.Command_Pause();

                case "--run":
                    return win.Command_Run();

                case "--reload":
                    return win.Command_Reload();

                case "--pid":
                    return Environment.ProcessId.ToString();

                default:
                    return ShowHelp();
            }
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

";
    }
}
