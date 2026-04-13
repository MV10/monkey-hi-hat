
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;
using System.Runtime.InteropServices;
using Serilog.Events;

namespace mhh;

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
    
    private const string STARTUP_CATEGORY = "AppStartupMessage";

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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            PathHelper.ExpandLinuxHomeDirectory(ref logPath);
        }
        logPath = Path.GetFullPath(logPath);
        
        // v5.4.0 let the file logger's settings handle cleanup / retention
        //if (!alreadyRunning && File.Exists(logPath)) File.Delete(logPath);

        var cfg = new LoggerConfiguration();

        // Set minimum log level (applies to log file only; console is hard-coded to Warning)
        var logLevel = appConfig.ReadValue("setup", "loglevel").ToEnum(LogLevel.Warning);
        LevelSwitch = new(LevelConvert.ToSerilogLevel(logLevel));
        cfg.MinimumLevel.ControlledBy(LevelSwitch);

        // Allow dummy AppStartupMessage to always emit Info entries
        cfg.MinimumLevel.Override(STARTUP_CATEGORY, LogEventLevel.Information);

        // Configure outputs
        cfg.WriteTo.Async(a => a.File(
            path: logPath, 
            shared: true, 
            outputTemplate: OUTPUT_TEMPLATE,
            fileSizeLimitBytes: 5 * 1024 * 1024,
            rollingInterval: RollingInterval.Infinite,
            rollOnFileSizeLimit: true,
            retainedFileCountLimit: 10,
            retainedFileTimeLimit: TimeSpan.FromDays(7)
            ));
        
        if (appConfig.ReadValue("setup", "logtoconsole").ToBool(false))
        {
            cfg.WriteTo.Console(
                outputTemplate: OUTPUT_TEMPLATE,
                restrictedToMinimumLevel: LogEventLevel.Warning
                );
        }

        // Log category suppression
        //var suppress = (appConfig.ReadValue("setup", "logsuppress").DefaultString("Eyecandy,CommandLineSwitchPipe")).Split(',', Const.SplitOptions);
        //foreach (var cat in suppress) cfg.Filter.ByExcluding(Matching.FromSource(cat));

        // Log category inclusion
        // https://github.com/serilog/serilog/issues/1191#issuecomment-405914424
        var allow = (appConfig.ReadValue("setup", "logcategories").DefaultString("MHH,Eyecandy,CommandLineSwitchPipe")).Split(',', Const.SplitOptions);
        cfg.Filter.ByExcluding(e =>
        {
            // Disregard events without SourceContext
            if (!e.Properties.TryGetValue("SourceContext", out var sourceContextProperty)) return true;
            
            // Remove leading slashes and surrounding quotes that Serilog sometimes adds in ToString()
            var src = sourceContextProperty.ToString().TrimStart('/', '\\').Trim('"', '\'');
        
            // Always permit the startup marker, regardless of the allow list
            if (string.Equals(src, STARTUP_CATEGORY, Const.CompareFlags)) return false;
        
            foreach (var cat in allow)
            {
                if (src.StartsWith(cat, Const.CompareFlags)) return false;
            }
            
            return true;
        });
        
        // Get this party started
        LoggerFactory = new SerilogLoggerFactory(cfg.CreateLogger(), dispose: true);

        // Clearly indicate start of a new session in the log file
        var startupLogger = LoggerFactory.CreateLogger(STARTUP_CATEGORY);
        startupLogger.LogInformation("".PadLeft(60, '-'));
        startupLogger.LogInformation($"{DateTime.Now:U}");
        startupLogger.LogInformation($"v{Program.VersionNumber}, config {Program.ConfigFilePathname}");

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
