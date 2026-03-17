
using System.Runtime.InteropServices;

// Types borrowed from the Windows installer directory
using mhhinstall;

namespace updateconf;

public static class Program
{
    public static string programPath = 
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ReleaseConstants.programPath : LinuxConstants.programPath;
    
    public static string contentPath =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ReleaseConstants.contentPath : LinuxConstants.contentPath;
    
    public static Version versionFound = new Version(0,0,0); // based on version.txt, or 1.0.0 if missing
    
    static void Main(string[] args)
    {
        // Platform-specific log to append
        Output.LogPathname = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ReleaseConstants.log : LinuxConstants.log;

        try
        {
            // No arguments on Windows means initialize the config file
            if (args.Length == 0)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ConfigHelper.NewWindowsInstall();
                    Environment.Exit(0);
                }
                throw new Exception("The installed version number must be supplied");
            }

            // For updating, a single arg is required specifying the version being replaced
            if (args.Length > 1) throw new Exception("The updateconf utility only accepts one argument, the installed version number");

            try { versionFound = new Version(args[0]); } catch { }

            // Apply the updates
            ConfigHelper.Update();
            Environment.Exit(0);
        }
        catch(Exception ex)
        {
            Output.Write($"\nException:\n{ex.GetType()}\n{ex.Message}");
            Environment.Exit(1);
        }
    }
}
