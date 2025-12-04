#!/bin/bash

#
# Until I figure out how .deb packaging works, Linux releases will
# be a manual installation process using install.sh or update.sh.
#
# Stages the install scripts and all archived content that the installer
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
PUBLISH="/data/Source/monkey-hi-hat/mhh/mhh/bin/Release/net8.0/linux-x64"
CONTENT="/data/Source/volts-laboratory"
INSTALLER="$TARGET/install-$1.sh"
UPDATER="$TARGET/update-$1.sh"

VERSIONMARKER="# BUILD SCRIPT ADDS VERSION VARIABLES BELOW"
read -d '' VERSIONVARS << EOF
APPVERSION="$1"
CONTENTVERSION="$2"
TEXTUREVERSION="$3"
EOF

if [ ! -d "$PUBLISH" ]; then
  echo ""
  echo "ERROR:"
  echo "Linux published release build directory not found."
  echo "Expected: $PUBLISH"
  echo ""
  exit 1
fi

if [ ! -d "$TARGET" ]; then
  echo ""
  echo "ERROR:"
  echo "Packaging staging directory not found; run Windows packaging first."
  echo "Expected: $TARGET"
  echo ""
  exit 1
fi

echo ""
echo "======================================================="
echo "Packaging Monkey Hi Hat v$1 (Linux build)"
echo "======================================================="

echo "Deleting non-Linux libraries"
rm "$PUBLISH"/README_LIBS.md || true
rm "$PUBLISH"/Processing.NDI.Lib.x64.dll || true
rm "$PUBLISH"/CppSharp*.dll || true
rm "$PUBLISH"/libCppSharp*.so || true
rm "$PUBLISH"/libStd-symbols.so || true
rm "$PUBLISH"/NAudio*.dll || true
rm "$PUBLISH"/Spout*.dll || true

echo "Copying install and update scripts..."
cp install.sh "$INSTALLER"
cp update.sh "$UPDATER"
sed -i "/^${VERSIONMARKER}$/r /dev/stdin" "$INSTALLER" <<< "$VERSIONVARS"
sed -i "/^${VERSIONMARKER}$/r /dev/stdin" "$UPDATER" <<< "$VERSIONVARS"

echo "Creating application download archive"
echo "Source: $PUBLISH"
( cd "$PUBLISH" ; tar -czf "$TARGET/monkeyhihat-$1.tgz" ./* >/dev/null )

echo "Creating shader content download archive"
( cd /data/Source/volts-laboratory ; \
tar -czf "$TARGET/mhh-content-$2.tgz" ./crossfades/* ./fx/* ./libraries/* ./playlists/* ./shaders/* ./templates/* notes.txt >/dev/null )

echo "Creating shader texture download archive"
( cd /data/Source/volts-laboratory ; tar -czf "$TARGET/mhh-texture-$3.tgz" ./textures/* >/dev/null )

echo "======================================================="
echo "Linux packaging completed"
echo "Location: $TARGET"
echo "======================================================="
echo ""