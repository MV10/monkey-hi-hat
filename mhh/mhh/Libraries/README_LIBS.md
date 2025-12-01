
# Monkey Hi Hat Third-Party Libraries

In addition to the usual NuGet software libraries, MHH relies upon non-NuGet third-party libraries.  All libraries distributed with or installed by the application are provided in accordance with the third-party owners' licensing terms.

## Windows

The FFmpeg libraries installed by the Windows application are unmodified official binaries.

Developers working on the source code should read the MHH wiki, but the short version is, put the FFmpeg DLLs somewhere in your path. Personally I maintain a specific directory (`C:\Source\_libraries_in_path`) for this purpose.

Because the NDI files are large and change infrequently, the Windows installer application .bin files (which is just a renamed .zip file) excludes them. They are available to the installer as a separate .bin download.

## Linux

As I write this, I haven't yet tackled Linux installation, but I hope to provide a .deb package.

The FFmpeg libraries needed by Linux should be installed from official package sources. If I provide a .deb file, it will simply declare a dependency on FFmpeg.

There is no packaged download available for NDI files. Most likely they will be bundled with the .deb package.
