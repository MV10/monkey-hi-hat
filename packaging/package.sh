#!/bin/bash

#
# See the packaging README for details.
# 
# Requires three x-x-x version number arguments (app, content, textures)
# Parenthesis makes the commands temporary (ie. temporary change directory)
#
# This merely invokes media.sh, windows.sh and linux-zip.sh, in that order.
#
# Stages the Windows-based install program and all content that the installer
# downloads in the /tmp/mhhpkg directory. For Linux, until I figure out how
# .deb packaging works, installation is by script using install.sh or update.sh
# (which are modified for version number info).
#

# verify arguments are provided
if [ "$#" -ne 3 ]; then
  echo ""
  echo "Usage: $0 <app-ver> <content-ver> <textures-ver>"
  echo "Versions should be x-y-z format. All three are required."
  echo ""
  exit 1
fi

# abort if one of the child scripts fails
set -e

# tells the child scripts it's ok to run
export INVOKED_BY_PACKAGE=1

# Must be invoked in this sequence; content.sh clears/creates temp directory.
# Even though windows.sh and linux-zip.sh don't package content files, they need
# to know the version numbers for downloading dependencies. (Technically only
# linux needs it at this time.)
./media.sh $2 $3
./windows.sh $1 $2 $3
./linux-zip.sh $1 $2 $3

echo "======================================================="
echo "All packaging completed."
echo "======================================================="
echo ""
echo "ls -lh /tmp/mhhpkg"
ls -lh /tmp/mhhpkg
echo ""
