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

        // key is section, value is setting name
        static Dictionary<string, string> RemovedSettings;

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
            var libPath = Path.Combine(Installer.contentPath, "libraries");
            var fxPath = Path.Combine(Installer.contentPath, "fx");
            var playlistPath = Path.Combine(Installer.contentPath, "playlists");
            var texturePath = Path.Combine(Installer.contentPath, "textures");
            var crossfadePath = Path.Combine(Installer.contentPath, "crossfades");

            AddReplacement("windows", "#VisualizerPath=", "Visualizer Paths", $"VisualizerPath={vizPath};{libPath}");
            AddReplacement("windows", "#FXPath=", "FX Paths", $"FXPath={fxPath};{libPath}");
            AddReplacement("windows", "#PlaylistPath=", "Playlist Path", $"PlaylistPath={playlistPath}");
            AddReplacement("windows", "#TexturePath=", "Texture Path", $"TexturePath={texturePath}");
            AddReplacement("windows", "#CrossfadePath=", "Crossfade Path", $"CrossfadePath={crossfadePath}");
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

            // Code below invokes the config update starting with the version
            // already found on the machine. Each should "chain" to the next one.
            switch(Installer.versionFound.ToString(3))
            {
                case "3.1.0":
                    From_310_to_400();
                    break;

                case "4.0.0":
                    From_400_to_410();
                    break;

                case "4.1.0":
                    From_410_to_420();
                    break;

                case "4.2.0":
                    From_420_to_430();
                    break;

                case "4.3.0":
                    From_430_to_431();
                    break;

                case "4.3.1":
                    From_431_to_440();
                    break;

                case "4.4.0":
                    From_440_to_450();
                    break;

                case "4.5.0":
                    From_450_to_500();
                    break;

                case "5.0.0":
                    From_500_to_510();
                    break;

                //case "5.1.0":
                //    From_510_to_XXX();
                //    break;

                default:
                    Output.Write($"-- No changes; installed version {Installer.versionFound.ToString(3)} is not recognized.");
                    return;
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

            // no config changes

            From_410_to_420();
        }

        static void From_410_to_420()
        {
            Output.Write("-- v4.1.0 to v4.2.0 changes:");

            AddSetting("setup", "SizeY", "StartX / StartY",
                $"\n# SETTING ADDED FOR v4.2.0 UPDATE ON {DateTime.Now}" +
                "\n# Default is 100x100 on the primary monitor. Coordinates on other\n# monitors are in \"virtual screen space\" relative to the primary.\n# The starting window position is optional. Use the --display switch\n# to get a list of monitor names, ID numbers, and coordinate rectanges.\n# The application doesn't perform any validation of starting coords.\nStartX=100\nStartY=100");

            From_420_to_430();
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

        
            From_430_to_431();
        }

        static void From_430_to_431()
        {
            Output.Write("-- v4.3.0 to v4.3.1 changes:");

            AddSetting("setup", "HideMousePointer", "HideWindowBorder",
                $"\n# SETTING ADDED FOR v4.3.1 UPDATE ON {DateTime.Now}" +
                "\n# In windowed mode, controls whether the window is sizeable or fixed\n# size with no border. The default is false. No effect in full-screen.\nHideWindowBorder=false");

            From_431_to_440();
        }

        static void From_431_to_440()
        {
            Output.Write("-- v4.3.1 to v4.4.0 changes:");

            AddSetting("setup", "DetectSilenceAction", "SilenceReplacement",
                $"\n# SETTING ADDED FOR v4.4.0 UPDATE ON {DateTime.Now}" +
                "\n# When DetectSilenceSeconds is 0 (disabled), periods of silence can be\n# replaced by synthetically generated data which can prevent a blank\n# screen with some audio-reactive visualizers. Enable this by setting\n# ReplaceSilenceAfterSeconds to a non-zero value. MinimumSilenceSeconds\n# is the period of time silence must occur to respond. SyntheticAlgorithm\n# determines what kind of data is generated and the other Settings\n# control aspects of the generated data. Refer to the wiki for help.\nMinimumSilenceSeconds=0.25\nReplaceSilenceAfterSeconds=2.0\nSyntheticDataBPM=120\nSyntheticDataBeatDuration=0.1\nSyntheticDataBeatFrequency=440\nSyntheticDataAmplitude=0.5\nSyntheticDataMinimumLevel=0.1\nSyntheticDataAlgorithm=MetronomeBeat");

            From_440_to_450();
        }

        static void From_440_to_450()
        {
            Output.Write("-- v4.4.0 to v4.5.0 changes:");

            AddSetting("setup", "LogToConsole", "LogCategories", @"
# Log categories indicate which parts of the program or a library
# generated a log message. The LogCategories setting is a comma-
# separated list of names which will be included in the log output.
# Since logging can be quite noisy, by defualt all categories are
# suppressed unless expressly included here. The wiki has a full
# list of all available log categories.
LogCategories= MHH, Eyecandy.OpenGL");

            From_450_to_500();
        }

        static void From_450_to_500()
        {
            Output.Write("-- v4.5.0 to v5.0.0 changes:");

            AddSection("text", "ndi", $@"
# SECTION ADDED FOR v5.0.0 UPDATE ON {DateTime.Now}
[ndi]
# Support for network streaming using the NDI protocol. Refer to the
# wiki DJ/VJ section for help using these settings.
NDISender=false
NDIDeviceName=
NDIGroupList=

#########################################################################");

            AddReplacement("windows", "# These are only valid with OpenALSoft", "updated CaptureDeviceName comment", 
@"# Leave this blank for WASAPI loopback. Specify a device name for WASAPI line-in
# or microphone input. For OpenALSoft, leave blank to use the default capture
# device, or specify an exact device name.");

            RemoveSetting("windows", "CaptureDriverName");

            AddSetting("windows", "ShowSpotifyTrackPopups", "Spout support", @"

# Spout is a Windows system used by DJs and VJs for sharing images with other
# applications running on the same PC. Set SpoutSender to true to enable exposing
# rendering output as a Spout source. The Spout name is always ""Monkey Hi Hat"".
SpoutSender=false");

            From_500_to_510();
        }

        static void From_500_to_510()
        {
            Output.Write("-- v5.0.0 to v5.1.0 changes:");

            AddSetting("setup", "LogCategories", "OpenGL error logging", @"
# Because OpenGL errors can be logged at very high frequency, this
# setting limits how often a given error is actually output to the
# log. The default is 1 minute in milliseconds. When the window is
# closed, the log is updated with each rate-limited entry and the
# total number of times the error was raised.
OpenGLErrorThrottle= 60000

# Controls how OpenGL error logging works. Behaviors and any impact
# on performance is driver-specific.
# Normal         Some errors and content suppressed
# DebugContext   Maximum detail, may impact performance
# LowDetail      Normal, single-line output, no execution details
# Disabled       Blocks all OpenGL error logging
OpenGLErrorLogging= Normal

# App developer setting. If true when a debugger is attached,
# a break is triggered if the OpenGL error callback is invoked.
OpenGLErrorBreakpoint= false");

            AddSetting("ndi", "NDIGroupList", "NDI receiver support", @"
# This should be the sender name of an NDI stream source. It can be used with
# [streaming] texture uniforms in visuzliation or FX config files. The value
# must include the machine name followed by the sender name in parenthesis:
#   MACHINE_NAME (SENDER NAME)
# To flip the incoming frames, set NDIReceiveInvert to true (the default).
NDIReceiveFrom=
NDIRecieveInvert=true");

            var crossfadePath = Path.Combine(Installer.contentPath, "crossfades");
            AddSetting("windows", "FXPath=", "Crossfade path", $@"
# location of crossfade shader frag files
CrossfadePath={crossfadePath}");

            AddSetting("windows", "SpoutSender", "Spout receiver support", @"
# This should be the sender name of a Spout stream source. It can be used with
# [streaming] texture uniforms in visuzliation or FX config files. To flip the
# incoming frames, set SpoutReceiveInvert to true (which is the default).
SpoutReceiveFrom=
SpoutReceiveInvert=true");

            //From_500_to_XXX();
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        // These define the change operations that the update functions can register to apply.
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        static void AddSetting(string section, string afterMatching, string newContentName, string newContent)
        {
            if (NewSettings is null) ResetContentCaches();
            var sec = section.ToLowerInvariant();
            if (!NewSettings.ContainsKey(sec)) NewSettings.Add(sec, new Dictionary<string, (string, string)>());
            NewSettings[sec].Add(afterMatching.ToLowerInvariant(), (newContentName, newContent));

            Output.LogOnly($"-- Registered setting {newContentName} to insert after matching token [{section}] \"{afterMatching}\"");
        }

        static void RemoveSetting(string section, string setting)
        {
            if (RemovedSettings is null) ResetContentCaches();
            var sec = section.ToLowerInvariant();
            if (!RemovedSettings.ContainsKey(sec)) RemovedSettings.Add(sec, setting);
        }

        static void AddSection(string afterSection, string newSectionName, string newContent)
        {
            if (NewSections is null) ResetContentCaches();
            var after = afterSection.ToLowerInvariant();
            if (!NewSections.ContainsKey(after)) NewSections.Add(after, new List<(string, string)>());
            NewSections[after].Add((newSectionName, newContent));

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
                    // dump what was accumulated (blanks, comments)
                    newConf.AddRange(nonToken);
                    nonToken.Clear();

                    // token starting a new section, are there any new sections to
                    // insert after the section we're leaving?
                    if (NewSections.ContainsKey(section))
                    {
                        foreach(var newSec in NewSections[section])
                        {
                            Output.Write($"-- Adding section [{newSec.Item1}] after section [{section}]");
                            newConf.Add(newSec.Item2);
                        }
                    }

                    // start the next section
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

                    var key = line.Split('=')[0].Trim().ToLowerInvariant();
                    if(!RemovedSettings.ContainsKey(key)) newConf.Add(line);

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

        static void ResetContentCaches()
        {
            NewSettings = new Dictionary<string, Dictionary<string, (string, string)>>();
            NewSections = new Dictionary<string, List<(string, string)>>();
            LineReplacements = new Dictionary<string, Dictionary<string, (string, string)>>();
            RemovedSettings = new Dictionary<string, string>();
        }
    }
}
