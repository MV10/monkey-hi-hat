using System;
using System.IO;

// This was created to facilitate sharing release-specific constants
// and also a few Windows-specific constants needed by the updateconf
// updateconf project used to update existing config files.

namespace mhhinstall
{
    public class ReleaseConstants
    {
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Update these for each app release (content and/or texture version can lag app version)

        public static readonly Version appVersion = new Version("5.4.0");
        //                                                       ^ update version

        public static readonly string programUrl = "https://www.monkeyhihat.com/installer_assets/mhh-win-5-4-0.zip";
        //                                                                                               ^ update version

        public static readonly string contentUrl = "https://www.monkeyhihat.com/installer_assets/mhh-content-5-4-0.zip";
        //                                                                                                   ^ update version

        public static readonly string textureUrl = "https://www.monkeyhihat.com/installer_assets/mhh-texture-5-4-0.zip";
        //                                                                                                   ^ update version
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        // Update this when FFmpeg is updated
        public static readonly string FFmepgUrl = "https://www.monkeyhihat.com/installer_assets/ffmpeg-win-7-1-1.zip";

        // Update this when NDI is updated
        public static readonly string ndiUrl = "https://www.monkeyhihat.com/installer_assets/ndi-6-2-1.zip";
        
        // Update this when Spout is updated
        public static readonly string spoutUrl = "https://www.monkeyhihat.com/installer_assets/spout-2-007-17.zip";
        
        // Update these for dotnet runtime bumps
        public static readonly string dotnetVer = "10";
        public static readonly string dotnetUrl = "https://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.5/dotnet-runtime-10.0.5-win-x64.exe";

        public static readonly string programPath = "C:\\Program Files\\mhh";
        public static readonly string contentPath = "C:\\ProgramData\\mhh-content";
        public static readonly string FFmpegPath = $"{programPath}\\ffmpeg";
        
        public static readonly string temp = Path.GetTempPath();
        public static readonly string log = Path.Combine(temp, "install-monkey-hi-hat.log");
    }
}
