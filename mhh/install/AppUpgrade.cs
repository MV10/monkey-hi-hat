using System;

/////////////////////////////////////////////////////////////////////////////////////////////////////
// The program is installed, upgrade it (optionally remove legacy components).
/////////////////////////////////////////////////////////////////////////////////////////////////////
namespace mhhinstall
{
    public static class AppUpgrade
    {
        public static void Execute()
        {
            Console.Clear();
            Output.Write("Upgrading Monkey Hi Hat");
            Output.Separator();

            // remove OpenAL and VB-Audio driver?
            bool removeAudio = false;
            if (Installer.openALFound || Installer.audioDriverFound)
            {
                Output.Write("\nRemove third-party audio loopback support? (Recommended, no longer needed by this app.)");
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
                    Output.Write("The TCP relay service will not be started.");
                }
            }

            Console.Clear();
            Output.Write("Upgrading Monkey Hi Hat");
            Output.Separator();

            // downloads
            if (!Installer.dotnetOk) Downloader.GetDotnetInstaller();
            if (removeAudio && Installer.audioDriverFound) Downloader.GetAudioDriverInstaller();

            // stop/unregister TCP relay
            Output.Write("Stopping TCP relay service...");
            Installer.StopMSMD();

            // clean up legacy audio support
            if (Installer.openALFound && removeAudio) Installer.DeleteOpenAL();
            if (Installer.audioDriverFound && removeAudio) Installer.RemoveLoopbackDriver();

            // install dotnet

            // install program and content

            // register/start TCP relay

            // create shortcuts and startup settings

            // set folder permissions

            // update mhh.conf
        }

    }
}
