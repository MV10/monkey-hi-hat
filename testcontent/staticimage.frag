#version 450
precision highp float;

in vec2 fragCoord;
uniform sampler2D imageA;
out vec4 fragColor;

void main()
{
	fragColor = texture(imageA, fragCoord);
}
