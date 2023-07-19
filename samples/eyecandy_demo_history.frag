#version 320 es
precision highp float;

in vec2 fragCoord;
uniform sampler2D sound;
out vec4 fragColor;

void main()
{
    fragColor = texture(sound, fragCoord);
}
