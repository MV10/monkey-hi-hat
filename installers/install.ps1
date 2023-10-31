#
# Monkey-Hi-Hat Windows Install Script
# https://github.com/MV10/monkey-hi-hat
#
# Requires Internet access and administrator rights.
# All output is logged to mhh-install.log in user's temp directory.
#
# TODO list (possibly):
#
# Create uninstall script. (Register with Windows app list?)
#
# Figure out how to detect Microsoft's ludicrous "Ransomware Protection" system
# and get the script whitelisted, which appears to only be possible by triggering
# the *silent* "warning" (utterly dumb-ass design) then walking the user through
# the god-awful "modern" Settings UI to allow the script to run.
#
# Once the script is whitelisted, possibly use a third-party PowerShell module
# (which, stupidly, must install into the users "protected" Documents directory)
# to analyze and configure the Sound settings after VB-Audio Cable is installed.
#
# Provide a separate script to read/log/fix Sound settings on demand.
#
# Offer Virtual Audio Cable (https://vac.muzychenko.net/en/index.htm) as a paid
# alternative to VB-Audio Cable?
#

using namespace System.IO

$temp = [Path]::GetTempPath()
$log = [Path]::Combine($temp, "install-monkey-hi-hat.log")
$unzipPath = [Path]::Combine($temp, "mhh-unzip")

$dotnetVer = "6"
$dotnetUrl = "https://download.visualstudio.microsoft.com/download/pr/62bf9f50-dcd9-4e4c-ac02-4d355efb914d/a56b37b98cb07899cd8c44fa7d50dff3/dotnet-runtime-6.0.24-win-x64.exe"
$driverUrl = "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack43.zip"
$programUrl = "https://mcguirev10.com/assets/misc/mhh-app.bin"
$contentUrl = "https://mcguirev10.com/assets/misc/mhh-content.bin"
$openalLegacyUrl = "https://openal.org/downloads/oalinst.zip"
$openalSoftUrl = "https://www.openal-soft.org/openal-binaries/openal-soft-1.23.1-bin.zip"
$audioConfigUrl = "https://github.com/MV10/monkey-hi-hat/wiki/Post%E2%80%90Install%E2%80%90Instructions"
$donationUrl = "https://shop.vb-audio.com/en/win-apps/11-vb-cable.html"
$programPath = "C:\Program Files\mhh"
$contentPath = "C:\ProgramData\mhh-content"

$dotnetTemp = [Path]::Combine($temp, "mhh-installer-dotnet.exe")
$driverTemp = [Path]::Combine($temp, "mhh-installer-driver.zip")
$programTemp = [Path]::Combine($temp, "mhh-program.zip")
$contentTemp = [Path]::Combine($temp, "mhh-content.zip")
$openalLegacyTemp = [Path]::Combine($temp, "mhh-openal-installer.zip")
$openalSoftTemp = [Path]::Combine($temp, "mhh-openalsoft.zip")


################################################################################
# OPERATIONS AND STATIC FUNCTIONS
################################################################################


# Necessary because a non-interactive session may have been launched
function PauseExit
{
    if(-not $psISE) 
    {
        Write-Host "`nPress any key to exit."
        $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
    }
    exit
}

function Output-Log([string]$Message)
{
    Add-Content $log $Message
}

function Output ([string]$Message)
{
    Write-Host $Message
    Output-Log $Message
}

function Output-List([string]$Description, [array]$ArrayContent)
{
    Output-Log "`n>>>>>>>>>>>> begin $Description"
    Output-Log ($ArrayContent -join "`n")
    Output-Log ">>>>>>>>>>>> end $Description`n"
}

function Unzip([string]$Pathname)
{
    Remove-Item -Path $([Path]::Combine($unzipPath, "*")) -Recurse -Force
    Expand-Archive -LiteralPath $Pathname -DestinationPath $unzipPath
}

function UnzipForced([string]$Pathname, [string]$Destination)
{
    Expand-Archive -LiteralPath $Pathname -DestinationPath $Destination -Force
}

function EndScript
{
    Output-Log "`nRemoving downloaded files."
    Remove-Item -Path $dotnetTemp -ErrorAction Ignore
    Remove-Item -Path $driverTemp -ErrorAction Ignore
    Remove-Item -Path $programTemp -ErrorAction Ignore
    Remove-Item -Path $contentTemp -ErrorAction Ignore
    Remove-Item -Path $openalLegacyTemp -ErrorAction Ignore
    Remove-Item -Path $openalSoftTemp -ErrorAction Ignore

    Output-Log "`nRemoving temp directory."
    Remove-Item -Path $unzipPath -Recurse -Force

    Output-Log "`nInstallation ended: $([DateTime]::Now)"

    Write-Host "Install log can be reviewed at:`n$log"
    PauseExit
}

