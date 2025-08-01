# Monkey Hi Hat <img src="https://github.com/MV10/volts-laboratory/blob/master/misc/mhh-icon.png" height="32px"/>

## **Streaming Music Visualizer**

Monkey Hi Hat displays colorful, interesting graphics, many of which are audio-reactive -- they move and change in time with whatever music is being played through your PC's speaker outputs.

> [2025-MAY-28: Install or update to version 4.2.0](https://github.com/MV10/monkey-hi-hat/releases)!

As of the latest release, there are nearly _**4,000 combinations**_ of visualizations and effects, plus 17 transitiional (crossfade) effects! Great for DJs, parties and other events!

Playlists and many of the visualizations are customizable. The program and all content is 100% free. It's very stable and trouble-free, I have let it run 24 hours with no crashes or memory leaks.

I encourage shader programmers to contribute new visualizations; see [Creating Visualizations](https://github.com/MV10/monkey-hi-hat/wiki/05.-Creating-Visualizations) in the wiki. If you're a .NET programmer and want to work on bugs or features, see [Contributor's Getting Started](https://github.com/MV10/monkey-hi-hat/wiki/13.-Contributor's-Getting-Started) in the wiki.

## Requirements and Usage

Download and run the installer from the [release](https://github.com/MV10/monkey-hi-hat/releases) page, then read the [documentation home page](https://github.com/MV10/monkey-hi-hat/wiki) for a quick step-by-step first-run walk-through. The docs range from basic usage to advanced customization.

This should run on almost any 64-bit Windows 10 or Windows 11 PC with a decent graphics card and practically any type of audio playback. (Linux support is indefinitely on hold: it works on Raspberry Pi but that GPU is underpowered, it doesn't work on WSL, and I don't have any need for or much interest in maintaining a full-blown Linux desktop.)

> 2024-NOV: The Shadertoy example linked below is not working. It appears Soundcloud is blocking ALL access from Shadertoy at this time, and Shadertoy doesn't offer any other audio support except microphone loopback. I'll keep an eye on the situation but it's beyond my control.

CPU and memory requirements are minimal, 99% of the work is on the graphics card. If your PC can run [this Shadertoy example](https://www.shadertoy.com/view/mtKfWd) full-screen, which is a Monkey Hi Hat visualization with effects that I back-ported, you should be able to run Monkey Hi Hat just fine. No third-party drivers are required (earlier versions did require one). Overhead is so low, on my desktop PC I often run the program on a second monitor while I do other work.

The music reactivity responds to _anything_ your PC is playing from _any_ source, whether that is Spotify, Soundcloud, Pandora, an external device connected to the line-in or surround-sound jacks, MP3s, YouTube, etc. If you can hear it from your speakers, the program can "hear" it too. For on-screen track info, only the native Windows Spotify client is supported. 

Please understand there is no "user interface" -- the program is designed to run full screen, and to be controlled from _another_ PC or Android device. Remote control is optimal but not mandatory, see the _Related material_ section at the end of this page for details. The general idea is to get it running and let it do its thing.

## Sample Videos

Soon I will add a version 4 video which shows crossfade transition effects, text overlays, and cool new visualizers and effects.

Here's a 2 minute look at some of the version 3 post-processing effects released around the end of 2023. These very small videos still have compression artifacts due to Github's file-size limit, but they will give you a good idea of what's possible. The real thing looks about a million times better (especially on a really good screen like big 4K OLED).

https://github.com/MV10/monkey-hi-hat/assets/794270/6705bbe7-e558-4753-b57d-c90b4a07cb89

This is from version 2 released in the summer of 2023, which only had basic visualizations. 

https://github.com/MV10/monkey-hi-hat/assets/794270/9e33ab83-2b93-48f2-8833-6b1c09eb6494


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

I have been asked about music reactivity for DJ usage. Have your "real" mixers, amps and speakers set up separately, then feed a line-in to any PCs connected to the displays. As long as the PC "thinks" it is outputting audio, the visualizers will work -- either leave the PC's speaker-out jacks disconnected, or mute the PC speakers externally. If you mute them from within Windows, no sound will be "seen" by the program, it really is driven by _playback_. If you have a pre-determined music set, you can create a playlist with manual advance (`Switch` mode `External`), and simply use the right-arrow to load the next viz/FX combo manually. You can even script the crossfade transitions.

Content creators should check out my Jan-2024 blog article [Getting Started Tutorial](https://mcguirev10.com/2024/01/20/monkey-hi-hat-getting-started-tutorial.html). Note the article refers to the v3 install script; as of version 4 released Feb-2024, the installer is a stand-alone program that is much easier to use, and the program itself no longer requires third-party drivers and all the associated configuration hassles.

The automatically-installed shaders, playlists, effects, crossfades, accompanying configuration files, icons, and the origin of all these oddball names can be found in my [Volt's Laboratory](https://github.com/MV10/volts-laboratory) repository.

## Known issues

* None

