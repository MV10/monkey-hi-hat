using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;

namespace mhhinstall
{
    public class Installer
    {
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Update these for each app release (content and/or texture version can lag app version)

        public static readonly Version appVersion = new Version("5.2.0");
        //                                                       ^ update version

        public static readonly string programUrl = "https://www.monkeyhihat.com/installer_assets/mhh-win-5-2-0.zip";
        //                                                                                               ^ update version

        public static readonly string contentUrl = "https://www.monkeyhihat.com/installer_assets/mhh-content-5-2-0.zip";
        //                                                                                                   ^ update version

        public static readonly string textureUrl = "https://www.monkeyhihat.com/installer_assets/mhh-texture-5-2-0.zip";
        //                                                                                                   ^ update version
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        // Update this when FFmpeg is updated
        public static readonly string FFmepgUrl = "https://www.monkeyhihat.com/installer_assets/ffmpeg-win-7-1-1.zip";

        // Update this when NDI is updated
        public static readonly string ndiUrl = "https://www.monkeyhihat.com/installer_assets/ndi-6-2-1.zip";
        
        // Update this when Spout is updated
        public static readonly string spoutUrl = "https://www.monkeyhihat.com/installer_assets/spout-2-007-17.zip";
        
        // Update these for dotnet runtime bumps
        public static readonly string dotnetVer = "8";
        public static readonly string dotnetUrl = "https://builds.dotnet.microsoft.com/dotnet/Runtime/8.0.16/dotnet-runtime-8.0.16-win-x64.exe";

        public static readonly string temp = Path.GetTempPath();
        public static readonly string log = Path.Combine(temp, "install-monkey-hi-hat.log");
        public static readonly string tempUnzipDir = Path.Combine(temp, "mhh-unzip");
        public static readonly string tempDotnetExe = Path.Combine(temp, "mhh-installer-dotnet.exe");
        public static readonly string tempProgramZip = Path.Combine(temp, "mhh-program.zip");
        public static readonly string FFmpegZip = Path.Combine(temp, "mhh-ffmpeg.zip");
        public static readonly string tempNDIZip = Path.Combine(temp, "ndi.zip");
        public static readonly string tempSpoutZip = Path.Combine(temp, "spout.zip");
        public static readonly string tempContentZip = Path.Combine(temp, "mhh-content.zip");
        public static readonly string tempTextureZip = Path.Combine(temp, "mhh-content.zip");

        // Any download smaller than 500K is assumed to be bad content (404 HTML page etc)
        public static readonly long minDownloadSize = 500 * 1024;

        public static readonly string programPath = "C:\\Program Files\\mhh";
        public static readonly string contentPath = "C:\\ProgramData\\mhh-content";
        public static readonly string FFmpegPath = $"{programPath}\\ffmpeg";

        public static readonly string wikiUrl = "https://github.com/MV10/monkey-hi-hat/wiki/";
        public static readonly string postInstallUrl = "https://github.com/MV10/monkey-hi-hat/wiki/Post%E2%80%90Install%E2%80%90Instructions";
        public static readonly string troubleshootingUrl = "https://github.com/MV10/monkey-hi-hat/wiki/Troubleshooting";

