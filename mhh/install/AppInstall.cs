
using System;
using System.IO;

/////////////////////////////////////////////////////////////////////////////////////////////////////
// The program is not currently installed. Dotnet and/or content might be.
/////////////////////////////////////////////////////////////////////////////////////////////////////
namespace mhhinstall
{
    public static class AppInstall
    {
        public static void Execute()
        {
            Console.Clear();
            Output.Write("Installing Monkey Hi Hat - Options");
            Output.Separator();

            // use msmd TCP relay service?
            bool startMSMD = false;
            Output.Write("\nUse the TCP relay service? (Requires remote control app like Monkey-Droid.)");
            var usemsmd = Output.Prompt("YN");
            if (usemsmd == "Y")
            {
                Output.Write("The TCP relay service will be registered and started.");
                startMSMD = true;
            }
            else
            {
                Output.Write("The TCP relay service is available but will not be started.");
            }

            // create Desktop shortcut
            bool desktop = false;
            Output.Write("\nDo you want shortcuts on your desktop? (Start Menu shortcuts are always added.)");
            var usedesktop = Output.Prompt("YN");
            if(usedesktop == "Y")
            {
                Output.Write("Program and console shortcuts will be added to your desktop.");
                desktop = true;
            }
            else
            {
                Output.Write("No desktop shortcuts will be created; use the Start Menu shortcuts.");
            }

            // F10 hotkey
            bool hotkey = false;
            Output.Write("\nUse F10 as the Start Menu shortcut hotkey to start the program?");
            var usehotkey = Output.Prompt("YN");
            if (usehotkey == "Y")
            {
                Output.Write("The Start Menu shortcut will set F10 as the hotkey.");
                hotkey = true;
            }
            else
            {
                Output.Write("No shortcut hotkey will be set.");
            }

            // start MHH with Windows
            bool autoStart = false;
            Output.Write("\nStart Monkey Hi Hat when Windows starts? (Can configure to start minimized.)");
            var startup = Output.Prompt("YN");
            if(startup == "Y")
            {
                Output.Write("The program will be added to the Start Menu's Startup group.");
                autoStart = true;
            }
            else
            {
                Output.Write("The program will not be auto-started when Windows starts.");
            }

            Console.Clear();
            Output.Write("Installing Monkey Hi Hat");
            Output.Separator();

            // downloads
            Downloader.GetAppArchive();
            Downloader.GetContentArchive();
            if (!Installer.dotnetOk) Downloader.GetDotnetInstaller();

            // install dotnet
            if (!Installer.dotnetOk)
            {
                Output.Write($"Installing .NET v{Installer.dotnetVer} runtime...");
                External.ExecuteCmd($"{Installer.tempDotnetExe} /install /quiet /norestart");
            }

            // install program and content
            Output.Write("Writing application and content directories...");
            Installer.UnzipApp();
            Installer.UnzipContent();

            // start TCP relay (restart only if it was running before; registering applies auto-start)
            if (Installer.msmdRunning || startMSMD)
            {
                Output.Write("Starting TCP relay service...");
                Installer.StartMSMD();
            }

            // shortcuts and hotkeys https://stackoverflow.com/questions/4897655/
            Output.Write("Creating shortcuts...");
            CreateShortcuts(hotkey, desktop, autoStart);

            // copy mhh.conf template and add path specs
            ConfigHelper.NewInstall();

            // give write permissions on all app/content to the Users group
            Installer.SetDirectoryPermissions();

            Output.Write("");
            Output.Separator();
            Output.Write("Installation completed.");
        }

        static void CreateShortcuts(bool useF10Hotkey, bool desktopShortcuts, bool autoStart)
        {
            if (!Directory.Exists(Installer.shortcutStartMenuFolder)) Directory.CreateDirectory(Installer.shortcutStartMenuFolder);

            var exe = Path.Combine(Installer.programPath, "mhh.exe");
            var cmd = "C:\\Windows\\System32\\cmd.exe";
            var cmdArgs = "/k mhh --help";

            CreateShortcut(Installer.shortcutStartMenuAppLink, exe, Installer.programPath, useF10Hotkey: useF10Hotkey);
            CreateShortcut(Installer.shortcutStartMenuCmdLink, cmd, Installer.programPath, commandArgs: cmdArgs);

            if (desktopShortcuts)
            {
                CreateShortcut(Installer.shortcutDesktopAppLink, exe, Installer.programPath);
                CreateShortcut(Installer.shortcutDesktopCmdLink, cmd, Installer.programPath, commandArgs: cmdArgs);
            }

            if (autoStart)
            {
                CreateShortcut(Installer.shortcutStartupLink, exe, Installer.programPath);
            }
        }

        static void CreateShortcut(string linkPathname, string command, string workingDirectory, string commandArgs = "", bool useF10Hotkey = false)
        {
            Output.LogOnly($"-- Creating shortcut {linkPathname}");

            // F10 is 0x0079... ask Grok.
            ushort key = useF10Hotkey ? (ushort)0x0079 : (ushort)0x0;
            
            ShortcutCreator.CreateShortcut(
                linkPath: linkPathname,
                targetPath: command,
                arguments: commandArgs,
                workingDirectory: workingDirectory,
                description: Installer.wikiUrl,
                hotkey: key);
            
            /*
             This works on Windows using the Windows Scripting Host, but is apparently considered
             old-fashioned or obsolete, and since WSH doesn't exist on Linux, Rider can't compile
             the installer. The replacement is shell32.dll-based and only needs interop declarations.
             This code required this COM reference in csproj:
             
               <ItemGroup>
                 <COMReference Include="IWshRuntimeLibrary">
                   <Guid>{F935DC20-1CF0-11D0-ADB9-00C04FD58A0B}</Guid>
                   <VersionMajor>1</VersionMajor>
                   <VersionMinor>0</VersionMinor>
                   <Lcid>0</Lcid>
                   <WrapperTool>tlbimp</WrapperTool>
                   <Isolated>False</Isolated>
                   <EmbedInteropTypes>True</EmbedInteropTypes>
                 </COMReference>
               </ItemGroup>

            using IWshRuntimeLibrary;
             
            var link = new WshShell().CreateShortcut(linkPathname);
            link.Description = Installer.wikiUrl;
            link.TargetPath = command;
            if (!string.IsNullOrEmpty(commandArgs)) link.Arguments = commandArgs;
            link.WorkingDirectory = workingDirectory;
            if (useF10Hotkey) link.HotKey = "F10";
            link.Save();
            */
        }
    }
}
