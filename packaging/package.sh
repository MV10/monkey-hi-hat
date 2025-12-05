#!/bin/bash

#
# See the packaging README for details.
# 
# Requires three x-x-x version number arguments (app, content, textures)
# Parenthesis makes the commands temporary (ie. temporary change directory)
#
# This merely invokes windows.sh and linux-zip.sh, in that order.
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

# must be invoked in this sequence; windows.sh clears/creates temp directory
./windows.sh $1 $2 $3
./linux-zip.sh $1 $2 $3

echo "======================================================="
echo "All packaging completed."
echo "======================================================="
echo ""
ls -lh /tmp/mhhpkg
echo ""
