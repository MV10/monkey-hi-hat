# monkey-hi-hat
Raspberry Pi OpenGL streaming music visualization host

This is a work-in-progress based on my [eyecandy](https://github.com/MV10/eyecandy) audio-to-texture library. The basic idea is to create "playlists" of shaders to run on our big TV when we're listening to music or otherwise in the room but not actually interested in TV. What can I say? A 77" OLED deserves better than standard-definition Star Trek and Babylon 5 re-runs.

Although my goal is to run this on a Raspberry Pi 4B, for what it's worth, everything runs on Windows, too. For audio playback, Windows has a native Spotify client available instead of spotifyd used on the Pi. The eyecandy repo has (or will have) instructions about OS configuration for audio capture.

As the Pi will be stashed away behind the AV equipment, control will be via SSH terminal connections, sending commands (like "refresh the playlist" or "switch to shader XYZ") via named-pipe using my [CommandLineSwitchPipe](https://github.com/MV10/CommandLineSwitchPipe) library. And maybe if I'm feeling frisky, I'll write a simple Android app, too.

Why "monkey-hi-hat"? My wife's D&D familiar is some sort of monkey, and at some point we're going to try to animate a model of this thing in time with music. Ya gotta have goals, right? Plus, I had to call it _something_...

Stay tuned...