
# Third-Party Libraries

In addition to the usual NuGet software libraries, MHH relies upon non-NuGet third-party libraries.  All libraries distributed with or installed by the application are provided in accordance with the third-party owners' licensing terms.

## NDI Streaming Support

The NDI files are directly included in the repo and are in this Libraries directory for dev and test purposes. Because they are large and change infrequently, for installation purposes the app .bin archives (which are just a renamed .zip file) exclude them. They are available to the installers as a separate .bin download.

## FFMpeg Video Support

The FFmpeg libraries installed by the Windows application are unmodified official binaries. Developers working on the source code should read the MHH docs, but the short version is, put the FFmpeg DLLs somewhere in your path. Personally I maintain a specific directory (`C:\Source\_libraries_in_path`) for this purpose.

As I write this, I haven't yet tackled fully-automated Linux installation, but I hope to provide a .deb package. The FFmpeg libraries needed by Linux should be installed from official package sources. If I provide a .deb file, it will simply declare a dependency on FFmpeg.
