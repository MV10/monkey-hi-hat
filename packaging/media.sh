#!/bin/bash

# abort if not invoked by package.sh
if [[ -z "$INVOKED_BY_PACKAGE" ]]; then
    echo "Please execute ./package.sh instead." >&2
    exit 1
fi

TARGET="/tmp/mhhpkg"
CONTENT="/data/Source/volts-laboratory"

echo ""
echo "======================================================="
echo "Packaging media (content v$1, textures v$2)" | sed 's/-/./g'
echo "======================================================="

echo "Preparing target directory"
rm -rf "$TARGET" || true
mkdir "$TARGET"

echo "Creating shader content download archive"
( cd /data/Source/volts-laboratory ; \
zip -9qr "$TARGET/mhh-content-$1.zip" ./crossfades/* ./fx/* ./libraries/* ./playlists/* ./shaders/* ./templates/* notes.txt )

echo "Creating shader texture download archive"
( cd /data/Source/volts-laboratory ; zip -9qr "$TARGET/mhh-texture-$2.zip" ./textures/* )

echo "======================================================="
echo "Media packaging completed"
echo "======================================================="
echo ""