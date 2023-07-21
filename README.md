# monkey-hi-hat
Raspberry Pi OpenGL streaming music visualization host

This is a work-in-progress based on my [eyecandy](https://github.com/MV10/eyecandy) audio-to-texture library. The basic idea is to create "playlists" of audio-reactive shaders to run on our big TV when we're listening to music or otherwise in the room but not actually interested in TV. What can I say? A 77" OLED deserves better than standard-definition Star Trek and Babylon 5 re-runs.

Although my goal is to run this on a Raspberry Pi 4B, for what it's worth, everything runs on Windows, too. For audio playback, Windows has a native Spotify client available instead of spotifyd used on the Pi. The eyecandy repo has (or will have) instructions about OS configuration for audio capture.

As the Pi will be stashed away behind the AV equipment, control will be via SSH terminal connections, sending commands (like "refresh the playlist" or "switch to shader XYZ") via named-pipe using my [CommandLineSwitchPipe](https://github.com/MV10/CommandLineSwitchPipe) library. And maybe if I'm feeling frisky, I'll write a simple Android app, too.

Why "monkey-hi-hat"? My wife's D&D character's familiar is some monkey-based, and at some point we're going to try to animate a model of this thing in time with music. Ya gotta have goals, right? Plus, I had to call it _something_...

# Commands

Executing "mhh" with no switches launches the program. After that, you run it again with the switches shown below. These are passed to the running instance. The output from `mhh --help` lists most of the switches:

```
--help                      shows help (surprise!)
--load [shader]             loads [shader].conf from ShaderPath defined in mhh.conf
--load [path/shader]        must use forward slash; if present, loads [shader].conf from requested location
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

By default, warnings and errors are written to `mhh.log` in the application directory. The log level can be changed although currently the application and its libraries only write events with Warning or Error levels. Microsoft libraries are not wired into the logger at this time.

As the last two lines note, visualizers may support additional commands. At this time only visualizer supports commands. When it's loaded and running, `--help viz` shows:

```
Runtime commands for VisualizerVertexIntegerArray:

--viz [mode] [Points|Lines|LineStrip|LineLoop|Triangles|TriangleStrip|TriangleFan]
```

# Application Configuration

The [`mhh.conf`](https://github.com/MV10/monkey-hi-hat/blob/master/mhh/mhh/mhh.conf) file in the repository documents all currently-available settings. This will probably change a lot as I continue to work on the project. When the project is more stable, I will document this in more detail in the repo's wiki.

Generally it contains things like paths and audio device information. Some information is listed twice according to whether it is running under Windows or Linux.

# Visualization Configuration

A visualization consists of three files: the configuration, a vertex shader source file, and a fragment shader source file. There are many examples in the repo's [samples](https://github.com/MV10/monkey-hi-hat/tree/master/samples) directory, but here is a one that reproduces the FFT Frequency Magnitude display from the eyecandy repository's History demo.

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
