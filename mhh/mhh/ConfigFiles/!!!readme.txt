
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
