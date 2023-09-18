#version 460
precision highp float;

in vec2 fragCoord;
uniform sampler2D imageA;
out vec4 fragColor;

void main()
{
	fragColor = vec4(texture(imageA, fragCoord).xyz, 1.0);
}
