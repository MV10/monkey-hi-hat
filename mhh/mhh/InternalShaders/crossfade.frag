#version 460
precision highp float;

// This is used when mhh.conf specifies RandomizeCrossfade=false.
// When enabled, crossfade shaders are loaded from the viz/library
// paths. See comments in Volt's Laboratory's libraries directory
// in crossfade_simple.frag for more details.

in vec2 fragCoord;
uniform float fadeLevel;
uniform sampler2D oldBuffer;
uniform sampler2D newBuffer;
out vec4 fragColor;

void main()
{
    vec4 oldTexel = texture(oldBuffer, fragCoord);
    vec4 newTexel = texture(newBuffer, fragCoord);
    fragColor = mix(oldTexel, newTexel, fadeLevel);
}
