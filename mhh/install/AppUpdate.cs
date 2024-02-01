using System;
using System.IO;

/////////////////////////////////////////////////////////////////////////////////////////////////////
// The program is installed, upgrade it (optionally remove legacy components).
/////////////////////////////////////////////////////////////////////////////////////////////////////
namespace mhhinstall
{
    public static class AppUpdate
    {
        public static void Execute()
        {
            Console.Clear();
            Output.Write("Updating Monkey Hi Hat - Options");
            Output.Separator();

            // use msmd TCP relay service?
            bool startMSMD = Installer.msmdRunning;
            if (!Installer.msmdRunning)
            {
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
            }

            // remove OpenAL and VB-Audio driver?
            bool removeAudio = false;
            if (Installer.openALFound || Installer.audioDriverFound)
            {
                Output.Write("\nRemove third-party audio loopback support? (Recommended, no longer used by this app.)");
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
            Output.Write("Updating Monkey Hi Hat");
            Output.Separator();

            // downloads
            Downloader.GetAppArchive();
            Downloader.GetContentArchive();
            if (!Installer.dotnetOk) Downloader.GetDotnetInstaller();
            if (removeAudio && Installer.audioDriverFound) Downloader.GetAudioDriverInstaller();

            // stop TCP relay (show message only if running, but always unregister)
            if (Installer.msmdRunning) Output.Write("Stopping TCP relay service...");
            Installer.StopMSMD();

            // clean up legacy audio support
            if (Installer.openALFound && removeAudio) Installer.RemoveOpenAL();
            if (Installer.audioDriverFound && removeAudio) Installer.RemoveLoopbackDriver();

            // install dotnet
            if(!Installer.dotnetOk)
            {
                Output.Write($"Installing .NET v{Installer.dotnetVer} runtime...");
                External.ExecuteCmd($"{Installer.tempDotnetExe} /install /quiet /norestart");
            }

            // install program and content, and give Users group write permissions
            Output.Write("Updating application and content directories...");
            Installer.UnzipApp();
            Installer.UnzipContent();
            Installer.SetDirectoryPermissions();

            // start TCP relay (restart only if it was running before; registering applies auto-start)
            if (Installer.msmdRunning || startMSMD)
            {
                Output.Write("Restarting TCP relay service...");
                Installer.StartMSMD();
            }

            // update mhh.conf
            ConfUpdate.Execute();

            Output.Write("");
            Output.Separator();
            Output.Write("Update completed.");
        }

    }
}
