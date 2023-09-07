# monkey-hi-hat <img src="https://github.com/MV10/volts-laboratory/blob/master/misc/mhh-icon.png" height="32px"/>

**Streaming music visualization host**

> Version 2.0.0 has been [released](https://github.com/MV10/monkey-hi-hat/releases)!

All important documentation has been moved to the [wiki](https://github.com/MV10/monkey-hi-hat/wiki).

This application intercepts audio using OpenAL and my [eyecandy](https://github.com/MV10/eyecandy) audio-to-texture library, allowing the creation of various audio-reactive OpenGL shaders as interesting visualizations to accompany the music. Playlists of these visualizations can be created with various criteria for rotating among the listed shaders.

Requires .NET 6 and runs under Windows or Linux.

Version 2 is a _major_ update with many new features, architectural improvements and more. In particular, shader crossfade is here, and a new multi-pass shader model is available, allowing for dramatically more complex effects. This version requires OpenGL 4.6 support (which should be widely available, and is apparently the final version of OpenGL now that Khronos is down the rabbit-hole of the "new" Vulkan API).

As the computer will be stashed away behind the AV equipment, remote control was an essential feature. Windows or Android users can install the convenient [monkey-droid](https://github.com/MV10/monkey-droid) GUI (the installers are available from this repo's release page) -- these can control monkey-hi-hat running on a Linux host, but there is no Linux GUI. Alternately, basic command-line-style control is via SSH terminal connections, sending commands (like "refresh the playlist" or "switch to shader XYZ") via named-pipe using my [CommandLineSwitchPipe](https://github.com/MV10/CommandLineSwitchPipe) library. See the wiki for details about setting up and using SSH.

Sample shaders and playlists, accompanying configuration files, icons, and the origin of all these oddball names can be found in my [Volt's Laboratory](https://github.com/MV10/volts-laboratory) repository.

_**v1.2.0 is the final release supporting the Raspberry Pi and ARM32HF.**_ My original goal was to run this on a Raspberry Pi 4B, however the GPU can only handle relatively simple shaders with any decent frame rate, and OpenGL ES can't handle some of the future changes I'm planning, so my target is now a Windows-based mini-PC. However, this should still work on other Linux hardware. The wiki has instructions about OS configuration for audio capture on both platforms.

### Planned features
* Multi-pass shader support
* Lightweight remote-launcher service
* Visualizer plugin support
* Eyecandy audio processing plugin support (maybe)

### Known issues
* OpenAL audio library exception at exit; no impact but needs to be fixed
* Doesn't temporarily disable screensavers and/or sleep timers

### Sample video
Although YouTube _badly_ mangles the quality (not sure why, it was recorded at 1080P and 60FPS), click this for a quick video (1m20s) which cycles through a few of the visualizers -- but seriously, the real thing looks fantastic by comparison -- also, the audio recording came out weird (you get what you pay for, I guess):

[![sample video](http://img.youtube.com/vi/mcuK73TyRRA/0.jpg)](http://www.youtube.com/watch?v=mcuK73TyRRA)

