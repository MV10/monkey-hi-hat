# Monkey Hi Hat <img src="https://github.com/MV10/volts-laboratory/blob/master/misc/mhh-icon.png" height="32px"/>

## **Streaming Music Visualizer**

> Version 3.1.0 has been [released](https://github.com/MV10/monkey-hi-hat/releases)!

Monkey Hi Hat displays colorful, interesting graphics, many of which are audio-reactive -- they move and change in time with whatever music is being played through your PC. The program and all content are 100% free, and I encourage the public to contribute new visualizations.

**NEW:** Version 3 releases include an easy-to-use install script.

## Basic Details

All important documentation has been moved to the [wiki](https://github.com/MV10/monkey-hi-hat/wiki).

It requires .NET 8, an OpenGL 4.6 GPU and drivers, and it runs under Windows 10, and Windows 11. Linux support was temporarily removed, but a bug has been fixed and Linux x64 testing will begin shortly. A tar.gz archive will be added to the release page once everything is verified working. Setup details are covered in the _Quick Start_ sections of the Wiki.

The program intercepts audio using OpenAL and my [eyecandy](https://github.com/MV10/eyecandy) audio-to-texture library, allowing the creation of various audio-reactive OpenGL shaders as interesting visualizations to accompany the music. Playlists of these visualizations can be created with various criteria for rotating among the listed shaders.

Version 2 was a _major_ update with many new features, architectural improvements and more. In particular, shader crossfade is here, and a new multi-pass shader model is available, allowing for dramatically more complex effects. This version requires OpenGL 4.6 support (which should be widely available, and is apparently the final version of OpenGL now that Khronos is down the rabbit-hole of the "new" Vulkan API).

Version 3 is another _huge_ update which takes full advantage of the big changes in version 2. The addition of post-processing effects (aka "FX") are the most obvious change, but head to the wiki change-log to see the giant list of new features. Additionally, the wiki has been almost completely rewritten with all of the details, and Volt's Laboratory has a huge number of new visualizations and effects.

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

