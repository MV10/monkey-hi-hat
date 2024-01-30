using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace mhhinstall
{
    public class Installer
    {
        public static readonly Version appVersion = new Version("4.0.0");
        public static readonly string dotnetVer = "8";

        public static readonly string temp = Path.GetTempPath();
        public static readonly string log = Path.Combine(temp, "install-monkey-hi-hat.log");
        public static readonly string tempUnzipDir = Path.Combine(temp, "mhh-unzip");
        public static readonly string tempDotnetExe = Path.Combine(temp, "mhh-installer-dotnet.exe");
        public static readonly string tempProgramZip = Path.Combine(temp, "mhh-program.zip");
        public static readonly string tempContentZip = Path.Combine(temp, "mhh-content.zip");

        public static readonly string dotnetUrl = "https://download.visualstudio.microsoft.com/download/pr/7f4d5cbc-4449-4ea5-9578-c467821f251f/b9b19f89d0642bf78f4b612c6a741637/dotnet-runtime-8.0.0-win-x64.exe";
        public static readonly string programUrl = "https://mcguirev10.com/assets/misc/mhh-app-3-1-0.bin";
        public static readonly string contentUrl = "https://mcguirev10.com/assets/misc/mhh-content-3-1-0.bin";

        public static readonly string programPath = "C:\\Program Files\\mhh";
        public static readonly string contentPath = "C:\\ProgramData\\mhh-content";
        public static readonly string postInstallUrl = "https://github.com/MV10/monkey-hi-hat/wiki/Post%E2%80%90Install%E2%80%90Instructions";

        public static readonly string shortcutDesktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Monkey Hi Hat.lnk");
        public static readonly string shortcutStartMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Monkey-Hi-Hat");
        public static readonly string shortcutStartupLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Startup", "Monkey Hi Hat.lnk");

        // Only used to remove unneeded components installed with prior releases.
        public static readonly string driverUrl = "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack43.zip";
        public static readonly string tempDriverZip = Path.Combine(temp, "mhh-installer-driver.zip");

        // What is already installed
        public static bool dotnetOk = false;
        public static bool audioDriverFound = false; // pre-v4, offer to remove
        public static bool openALFound = false; // pre-v4, offer to remove
        public static bool msmdRunning = false;
        public static Version versionFound = new Version(0,0,0); // based on version.txt, or 1.0.0 if missing
        public static bool programFound = false;
        public static bool contentFound = false;

        static void Main(string[] args)
        {
            try
            {
                Init();

                // Call FreshInstall, UpgradeInstall, or Uninstall
                if(!programFound)
                {
                    Output.Write("Do you want to proceed with the install?");
                    var doinstall = Output.Prompt("YN");
                    if(doinstall == "N")
                    {
                        Output.Write("Canceled at user's request.");
                        PauseExit();
                    }
                    AppInstall.Execute();
                }
                else
                {
                    if(versionFound == appVersion)
                    {
                        Output.Write("Do you want to uninstall the application?");
                        var douninstall = Output.Prompt("YN");
                        if(douninstall == "N")
                        {
                            Output.Write("Canceled at user's request.");
                            PauseExit();
                        }
                        AppUninstall.Execute();
                    }
                    else
                    {
                        Output.Write("Do you want to upgrade this older installation?\n[Y]  Yes (upgrade)\n[N]  No (quit)\n[U]  Uninstall only");
                        var doupgrade = Output.Prompt("YNU");
                        if(doupgrade == "N")
                        {
                            Output.Write("Canceled at user's request.");
                            PauseExit();
                        }
                        if(doupgrade == "Y")
                        {
                            AppUpgrade.Execute();
                        }
                        else
                        {
                            AppUninstall.Execute();
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Output.LogOnly($"\nException:\n{ex.GetType()}\n{ex.Message}");
            }

            PauseExit();
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // Create the log and temp directories, collect and display system info.
        /////////////////////////////////////////////////////////////////////////////////////////////////////
        static void Init()
        {
            File.WriteAllText(log, $"INSTALLATION STARTED {DateTime.Now}\n");
            Console.Clear();

            Output.LogOnly($"Creating temp unzip path: {tempUnzipDir}");
            Directory.CreateDirectory(tempUnzipDir);

            // Under .NET Framework, for some reason Environment.OSVersion.Version.Major doesn't work right. Sigh.
            if (!Environment.Is64BitOperatingSystem)
            {
                Output.Write("The application requires 64-bit Windows 10 or Windows 11.");
                PauseExit();
            }

            Output.Write($"Monkey Hi Hat v{appVersion} Installer");
            Output.Separator();
            Output.Write("Collecting system information:\n");
            
            // check for dotnet
            dotnetOk = External.FindString($"Microsoft.NETCore.App {dotnetVer}.", "dotnet --list-runtimes");

            // check for audio loopback driver
            audioDriverFound = External.FindString("VB-Audio Virtual Cable", "driverquery");

            // check for OpenAL libs
            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var t1 = Path.Combine(win, "System32", "OpenAL32.dll");
            var t2 = Path.Combine(win, "SysWOW64", "OpenAL32.dll");
            var t3 = Path.Combine(win, "System32", "soft_oal.dll");
            var t4 = Path.Combine(win, "SysWOW64", "soft_oal.dll");
            openALFound = File.Exists(t1) || File.Exists(t2) || File.Exists(t3) || File.Exists(t4);

            // check for MHH
            if(File.Exists(Path.Combine(programPath, "mhh.exe")))
            {
                versionFound = new Version(1, 0, 0); // version unknown
                var vtxt = Path.Combine(programPath, "ConfigFiles", "version.txt");
                if (File.Exists(vtxt))
                {
                    vtxt = File.ReadLines(vtxt).FirstOrDefault() ?? "1.0.0";
                    try { versionFound = new Version(vtxt); } catch { }
                }
            }
            programFound = versionFound.Major > 0;

            // check for content
            contentFound = File.Exists(Path.Combine(contentPath, "libraries", "color_conversions.glsl"));

            // check for relay service
            msmdRunning = External.FindString("RUNNING", "sc query \"Monkey Hi Hat TCP Relay (msmd)\"");

            // display results
            Output.Write($"Monkey Hi Hat already installed at default location? {programFound}");
            Output.Write($"Visualizer content installed at default location? {contentFound}");
            Output.Write($".NET runtime version {dotnetVer} installed? {dotnetOk}");
            Output.Write($"TCP relay service running? {msmdRunning}");
            Output.Write($"Legacy audio loopback support installed? {audioDriverFound || openALFound}");
            Output.Separator();
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // Reusable utility stuff below
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        public static void PauseExit()
        {
            // Wrap up the logs, clean up, wait for keypress to exit.

            Output.LogOnly("Removing temporary files.");
            SilentDeleteFile(tempContentZip);
            SilentDeleteFile(tempDotnetExe);
            SilentDeleteFile(tempDriverZip);
            SilentDeleteFile(tempProgramZip);

            Output.LogOnly($"Removing temp unzip path: {tempUnzipDir}");
            DeleteUnzipDir();

            Output.LogOnly($"\nINSTALLATION ENDED {DateTime.Now}");
            Console.WriteLine($"\nInstallation log: {log}");
            Console.WriteLine("\nPress any key to exit.");
            Console.ReadKey(true);
            Environment.Exit(0);
        }

        public static void DeleteUnzipDir(bool recreate = false)
        {
            SilentDeleteDir(tempUnzipDir);
            if (recreate) Directory.CreateDirectory(tempUnzipDir);
        }

        public static void RemoveLoopbackDriver()
        {
            Output.Write("Removing legacy audio loopback driver...");
            DeleteUnzipDir(recreate: true);
            ZipFile.ExtractToDirectory(tempDriverZip, tempUnzipDir);
            var exe = Path.Combine(tempUnzipDir, "VBCABLE_Setup_x64.exe");
            External.ExecuteCmd($"{exe} -u -h");
        }

        public static void DeleteOpenAL()
        {
            Output.Write("Removing legacy audio libraries...");
            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            SilentDeleteFile(Path.Combine(win, "System32", "OpenAL32.dll"));
            SilentDeleteFile(Path.Combine(win, "SysWOW64", "OpenAL32.dll"));
            SilentDeleteFile(Path.Combine(win, "System32", "soft_oal.dll"));
            SilentDeleteFile(Path.Combine(win, "SysWOW64", "soft_oal.dll"));
        }

        public static void SilentDeleteDir(string path)
        {
            try { new DirectoryInfo(path).Delete(true); } catch { }
        }

        public static void SilentDeleteFile(string pathname)
        {
            try { if (File.Exists(pathname)) File.Delete(pathname); } catch { }
        }

        public static void StopMSMD()
        {
            External.ExecuteCmd($"\"{Path.Combine(programPath, "WinServiceStop.cmd")}\"");
            External.ExecuteCmd($"\"{Path.Combine(programPath, "WinServiceDelete.cmd")}\"");
        }

        public static void StartMSMD()
        {
            External.ExecuteCmd($"\"{Path.Combine(programPath, "WinServiceCreate.cmd")}\"");
            External.ExecuteCmd($"\"{Path.Combine(programPath, "WinServiceRun.cmd")}\"");
        }
    }
}
