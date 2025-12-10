### About this TO-DO list

I will not accept a PR which alters this file.

If you see something that interests you, open an Issue to discuss the details.

Don't assume anything here is working or will be available in some future release. Previously this was not pushed to the repo, but since I'd hate to lose it, I added it to source control. It describes things I've only done locally so far, or unreleased changes, ideas, plans, wishlist items, and so on.


### Terminal Paths

Remember to set an env var to always use `mhh.debug.conf`:
  sudo nano /etc/profile.d/monkeyhihat-dev.sh
  export MONKEY_HI_HAT_CONFIG=/data/Source/monkey-hi-hat/mhh/mhh/ConfigFiles/mhh.debug.conf
  (logout)

C:\Source\monkey-hi-hat\mhh\mhh\bin\x64\Debug\net8.0
/data/Source/monkey-hi-hat/mhh/mhh/bin/x64/Debug/net8.0


### Version and Changelog

* 5.2.0 released 2025-12-07
* https://github.com/MV10/monkey-hi-hat/wiki/12.-Changelog
 
* 5.3.0 WIP
* Test mode - show keys on screen
* Test mode - abort when `--load` or similar commands are issued
* Test Content - remove SLN folder, use directory via Rider "attached" section
* Test Content - add `TestingExcludePaths` to `[linux]` and `[windows]` sections
* Config - remove `TestingSkipVizCount` and `TestingSkipFXCount`


### Work In Progress
 
* GLFW - add window icon (NativeWindowSettings.Icon = Image @ 256x256 RGBA)
 

### MHH TODO

* Make a Proto video (1080x1920)
* Write "update config" utility to run after install for both Linux and Windows?
* Linux - figure out .deb packaging and hosting a package repo
* Releases - csproj conditional copy based on OS build target
* Local - why does living room PC no longer see Spotify tracks?
* Local - check living room PC's TCP relay service
* Linux - change to event model for track changes?
* Windows - https://github.com/DubyaDude/WindowsMediaController
* Linux - detect when media player changes
* Paylist - auto-advance on track change (after WMC & DBus support)
* Linux - terminal-hiding support (X11 only?)
* Linux - TCP relay service?
* OMT Streaming https://github.com/openmediatransport
* On-screen warning when log file reaches a certain size (with persistence options)
* Limit maximum log file size
* Refuse to run a streaming-oriented FX if a streaming viz is running?
* Global error logger via system.appdomain.unhandledexception event
* eyecandy - add Eyecandy.ShaderCompiler error logging category
* Use Spout sender to debug intermediate buffers?
* Document using VLC / NDI (or VLC / Spout?) to create an RTSP feed
* Modernize with GL Direct State Access (https://juandiegomontoya.github.io/modern_opengl.html)
* Wiki - explain OpenGL full-screen behaviors (trying to use 2nd console etc)
* Playlist - hotkey to extend auto-advance time for current viz
* monkey-see-monkey-do - relay delay time
* monkey-see-monkey-do - add utility command(s)
* Logo overlay support (random and playlist)
* Test mode - Failed crossfade compile crashes test mode; finds config but not cached
* config - `DisableCrossfadeCache` option (vs cache size for other shader types)
* Installer - configtest switch (creates sample conf)
* Installer - Start menu link to edit .conf
* Installer - Start menu link to view mhh.log and msmd.log
* Installer - Start menu link to notes.txt as viz credits
* Installer - add tcpargs utility
* Installer - Use winget to retrieve .NET runtime
* Installer - winget distro? https://github.com/Belphemur/SoundSwitch/issues/1220
* Create config GUI (WinForms now available for modern .NET; Qt or GTK for Linux?)
* Playlist - add `[collections]` section (playlist of other playlists)
* Add * support to [FX-Blacklist] section (and update wiki section 6)
* Add alternate [FX-Whitelist] section for large-blacklist visualizers
* Hotkey to popup list of common hotkeys
* Allow aliasing multipass uniform names for reusable utility frag shaders
* Randomized crossfade duration with `CrossfadeRandomMax` (0 disables)
* Frag Quad -> remove inputs per discord convo (see OneNote TODO)
* Add test content to intentionally generate errors
* Use FontAtlasFilename? (update Wiki with info about generating new ones)
* Video generation? Step-wise clocks and timers?


### MHH NON-STARTERS

* Video decoding on background thread: too much locking and context-switching overhead
* Image and video retrieval over HTTP: minimal benefit and caching is too much bookkeeping
* Rendering text once: due to fade re-renders it isn't really worth the effort


### EYECANDY TODO (MAJOR)

* Nuthin' here boss


### MONKEY-DROID TODO

* FUCKING REWRITE (planned for .NET 10 ... maybe MAUI doesn't suck 3 major releases later)
* Truncates "E" on "ERR" responses; displays ERR as viz description? (maybe that's ok?)
* Crashes if playlist tab selected with no server selected
* Newly-added server isn't showing up (Android only?)
* Add playlist `--next fx` button
* Add FX tab
* Fix Util "CLS" label on Android/narrow UI
* Prompt for framerate lock on `--fps` button?
* Add new command buttons:
    * `--fullscreen`
    * `--standby`
    * `--console`


### Posting Demo Videos

* Record at 720P with OBS Studio
* Shrink with ffmpeg:

```
c:\source\_dev_utils_standalone\ffmpeg_20240426\bin\shrinkmp4.cmd

@rem 1GB = approx 23MB @ 320x180 with audio
ffmpeg -i c:\users\jon\desktop\mhh.mp4 
    -vf "scale=trunc(iw/8)*2:trunc(ih/8)*2" -c:v libx264 
    -crf 23 c:\users\jon\desktop\mhh_small.mp4

ffmpeg -i c:\users\jon\desktop\mhh.mp4 -vf "scale=trunc(iw/8)*2:trunc(ih/8)*2" -c:v libx264 -crf 23 c:\users\jon\desktop\mhh_small.mp4

```

* Rename and drag-drop into README.md via Github online editor
* Pull updated content back to the local repo clone


### Old Notes (keepers)

* Cubemap support
    * face unwrap https://www.shadertoy.com/view/tlyXzG
    * loading https://stackoverflow.com/a/4985280/152997
    * usage https://inspirnathan.com/posts/63-shadertoy-tutorial-part-16/
    * use six separate files? https://ogldev.org/www/tutorial25/tutorial25.html
    * Emil has lots of HQ skyboxes https://opengameart.org/content/mountain-skyboxes

