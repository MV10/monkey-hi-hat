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
* 5.4.0 released 2026-03-23 (content 5.4.0, textures 5.4.0)
* 5.4.1 released 2026-04-01 (content 5.4.0, textures 5.4.0)

### Work In Progress

* 5.5.0 WIP
* HTTP retrieval and caching of textures (still-image only)
  * HTTP in-memory download works even if caching is disabled
  * Cache is only pruned by timestamp once during startup
  * Cache pruning by size or file count happens after each download
  * Cached images are stored as PNG (preserves alpha channel)
  * For viz/FX `[textures]` and `[cubemaps]` sections:
    * Use a URL instead of a filename: `uniform:http://...`
    * Use `!http` to force a download: `uniform:!http://...`
    * Forced downloads are still cached but are re-downloaded every time
    * If an old version is in the cache, it will be used while retrieving a new one
    * No support for `[videos]` (unlikely to be small enough for viz/FX usage)
  * New cache control commands:
    * `--cache purge` removes all cached content
    * `--cache info` shows cache statistics (counts, size)
    * `--cache add [url]` retrieves and caches a texture
    * `--cache find [url]` shows details if URL is already cached
    * `--cache list` shows all cached files and details
    * `--cache load` pre-fills the cache for all viz/FX
  * New `mhh.conf` section `[httpcache]` settings (all optional):
    * `CacheEnabled` (default is true)
    * `WindowsPath` (default is blank which maps to `[user]\AppData\temp\monkeyhihat`)
    * `LinuxPath` (default is blank which maps to `~/.cache/monkeyhihat`)
    * `MaxFileCount` (0 disables, minimum 50, default is 500)
    * `MaxTotalMB` (0 disables, minimum 50, default is 500)
    * `MaxAgeDays` (0 disables, minimum 1, default is 90)
    * `PollingMS` download-completion polling rate (milliseconds, default is 250)
    * `MaxDimension` resize large images (0 disables, default is 1920)
    * `PlaceholderTexture` filename (default blank which uses internal `badtexture.jpg`)
    * Note that `PlaceholderTexture` is for all HTTP downloads even if caching is disabled
  * Sample sources for testing:
    * South Korean street: https://www.opentopia.com/webcam/18247
    * Chicago skyline: https://www.opentopia.com/webcam/17508
    * Jakarta traffic: https://www.opentopia.com/webcam/18479
    * Illinois Dog Day Care: https://www.opentopia.com/webcam/19030
    * Michigan Dog Day Care: https://www.opentopia.com/webcam/18178
    * NASA solar: https://sdowww.lmsal.com/sdomedia/SunInTime/mostrecent/l_211_193_171.jpg
    * NASA solar: https://sdowww.lmsal.com/sdomedia/SunInTime/mostrecent/l0171.jpg
    * NASA solar: https://sdowww.lmsal.com/sdomedia/SunInTime/mostrecent/l0304.jpg
    * NASA solar: https://soho.nascom.nasa.gov/data/realtime/eit_171/1024/latest.jpg
    * Very large / slow panoramics: https://www.eso.org/public/outreach/webcams/
* Moved MHH testcontent/* to volts-laboratory/mhhdev/*
* Updated and fixed some typos on standby screen
* Tests for valid `HOME` environment variable on Linux at startup
* Changed `GLImageTexture.ResizeMaxDimension` to `GLImageTexture.StreamingMaxDimension`
* Optional custom viz/fx `Placeholder` texture, or `*` for a solid black placeholder
 
* Refactor "ResourceGroup" refs to "Framebuffer" (classes, methods, comments, vars)
* Monkey-Droid v2.2.0
    * Update to Avalonia 12.0 to comply with mandatory Android 16K page sizes
    * Correctly recognize/support `--cls` in Console view history
    * Show inferred `--` switch prefix in Console view history

### MHH TODO
 
* Make a Proto video (1080x1920)
* Linux - test and deploy MSMD (systemd and sysvinit)
* Monkey-Droid - installers?
* Monkey-Droid - offer to locally install alongside MHH?
* Linux - figure out .deb packaging and hosting a package repo
* Linux - change to event model for track changes?
* Windows - https://github.com/DubyaDude/WindowsMediaController
* Linux - detect when media device changes
* Playlist - auto-advance on track change (after WMC & DBus support)
* Linux - terminal-hiding support (X11 only?)
* OMT Streaming https://github.com/openmediatransport
* Refuse to run a streaming-oriented FX if a streaming viz is running?
* Global error logger via system.appdomain.unhandledexception event
* eyecandy - add Eyecandy.ShaderCompiler error logging category
* Use Spout sender to debug intermediate buffers?
* Document using VLC / NDI (or VLC / Spout?) to create an RTSP feed
* Modernize with GL Direct State Access (https://juandiegomontoya.github.io/modern_opengl.html)
* Playlist - hotkey to extend auto-advance time for current viz
* monkey-see-monkey-do - relay delay time
* Test mode - Failed crossfade compile crashes test mode; finds config but not cached
* config - `DisableCrossfadeCache` option (vs cache size for other shader types)
* Installer - Start menu link to edit .conf
* Installer - Start menu link to view mhh.log and msmd.log
* Installer - Start menu link to notes.txt as viz credits
* Installer - add tcpargs utility
* Installer - Use winget to retrieve .NET runtime
* Installer - winget distro? https://github.com/Belphemur/SoundSwitch/issues/1220
* Create config GUI
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
* Rendering text once: due to fade re-renders it isn't really worth the effort


### EYECANDY TODO (MAJOR)

* Nuthin' here boss


### MONKEY-DROID TODO

* v2.0 wooo! finally!


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

