### About this TO-DO list

I will not accept a PR which alters this file.

If you see something that interests you, open an Issue to discuss the details.

Don't assume anything here is working or will be available in some future release. Previously this was not pushed to the repo, but since I'd hate to lose it, I added it to source control. It describes things I've only done locally so far, or unreleased changes, ideas, plans, wishlist items, and so on.


### Terminal Paths

Remember to set an env var to always use `mhh.debug.conf`:
  sudo nano /etc/profile.d/monkeyhihat-dev.sh
  export MONKEY_HI_HAT_CONFIG=/data/Source/monkey-hi-hat/mhh/mhh/ConfigFiles/mhh.debug.conf
  (logout)

C:\Source\monkey-hi-hat\mhh\mhh\bin\x64\Debug\net10.0
/data/Source/monkey-hi-hat/mhh/mhh/bin/x64/Debug/net10.0


### Version and Changelog Notes

* 5.2.0 released 2025-12-07 (content 5.2.0, textures 5.2.0)
* 5.3.0 released 2026-03-17 (content 5.2.0, textures 5.2.0)

### Work In Progress
* 5.4.0 (content 5.4.0, textures 5.2.0)
* Accept `--load` and `--playlist` switches at initial launch
* Optional byline display for visuzalizers (config `ShowVizByline=false`)
* Optional bottom-row banners (config `ShowTextBanners=false`, and `[text-banners]` section)
* Fixed text double-blank-line bug with newline in right-most column
* Honor custom font texture in `FontAtlasFilename` (update docs with info about generating new ones)
  * Sample custom font texture `Font Kode Mono 1024x1024.png`, use `OutlineWeight=0.62`
  * The original Shadertoy font generator is [here](https://evanw.github.io/font-texture-generator/)
  * Generate custom fonts [here](https://timmaffett.github.io/shadertoy_fontgen/generate_sdf.html)
  * Typically you should stick to monospaced fonts
  * Use a custom texture directory (remember updating MHH _replaces_ the `mhh-content` directories)
* Command `--show grid` now reflects `TextBufferX` and `TextBufferY` dimensions
* Implemented log limits: max ten 5MB files, retained 7 days, no longer wipes old logs at startup
* Fixed bug where numeric keypad right-arrow and down-arrow were being ignored
* Allow escape key to terminate app in standby mode

### MHH TODO

* Make a Proto video (1080x1920)
* Local - why does living room PC no longer see Spotify tracks?
* Local - check living room PC's TCP relay service
* On-screen instructions in standby mode
* Move config file comments to website documentation section
* Linux - figure out .deb packaging and hosting a package repo
* Releases - comprehensive one-shot build script?
* Linux - change to event model for track changes?
* Windows - https://github.com/DubyaDude/WindowsMediaController
* Linux - detect when media device changes
* Paylist - auto-advance on track change (after WMC & DBus support)
* Linux - terminal-hiding support (X11 only?)
* Linux - TCP relay service?
* OMT Streaming https://github.com/openmediatransport
* Refuse to run a streaming-oriented FX if a streaming viz is running?
* Global error logger via system.appdomain.unhandledexception event
* eyecandy - add Eyecandy.ShaderCompiler error logging category
* Use Spout sender to debug intermediate buffers?
* Document using VLC / NDI (or VLC / Spout?) to create an RTSP feed
* Modernize with GL Direct State Access (https://juandiegomontoya.github.io/modern_opengl.html)
* Docs - explain OpenGL full-screen behaviors (trying to use 2nd console etc)
* Playlist - hotkey to extend auto-advance time for current viz
* monkey-see-monkey-do - relay delay time
* monkey-see-monkey-do - add utility command(s)
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

