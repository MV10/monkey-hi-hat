#!/bin/bash

# abort if not invoked by package.sh
if [[ -z "$INVOKED_BY_PACKAGE" ]]; then
    echo "Please execute ./package.sh instead." >&2
    exit 1
fi

TARGET="/tmp/mhhpkg"
PUBLISH="/data/Source/monkey-hi-hat/mhh/mhh/bin/Release/net10.0/linux-x64"
CONTENT="/data/Source/volts-laboratory"
INSTALLER="$TARGET/install-$1.sh"
UPDATER="$TARGET/update-$1.sh"
UPDATECONF="/data/Source/monkey-hi-hat/mhh/updateconf/bin/Release/net10.0/linux-x64"

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

if [ ! -d "$UPDATECONF" ]; then
  echo ""
  echo "ERROR:"
  echo "updateconf published release build directory not found."
  echo "Expected: $UPDATECONF"
  echo ""
  exit 1
fi

echo ""
echo "======================================================="
echo "Packaging Monkey Hi Hat v$1 (Linux build)" | sed 's/-/./g'
echo "======================================================="

echo "Deleting separately-packaged libraries"
rm "$PUBLISH"/Processing.NDI.Lib.x64.dll || true
rm "$PUBLISH"/libndi.so || true
rm "$PUBLISH"/CppSharp*.dll || true
rm "$PUBLISH"/libCppSharp*.so || true
rm "$PUBLISH"/README_LIBS.md || true

echo "Merging config update utility into mhh publish directory"
cp --update=older $UPDATECONF/* $PUBLISH/

echo "Copying install and update scripts..."
cp install.sh "$INSTALLER"
cp update.sh "$UPDATER"
sed -i "/^${VERSIONMARKER}$/r /dev/stdin" "$INSTALLER" <<< "$VERSIONVARS"
sed -i "/^${VERSIONMARKER}$/r /dev/stdin" "$UPDATER" <<< "$VERSIONVARS"

echo "Creating application download archive"
echo "Source: $PUBLISH"
( cd "$PUBLISH" ; zip -9qr "$TARGET/mhh-linux-$1.zip" ./* )

echo "======================================================="
echo "Linux packaging completed"
echo "======================================================="
echo ""