#version 460
precision highp float;

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
