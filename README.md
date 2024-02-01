# Monkey Hi Hat <img src="https://github.com/MV10/volts-laboratory/blob/master/misc/mhh-icon.png" height="32px"/>

## **Streaming Music Visualizer**

> Version 4.0.0 has been [released](https://github.com/MV10/monkey-hi-hat/releases)!

Monkey Hi Hat displays colorful, interesting graphics, many of which are audio-reactive -- they move and change in time with whatever music is being played through your PC. The program and all content are 100% free, and I encourage the public to contribute new visualizations.

As of the version 4 release, there are over 2,000 combinations of visualizations and effects!

Check out my Jan-2024 blog article [Getting Started Tutorial](https://mcguirev10.com/2024/01/20/monkey-hi-hat-getting-started-tutorial.html). (Note the article refers to an install script; as of version 4, the installer is a stand-alone program that is even faster and easier to use.)

## Basic Details

All important documentation has been moved to the [wiki](https://github.com/MV10/monkey-hi-hat/wiki).

Note that the program is designed to run full screen, and to be controlled from another PC or Android device. You _can_ launch it in a window (instead of full screen), and use a local console to pull up content (like loading a playlist), then switch it to full screen. But if you have another device, skip down to the _Related Material_ section of the readme for more information, and also check out the instructions in the wiki.

Version 4 is an important update which introduces text overlays (including Spotify track information, if you're running the native Windows Spotify client), randomized crossfade transitions, and more. Versions 2 and 3 were also major updates with many new features and effects. The Changelog page in the wiki has all the details if you're interested.

It requires a reasonably modern video card supporting OpenGL 4.5, and it runs under Windows 10, and Windows 11. It also requires .NET 8 but the installer handles this for you. (Linux support was temporarily removed, but a bug has been fixed and Linux x64 testing will begin shortly. A tar.gz archive will be added to the release page once everything is verified working.)

The program intercepts audio using my [eyecandy](https://github.com/MV10/eyecandy) audio-to-texture library, allowing the creation of various audio-reactive OpenGL shaders as interesting visualizations to accompany the music. Playlists of these visualizations can be created with various criteria for rotating among the listed shaders.

## Sample Video

Although YouTube _badly_ mangles the video quality (not sure why, it was recorded at 720P and 60FPS), click this for a quick video (3 minutes) which cycles through a few of the visualizers -- but seriously, the real thing looks fantastic by comparison:

[![sample video](http://img.youtube.com/vi/YTmhQm-1bwU/0.jpg)](https://youtu.be/YTmhQm-1bwU)

Here's a look at some of the v3.0 post-processing FX shaders (2 minutes). This one is hosted on Bitchute because the compression artifacts are slightly-less-terrible:

[![sample video](http://img.youtube.com/vi/z9536ebpJDs/0.jpg)](https://www.bitchute.com/video/2sIdHZxskwdN/)

## Related Material

As the computer will be stashed away behind the AV equipment, remote control was an essential feature. Windows or Android users can install the convenient [monkey-droid](https://github.com/MV10/monkey-droid) GUI (the installers are available from this repo's release page) -- these can control monkey-hi-hat running on a Linux host, but there is no Linux GUI. Alternately, basic command-line-style control is via SSH terminal connections, sending commands (like "refresh the playlist" or "switch to shader XYZ") via named-pipe using my [CommandLineSwitchPipe](https://github.com/MV10/CommandLineSwitchPipe) library. See the wiki for details about setting up and using SSH.

Sample shaders and playlists, accompanying configuration files, icons, and the origin of all these oddball names can be found in my [Volt's Laboratory](https://github.com/MV10/volts-laboratory) repository.

_**v1.2.0 is the final release supporting the Raspberry Pi and ARM32HF.**_ My original goal was to run this on a Raspberry Pi 4B, however the GPU can only handle relatively simple shaders with any decent frame rate, and OpenGL ES can't handle some of the future changes I'm planning, so my target is now a Windows-based mini-PC. However, this should still work on other Linux hardware. The wiki has instructions about OS configuration for audio capture on both platforms.

## Known issues

* None

