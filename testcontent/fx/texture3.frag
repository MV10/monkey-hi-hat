#version 460
precision highp float;

in vec2 fragCoord;
uniform sampler2D input3;
out vec4 fragColor;

// Adapted from https://www.shadertoy.com/view/Msd3W2

void main()
{
	vec3 image = texture(input3, fragCoord).xyz;
	fragColor = vec4(image, 0.0);
}
