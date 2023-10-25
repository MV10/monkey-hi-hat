
#
# Monkey-Hi-Hat Windows Install Script
# https://github.com/MV10/monkey-hi-hat
#
# Requires Internet access and administrator access.
# All output is logged to mhh-install.log in user's  temp directory.
#
# Sequence of events:
#
#    --- initialization
#    validate admin rights and/or elevate
#    validate Win10 / Win11 x64
#    retrieve config from repository
#    verify .NET version
#    verify VB-Cable installation
#    if VB-Cable installed, check configuration
#    check for previous MHH in default location
#    check for mhh.config file
#    check for MHH content in defined paths and/or default location
#
#    --- preparation
#    list findings
#    if necessary, prompt to install .NET, quit if denied
#    if necessary, prompt to install VB-Cable, recommend quit if denied
#    if VB-Cable installed, if necessary, prompt to fix VB-Cable config
#    prompt for approval to install MHH or overwrite existing MHH installation
#    prompt for approval to install MHH content; removes any existing MHH directories
#    prompt for auto-reboot upon completion
#    prompt for desired start options:
#      -- Start Menu shortcut
#      -- Launch in standby at Windows startup
#      -- F10 shortcut on Start Menu shortcut
#
#    --- downloads (where necessary)
#    .NET runtime installer
#    VB-Cable installer
#    MHH application archive
#    MHH content archive
#
#    --- processing
#    run .NET installer and validate
#    note current default audio playback device
#    run VB-Cable installation
#    configure system Sound settings
#    unzip MHH application
#    remove existing MHH content
#    unzip MHH content
#    create or update mhh.config
#    clean up downloads
#    create any startup options
#    remind user to register VB-Cable (with link)
#    reboot
#
# Config file contents:
#    .NET major version (ex. "6")
#    URL to download .NET installer
#    URL to download VB-Cable installer
#    URL to download MHH program archive
#    URL to download MHH content archive
#    URL to pay for VB-Cable
#    default MHH program directory
#    default MHH content directory
#

using namespace System.IO

# Check for admin rights, and if necessary, restart as admin (with UAC prompting)
if (-not (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) 
{
	$args = "& '" + $myInvocation.MyCommand.Definition + "'"
	Start-Process powershell -Verb runAs -ArgumentList $args
	exit
}

# Necessary because a non-interactive session may have been launched
function PauseExit
{
    Read-Host "`nPress any key to exit"
    exit
}

# Validate OS version / bitness
if(([System.Environment]::OSVersion.Version.Major -ne "10") -or -not([Environment]::Is64BitProcess))
{
    "Monkey-Hi-Hat requires 64-bit Windows 10 or Windows 11."
    PauseExit
}

$temp = [Path]::GetTempPath()
$log = [Path]::Combine($temp, "install-monkey-hi-hat.log")
$configUrl = "https://github.com/MV10/monkey-hi-hat/installer/install.win"

function Output ([string]$message)
{
    Add-Content $log $message
    Write-Host $message
}

function EndScript
{
    Output "Installation ended: $([DateTime]::Now)"
    Output "------------------------------------------------"
    PauseExit
}

Output "------------------------------------------------"
Output "Installation started: $([DateTime]::Now)"


################################################################################
# INITIALIZATION
################################################################################


################################################################################
Output "Downloading config: $configUrl"
$target = [Path]::Combine($temp, "install.win")
try
{
    Invoke-WebRequest -Uri $configUrl | Select-Object -ExpandProperty Content | Out-File $target
}
catch
{
    Output $PSItem.ToString()
    EndScript
}

$config = Get-Content -Path $target

Output $config.Length

EndScript
