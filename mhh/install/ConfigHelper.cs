using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// All config changes should be noted in the wiki changelog
// https://github.com/MV10/monkey-hi-hat/wiki/12.-Changelog

// If versionFound is 1.0.0 that means version.txt was found but can't be parsed.
// v3.1.0 was the first to include a version.txt.

// For new releases, add a new function like From_ABC_to_XYZ,
// add the Version logic to the end of the Execute method, and
// use AddSetting and AddSection as seen in From_310_to_400.
// Chain a call from the previous update function to the new one.
// Test that shit!

namespace mhhinstall
{
    public static class ConfigHelper
    {
        static readonly string confPathname = Path.Combine(Installer.programPath, "mhh.conf");

        // outer key is section, value list is (name, content)
        static Dictionary<string, List<(string, string)>> NewSections;

        // outer key is section, inner key is match target, value is (name, content)
        static Dictionary<string, Dictionary<string, (string, string)>> NewSettings;

        // outer key is section, inner key is StartsWith match target, value is (name, content)
        static Dictionary<string, Dictionary<string, (string, string)>> LineReplacements;

        public static void NewInstall()
        {
            Output.Write($"Creating application config file...");

            Output.Write("-- Copying default config to app directory");
            var src = Path.Combine(Installer.programPath, "ConfigFiles", "mhh.conf");
            File.Copy(src, confPathname);

            Output.Write("-- Adding references to viz content directories");

            var vizPath = Path.Combine(Installer.contentPath, "shaders");
            var fxPath = Path.Combine(Installer.contentPath, "fx");
            var libPath = Path.Combine(Installer.contentPath, "libraries");
            var playlistPath = Path.Combine(Installer.contentPath, "playlists");
            var texturePath = Path.Combine(Installer.contentPath, "textures");

            AddReplacement("windows", "#VisualizerPath=", "Visualizer Paths", $"VisualizerPath={vizPath};{libPath}");
            AddReplacement("windows", "#PlaylistPath=", "Playlist Path", $"PlaylistPath={playlistPath}");
            AddReplacement("windows", "#TexturePath=", "Texture Path", $"TexturePath={texturePath}");
            AddReplacement("windows", "#FXPath=", "FX Paths", $"FXPath={fxPath};{libPath}");
            AddReplacement("windows", "#FFmpegPath=", "FFmpeg Path", $"FFmpegPath={Installer.ffmpegPath}");

            ApplyChanges();
        }
        
