
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;
using Serilog.Filters;

namespace mhh
{
    /// <summary>
    /// Holds the Microsoft-style ILogger instance and the Serilog LevelSwitch object,
    /// handles loading and prepping the log output file, and changing log-level.
    /// </summary>
    public static class LogHelper
    {
        /// <summary>
        /// Used to change log-level on the fly. Call SetLogLevel with an MS LogLevel.
        /// </summary>
        public static LoggingLevelSwitch LevelSwitch;

        // Prefix for all ILoggers created within this app.
        private const string LOGGER_CATEGORY = "MHH";

        // Serilog by default will suppress log categories
        // https://github.com/serilog/serilog/wiki/Formatting-Output
        private const string OUTPUT_TEMPLATE = "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

        private static ILoggerFactory LoggerFactory;

        /// <summary>
        /// Reads logger settings and prepares the public fields for use.
        /// </summary>
        public static void Initialize(ConfigFile appConfig, bool alreadyRunning)
        {
            // Prepare the log file
            var logPath = appConfig.ReadValue(ApplicationConfiguration.SectionOS, "logpath").DefaultString("./mhh.log");
            logPath = Path.GetFullPath(logPath);
            if (!alreadyRunning && File.Exists(logPath)) File.Delete(logPath);

            var cfg = new LoggerConfiguration();

            // Set minimum log level
            var logLevel = appConfig.ReadValue("setup", "loglevel").ToEnum(LogLevel.Warning);
            LevelSwitch = new(LevelConvert.ToSerilogLevel(logLevel));
            cfg.MinimumLevel.ControlledBy(LevelSwitch);

            // Configure outputs
            cfg.WriteTo.Async(a => a.File(logPath, shared: true, outputTemplate: OUTPUT_TEMPLATE));
            if (appConfig.ReadValue("setup", "logtoconsole").ToBool(false))
            {
                cfg.WriteTo.Console(outputTemplate: OUTPUT_TEMPLATE);
            }

            // Log category suppression
            var suppress = (appConfig.ReadValue("setup", "logsuppress").DefaultString("Eyecandy,CommandLineSwitchPipe,HttpFileCache")).Split(',', StringSplitOptions.TrimEntries);
            foreach (var s in suppress) cfg.Filter.ByExcluding(Matching.FromSource(s));

            // Get this party started
            LoggerFactory = new SerilogLoggerFactory(cfg.CreateLogger(), dispose: true);

            // Provide the factory to libaries
            CommandLineSwitchPipe.CommandLineSwitchServer.Options.LoggerFactory = LoggerFactory;
            eyecandy.ErrorLogging.LoggerFactory = LoggerFactory;

            // Create loggers for static classes
            RenderingHelper.Logger = CreateLogger(nameof(RenderingHelper));
        }

        /// <summary>
        /// Creates a categorized ILogger with the MHH category prefix. It shouldn't be possible
        /// to call this before LoggerFactory exists, but if this happens a null is returned.
        /// </summary>
        public static Microsoft.Extensions.Logging.ILogger CreateLogger(string category)
            => LoggerFactory?.CreateLogger($"{LOGGER_CATEGORY}.{category}");

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

        /// <summary>
        /// Technically statics don't have a dipose, but this is cleaner.
        /// </summary>
        public static void Dispose()
        {
            Log.CloseAndFlush();
            LoggerFactory?.Dispose();
        }
    }
}
