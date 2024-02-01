
About the "Install" Project
-------------------------------------------------------------------------------

Although monkey-hi-hat and all the associated libraries and programs are
modern .NET (currently .NET 8.0 as I write this in 2024), this is a .NET
Framework 4.7.2 console application that acts as an installer. Why? Because
that version of .NET Framework is *always* available on any version of
Windows that I care about (currently Win10 and Win11). 

See "Background" for more details.

The app embeds a manifest which should UAC-prompt for Admin rights when the
program is launched.

It supports fresh installation, uninstallation of current or older versions, and
upgrading older versions. For upgrades or uninstall, if the no-longer-necessary
audio driver or OpenAL libraries are found, it can remove these.

For upgrades, it can modify the configuration file to add new settings.


Background
-------------------------------------------------------------------------------

v1 and v2 of Monkey Hi Hat required a manual install process that looked
overwhelming (it's actually pretty easy, but people don't like to RTFM...)

v3 added a pretty elaborate install.ps1 PowerShell script which worked pretty
well but turned out to be hindered by Microsoft's stupid default policy of
blocking PowerShell script execution.

The state of install tools is pretty terrible (unless you're willing to spend
thousands on a pro tool, which is unreasonable for a freebie app like this),
the stuff from Microsoft like ClickOnce is too "enterprise-focused", and
the freebie products either can't handle even basic customizations or they're
ridiculously complicated.

Then I realized ... every version of Windows 10 and Windows 11 includes some
version of the .NET Framework. Why not just write a console-based installer
which targets the guaranteed-to-exist .NET runtime? Sure I'm sick and tired
of .NET Framework and I personally believe MS should have just "ripped off
the bandaid" and EOL'd all of it by now, but it is what it is.

This project targets 4.7.2 which currently has no end of life date. Targeting
4.7.2 should allow this to work on Win10 back to 2018 which is six years ago,
as I write this at the start of 2024. Good enough. If you aren't going to
update Windows for six years, you also probably don't have hardware that can
handle this stuff. Currently 4.7.2 doesn't have an EOL date, and 4.8.x is the
so-called "forever" release, so hopefully this strategy will let me distribute
an easy-to-use install.exe for quite a few years to come.


References
-------------------------------------------------------------------------------

.NET Framework versions shipped with Windows
https://learn.microsoft.com/en-us/dotnet/framework/migration-guide/versions-and-dependencies#net-framework-472

.NET Framework end-of-life dates
https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-framework

