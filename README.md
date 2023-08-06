# monkey-hi-hat <img src="https://github.com/MV10/volts-laboratory/blob/master/misc/mhh-icon.png" height="32px"/>

**Streaming music visualization host**

> Version 1.0.0 has been [released](https://github.com/MV10/monkey-hi-hat/releases)!

All important documentation has been moved to the [wiki](https://github.com/MV10/monkey-hi-hat/wiki).

This application intercepts audio using OpenAL and my [eyecandy](https://github.com/MV10/eyecandy) audio-to-texture library, allowing the creation of various audio-reactive OpenGL shaders as interesting visualizations to accompany the music. Playlists of these visualizations can be created with various criteria for rotating among the listed shaders.

Although I still consider this a work-in-progress, I try to keep the main branch of the repository in a working state. It currently requires .NET 6 and runs under Windows or Linux. My original goal was to run this on a Raspberry Pi 4B, however the GPU can only handle relatively simple shaders with any decent frame rate, so my target is now a Windows-based mini-PC. However, I will still test on the Pi and document that setup process (which is enormously more complicated than Windows), so I'm guessing this will still work on other Linux hardware, too. The eyecandy repo has (or will have) instructions about OS configuration for audio capture on both platforms.

As the computer will be stashed away behind the AV equipment, control is via SSH terminal connections, sending commands (like "refresh the playlist" or "switch to shader XYZ") via named-pipe using my [CommandLineSwitchPipe](https://github.com/MV10/CommandLineSwitchPipe) library. And maybe if I'm feeling frisky, I'll write a simple Android app, too (keep an eye out for this at my [monkey-droid](https://github.com/MV10/monkey-droid) repository).

Sample shaders, accompanying configuration files, and where all these oddball names came from can be found in my [Volt's Laboratory](https://github.com/MV10/volts-laboratory) repository.

Click this for a quick video (1m20s) which cycles through a few of the visualizers:

[![sample video](http://img.youtube.com/vi/mcuK73TyRRA/0.jpg)](http://www.youtube.com/watch?v=mcuK73TyRRA)

