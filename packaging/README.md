## Packaging Process

> This only matches _my_ home setup (`/data/Source/monkey-hi-hat/...` and others).

These are instructions and scripts to prep the files for uploading to the repo Releases page and installer archives to monkeyhihat.com (both Windows and Linux). Because Linux is my primary OS now, the scripts are bash .sh files. Since I'll be the only person actually publishing releases, that shouldn't matter to anybody still fighting to keep their Windows OS alive despite Microsoft's best efforts to wreck your day. (Ask me how I really feel.) I suppose I should screw around with Github builds, but every time I've done that, something eventually becomes outdated and breaks, and frankly, again, nobody but me will create releases anyway, so this is Good Enough.

The easiest way to run the scripts from within Rider is to open the "attached" list, right-click the `packaging` directory -> Open In -> Terminal. Make sure the .sh files are executable (`chmod +x *.sh`).

## Windows Status

* Working, reasonably well-automated
* No major issues or planned changes

## Linux Status

* Initially just doing an archive-based manual install
* TODO: Figure out Linux .deb packaging
* TODO: Host a package source on monkeyhihat.com?

## Third-Party Dependencies

* If FFmpeg was updated:
  * Windows: 
    * Create a new `mhh-ffmpeg-x-x-x.zip` where x-x-x is FFmpeg version
    * Rename the .zip to .bin
    * Upload .bin file to monkeyhihat.com in `public_html/installer_assets` via cpanel File Manager
  * Linux:
    * Manual installation required; update the instructions 
 
* If NDI is updated:
  * Windows: 
    * Zip `Processing.NDI.Lib.x64.dll` to `ndi-win-x.x.x.zip` (version on NDI Tools page)
    * Rename the .zip to .bin
    * Upload .bin file to monkeyhihat.com in `public_html/installer_assets` via cpanel File Manager
  * Linux:
    * `libndi.so` is included in the manual-install archive  

* For any updates (including dotnet):
  * Windows: update the download URLs in `Installer.cs`
  * Linux: manual installation required; update the instructions

## Manual Pre-Packaging Steps

* Update `mhh/ConfigFiles/version.txt` to match release number
* Verify `Installer.cs` has current version numbers (4 places)
* Add any new-install config changes to `ConfigHelper.NewInstall`
* Add any version-based config changes to `ConfigHelper.Update`
* Build `install.exe` (Release build)
* Publishing (Solution -> mhh -> right click -> Publish)
  * Publish mhh Windows release build (`bin/Release/net8.0/win-x64`)
  * Publish mhh Linux release build (`bin/Release/net8.0/linux-x64`)

## Scripted Packaging Steps

> Always run the Windows script first, followed by the Linux script. The Windows script creates a new empty target directory, and the Linux script adds files to that. 

* Open a terminal and `cd /data/Source/monkey-hi-hat/packaging`
* Execute `./windows.sh a-a-a b-b-b c-c-c` (versions: a=app, b=content, c=textures)
* This performs the following operations:
  * Deletes any old `/tmp/mhhpkg` directory and creates a new one 
  * Renames `install.exe` to `install-a-a-a.exe`
  * Deletes third-party dependencies which are downloaded by installer
  * Deletes Linux-related libraries
  * Merges monkey-see-monkey-do published build into mhh publish directory
  * Archives `bin/Release/net8.0/win-x64` directory into `mhh-app-a-a-a.bin` (x-x-x is version)
  * Archives Volt's Lab shaders into `mhh-content-b-b-b.bin`
  * Archives Volt's Lab textures into `mhh-texture-c-c-c.bin`
  * All content is stored in `/tmp/mhhpkg`
* Execute `./linux-tgz.sh a-a-a b-b-b c-c-c` (versions: a=app, b=content, c=textures)
* This performs the following operations:
  * Deletes Windows-related libraries
  * Archives `bin/Release/net8.0/linux-x64` directory into `monkeyhihat-a-a-a.tgz`
  * Archives Volt's Lab shaders into `mhh-content-b-b-b.tgz`
  * Archives Volt's Lab textures into `mhh-texture-c-c-c.tgz`
  * All content is stored in `/tmp/mhhpkg`

## Manual Post-Packaging Steps

* Upload `/tmp/mhhpkg/*.bin` and `/tmp/mhhpkg/mhh*.tgz` files to `monkeyhihat.com/public_html/installer_assets`
* Update main README etc, push changes
* PR and merge dev branch into master (do not delete dev)
* Create new Release:
  * Copy / update previous release verbiage
  * Create new vx.x.x tag
  * Upload `/tmp/mhhpkg/install-x-x-x.exe`
  * Upload `/tmp/mhhpkg/monkeyhihat-x-x-x.tgz`
  * Upload `/tmp/mhhpkg/com.mindmagma.monkeydroid.apk`
  * Upload `/tmp/mhhpkg/monkeydroid_1.0.1.0_x86.msix`
  * Publish release
* Delete `/tmp/mhhpkg/`
* Update wiki release history (and other wiki content)
* Update [pinned release tracker](https://github.com/MV10/monkey-hi-hat/issues/3)
* Pull new master locally
