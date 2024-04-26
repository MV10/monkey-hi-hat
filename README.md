# Monkey Hi Hat <img src="https://github.com/MV10/volts-laboratory/blob/master/misc/mhh-icon.png" height="32px"/>

## **Streaming Music Visualizer**

Monkey Hi Hat displays colorful, interesting graphics, many of which are audio-reactive -- they move and change in time with whatever music is being played through your PC's speaker outputs.

> [2024-02-01: Install or update to version 4.0.0](https://github.com/MV10/monkey-hi-hat/releases)!

As of the latest release, there are more than _**2,000 combinations**_ of visualizations and effects! Great for DJs, parties and other events!

Playlists and many of the visualizers are customizable. The program and all content are 100% free, and I encourage users to contribute new visualizations.

## Requirements and Usage

Get the installer from the [release](https://github.com/MV10/monkey-hi-hat/releases) page, then read the documentation [wiki home page](https://github.com/MV10/monkey-hi-hat/wiki) for a quick step-by-step first-run walk-through. The docs range from basic usage to advanced customization.

This should run on almost any 64-bit Windows 10 or Windows 11 PC with a decent graphics card and practically any type of audio playback. Linux support is in testing and will hopefully be available soon.

 CPU and memory requirements are minimal, 99% of the work is on the graphics card. If your PC can run this [Shadertoy](https://www.shadertoy.com/view/mtKfWd) example full-screen (it's a Monkey Hi Hat visualization with effects that I back-ported), you should be able to run Monkey Hi Hat just fine. No third-party drivers are required (which earlier versions did require). Overhead is so low, on my desktop PC I often run the program on a second monitor while I do other work. It's very stable and trouble-free, I have let it run 24 hours with no crashes or memory leaks.

Please understand there is no "user interface" -- the program is designed to run full screen, and to be controlled from _another_ PC or Android device. Remote control is optimal but not mandatory, see the _Related material_ section at the end of this page for details. The general idea is to get it running and let it do its thing.

The music reactivity responds to _anything_ your PC is playing from _any_ source, whether that is Spotify, Soundcloud, Pandora, an external device connected to the line-in jack, MP3s, etc. If you can hear it from your speakers, the program can "hear" it too. For on-screen track info, only the native Windows Spotify client is supported. 

## Sample Videos

Although YouTube's compression _badly_ mangles the video quality (even though it was recorded at 720P and 60FPS), click this for a quick 3 minute video which cycles through a few of the visualizers -- but seriously, the real thing looks fantastic by comparison:

[![sample video](http://img.youtube.com/vi/YTmhQm-1bwU/0.jpg)](https://youtu.be/YTmhQm-1bwU)

The video above was from v2, which only had basic visualizers. Here's a 2 minute look at some of the v3 post-processing effects and a different series of visualizers. This one is hosted on Bitchute because the compression artifacts are slightly less terrible (but still far worse than how it looks in real life):

[![sample video](http://img.youtube.com/vi/z9536ebpJDs/0.jpg)](https://www.bitchute.com/video/2sIdHZxskwdN/)

Soon I will add a v4 video which shows crossfade transition effects, text overlays, and even more cool visualizers and effects (all undoubtedly mangled by YouTube's talking-head-oriented compression algorithms).

## Related Material

In my living room setup where we watch this most often, the computer running Monkey Hi Hat is meant to be hidden from view like all the other AV equipment, so remote control was an essential feature. There are four options:

* Recommended: install the convenient [Monkey Droid](https://github.com/MV10/monkey-droid) GUI

    * [Windows](https://github.com/MV10/monkey-hi-hat/releases/download/3.1.0/monkeydroid_1.0.1.0_x86.msix) installer

    * [Android](https://github.com/MV10/monkey-hi-hat/releases/download/3.1.0/com.mindmagma.monkeydroid.apk) APK package

    * See the documentation _Quick Start_ for usage instructions

* Command-line control is via SSH terminal connections. See the documentation _Quick Start_ for details about setting up and using SSH. My systems are configured for this, but frankly Monkey Droid is so much easier to use, I never actually connect via SSH any more.

* Old school: a wireless keyboard with integrated trackball or other pointer control, which is obviously handy for more than just controlling this program. The program responds to quite a few useful keystrokes. As usual, please refer to the documentation, but my keyboard is hidden off to the side of the couch, and I most commonly use just a four keyboard commands that I can readily find by touch:

    * `Right Arrow` to skip to the next visualization
    * `Down Arrow` to add an FX to the current visualizer
    * `W` (for WHAT?) to show the names of the current vizualizer / FX
    * `T` to show the name of the Spotify track being played

* Although remote control is the intended usage, control from the same PC is certainly possible. Configure the program to launch in a window, start another console window to issue commands, get it showing the content you want (such as a playlist), then either issue the `--fullscreen` command or focus on the window and hit the spacebar. This is how the documentation's first-time walk-through works.

I have been asked about music reactivity for DJ usage. Have your "real" mixers, amps and speakers set up separately, then feed a line-in to any PCs connected to the displays. As long as the PC "thinks" it is outputting audio, the visualizers will work -- either leave the PC's speaker-out jacks disconnected, or mute the PC speakers externally. If you mute them from within Windows, no sound will be "seen" by the program, it really is driven by _playback_. If you have a pre-determined music set, you can create a playlist with manual advance, and simply use the right-arrow to load the next viz/FX combo manually.

Content creators should check out my Jan-2024 blog article [Getting Started Tutorial](https://mcguirev10.com/2024/01/20/monkey-hi-hat-getting-started-tutorial.html). Note the article refers to the v3 install script; as of v4, the installer is a stand-alone program that is much easier to use, and the program itself no longer requires third-party drivers and all the associated configuration hassles.

The automatically-installed shaders, playlists, effects, crossfades, accompanying configuration files, icons, and the origin of all these oddball names can be found in my [Volt's Laboratory](https://github.com/MV10/volts-laboratory) repository.

## Known issues

* None