# PowerShell function implementation is asinine: https://stackoverflow.com/a/42743143/152997
class Installer
{
    static [bool] YesNo()
    {
        $response = ""
        do
        {
            $response = Read-Host -Prompt "  [Y]es or [N]o"
        } until ($response -eq "y" -or $response -eq "n")
        return ($response -eq "y")
    }
}


################################################################################
# ANALYZE SYSTEM STATUS
################################################################################


################################################################################
# Restart as admin (with UAC prompt) if needed

if (-not (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) 
{
	$args = "& '" + $myInvocation.MyCommand.Definition + "'"
	Start-Process powershell -Verb runAs -ArgumentList $args
	exit
}

################################################################################
# Validate OS version and architecture

if(([System.Environment]::OSVersion.Version.Major -ne "10") -or -not([Environment]::Is64BitProcess))
{
    "Monkey-Hi-Hat requires 64-bit Windows 10 or Windows 11."
    PauseExit
}


################################################################################
# No fatal errors, start logging

Output-Log "`n------------------------------------------------"
Output-Log "Installation started: $([DateTime]::Now)"
Output "`n`nCollecting system information:"


################################################################################
# Create temp unzip directory

[void][Directory]::CreateDirectory($unzipPath)


################################################################################
# Check .NET runtime installation

Output "Retrieving list of installed .NET runtimes."
$Error.Clear()
try { $runtimes = dotnet --list-runtimes } catch { }
if($Error.Count -eq 0)
{
    Output-List "dotnet runtimes" $runtimes
    $target = "Microsoft.NetCore.App $dotnetVer."
    $dotnetOk = ($runtimes -match ([regex]::Escape($target))).Length -gt 0 
    Remove-Variable runtimes
}
else
{
    Output-List "dotnet runtimes" $Error
    $dotnetOk = $false
}


################################################################################
# Check VB-Audio Cable installation

Output "Retrieving list of running drivers."
$Error.Clear()
try { $drivers = driverquery } catch { }
if($Error.Count -eq 0)
{
    Output-List "driver list" $drivers
    $target = "VB-Audio Virtual Cable"
    $audioDriverOk = ($drivers -match ([regex]::Escape($target))).Length -gt 0 
    Remove-Variable drivers
}
else
{
    Output-List "driver list" $Error
    $audioDriverOk = $false
}


################################################################################
# Check for OpenAL installations

Output "Checking for OpenAL libraries."

$target = [Path]::Combine($env:windir, "System32", "OpenAL32.dll")
$openalLegacyOk = Test-Path($target)
$target = [Path]::Combine($env:windir, "SysWOW64", "OpenAL32.dll")
$openalLegacyOk = ($openalLegacyOk -and (Test-Path($target)))

$target = [Path]::Combine($env:windir, "System32", "soft_oal.dll")
$openalSoftOk = Test-Path($target);
$target = [Path]::Combine($env:windir, "SysWOW64", "soft_oal.dll")
$openalSoftOk = ($openalSoftOk -and (Test-Path($target)))


################################################################################
# Check for existing MHH installation at default path

Output "Checking for existing application."
$target = [Path]::Combine($programPath, "mhh.exe")
Output-Log "Target $target"
$programExists = Test-Path($target)


################################################################################
# Check for MHH content at default path

Output "Checking for existing visualization content."
$target = [Path]::Combine($contentPath, "libraries", "color_conversions.glsl")
Output-Log "Target $target"
$contentExists = Test-Path($target)


################################################################################
# REPORT PRE-INSTALL STATUS
################################################################################


Output "`n`nPre-install status:"
Output ".NET runtime version $dotnetVer installed? $dotnetOk"
Output "VB-Audio Cable loopback driver installed? $audioDriverOk"
Output "OpenAL legacy router installed? $openalLegacyOk"
Output "OpenAL-Soft library installed? $openalSoftOk"
Output "Installation found at default location? $programExists"
Output "Content found at default location? $contentExists"


################################################################################
# OPTION PROMPTS
################################################################################


$installDotnet = -not $dotnetOk
$installDriver = -not $audioDriverOk
$installProgram = $true
$installContent = $true
$installOpenAL = -not $openalLegacyOk -or -not $openalSoftOk
$startMenu = $false
$startDesktop = $false
$startHotkey = $false
$startBootUp = $false

Output "`n`nInstallation options:"

$options = New-Object System.Collections.Generic.List[string]


################################################################################
# Install .NET runtime?

if($installDotnet)
{
    Output "`nThe .NET $dotnetVer runtime is required. Install it?"
    if(-not [Installer]::YesNo())
    {
        Output "`nInstallation aborted. The .NET $dotnetVer runtime is required to use this application."
        EndScript
    }
    $options.Add("The .NET $dotnetVer runtime will be installed")
}


################################################################################
# Install VB-Audio Cable driver?

if($installDriver)
{
    Output "`nThe VB-Audio Cable loopback driver is not installed. This is the recommended"
    Output "loopback driver, but the wiki offers at least one alternative."
    Output "Install the VB-Audio Cable driver?"
    if(-not [Installer]::YesNo())
    {
        $installDriver = $false
        $options.Add("VB-Audio Cable loopback driver will NOT be installed.")
        $options.Add("A loopback driver is required. See the wiki for options and assistance.")
    }
    else
    {
        $options.Add("The audio loopback driver will be installed. This installer can help you configure it.")
    }
}


################################################################################
# Install OpenAL libraries?

if($installOpenAL)
{
    Output "`nThe OpenAL and/or OpenAL-Soft libraries were not found. The application"
    Output "needs these to intercept streaming audio. Install them?"
    if(-not [Installer]::YesNo())
    {
        $installOpenAL = $false
        $options.Add("The OpenAL and/or OpenAL-Soft libraries will NOT be installed.")
        $options.Add("This program requires OpenAL and OpenAL-Soft libraries. See the wiki for assistance.")
    }
    else
    {
        $options.Add("The OpenAL and/or OpenAL-Soft libraries will be installed.")
    }
}


################################################################################
# Install or overwrite program?

if($programExists)
{
    Output "`nThe program is already installed at the default location."
    Output "If you have custom configuration in mhh.conf, this will not change."
    Output "`nOverwrite the existing program installation?"
    $installProgram = [Installer]::YesNo()
    if($installProgram)
    {
        $options.Insert(0, "The existing Monkey-Hi-Hat installation will be overwritten.")
    }
    else
    {
        $options.Insert(0, "The existing Monkey-Hi-Hat program installation will NOT be updated.")
    }
}
else
{
    $options.Insert(0, "The Monkey-Hi_hat program will be installed to $programPath")
}


################################################################################
# Install or overwrite content?

if($contentExists)
{
    Output "`nVisualization content was found at the default location."
    Output "Content in other locations or non-standard subdirectories will not change."
    Output "`nReplace default content directories with updated content?"
    $installContent = [Installer]::YesNo()
    if($installContent)
    {
        $options.Insert(1, "The existing content directories will be removed and replaced with current content.")
    }
    else
    {
        $options.Insert(1, "The existing content will NOT be updated.")
    }
}
else
{
    $options.Insert(1, "Visualizer content will be installed to $contentPath")
    if($programExists)
    {
        $options.Insert(2, "The existing program configuration will NOT be updated automatically (if needed).")
    }
    else
    {
        $options.Insert(2, "The program will be configured to use this content location.")
    }
}


################################################################################
# App launch options

if($programExists -or $installProgram)
{
    Output "`nThere are several application startup options."

    Output "Create a Start Menu shortcut?"
    $startMenu = [Installer]::YesNo()

    Output "Create a Desktop shortcut?"
    $startDesktop = [Installer]::YesNo()

    if($startMenu -or $startDesktop)
    {
        Output "Set an F10 as the shortcut hot-key to launch the program?"
        $startHotkey = [Installer]::YesNo()
    }

    Output "Start the program when Windows starts?"
    $startBootUp = [Installer]::YesNo()

    if($startMenu)
    {
        $options.Add("A Start Menu shortcut will be created.")
    }
    if($startMenu)
    {
        $options.Add("A Desktop shortcut will be created.")
    }
    if($startHotkey -and ($startMenu -or $startDesktop)) 
    {
        $options.Add("F10 will be set as the shortcut hot-key.")
    }
    if($startBootUp)
    {
        $options.Add("The program when be started Windows starts.")
    }
}
else
{
    $options.Add("The application isn't being installed and wasn't found, so shortcut/startup options aren't available.")
}


################################################################################
# Summarize options

Output "`n`nSummary of installation tasks:`n"
Output ($options -join "`n")


################################################################################
# DOWNLOADS
################################################################################


Output "`n`nDownloading files:`n"


################################################################################
# Download .NET runtime installer

if($installDotnet)
{
    Output ".NET runtime installer..."
    Output-Log "  From: $dotnetUrl"
    Output-Log "  Save: $dotnetTemp"
    try
    {
        Invoke-WebRequest -Uri $dotnetUrl -OutFile $dotnetTemp
    }
    catch
    {
        Output "Fatal error, download failed:`n$($PSItem.ToString())"
        EndScript
    }
}


################################################################################
# Download VB-Cable installer archive

if($installDriver)
{
    Output "VB-Cable audio loopback driver installer..."
    Output-Log "  From: $driverUrl"
    Output-Log "  Save: $driverTemp"
    try
    {
        Invoke-WebRequest -Uri $driverUrl -OutFile $driverTemp
    }
    catch
    {
        Output "Fatal error, download failed:`n$($PSItem.ToString())"
        EndScript
    }
}


################################################################################
# Download OpenAL legacy installer archive

if(-not $openalLegacyOk -and $installOpenAL)
{
    Output "Legacy OpenAL router installer archive..."
    Output-Log "  From: $openalLegacyUrl"
    Output-Log "  Save: $openalLegacyTemp"
    try
    {
        Invoke-WebRequest -Uri $openalLegacyUrl -OutFile $openalLegacyTemp
    }
    catch
    {
        Output "Fatal error, download failed:`n$($PSItem.ToString())"
        EndScript
    }
}


################################################################################
# Download OpenAL-Soft archive

if(-not $openalSoftOk -and $installOpenAL)
{
    Output "OpenAL-Soft library archive..."
    Output-Log "  From: $openalSoftUrl"
    Output-Log "  Save: $openalSoftTemp"
    try
    {
        Invoke-WebRequest -Uri $openalSoftUrl -OutFile $openalSoftTemp
    }
    catch
    {
        Output "Fatal error, download failed:`n$($PSItem.ToString())"
        EndScript
    }
}


################################################################################
# Download MHH application archive

if($installProgram)
{
    Output "Monkey-Hi-Hat application archive..."
    Output-Log "  From: $programUrl"
    Output-Log "  Save: $programTemp"
    try
    {
        Invoke-WebRequest -Uri $programUrl -OutFile $programTemp
    }
    catch
    {
        Output "Fatal error, download failed:`n$($PSItem.ToString())"
        EndScript
    }
}


################################################################################
# Download MHH content archive

if($installContent)
{
    Output "Monkey-Hi-Hat visualizer content archive..."
    Output-Log "  From: $contentUrl"
    Output-Log "  Save: $contentTemp"
    try
    {
        Invoke-WebRequest -Uri $contentUrl -OutFile $contentTemp
    }
    catch
    {
        Output "Fatal error, download failed:`n$($PSItem.ToString())"
        EndScript
    }
}




################################################################################
# PROCESSING
################################################################################

Output "`n`nInstalling requested items:"


################################################################################
# Run .NET installer

if($installDotnet)
{
    Output "`nInstalling .NET runtime..."
    $Error.Clear()
    try
    {
        Output-Log "Invoking command:`n$dotnetTemp /install /quiet /norestart"
        Start-Process -FilePath $dotnetTemp -ArgumentList "/install", "/quiet", "/norestart" -WorkingDirectory $temp -Wait -Verb RunAs
        # won't be able to run dotnet.exe yet, this script's environment won't have the path set
    } 
    catch { }

    if($Error.Count -gt 0)
    {
        Output "Aborting, error installing .NET runtime."
        Output-List "dotnet runtime installation" $Error
        EndScript
    }
}


################################################################################
# Run VB-Cable installation

if($installDriver)
{
    Output "`nInstalling audio loopback driver..."
    $Error.Clear()
    try
    {
        Unzip($driverTemp)
        $cmd = [Path]::Combine($unzipPath, "VBCABLE_Setup_x64.exe")
        Output-Log "Invoking command:`n$cmd -i -h"
        Start-Process -FilePath $cmd -ArgumentList "-i", "-h" -WorkingDirectory $unzipPath -Wait -Verb RunAs
    } 
    catch { }

    if($Error.Count -gt 0)
    {
        Output "Aborting, error installing VB-Audio Cable loopback driver."
        Output-List "loopback driver installation" $Error
        EndScript
    }
}


################################################################################
# Unzip and run OpenAL legacy installer

if(-not $openalLegacyOk -and $installOpenAL)
{
    Output "`nInstalling OpenAL legacy router..."
    $Error.Clear()
    try
    {
        Unzip($openalLegacyTemp)
        $cmd = [Path]::Combine($unzipPath, "oalinst.exe")
        Output-Log "Invoking command:`n$cmd /s"
        Start-Process -FilePath $cmd -ArgumentList "/s", "-h" -WorkingDirectory $unzipPath -Wait -Verb RunAs
    }
    catch { }

    if($Error.Count -gt 0)
    {
        Output "Aborting, error installing OpenAL legacy router."
        Output-List "OpenAL router installation" $Error
        EndScript
    }
}


################################################################################
# Unzip and copy OpenAL-Soft files

if(-not $openalSoftOk -and $installOpenAL)
{
    Output "`nInstalling OpenAL-Soft libraries..."
    $Error.Clear()
    try
    {
        Unzip($openalSoftTemp)
        $rootSrc = [Path]::Combine($unzipPath, "openal-soft-1.23.1-bin", "bin")

        $src = [Path]::Combine($rootSrc, "Win64", "soft_oal.dll")
        $dest = [Path]::Combine($env:windir, "System32", "soft_oal.dll")
        Output-Log "File copy:`n  Src:  $src'n  Dest: $dest"
        Copy-Item -Path $src -Destination $dest

        $src = [Path]::Combine($rootSrc, "Win32", "soft_oal.dll")
        $dest = [Path]::Combine($env:windir, "SysWOW64", "soft_oal.dll")
        Output-Log "File copy:`n  Src:  $src'n  Dest: $dest"
        Copy-Item -Path $src -Destination $dest

    }
    catch { }

    if($Error.Count -gt 0)
    {
        Output "Aborting, error installing OpenAL-Soft libraries."
        Output-List "OpenAL-Soft installation" $Error
        EndScript
    }
}


################################################################################
# Unzip application over any existing install

if($installProgram)
{
    Output "`nInstalling Monkey-Hi-Hat application..."
    $Error.Clear()
    try
    {
        if(-not (Test-Path($programPath)))
        {
            Output "  Creating directory $programPath"
            [void][Directory]::CreateDirectory($programPath)
        }

        Output "  Force-expanding archive to $programPath"
        UnzipForced -Pathname $programTemp -Destination $programPath
    }
    catch { }

    if($Error.Count -gt 0)
    {
        Output "Aborting, error installing Monkey-Hi-Hat application."
        Output-List "MHH app installation" $Error
        EndScript
    }
}


################################################################################
# Remove existing content directories

if($installContent)
{
    Output "`nClearing any existing content directories..."
    Remove-Item -Path $([Path]::Combine($contentPath, "fx")) -Recurse -ErrorAction Ignore
    Remove-Item -Path $([Path]::Combine($contentPath, "libraries")) -Recurse -ErrorAction Ignore
    Remove-Item -Path $([Path]::Combine($contentPath, "playlists")) -Recurse -ErrorAction Ignore
    Remove-Item -Path $([Path]::Combine($contentPath, "shaders")) -Recurse -ErrorAction Ignore
    Remove-Item -Path $([Path]::Combine($contentPath, "templates")) -Recurse -ErrorAction Ignore
    Remove-Item -Path $([Path]::Combine($contentPath, "textures")) -Recurse -ErrorAction Ignore
}


################################################################################
# Unzip MHH content

if($installContent)
{
    Output "`nInstalling visualization content..."
    $Error.Clear()
    try
    {
        if(-not (Test-Path($contentPath)))
        {
            Output "  Creating directory $contentPath"
            [void][Directory]::CreateDirectory($contentPath)
        }

        Output "  Force-expanding archive to $contentPath"
        UnzipForced -Pathname $contentTemp -Destination $contentPath
    }
    catch { }

    if($Error.Count -gt 0)
    {
        Output "Aborting, error installing visualization content."
        Output-List "MHH content installation" $Error
        EndScript
    }
}


################################################################################
# If mhh.config doesn't exist, copy and update it

$mhhConfUpdated = $false

if($installProgram -or $installContent)
{
    Output "`nThe program and/or content was installed: checking app config..."
    $target = [Path]::Combine($programPath, "mhh.conf")
    if(Test-Path($target))
    {
        Output "An existing mhh.conf configuration was found and will not be modified."
        Output "`nIMPORTANT:"
        Output "If the visualization content is new, you may need to update the paths in the [Windows] section."
    }
    else
    {
        Output "Copying the configuration template to $target"
        $src = [Path]::Combine($programPath, "ConfigFiles", "mhh.conf")
        Copy-Item -Path $src -Destination $programPath

        if($installContent)
        {
            $mhhConfUpdated = $true
            Output "Adding standard content paths to end of configuration file."

            $newContent = @("")
            $newContent += "# These standard visualizer content paths were added by the installer."
            $newContent += "# The installer will not update this configuration file again."
            
            $libPath = [Path]::Combine($contentPath, "libraries")

            $path = [Path]::Combine($contentPath, "shaders")
            $newContent += "VisualizerPath=$path;$libPath"
            
            $path = [Path]::Combine($contentPath, "playlists")
            $newContent += "PlaylistPath=$path"
            
            $path = [Path]::Combine($contentPath, "fx")
            $newContent += "FXPath=$path;$libPath"
            
            $path = [Path]::Combine($contentPath, "textures")
            $newContent += "TexturePath=$path"

            $newContent += "# End of installer-generated content paths."
            $newContent += ""

            Output-List "appended to mhh.conf" $newContent

            $mhhConf = Get-Content -Path $target
            $mhhConf += $newContent
            Set-Content -Path $target -Value $mhhConf -Force
        }
        else
        {
            Output "`nIMPORTANT:"
            Output "Visualization content was not installed. You must specify content paths in the [Windows] section."
            Output "The mhh.conf that was copied DOES NOT specify content paths, and the program won't run without it."
        }
    }
}


################################################################################
# Set config file and content directory permissions

# https://learn.microsoft.com/en-us/dotnet/api/system.security.accesscontrol.filesystemrights?view=windowsdesktop-5.0
# https://learn.microsoft.com/en-us/dotnet/api/system.security.accesscontrol.inheritanceflags?view=windowsdesktop-5.0
# https://learn.microsoft.com/en-us/dotnet/api/system.security.accesscontrol.propagationflags?view=windowsdesktop-5.0

if($installContent)
{
    Output "Setting write permissions for `"Users`" group on content directory..."
    $acl = Get-Acl -Path $contentPath
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule("Users", "Write", "3", "0", "Allow")
    $acl.SetAccessRule($rule)
    $acl | Set-Acl -Path $contentPath
}

if($mhhConfUpdated)
{
    Output "Setting write permissions for `"Users`" group on configuration file..."
    $target = [Path]::Combine($programPath, "mhh.conf")
    $acl = Get-Acl -Path $target
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule("Users", "Write", "0", "0", "Allow")
    $acl.SetAccessRule($rule)
    $acl | Set-Acl -Path $target
}



################################################################################
# Shortcut and startup options

if($startMenu -or $startDesktop -or $startBootUp)
{
    Output "`nCreating shortcuts and startup settings..."
    $shell = New-Object -ComObject WScript.Shell
    # https://ss64.com/vb/shortcut.html

    if($startMenu)
    {
        $path = [System.Environment]::GetFolderPath("CommonStartMenu")
        $path = [Path]::Combine($path, "Programs", "Monkey-Hi-Hat")
        [void][Directory]::CreateDirectory($path)
        $path = [Path]::Combine($path, "Monkey Hi Hat.lnk")
        if(Test-Path($path))
        {
            Output "Start Menu shortcut already exists."
        }
        else
        {
            Output "Creating Start Menu shortcut."
            $link = $shell.CreateShortcut($path)
            $link.Description = "https://github.com/MV10/monkey-hi-hat/wiki"
            $link.TargetPath = [Path]::Combine($programPath, "mhh.exe")
            $link.WorkingDirectory = $programPath
            if($startHotKey)
            {
                $link.HotKey = "F10"
            }
            $link.Save()
        }
    }

    if($startDesktop)
    {
        $path = [Path]::Combine($env:USERPROFILE, "Desktop", "Monkey Hi Hat.lnk")
        if(Test-Path($path))
        {
            Output "Desktop shortcut already exists."
        }
        else
        {
            Output "Creating desktop shortcut."
            $link = $shell.CreateShortcut($path)
            $link.Description = "https://github.com/MV10/monkey-hi-hat/wiki"
            $link.TargetPath = [Path]::Combine($programPath, "mhh.exe")
            $link.WorkingDirectory = $programPath
            if($startHotKey -and -not $startMenu)
            {
                $link.HotKey = "F10"
            }
            $link.Save()
        }
    }


    if($startBootUp)
    {
        $path = [System.Environment]::GetFolderPath("CommonStartMenu")
        $path = [Path]::Combine($path, "Programs", "Startup", "Monkey Hi Hat.lnk")
        if(Test-Path($path))
        {
            Output "Startup shortcut already exists."
        }
        else
        {
            Output "Creating Start Menu Startup shortcut."
            $link = $shell.CreateShortcut($path)
            $link.Description = "https://github.com/MV10/monkey-hi-hat/wiki"
            $link.TargetPath = [Path]::Combine($programPath, "mhh.exe")
            $link.WorkingDirectory = $programPath
            $link.Save()
        }
    }

    Remove-Variable shell
}



################################################################################
# PREPARE TO EXIT
################################################################################

Output "`n`nInstallation has been completed."


################################################################################
# Offer step-by-step help for VB-Audio Cable configuration

if($installDriver)
{
    "`nThe audio loopback driver was installed but it needs to be configured."
    "Microsoft has blocked certain scripting features which means configuration"
    "is currently a manual process. Would you like a step-by-step walk-through"
    "for configuring the driver? If not, we can link you to a web page instead."
    "View audio configuration walk-through now?"
    if([Installer]::YesNo())
    {
        ""
        "Audio Loopback Configuration Walk-Through"
        "Press <Enter> after each step."
        ""
        mmsys.cpl sounds
        "The Windows `"Sound`" control panel dialog should be visible now."
        "(If it isn't, open the Start menu, type `"Control Panel`" and run that app,"
        "and click the `"Sound`" icon; it might be hidden under `"Hardware and Sound`")"
        Read-Host -Prompt "  <Enter> to continue"
        "On the `"Playback`" tab, note which device is the default (green checkmark)."
        Read-Host -Prompt "  <Enter> to continue"
        "Right-click the `"CABLE Input`" device and choose `"Set as default device`"."
        Read-Host -Prompt "  <Enter> to continue"
        "Click the `"Recording`" tab at the top of the dialog box."
        Read-Host -Prompt "  <Enter> to continue"
        "Right-click the `"CABLE Output`" device and choose `"Set as default device`"."
        "(It might already be set as the default device, Windows does that sometimes.)"
        Read-Host -Prompt "  <Enter> to continue"
        "Select the `"CABLE Output`" device and click the `"Properties`" button."
        "A new `"CABLE Output Properties`" dialog box will open."
        Read-Host -Prompt "  <Enter> to continue"
        "Click the `"Listen`" tab and check the box next to `"Listen to this device`"."
        Read-Host -Prompt "  <Enter> to continue"
        "Set the `"Playback`" drop-down to the default device noted earlier."
        Read-Host -Prompt "  <Enter> to continue"
        "Click `"Ok`" to exit both dialog boxes. The driver is configured."
    }
}


################################################################################
# Offer to open browser to VB-Audio Cable configuration help

if($installDriver)
{
    "`nA web page with step-by-step audio configuration instructions is available."
    "Even if you have already configured the audio loopback driver, we recommend"
    "bookmarking this page. View the web page in your default browser now?"
    if([Installer]::YesNo())
    {
        Start-Process $audioConfigUrl
    }
}


################################################################################
# Remind user to register Cable, offer to open browser to the VB-Audio store

if($installDriver)
{
    "`nThe VB-Audio Cable loopback driver is shareware. It is not free. If you"
    "continue using this driver, please send $5 to the author to support his work."
    "Would you like to view the payment page in your default browser now?"
    if([Installer]::YesNo())
    {
        Start-Process $donationUrl
    }
}

EndScript

#
# DEV / TEST NOTES
#
# Uninstall commands for the various products:
#
# dotnet     installer /uninstall /quiet
# vbcable    vbcable_setup_x64 -u -h
# openal     oalinst /u /s
#
# Directories:
#
# C:\Users\glitch\AppData\Local\Temp
# C:\ProgramData\Microsoft\Windows\Start Menu\Programs
#
