## Packaging Process

> This only matches _my_ home setup (`/data/Source/monkey-hi-hat/...` and others).

These are instructions and scripts to prep the files for uploading to the repo Releases page and installer archives to monkeyhihat.com (both Windows and Linux). Because Linux is my primary OS now, the scripts are bash .sh files. Since I'll be the only person actually publishing releases, that shouldn't matter to anybody still fighting to keep their Windows OS alive despite Microsoft's best efforts to wreck your day. (Ask me how I really feel.) I suppose I should screw around with Github builds, but every time I've done that, something eventually becomes outdated and breaks, and frankly, again, nobody but me will create releases anyway, so this is Good Enough.

The easiest way to run the scripts from within Rider is to open the "attached" list, right-click the `packaging` directory -> Open In -> Terminal. Make sure the .sh files are executable (`chmod +x *.sh`).

## Windows Status

* Working, reasonably well-automated
* No major issues or planned changes
* Zip files are renamed to .bin for historical reasons

## Linux Status

* Initially just doing an archive-based manual install script
* TODO: Figure out Linux .deb packaging
* TODO: Host a package source on monkeyhihat.com?

## Updating Third-Party Dependencies

### FFmpeg
* Windows: 
  * Create a local copy in `/data/Source/_dev_utils_standalone/ffmpeg_date_ver_bin/`
  * Zip the files into `ffmpeg-win-x-x-x.zip` where x-x-x is FFmpeg version
  * Upload .zip file to `monkeyhihat.com/public_html/installer_assets` via cpanel File Manager
* Linux:
  * User-installed as a dependency (`sudo apt update ffmpeg`, not version-specific) 
 
### NDI
* Update the NuGet package
* Zip the following files into `ndi-x.x.x.zip` (version on NDI Tools page)
  * `Processing.NDI.Lib.x64.dll`
  * `libndi.so`
* Upload .zip file to `monkeyhihat.com/public_html/installer_assets` via cpanel File Manager

### Spout
* Update the NuGet package
* Run a Windows publish build
* Zip the `CppSharp*.dll` files into `spout-x-x-x.zip` (use Spout.NetCore version)
* Spout isn't Linux-compatible so we don't include `libCppSharp.CppParser.so` from a Linux build
* Upload .zip file to `monkeyhihat.com/public_html/installer_assets` via cpanel File Manager

### .NET
  * Windows: update the download URLs in `Installer.cs`
  * Linux: user installs dependencies; update the instructions

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

* Open a terminal and `cd` to `/data/Source/monkey-hi-hat/packaging`
* Execute `./package.sh a-a-a b-b-b c-c-c` (versions: a=app, b=content, c=textures)
  * Note: For app-only changes, specify the most recent content versions, ignore the .zips 
* This performs the following operations via `media.sh`:
  * Deletes any old `/tmp/mhhpkg` directory and creates a new one
  * Archives Volt's Lab shaders into `mhh-content-b-b-b.zip`
  * Archives Volt's Lab textures into `mhh-texture-c-c-c.zip`
* This performs the following operations via `windows.sh`:
  * Renames `install.exe` to `install-a-a-a.exe`
  * Deletes separately-packaged content (NDI, Spout, etc.)
  * Merges monkey-see-monkey-do published build into mhh publish directory
  * Archives `bin/Release/net8.0/win-x64` directory into `mhh-win-a-a-a.zip`
* This performs the following operations via `linux-zip.sh`:
  * Deletes separately-packaged content (NDI, Spout, etc.)
  * Renames `install.sh` to `install-a-a-a.sh` and injects media version variables
  * Renames `update.sh` to `update-a-a-a.sh` and injects media version variables
  * Archives `bin/Release/net8.0/linux-x64` directory into `mhh-linux-a-a-a.zip`
* All content is stored in `/tmp/mhhpkg`

## Manual Post-Packaging Steps

* Upload `/tmp/mhhpkg/*.zip` files to `monkeyhihat.com/public_html/installer_assets`
* Update main README etc, push changes
* PR and merge dev branch into master (do not delete dev)
* Create new Release:
  * Copy / update previous release verbiage
  * Create new vx.x.x tag
  * Upload `/tmp/mhhpkg/install-a-a-a.exe`
  * Upload `/tmp/mhhpkg/install-a-a-a.sh`
  * Upload `/tmp/mhhpkg/update-a-a-a.sh`
  * Upload `/tmp/mhhpkg/com.mindmagma.monkeydroid.apk` (from `/data/Source/_mhh_release_files`)
  * Upload `/tmp/mhhpkg/monkeydroid_1.0.1.0_x86.msix` (from `/data/Source/_mhh_release_files`)
  * Publish release
* Delete temp: `rm -rf /tmp/mhhpkg`
* Update wiki release history (and other wiki content)
* Update [pinned release tracker](https://github.com/MV10/monkey-hi-hat/issues/3)
* Pull new master locally