        public static void Update()
        {
            Output.Write($"Updating application config file...");

            if(!File.Exists(confPathname))
            {
                Output.Write($"-- No changes; config not found at {confPathname}");
                return;
            }

            if(Installer.versionFound.Major == 0)
            {
                Output.Write("-- No changes; Configs prior to v3.1.0 must be updated manually.");
                return;
            }

            if (Installer.versionFound.Major < 3)
            {
                Output.Write("-- No changes; installed version.txt did not parse properly.");
                return;
            }

            Output.Write("-- Copying mhh.conf to mhh.conf.bak in the application directory.");
            File.Copy(confPathname, Path.Combine(Installer.programPath, "mhh.conf.bak"), overwrite: true);

            // Code below nvokes the per-release config updates...

            if(Installer.versionFound.Major == 3)
            {
                if (Installer.versionFound.Minor < 2) From_310_to_400();
            }

            if (Installer.versionFound.Major == 4)
            {
                if (Installer.versionFound.Minor < 2) From_410_to_420();
                if (Installer.versionFound.Minor < 3) From_420_to_430();
                if (Installer.versionFound.Minor == 3 && Installer.versionFound.Revision < 1) From_430_to_431();
            }

            ApplyChanges();
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // Defines the changes from one version to the next
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        static void From_310_to_400()
        {
            Output.Write("-- v3.1.0 to v4.0.0 changes:");

            AddSetting("setup", "LibraryCacheSize", "RandomizeCrossfade",
                $"\n# SETTING ADDED FOR v4.0.0 UPDATE ON {DateTime.Now}" +
                "\n# Default is false, which uses the internal crossfade. When true, the\n# visualization and library paths are searched for .frag files with\n# a crossfade_ prefix (such as crossfade_burning.frag). No conf file\n# is used. See comments in crossfade_burning in Volt's Laboratory for\n# more details. All crossfade shaders are always cached (there is no\n# separate cache size setting).\nRandomizeCrossfade=true\n");

            AddSection("setup", "msmd",
                $"\n# SECTION ADDED FOR v4.0.0 UPDATE ON {DateTime.Now}" +
                "\n#########################################################################\n[msmd]\n# This section is not used by MHH directly. Instead it is read by the\n# https://github.com/MV10/monkey-see-monkey-do background service, which\n# can launch MHH if commands are received while it is not running, and\n# will relay received commands to MHH. The UnsecuredPort option must also\n# be specified for the relay service to work.\n\n# This is the port number the relay service listens on. It is 50002 by\n# default. If it is not specified (or if the MHH UnsecuredPort is not\n# specified) the relay service will log an error and exit.\nUnsecuredRelayPort=50002\n\n# Use \"4\" or \"6\" to restrict the relay service to IPv4 or IPv6 for\n# localhost name resolution. If unspecified, the default is 0 which uses\n# both. Due to an issue in .NET's socket-handling, it can be slow to query\n# IPv6 when the network isn't actually using IPv6.\nRelayIPType=4\n\n# How long the service waits for the application to start before relaying\n# the command. If unspecified, the default is 5 seconds.\nLaunchWaitSeconds=9\n");

            AddSetting("windows", "CaptureDeviceName", "LoopbackApi",
                $"\n# SETTING ADDED FOR v4.0.0 UPDATE ON {DateTime.Now}" +
                "\n# There is probably no need to change this. WindowsInternal uses the built-in\n# Windows multimedia WASAPI loopback support and is the default. The other\n# option is OpenALSoft which requires installing and configuring a separate\n# loopback driver and OpenAL library DLLs. (Linux always uses OpenALSoft.)\nLoopbackApi=WindowsInternal\n");

            // Only here as an example -- each should "chain" to the next higher version as they're released.
            From_400_to_410();
        }

        static void From_400_to_410()
        {
            Output.Write("-- v4.0.0 to v4.1.0, no config changes");

            // Not in use, just an example, see end of above, From_310_to_400

            From_410_to_420();
        }

        static void From_410_to_420()
        {
            Output.Write("-- v4.1.0 to v4.2.0 changes:");

            AddSetting("setup", "SizeY", "StartX / StartY",
                $"\n# SETTING ADDED FOR v4.2.0 UPDATE ON {DateTime.Now}" +
                "\n# Default is 100x100 on the primary monitor. Coordinates on other\n# monitors are in \"virtual screen space\" relative to the primary.\n# The starting window position is optional. Use the --display switch\n# to get a list of monitor names, ID numbers, and coordinate rectanges.\n# The application doesn't perform any validation of starting coords.\nStartX=100\nStartY=100");
        }

        static void From_420_to_430()
        {
            Output.Write("-- v4.2.0 to v4.3.0 changes:");

            AddSetting("setup", "TestingSkipFXCount", "VideoFlip",
                $"\n# SETTING ADDED FOR v4.3.0 UPDATE ON {DateTime.Now}" +
                "\n# This controls how (or if) video files are inverted. Most graphic formats\n# have the origin (pixel 0,0) at the top-left corner, but OpenGL has the\n# origin at the bottom-left corner. Testing seems to indicate Internal is\n# the fastest option, but you can also specify FFmpeg. If you're only using\n# custom videos that are stored inverted, you can set this to None. The\n# default value is Internal.\nVideoFlip=Internal");

            AddSetting("windows", "FXPath", "FFmpegPath",
                $"\n# SETTING ADDED FOR v4.3.0 UPDATE ON {DateTime.Now}" +
                $"\n# location of the FFmpeg binaries; normally the ffmpeg subdirectory under the app install directory\nFFmpegPath={Installer.ffmpegPath}");

        }

        static void From_430_to_431()
        {
            Output.Write("-- v4.3.0 to v4.3.1 changes:");

            AddSetting("setup", "HideMousePointer", "HideWindowBorder",
                $"\n# SETTING ADDED FOR v4.3.1 UPDATE ON {DateTime.Now}" +
                "\n# In windowed mode, controls whether the window is sizeable or fixed\n# size with no border. The default is false. No effect in full-screen.\nHideWindowBorder=false");

        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // These define the change operations that the update functions can register to apply.
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        static void ResetContentCaches()
        {
            NewSettings = new Dictionary<string, Dictionary<string, (string, string)>>();
            NewSections = new Dictionary<string, List<(string, string)>>();
            LineReplacements = new Dictionary<string, Dictionary<string, (string, string)>>();
        }

        static void AddSetting(string section, string afterMatching, string newContentName, string newContent)
        {
            if (NewSettings is null) ResetContentCaches();
            var sec = section.ToLowerInvariant();
            if (!NewSettings.ContainsKey(sec)) NewSettings.Add(sec, new Dictionary<string, (string, string)>());
            NewSettings[sec].Add(afterMatching.ToLowerInvariant(), (newContentName, newContent));

            Output.LogOnly($"-- Registered setting {newContentName} to insert after matching token [{section}] \"{afterMatching}\"");
        }

        static void AddSection(string afterSection, string newSectionName, string newContent)
        {
            if (NewSections is null) ResetContentCaches();
            var sec = afterSection.ToLowerInvariant();
            if (!NewSections.ContainsKey(sec)) NewSections.Add(sec, new List<(string, string)>());
            NewSections[sec].Add((newSectionName, newContent));

            Output.LogOnly($"-- Registered section [{newSectionName}] to insert after section [{afterSection}]");
        }

        // lineStartsWith is case-sensitive!
        static void AddReplacement(string section, string lineStartsWith, string newContentName, string newContent)
        {
            if (LineReplacements is null) ResetContentCaches();
            var sec = section.ToLowerInvariant();
            if (!LineReplacements.ContainsKey(sec)) LineReplacements.Add(sec, new Dictionary<string, (string, string)>());
            LineReplacements[sec].Add(lineStartsWith, (newContentName, newContent));

            Output.LogOnly($"-- Registered {newContentName} to replace line starting with \"{lineStartsWith}\" in [{section}]");
        }

        static void ApplyChanges()
        {
            // Two types of lines are counted as "tokens" ... a [section] entry or a key=value entry.
            // The loop accumulates non-token lines until a token line is encountered, at which point the
            // content cache comparisons are performed. For a section, if a new section is being emitted
            // before that section, it'll be written, then the accumulated content and token line will be
            // written. For a key match, the accumulated content and token line are emitted before the
            // new content is written.

            var oldConf = File.ReadAllLines(confPathname).ToList();
            Output.LogOnly($"-- Read {oldConf.Count} lines from old configuration");

            var newConf = new List<string>(oldConf.Count + NewSections.Count + NewSettings.Count);
            var nonToken = new List<string>();
            var section = "";

            foreach(var oldLine in oldConf)
            {
                // Test line-replacement
                string line = oldLine;
                if (LineReplacements.ContainsKey(section))
                {
                    foreach(var newline in LineReplacements[section])
                    {
                        // when a starts-with match is identified, the
                        // ENTIRE line is replaced with the new content
                        if(line.Trim().StartsWith(newline.Key))
                        {
                            Output.LogOnly($"-- Replacing {newline.Value.Item1} in [{section}]");
                            line = newline.Value.Item2;
                            break;
                        }
                    }
                }

                // Accumulate non-token line
                if(string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("#"))
                {
                    // accumulate whitespace and comments in case we hit a section
                    // token next and have to insert a new section ahead of that
                    nonToken.Add(line);
                }
                
                // Start new section
                else if (line.Trim().StartsWith("[") && line.Contains("]"))
                {
                    // token starting a new section, are there any new sections to
                    // insert after the section we're leaving?
                    if(NewSections.ContainsKey(section))
                    {
                        foreach(var newSec in NewSections[section])
                        {
                            Output.Write($"-- Adding section [{newSec.Item1}] after section [{section}]");
                            newConf.Add(newSec.Item2);
                        }
                    }

                    // dump what was accumulated (blanks, comments, and start the next section
                    newConf.AddRange(nonToken);
                    nonToken.Clear();
                    newConf.Add(line);

                    // extract and store the section name
                    var start = line.IndexOf("[");
                    var length = line.IndexOf("]") - start - 1;
                    section = line.Substring(start + 1, length).ToLowerInvariant();
                    Output.LogOnly($"-- Entering config section [{section}]");
                }
                
                // Key=Value token
                else if (line.Contains("="))
                {
                    // token should be a key=value entry, dump everything we've
                    // been accumulating, dump this line, then see if we have
                    // any NewSettings matches to emit
                    newConf.AddRange(nonToken);
                    nonToken.Clear();
                    newConf.Add(line);

                    if(NewSettings.ContainsKey(section))
                    {
                        Output.LogOnly($"-- Evaluating setting [{section}] {line.Trim()}");
                        var l = line.ToLowerInvariant();
                        foreach(var newSet in NewSettings[section])
                        {
                            if(l.Contains(newSet.Key))
                            {
                                Output.Write($"-- Adding setting {newSet.Value.Item1} to section [{section}]");
                                newConf.Add(newSet.Value.Item2);
                            }
                        }
                    }
                }
                
                // Accumulate miscellaneous non-token line
                else
                {
                    // no idea what the hell this is, store it and move on
                    nonToken.Add(line);
                }

            }

            // dump any leftovers that were accumulated
            if (nonToken.Count > 0) newConf.AddRange(nonToken);

            // output the new content ... fingers crossed!
            File.WriteAllLines(confPathname, newConf.ToArray());
            Output.LogOnly($"-- Wrote {newConf.Count} lines as new configuration");

            NewSettings = null;
            NewSections = null;
            LineReplacements = null;
        }
    }
}
