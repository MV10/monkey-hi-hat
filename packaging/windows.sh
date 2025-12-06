#!/bin/bash

# abort if not invoked by package.sh
if [[ -z "$INVOKED_BY_PACKAGE" ]]; then
    echo "Please execute ./package.sh instead." >&2
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
echo "Packaging Monkey Hi Hat v$1 (Windows build)" | sed 's/-/./g'
echo "======================================================="

echo "Copying install program"
cp "$INSTALLER" "$TARGET/install-$1.exe"

echo "Deleting separately-packaged libraries"
rm "$PUBLISH"/Processing.NDI.Lib.x64.dll || true
rm "$PUBLISH"/libndi.so || true
rm "$PUBLISH"/CppSharp*.dll || true
rm "$PUBLISH"/libCppSharp*.so || true
rm "$PUBLISH"/README_LIBS.md || true

echo "Merging monkey-see-monkey-do published build into mhh publish directory"
cp --update=none $MSMD/* $PUBLISH/

echo "Creating application download archive"
echo "Source: $PUBLISH"
( cd "$PUBLISH" ; zip -9qr "$TARGET/mhh-win-$1.zip" ./* )

echo "======================================================="
echo "Windows packaging completed"
echo "======================================================="
echo ""