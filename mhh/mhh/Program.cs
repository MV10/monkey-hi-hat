
using CommandLineSwitchPipe;
using eyecandy;

namespace mhh
{
    internal class Program
    {
        // Cancel this to terminate the switch server's named pipe.
        private static CancellationTokenSource ctsSwitchPipe;

        // Cancel this to terminate the running instance.
        private static CancellationTokenSource ctsRunningInstance;

        // Where the magic happens
        private static VisualizerHostWindow win;

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

                // We're the first instance, listen for args and get the show started...
                ctsSwitchPipe = new();
                _ = Task.Run(() => CommandLineSwitchServer.StartServer(ProcessExecutionSwitches, ctsSwitchPipe.Token));

                ProcessStartupSwitches(args);

                Console.Clear();
                Console.WriteLine($"\nmonkey-hi-hat - PID {Environment.ProcessId}");

                // TODO - read config
                var audioConfig = new EyeCandyCaptureConfig();

                // TODO - read config
                var windowConfig = new EyeCandyWindowConfig();
                windowConfig.OpenTKNativeWindowSettings.Title = "monkey-hi-hat";
                windowConfig.OpenTKNativeWindowSettings.Size = (960, 540);
                //windowConfig.StartFullScreen = xxxx;

                // The window class will create a VizDefinition whose defaults match these:
                windowConfig.VertexShaderPathname = Defaults.IdleVertexShaderPathname;
                windowConfig.FragmentShaderPathname = Defaults.IdleFragmentShaderPathname;

                ctsRunningInstance = new();
                win = new(windowConfig, audioConfig, ctsRunningInstance.Token);
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
                // Stephen Cleary says disposal is unnecessary as long as the token is cancelled
                ctsSwitchPipe?.Cancel();
                ctsRunningInstance?.Cancel(); // in case of exception...
                win?.Dispose();
            }
        }

        private static string ProcessExecutionSwitches(string[] args)
        {
            // TODO - figure out execution switches ... -next -quit etc.
            return string.Empty;
        }

        private static void ProcessStartupSwitches(string[] args)
        {
            // TODO - figure out startup switches?
        }
    }
}
