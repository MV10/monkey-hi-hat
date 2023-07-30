# monkey-hi-hat <img src="https://github.com/MV10/volts-laboratory/blob/master/misc/mhh-icon.png" height="32px"/>

**Streaming music visualization host**

This application intercepts audio using OpenAL and my [eyecandy](https://github.com/MV10/eyecandy) audio-to-texture library, allowing the creation of various audio-reactive OpenGL shaders as interesting visualizations to accompany the music. Playlists of these visualizations can be created with various criteria for rotating among the listed shaders.

Although I still consider this a work-in-progress, I try to keep the main branch of the repository in a working state. It currently requires .NET 6 and runs under Windows or Linux. My original goal was to run this on a Raspberry Pi 4B, however the GPU can only handle relatively simple shaders with any decent frame rate, so my target is now a Windows-based mini-PC. However, I will still test on the Pi and document that setup process (which is enormously more complicated than Windows), so I'm guessing this will still work on other Linux hardware, too. The eyecandy repo has (or will have) instructions about OS configuration for audio capture on both platforms.

Until I feel like declaring a "version 1.0" you will have to build from source.

As the computer will be stashed away behind the AV equipment, control is via SSH terminal connections, sending commands (like "refresh the playlist" or "switch to shader XYZ") via named-pipe using my [CommandLineSwitchPipe](https://github.com/MV10/CommandLineSwitchPipe) library. And maybe if I'm feeling frisky, I'll write a simple Android app, too (keep an eye out for this at my [monkey-droid](https://github.com/MV10/monkey-droid) repository).

Sample shaders, accompanying configuration files, and where all these oddball names came from can be found in my [Volt's Laboratory](https://github.com/MV10/volts-laboratory) repository.

# Commands

Executing "mhh" with no switches launches the program. After that, you run it again with the switches shown below. These are passed to the running instance. The output from `mhh --help` lists most of the switches:

```
--help                      shows help (surprise!)
--load [shader]             loads [shader].conf from ShaderPath defined in mhh.conf
--load [path/shader]        must use forward slash; if present, loads [shader].conf from requested location
--playlist [file]           loads [file].conf from PlaylistPath defined in mhh.conf
--playlist [path/file]      must use forward slash; if present, loads [file].conf from requested location
--next                      when a playlist is active, advances to the next shader (according to the Order)
--list [viz|playlists]      shows visualization confs or playlists in the default storage locations
--quit                      ends the program
--info                      writes shader and execution details to the console
--fps                       writes FPS information to the console
--idle                      load the default/idle shader
--pause                     stops the current shader
--run                       executes the current shader
--reload                    unloads and reloads the current shader
--pid                       shows the current Process ID
--log [level]               shows or sets log-level (None, Trace, Debug, Information, Warning, Error, Critical)
--viz [command] [value]     send commands to the current visualizer (if supported; see below)
--help viz                  list --viz command/value options for the current visalizer, if any
```

By default, warnings and errors are written to `mhh.log` in the application directory. The log level can be changed in `mhh.conf` or on-the-fly with the `--log` switch. Microsoft libraries are not wired into the logger at this time.

As the last two lines note, visualizers may support additional commands. At this time only one visualizer supports commands. When it's loaded and running, `--help viz` shows:

```
Runtime commands for VisualizerVertexIntegerArray:

--viz [mode] [Points|Lines|LineStrip|LineLoop|Triangles|TriangleStrip|TriangleFan]
```

# Application Configuration

The [`mhh.conf`](https://github.com/MV10/monkey-hi-hat/blob/master/mhh/mhh/mhh.conf) file in the repository documents all currently-available settings. When the project is more stable, I will document this in more detail in the repo's wiki.

Generally it contains things like paths and audio device information. Some information is listed twice according to whether it is running under Windows or Linux.

# Visualization Configuration

A visualization consists of three files: the configuration, a vertex shader source file, and a fragment shader source file. There are many examples in [shader repo](https://github.com/MV10/volts-laboratory/tree/master/shaders), and I'll be adding to those over time, but here is a one that reproduces the FFT Frequency Magnitude display from the eyecandy repository's History demo.

This illustrates a few important points. This particular visualization needs an audio texture. The type of visualizer is similar to Shadertoy.com, which means most of the work is done in the frag shader. Thus, a default vert shader is used (although that isn't required by any means). The frag shader is re-used among multiple visualizations based on the eyecandy History demo (namely, the other audio texture styles).

#### ```eyecandy_demo_history_freqmag.conf```

```ini
[shader]
Description=Eyecandy Demo: FFT Frequency Magnitude History
VisualizerTypeName=VisualizerFragmentQuad

VertexShaderFilename=VisualizerFragmentQuad.vert
FragmentShaderFilename=eyecandy_demo_history.frag

[audiotextures]
0=sound AudioTextureFrequencyMagnitudeHistory

[audiotexturemultipliers]
0=5.0
```

#### ```VisualizerFragmentQuad.vert```

```glsl
#version 320 es

// Normally the primary processing for VisualizerFragmentQuad happens
// in the frag shader and this default vert shader always applies.

layout(location = 0) in vec3 vertices;
layout(location = 1) in vec2 vertexTexCoords;
out vec2 fragCoord;

void main(void)
{
    fragCoord = vertexTexCoords;
    gl_Position = vec4(vertices, 1.0);
}
```

#### ```eyecandy_demo_history.frag```

```glsl
#version 320 es
precision highp float;

in vec2 fragCoord;
uniform sampler2D sound;
out vec4 fragColor;

void main()
{
    fragColor = texture(sound, fragCoord);
}
```

# Playlist Configuration

The sample [`demo_playlist.conf`](https://github.com/MV10/volts-laboratory/blob/master/playlists/demo_playlist.conf) file in the shader repository documents all currently-available settings and options.

If silence detection doesn't seem to be working, run the "silence" demo in the eyecandy library, your system may generate a low level of noise that isn't audible to you. Adjust the `DetectSilenceMaxRMS` value in the `mhh.conf` configuration file. For example, my desktop machine is "silent" at about 1.5, but my Raspberry Pi works as low as 0.2. 
