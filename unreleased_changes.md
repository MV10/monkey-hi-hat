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

* 5.4.1 released 2026-04-01 (content 5.4.0, textures 5.4.0)
* 5.5.0 released 2026-04-13 (content 5.5.0, textures 5.5.0)
 
* 5.6.0 in-progress (content 5.5.0, textures 5.5.0)
* Update to Tmds.DBus 0.93.0
* Update to eyecandy x.x.x
* Update to Monkey-Droid x.x.x
* Update to CommandLineSwitchPipe 2.2.0
* Addded `timedelta` uniform (float; elapsed time since start of previous frame in seconds)
* Window-creation retries to avoid GLFW bug https://github.com/glfw/glfw/pull/2767

### New version TODO

* Windows - change Spotify support to general Windows Media Controller https://github.com/DubyaDude/WindowsMediaController
* Linux - change to event model for track changes
* Linux - detect when media device changes
* Playlist - auto-advance on track change (after WMC & DBus support)
* Add sleep-state prevention and config settings
* eyecandy - optionally generate `glGetShaderInfoLog` and `glGetProgramInfoLog` shader compile/link outputs
* eyecandy - add Eyecandy.ShaderCompiler error logging category
* Monkey-Droid - option to output detailed error logging to console
* Monkey-Droid - change console to monospaced font
* Monkey-Droid - update to CommandLineSwitchPipe 2.2.0


### MHH TODO

* IDE - move file handling (app config and viz/fx config) to a separate library for sharing with SSM
* IDE - add `--ide` switch to support integration with SSM
* IDE - related features (step mode, overriding uniforms, reporting texture data, etc)

* Plugin DLL support: `IUniformSource`, `IRenderer`, `IVertexSource`
* Make a Proto video (1080x1920)
* Docs - add playlist questions to FAQ
* Docs - add layout details about cubemaps
* Add realtime clock at top right (off, always on, track change, viz change)
* Linux - test and deploy MSMD (systemd and sysvinit)
* New cubemap content:
  * https://sketchfab.com/tags/cubemap 
  * Consider HDRI conversions from PolyHaven:
  * https://polyhaven.com/hdris/indoor
  * https://github.com/insopitus/equirect2cubemap
  * https://github.com/dariomanesku/cmft
* Monkey-Droid - offer to locally install alongside MHH?
* Gemini equirectangular image generation (via mhh website) for randomized cubemaps
  * online converter https://jaxry.github.io/panorama-to-cubemap/
  * source https://github.com/jaxry/panorama-to-cubemap
  * pano viewer https://renderstuff.com/tools/360-panorama-web-viewer/
  > Draw a 360-degree equirectangular projection of a random setting, location, environment, or scenario, rendered in a photorealistic style. The scene should "tell a story" with compelling thematic elements, but avoid people and animals. The left and right edges must wrap seamlessly to form a continuous panoramic environment. Avoid mirrored composition to achieve artificial symmetry in favor of fine-tuning the edges of the images to produce a seamless wrap-around. Use the highest possible resolution while maintaining the required 2:1 aspect ratio.
* Linux - figure out .deb packaging and hosting a package repo
* Linux - terminal-hiding support (X11 only?)
* OMT Streaming https://github.com/openmediatransport
* Refuse to run a streaming-oriented FX if a streaming viz is running?
* Global error logger via system.appdomain.unhandledexception event
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
* Single-pass text rendering: due to fade in/out, it isn't really worth the effort


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

