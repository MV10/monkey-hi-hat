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
TARGET="/tmp/mhhpkg"
APPARCHIVE="mhh-linux-$APPVERSION.zip"
CONTENTARCHIVE="mhh-content-$CONTENTVERSION.zip"
TEXTUREARCHIVE="mhh-texture-$TEXTUREVERSION.zip"
NDIARCHIVE="ndi-6-2-1.zip"
DOTNETVERSION="8"

# abort if app / content directories do not exist
if [ ! -d "$APPDIR" ] || [ ! -d "$CONTENTDIR" ]; then
  echo "App and/or content directories not found. Use 'update.sh' instead."
  echo "App: $APPDIR"
  echo "Content: $CONTENTDIR"
  exit 1
fi

# abort if wget is not installed
if [ ! -x /usr/bin/wget ]; then
    # additional check if wget is not installed at the usual place
    command -v wget >/dev/null 2>&1 || { echo >&2 "Please install wget, then run install again."; exit 1; }
fi

# abort if unzip is not installed
if [ ! -x /usr/bin/unzip ]; then
    # additional check if wget is not installed at the usual place
    command -v unzip >/dev/null 2>&1 || { echo >&2 "Please install unzip, then run install again."; exit 1; }
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
    echo "Please install FFmpeg as follows, then run install again:" >&2
    echo "sudo apt-get update && sudo apt-get install -y ffmpeg"
    exit 1
fi

# abort if any command fails
set -e

# all checks passed, begin installation
echo ""
echo "======================================================="
echo "Updating to Monkey Hi Hat v$APPVERSION" | sed 's/-/./g'
echo "======================================================="

echo "Preparing target directory"
rm -rf "$TARGET" || true
mkdir "$TARGET"

echo "Downloading app archive..."
( cd $TARGET ; wget "$SOURCE/$APPARCHIVE" )

echo "Downloading streaming support..."
( cd $TARGET ; wget "$SOURCE/$NDIARCHIVE" )

echo "Downloading content archive..."
( cd $TARGET ; wget "$SOURCE/$CONTENTARCHIVE" )

echo "Downloading texture archive..."
( cd $TARGET ; wget "$SOURCE/$TEXTUREARCHIVE" )

echo "Expanding archives..."
mkdir "$APPDIR"
mkdir "$CONTENTDIR"
( cd $APPDIR ; unzip -oqq "$TARGET/$APPARCHIVE" )
( cd $APPDIR ; unzip -oqq "$TARGET/$NDIARCHIVE" )
( cd $CONTENTDIR ; unzip -oqq  "$TARGET/$CONTENTARCHIVE")
( cd $CONTENTDIR ; unzip -oqq "$TARGET/$TEXTUREARCHIVE")

echo "Cleaning up temporary files..."
rm -rf "$TARGET"

echo "======================================================="
echo "Update completed. You must manually update the config"
echo "file (mhh.conf). Review the changes for this release:"
echo "https://github.com/MV10/monkey-hi-hat/wiki/12.-Changelog"
echo "======================================================="
echo ""
