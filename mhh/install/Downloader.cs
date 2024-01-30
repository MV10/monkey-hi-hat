using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mhhinstall
{
    public static class Downloader
    {
        public static void GetAppArchive()
        {
            Output.Write("Downloading application archive...");
            GetFile(Installer.programUrl, Installer.tempProgramZip);
        }

        public static void GetContentArchive()
        {
            Output.Write("Downloading visualization content archive...");
            GetFile(Installer.contentUrl, Installer.tempContentZip);
        }

        public static void GetDotnetInstaller()
        {
            Output.Write("Downloading .NET install utility...");
            GetFile(Installer.dotnetUrl, Installer.tempDotnetExe);
        }

        public static void GetAudioDriverInstaller()
        {
            Output.Write("Downloading audio driver install utility...");
            GetFile(Installer.driverUrl, Installer.tempDriverZip);
        }

        static void GetFile(string srcURL, string destPathname)
        {
            // https://superuser.com/a/755581/143047
            // curl.exe --output index.html --url https://foobar.com
            External.ExecuteCmd($"curl.exe --output {destPathname} --url {srcURL}");
        }
    }
}
