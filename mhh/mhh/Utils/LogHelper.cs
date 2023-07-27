
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;

namespace mhh
{
    /// <summary>
    /// Holds the Microsoft-style ILogger instance and the Serilog LevelSwitch object,
    /// handles loading and prepping the log output file, and changing log-level.
    /// </summary>
    public static class LogHelper
    {
        /// <summary>
        /// Standard Microsoft-style ILogger instance with category name "monkey-hi-hat".
        /// </summary>
        public static Microsoft.Extensions.Logging.ILogger Logger;

        /// <summary>
        /// Used to change log-level on the fly. Call SetLogLevel with an MS LogLevel.
        /// </summary>
        public static LoggingLevelSwitch LevelSwitch;

        /// <summary>
        /// Reads logger settings and prepares the public fields for use.
        /// </summary>
        public static void Initialize(ConfigFile appConfig, bool alreadyRunning)
        {
            var logPath = appConfig.ReadValue(ApplicationConfiguration.SectionOS, "logpath").DefaultString("./mhh.log");
            logPath = Path.GetFullPath(logPath);
            if (!alreadyRunning && File.Exists(logPath)) File.Delete(logPath);

            var logLevel = appConfig.ReadValue("setup", "loglevel").ToEnum(LogLevel.Warning);
            LevelSwitch = new(LevelConvert.ToSerilogLevel(logLevel));

            var cfg = new LoggerConfiguration()
                    .MinimumLevel.ControlledBy(LevelSwitch)
                    .WriteTo.Async(a => a.File(logPath, shared: true));

            if (appConfig.ReadValue("setup", "logtoconsole").ToBool(false)) cfg.WriteTo.Console();

            Log.Logger = cfg.CreateLogger();

            Logger = new SerilogLoggerFactory().CreateLogger("monkey-hi-hat");

            Logger.LogDebug($"ILogger created (PID {Environment.ProcessId})");
        }

        /// <summary>
        /// Changes the minimum logger output level on the fly. Defaults to Warning if
        /// the requested level is not recognized. Returns the level that was set.
        /// </summary>
        public static string SetLogLevel(string msLogLevel)
        {
            var logLevel = msLogLevel.ToEnum(LogLevel.Warning);
            LevelSwitch = new(LevelConvert.ToSerilogLevel(logLevel));
            return logLevel.ToString();
        }
    }
}
