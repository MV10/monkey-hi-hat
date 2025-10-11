### About this TO-DO list

I will not accept a PR which alters this file.

If you see something that interests you, open an Issue to discuss the details.

Don't assume anything here is working or will be available in some future release. Previously this was not pushed to the repo, but since I'd hate to lose it, I added it to source control. It describes things I've only done locally so far, or unreleased changes, ideas, plans, wishlist items, and so on.


### Version and Changelog

* 5.0.0 released 2025-10-05
* post 5.0.0 started 2025-10-10
* https://github.com/MV10/monkey-hi-hat/wiki/12.-Changelog


### Work In Progress

*


### MHH TODO

* Why did disco_tunnel viz stop working?
* When Spout is updated, support different texture size on receiver
* Installer support for content-only versioning / releases
* Test mode - show keys on screen
* Test mode - abort when `--load` or similar commands are issued
* Use Spout sender to debug intermediate buffers?
* Document using VLC / NDI (or VLC / Spout?) to create an RTSP feed
* Modernize GL usage such as Direct State Access (https://juandiegomontoya.github.io/modern_opengl.html)
* Wiki - explain OpenGL full-screen behaviors (trying to use 2nd console etc)
* Playlist - hotkey to extend auto-advance time for current viz
* monkey-see-monkey-do - relay delay time
* monkey-see-monkey-do - utility command(s) (fixsound.ps1 for VBAudio bugs)
* Logo overlay support (random and playlist)
* Installer - Start menu link to edit .conf
* Installer - Start menu link to view mhh.log and msmd.log
* Installer - Start menu link to notes.txt as viz credits
* Installer - add tcpargs utility
* Installer - Use winget to retrieve .NET runtime
* Installer - winget distro? https://github.com/Belphemur/SoundSwitch/issues/1220
* Create config GUI (WinForms now available for modern .NET)
* Playlist - add `[collections]` section (playlist of other playlists)
* Add * support to [FX-Blacklist] section (and update wiki section 6)
* Add alternate [FX-Whitelist] section for large-blacklist visualizers
* Hotkey to popup list of common hotkeys
* Startup crashes if no audio device available? (ex. RDP disables audio); use synthetic audio?
* Allow aliasing multipass uniform names for reusable utility frag shaders
* Randomized crossfade duration with `CrossfadeRandomMax` (0 disables)
* Frag Quad -> remove inputs per discord convo (see OneNote TODO)
* Add test content to intentionally generate errors
* Use FontAtlasFilename? (update Wiki with info about generating new ones)
* Video generation? Step-wise clocks and timers?

* Soundcloud track overlay?
    * https://help.soundcloud.com/hc/en-us/articles/115000182454-SoundCloud-for-Windows
    * msmd to support sending Windows client commands?


### Release Process

* Verify `mhh\version.txt` matches release number
* Verify `install\Installer.cs` has current release number (in all 3 places)
* If ffmpeg or dotnet was updated (rare), modify those download URLs
* Add any version-based config changes to `install\ConfigHelper.cs`
* Build install.exe release build, rename `install-x-x-x.exe` (x-x-x is version)
* Publish mhh release build
* Copy monkey-see-monkey-do release build to mhh publish directory
* Archive publish directory into `mhh-app-x-x-x.zip` (x-x-x is version)
* Archive Volt's Lab files into `mhh-content-x-x-x.zip`
* If ffmpeg was updated, create a new `mhh-ffmpeg-x-x-x.zip` (use ffmpeg version)
* Rename .zip files to .bin extensions
* Push .bin files to mv10.github.io repo in /assets/misc
* Update readme etc, push changes
* Create new release tag, upload install-x-x-x.exe
* Update release history, wiki, etc.
* Update pinned release tracker: https://github.com/MV10/monkey-hi-hat/issues/3
* Merge dev branch into master (do not delete dev), pull new master locally


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


### Terminal Path

C:\Source\monkey-hi-hat\mhh\mhh\bin\x64\Debug\net8.0
