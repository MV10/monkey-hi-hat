## Packaging Process

> This only matches _my_ home setup (`/data/Source/monkey-hi-hat/...` and others).

These are instructions and scripts to prep the files for uploading to monkeyhihat.com (both Windows and Linux). Because Linux is my primary OS now, the scripts are bash .sh files. Since I'll be the only person actually publishing releases, that shouldn't matter to anybody still fighting to keep their Windows OS alive despite Microsoft's best efforts to wreck your day. (Ask me how I really feel.) I suppose I should screw around with Github builds, but every time I've done that, something eventually becomes outdated, and frankly, again, nobody but me will create releases anyway, so this is Good Enough.

## TODO
* Make Windows installer download NDI binaries
* Figure out Linux .deb packaging
* Separate versioning of content shaders and binaries
* Linux .deb post-init scripting to retrieve content packages
* Linux .deb post-init scripting of NDI binary downloads?
* Linux .deb post-init scripting of config modification? (shared code)

## Third-Party Dependencies

* If FFmpeg was updated:
  * Create a new `mhh-ffmpeg-x-x-x.zip` where x-x-x is FFmpeg version
  * Rename the .zip to .bin
  * Upload .bin file to monkeyhihat.com in `public_html/installer_assets` via cpanel File Manager
* If NDI is updated:
  * Windows: 
    * Zip `Processing.NDI.Lib.x64.dll` to `ndi-win-x.x.x.zip` (version on NDI Tools page)
    * Rename the .zip to .bin
    * Upload .bin file to monkeyhihat.com in `public_html/installer_assets` via cpanel File Manager
  * Linux:
    * _TODO_ 
    * no action? (library .so files are included in the package, or download?)
* For any updates (including dotnet):
  * Windows: update the download URLs in `Installer.cs`
  * Linux: update the packaging file dependencies? post-init downloads?

## Manual Pre-Packaging Steps

* Update `mhh/ConfigFiles/version.txt` to match release number
* Verify `Installer.cs` has current version numbers (4 places)
* Add any new-install config changes to `ConfigHelper.NewInstall`
* Add any version-based config changes to `ConfigHelper.Update`
* Build `install.exe` (Release build)
* Rename to `install-x-x-x.exe` where x-x-x is version
* Publishing (Solution -> mhh -> right click -> Publish)
  * Publish mhh Windows release build (`bin/Release/net8.0/win-x64`)
  * Publish mnn Linux release build (`bin/Release/net8.0/linux-x64`)

## Scripted Packaging Steps

* Open a terminal and `cd /data/Source/monkey-hi-hat/packaging`
* Execute `./windows.sh a-a-a b-b-b c-c-c` (versions: a=app, b=content, c=textures)
* This performs the following operations:
  * Under the `runtimes` directory, deletes all dirs except `win-x64`
  * Deletes third-party dependencies which (reduce package size; downloaded by install)
  * Copies monkey-see-monkey-do release build to mhh publish directory (excluding dirs)
  * Deletes `libndi.so` and `Processing.NDI.Lib.x64.dll` from Win Release directory
  * _TODO_ delete `Tmds.DBus*.dll`?
  * Archives `bin/Release/net8.0/win-x64` directory into `mhh-app-x-x-x.zip` (x-x-x is version)
  * If changed, archives Volt's Lab files into `mhh-content-x-x-x.zip` (excluding textures)
  * If changed, archives Volt's Lab files into `mhh-texture-x-x-x.zip` (textures only)
  * Renames .zip files to .bin extensions
  * All content is stored in `/tmp/mhhpkg/`
* Execute `./linux-deb.sh`
* This performs the following operations:
  * _TODO_ How hard can it be?
  * _TODO_ delete `Processing.NDI.Lib.x64.dll` from Linux Release directory
  * _TODO_ delete `CppSharp*.dll`, `NAudio*.dll`, `Spout*.dll`?
  * Result is `/tmp/mhhpkg/monkeyhihat-x-x-x.deb`

## Manual Post-Packaging Steps

* Upload `/tmp/mhhpkg/` .bin files to `monkeyhihat.com/public_html/installer_assets`
* Update main README etc, push changes
* PR and merge dev branch into master (do not delete dev)
* Create new Release:
  * Copy / update previous release verbiage
  * Create new vx.x.x tag
  * Upload `/tmp/mhhpkg/install-x-x-x.exe`
  * Upload `/tmp/mhhpkg/monkeyhihat-x-x-x.deb`
  * Upload `/tmp/mhhpkg/com.mindmagma.monkeydroid.apk`
  * Upload `/tmp/mhhpkg/monkeydroid_1.0.1.0_x86.msix`
  * Publish release
* Delete `/tmp/mhhpkg/`
* Update wiki release history (and other wiki content)
* Update [pinned release tracker](https://github.com/MV10/monkey-hi-hat/issues/3)
* Pull new master locally
