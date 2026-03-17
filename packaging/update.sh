#!/bin/bash

#
# This is essentially a copy of install.sh, except that it requires that
# the directories exist, it doesn't alter the config file and it doesn't
# create a .desktop launcher link, and upon exit it links to the changelog.
#

# DO NOT MODIFY THE FOLLOWING COMMENT; IT'S A MARKER FOR linux-zip.sh:
# BUILD SCRIPT ADDS VERSION VARIABLES BELOW

# hard-coded variables
APPDIR="$HOME/monkeyhihat"
CONTENTDIR="$HOME/mhh-content"
SOURCE="https://www.monkeyhihat.com/installer_assets"
TARGET="/tmp/mhhinstall"
LOGFILE="/tmp/mhh-update.log"
APPARCHIVE="mhh-linux-$APPVERSION.zip"
CONTENTARCHIVE="mhh-content-$CONTENTVERSION.zip"
TEXTUREARCHIVE="mhh-texture-$TEXTUREVERSION.zip"
NDIARCHIVE="ndi-6-2-1.zip"
DOTNETVERSION="10"

# init version number segments
MAJOR=0
MINOR=0
BUILD=0

# abort if any command fails
set -euo pipefail

# output everything to a log file
exec > >(tee -a "$LOGFILE") 2>&1

# abort if app / content directories do not exist
if [ ! -d "$APPDIR" ] || [ ! -d "$CONTENTDIR" ]; then
  echo "App and/or content directories not found. Use the installer script instead."
  echo "App: $APPDIR"
  echo "Content: $CONTENTDIR"
  exit 1
fi

# abort if wget is not installed
if [ ! -x /usr/bin/wget ]; then
    # additional check if wget is not installed at the usual place
    command -v wget >/dev/null 2>&1 || { echo >&2 "Please install wget, then run update again."; exit 1; }
fi

# abort if unzip is not installed
if [ ! -x /usr/bin/unzip ]; then
    # additional check if wget is not installed at the usual place
    command -v unzip >/dev/null 2>&1 || { echo >&2 "Please install unzip, then run update again."; exit 1; }
fi

# abort if DOTNETVERSION (major version) is not installed
check_dotnet() {
    if ! command -v dotnet >/dev/null 2>&1; then
        return 1
    fi
    if dotnet --list-sdks | grep -Eq "^${DOTNETVERSION}\."; then
        return 0
    fi
    if dotnet --list-runtimes | grep -Eq "^Microsoft\.NETCore\.App ${DOTNETVERSION}\."; then
        return 0
    fi
    return 1
}
if ! check_dotnet; then
  echo "Please install the .NET ${DOTNETVERSION}.x runtime (or SDK), then run install again:" >&2
  echo "https://learn.microsoft.com/en-us/dotnet/core/install/linux"
  exit 1
fi

# abort if FFmpeg is not installed
if ! command -v ffmpeg >/dev/null 2>&1; then
    echo "Please install FFmpeg as follows, then run update again:" >&2
    echo "sudo apt-get update && sudo apt-get install -y ffmpeg"
    exit 1
fi

# get existing version
version_file="$APPDIR/ConfigFiles/version.txt"
if [[ -s "$version_file" && -r "$version_file" ]]; then

    # read content using encoding that automatically removes Windows byte-order-mark (BOM)
    # if present, then just in case, also remove ordinary whitespace
    line=$(<"$version_file" tr -d '[:space:]')

    # explicitly remove UTF-8 BOM in case the shell / tr did not handle it
    line=${line#$'\xef\xbb\xbf'}

    if [[ -n "$line" ]] && IFS=. read -r maj min bld _ <<< "$line"; then
        [[ $maj =~ ^[0-9]+$ && -n "$maj" ]] && MAJOR="$maj"
        [[ $min =~ ^[0-9]+$ && -n "$min" ]] && MINOR="$min"
        [[ $bld =~ ^[0-9]+$ && -n "$bld" ]] && BUILD="$bld"
    fi
fi

# all checks passed, begin installation
echo ""
echo "======================================================="
echo "Updating to Monkey Hi Hat v$MAJOR.$MINOR.$BUILD to v$APPVERSION" | sed 's/-/./g'
echo "======================================================="

echo "Logging to $LOGFILE"

echo "Preparing target directory"
rm -rf "$TARGET" || true
mkdir "$TARGET"

echo "Downloading app archive..."
( cd $TARGET ; wget --no-verbose --show-progress --progress=bar:force:noscroll "$SOURCE/$APPARCHIVE" )

echo "Downloading streaming support..."
( cd $TARGET ; wget --no-verbose --show-progress --progress=bar:force:noscroll "$SOURCE/$NDIARCHIVE" )

echo "Downloading content archive..."
( cd $TARGET ; wget --no-verbose --show-progress --progress=bar:force:noscroll "$SOURCE/$CONTENTARCHIVE" )

echo "Downloading texture archive..."
( cd $TARGET ; wget --no-verbose --show-progress --progress=bar:force:noscroll "$SOURCE/$TEXTUREARCHIVE" )

echo "Expanding archives..."
( cd $APPDIR ; unzip -oqq "$TARGET/$APPARCHIVE" )
( cd $APPDIR ; unzip -oqq "$TARGET/$NDIARCHIVE" )
( cd $CONTENTDIR ; unzip -oqq  "$TARGET/$CONTENTARCHIVE")
( cd $CONTENTDIR ; unzip -oqq "$TARGET/$TEXTUREARCHIVE")

echo "Cleaning up temporary files..."
rm -rf "$TARGET"

echo "Updating the config file..."
( cd $APPDIR ; ./updateconf "$MAJOR.$MINOR.$BUILD" )

echo "======================================================="
echo "Update completed. Review the changes for this release:"
echo "https://www.monkeyhihat.com/docs/index.php#/changelog"
echo "======================================================="
echo ""
