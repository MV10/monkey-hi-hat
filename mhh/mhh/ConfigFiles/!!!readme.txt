﻿
If you use the install program, you probably don't need to worry about
the template files in this directory.

For developers running MHH in debug from Visual Studio, and for those
who prefer a manual unzip install, see below, and also read the
config section of the Wiki. (But seriously, use the installer.)

The program looks for configuration files in the top-level application 
directory. They are stored here so that unzipping a new release over the
old install directory won't overwrite your existing configuration (but 
be sure to check the change logs for any new or changed settings).

Alternately, you can set the "monkey-hi-hat-config" environment variable
to the full pathname (path and filename) to your configuration file.

During development, it is recommended to leave the files here and use
the Launch Profile Settings dialog box to define a process-level temp
environment variable which points to the mhh.debug.conf file in this
directory.
