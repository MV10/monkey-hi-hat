#!/bin/bash

#
# Stages the Windows-based install program and all content that the installer
# downloads in the /tmp/mhhpkg directory. See the packaging README for details.
# 
# Requires three x-x-x version number arguments (app, content, textures)
# Parenthesis makes the commands temporary (ie. temporary change directory)
#

if [ "$#" -ne 3 ]; then
  echo ""
  echo "Usage: $0 <app-ver> <content-ver> <textures-ver>"
  echo "Versions should be x-y-z format. All three are required."
  echo ""
  exit 1
fi

TARGET="/tmp/mhhpkg"
PUBLISH="/data/Source/monkey-hi-hat/mhh/mhh/bin/Release/net8.0/win-x64"
MSMD="/data/Source/monkey-see-monkey-do/msmd/msmd/bin/publish/win-x64"
INSTALLER="/data/Source/monkey-hi-hat/mhh/install/bin/Release/install.exe"
CONTENT="/data/Source/volts-laboratory"

if [ ! -d "$PUBLISH" ]; then
  echo ""
  echo "ERROR:"
  echo "Windows published release build directory not found."
  echo "Expected: $PUBLISH"
  echo ""
  exit 1
fi

if [ ! -d "$MSMD" ]; then
  echo ""
  echo "ERROR:"
  echo "monkey-see-monkey-do published release build directory not found."
  echo "Expected: $MSMD"
  echo ""
  exit 1
fi

if [ ! -f "$INSTALLER" ]; then
  echo ""
  echo "ERROR:"
  echo "Installer published release build directory not found."
  echo "Expected: $INSTALLER"
  echo ""
  exit 1
fi

echo ""
echo "======================================================="
echo "Packaging Monkey Hi Hat v$1 (Windows build)"
echo "======================================================="

echo "Preparing target directory"
rm -rf "$TARGET" || true
mkdir "$TARGET"

echo "Copying install program"
cp "$INSTALLER" "$TARGET/install-$1.exe"

echo "Deleting downloaded third-party dependencies and non-Windows libraries"
rm "$PUBLISH"/README_LIBS.md || true
rm "$PUBLISH"/Processing.NDI.Lib.x64.dll || true
rm "$PUBLISH"/libndi.so || true
rm "$PUBLISH"/Tmds.DBus.dll || true
rm "$PUBLISH"/Tmds.DBus.Protocol.dll || true

echo "Merging monkey-see-monkey-do published build into mhh publish directory"
cp --update=none $MSMD/* $PUBLISH/

echo "Creating application download archive"
echo "Source: $PUBLISH"
( cd "$PUBLISH" ; zip -q "$TARGET/mhh-app-$1.bin" ./* )

echo "Creating shader content download archive"
( cd /data/Source/volts-laboratory ; \
zip -q "$TARGET/mhh-content-$2.bin" ./crossfades/* ./fx/* ./libraries/* ./playlists/* ./shaders/* ./templates/* notes.txt )

echo "Creating shader texture download archive"
( cd /data/Source/volts-laboratory ; zip -q "$TARGET/mhh-texture-$3.bin" ./textures/* )

echo "======================================================="
echo "Windows packaging completed"
echo "Location: $TARGET"
echo "======================================================="
echo ""