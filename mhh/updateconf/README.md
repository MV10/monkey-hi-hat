# updateconf.csproj

This modern .NET cross-platform utility applies updates to the Monkey Hi Hat config file. It is executed by the Windows .NET Framework installer or the Linux update script when an existing installation is being updated. This is expected to be the last operation performed during the update, which means `ConfigFiles/version.txt` will have already been updated to match the new version. The installer must read the original version number at startup to track what is being updated.

It requires one command-line argument, which is the existing version that was found, formatted as major.minor.build (such as 5.3.0). For Windows only, if the argument is omitted, it will perform a new install initialization of the config. For Linux, new the config is initialized by the script.

It references a couple of external files from the Windows installer:

* `../install/Output.cs` provides console output and logging support
* `../install/ReleaseConstants.cs` provides Windows paths, but also version numbers etc.

Generally the local `LinuxConstants.cs` will not have to change for a new release.

The first official Linux version was 5.2.0, so that version and earlier update code is Windows-only. From version 5.3.0 forward, each update code block must support both Windows and Linux if OS-specific config changes apply.