        public static readonly string shortcutStartMenuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "Monkey-Hi-Hat");
        public static readonly string shortcutStartMenuAppLink = Path.Combine(shortcutStartMenuFolder, "Monkey Hi Hat.lnk");
        public static readonly string shortcutStartMenuCmdLink = Path.Combine(shortcutStartMenuFolder, "Monkey Hi Hat Console.lnk");
        public static readonly string shortcutDesktopAppLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Monkey Hi Hat.lnk");
        public static readonly string shortcutDesktopCmdLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Monkey Hi Hat Console.lnk");
        public static readonly string shortcutStartupLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "Startup", "Monkey Hi Hat.lnk");

        // Optional (really, obsolete) as of v4, only used to remove unneeded components installed with prior releases.
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
                        Output.Write("Do you want to update this older installation?\n[Y]  Yes (update)\n[N]  No (quit)\n[U]  No - Uninstall");
                        var doupdate = Output.Prompt("YNU");
                        if(doupdate == "N")
                        {
                            Output.Write("Canceled at user's request.");
                            PauseExit();
                        }
                        if(doupdate == "Y")
                        {
                            AppUpdate.Execute();
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
                Output.Write($"\nException:\n{ex.GetType()}\n{ex.Message}");
                Output.Write($"\nFor support links and/or manual setup, see the wiki Troubleshooting page:\n{troubleshootingUrl}");
            }

            PauseExit();
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // Create the log and temp directories, collect and display system info, and cleanup / exit.
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        static void Init()
        {
            File.WriteAllText(log, $"INSTALLATION STARTED {DateTime.Now}\n");
            Console.Clear();

            DeleteUnzipDir(recreate: true);

            // Under .NET Framework, for some reason Environment.OSVersion.Version.Major doesn't work right. Sigh.
            if (!Environment.Is64BitOperatingSystem)
            {
                Output.Write("The application requires 64-bit Windows 11.");
                PauseExit();
            }

            Output.Write($"Monkey Hi Hat v{appVersion} Installer");
            Output.Separator();
            Output.Write("\nCollecting system information:\n");

            // check for MHH
            if (File.Exists(Path.Combine(programPath, "mhh.exe")))
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
            Output.LogOnly($"Existing version number (v1 = unknown): {versionFound.Major}.{versionFound.Minor}.{versionFound.Build}");

            // check for content
            contentFound = File.Exists(Path.Combine(contentPath, "libraries", "color_conversions.glsl"));

            // check for relay service
            msmdRunning = External.FindString("RUNNING", "sc query \"Monkey Hi Hat TCP Relay (msmd)\"");

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

            // display results
            Output.Write($"Monkey Hi Hat already installed at default location? {programFound}");
            Output.Write($"Visualizer content installed at default location? {contentFound}");
            Output.Write($".NET runtime version {dotnetVer} installed? {dotnetOk}");
            Output.Write($"TCP relay service running? {msmdRunning}");
            Output.Write($"Legacy audio loopback support installed? {audioDriverFound || openALFound}");
            Output.Separator();
            Output.Write("");
        }

        public static void PauseExit()
        {
            // Wrap up the logs, clean up, wait for keypress to exit.

            Output.LogOnly("Removing temporary files.");
            SilentDeleteFile(tempContentZip);
            SilentDeleteFile(tempTextureZip);
            SilentDeleteFile(tempDotnetExe);
            SilentDeleteFile(tempDriverZip);
            SilentDeleteFile(tempProgramZip);
            SilentDeleteFile(FFmpegZip);
            SilentDeleteFile(tempNDIZip);
            SilentDeleteFile(tempSpoutZip);

            Output.LogOnly($"Removing temp unzip path: {tempUnzipDir}");
            DeleteUnzipDir();

            Output.LogOnly($"\nINSTALLATION ENDED {DateTime.Now}");
            Console.WriteLine($"\nLog can be viewed at:\n{log}");
            Console.WriteLine("\nPress any key to exit.");
            Console.ReadKey(true);
            Environment.Exit(0);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // Small tasks needed by multiple processes (install, uninstall, update)
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates the directory if necessary and unzips the content (with overwrite)
        /// </summary>
        public static void UnzipApp()
        {
            if(!Directory.Exists(programPath))
            {
                Output.Write($"Creating directory {programPath}");
                Directory.CreateDirectory(programPath);
            }
            
            Output.Write("Application archive extraction");
            ZipExtensions.ExtractWithOverwrite(tempProgramZip, programPath);
            
            Output.Write("FFmpeg archive extraction");
            ZipExtensions.ExtractWithOverwrite(FFmpegZip, programPath);
            
            Output.Write("Streaming archive extraction");
            ZipExtensions.ExtractWithOverwrite(tempNDIZip, programPath);
            ZipExtensions.ExtractWithOverwrite(tempSpoutZip, programPath);
        }

        /// <summary>
        /// Removes and re-creates the directory, then unzips the content
        /// </summary>
        public static void UnzipContent()
        {
            if (Directory.Exists(contentPath))
            {
                Output.Write($"Clearing directory {contentPath}");
                Directory.Delete(contentPath, recursive: true);
            }

            Output.Write($"Creating directory {contentPath}");
            Directory.CreateDirectory(contentPath);

            Output.Write("Content-archive extraction");
            ZipExtensions.ExtractWithOverwrite(tempContentZip, contentPath);
            ZipExtensions.ExtractWithOverwrite(tempTextureZip, contentPath);
        }

        /// <summary>
        /// Gives Users group write permissions to app and content directories
        /// </summary>
        public static void SetDirectoryPermissions()
        {
            Output.Write("Setting write permissions for \"Users\" group on application directory...");
            SetACL("Users", programPath);

            Output.Write("Setting write permissions for \"Users\" group on content directory...");
            SetACL("Users", contentPath);
        }

        /// <summary>
        /// Unzips VB-Audio CABLE installer and runs silent uninstall
        /// </summary>
        public static void RemoveLoopbackDriver()
        {
            Output.Write("Removing legacy audio loopback driver...");
            DeleteUnzipDir(recreate: true);
            ZipFile.ExtractToDirectory(tempDriverZip, tempUnzipDir);
            var exe = Path.Combine(tempUnzipDir, "VBCABLE_Setup_x64.exe");
            External.ExecuteCmd($"{exe} -u -h");
        }

        /// <summary>
        /// Deletes OpenAL and OpenAL-Soft DLLs from Windows system dirs
        /// </summary>
        public static void RemoveOpenAL()
        {
            Output.Write("Removing legacy audio libraries...");
            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            SilentDeleteFile(Path.Combine(win, "System32", "OpenAL32.dll"));
            SilentDeleteFile(Path.Combine(win, "SysWOW64", "OpenAL32.dll"));
            SilentDeleteFile(Path.Combine(win, "System32", "soft_oal.dll"));
            SilentDeleteFile(Path.Combine(win, "SysWOW64", "soft_oal.dll"));
        }

        /// <summary>
        /// Terminates TCP listener service and unregisters it
        /// </summary>
        public static void StopMSMD()
        {
            External.ExecuteCmd($"\"{Path.Combine(programPath, "WinServiceStop.cmd")}\"");
            External.ExecuteCmd($"\"{Path.Combine(programPath, "WinServiceDelete.cmd")}\"");
        }

        /// <summary>
        /// Registers TCP listener service for auto-start and starts it
        /// </summary>
        public static void StartMSMD()
        {
            External.ExecuteCmd($"\"{Path.Combine(programPath, "WinServiceCreate.cmd")}\"");
            External.ExecuteCmd($"\"{Path.Combine(programPath, "WinServiceRun.cmd")}\"");
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // General utility functions
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        public static void DeleteUnzipDir(bool recreate = false)
        {
            SilentDeleteDir(tempUnzipDir);
            if (recreate) Directory.CreateDirectory(tempUnzipDir);
        }

        public static void SilentDeleteDir(string path)
        {
            try { new DirectoryInfo(path).Delete(true); } catch { }
        }

        public static void SilentDeleteFile(string pathname)
        {
            try { if (File.Exists(pathname)) File.Delete(pathname); } catch { }
        }

        public static void SetACL(string identity, string path)
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.security.accesscontrol.filesystemrights?view=windowsdesktop-5.0
            // https://learn.microsoft.com/en-us/dotnet/api/system.security.accesscontrol.inheritanceflags?view=windowsdesktop-5.0
            // https://learn.microsoft.com/en-us/dotnet/api/system.security.accesscontrol.propagationflags?view=windowsdesktop-5.0

            // While setting mhh.conf permissions is technically "more correct" ... it's easier to simply
            // remove all restrictions the entire directory structure and contents; users can create backup
            // configurations, alternate configurations, change the built-in playlist, etc.

            var acl = Directory.GetAccessControl(path);

            var rule = new FileSystemAccessRule(
                identity, 
                FileSystemRights.FullControl, 
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, 
                PropagationFlags.None,
                AccessControlType.Allow);

            acl.SetAccessRule(rule);

            Directory.SetAccessControl(path, acl);
        }
    }
}
