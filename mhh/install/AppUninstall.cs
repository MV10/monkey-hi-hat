using System;

/////////////////////////////////////////////////////////////////////////////////////////////////////
// Remove the program and optionally other elements.
/////////////////////////////////////////////////////////////////////////////////////////////////////
namespace mhhinstall
{
    public static class AppUninstall
    {
        public static void Execute()
        {
            Console.Clear();
            Output.Write("Uninstalling Monkey Hi Hat - Options");
            Output.Separator();

            bool removeDotNet = false;
            bool removeAudio = false;

            // remove dotnet?
            if (Installer.dotnetOk)
            {
                Output.Write($"\nRemove .NET v{Installer.dotnetVer}? (Not recommended; other apps may need it.) ");
                var nukedotnet = Output.Prompt("YN");
                if (nukedotnet == "Y")
                {
                    Output.Write($"The .NET v{Installer.dotnetVer} runtime will be uninstalled.");
                    removeDotNet = true;
                }
                else
                {
                    Output.Write($"Not removing the .NET v{Installer.dotnetVer} runtime.");
                }
            }

            // remove OpenAL and VB-Audio driver?
            if (Installer.openALFound || Installer.audioDriverFound)
            {
                Output.Write("\nRemove third-party audio loopback support? (Recommended.)");
                var nukeaudio = Output.Prompt("YN");
                if (nukeaudio == "Y")
                {
                    Output.Write("The audio loopback driver and libraries will be removed.");
                    removeAudio = true;
                }
                else
                {
                    Output.Write("Not removing audio loopback driver or libraries.");
                }
            }

            Console.Clear();
            Output.Write("Uninstalling Monkey Hi Hat");
            Output.Separator();

            // downloads
            if (removeDotNet) Downloader.GetDotnetInstaller();
            if (removeAudio && Installer.audioDriverFound) Downloader.GetAudioDriverInstaller();

            // stop/unregister monkey-see-monkey-do
            Output.Write("Stopping TCP relay service...");
            Installer.StopMSMD();

            // remove app and related files
            Output.Write("Removing program, shortcuts, content, and startup settings...");
            Installer.SilentDeleteDir(Installer.programPath);
            Installer.SilentDeleteDir(Installer.contentPath);
            Installer.SilentDeleteFile(Installer.shortcutDesktopLink);
            Installer.SilentDeleteFile(Installer.shortcutStartupLink);
            Installer.SilentDeleteDir(Installer.shortcutStartMenu);

            // clean up legacy audio support
            if (Installer.openALFound && removeAudio) Installer.RemoveOpenAL();
            if (Installer.audioDriverFound && removeAudio) Installer.RemoveLoopbackDriver();

            // remove dotnet
            if (removeDotNet)
            {
                Output.Write($"Removing .NET v{Installer.dotnetVer} runtime...");
                External.ExecuteCmd($"{Installer.tempDotnetExe} /uninstall /quiet /norestart");
            }

            Output.Write("");
            Output.Separator();
            Output.Write("Uninstall completed.");
        }

    }
}
