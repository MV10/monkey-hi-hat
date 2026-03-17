#version 450
precision highp float;

// Combines the values stored in input0 pixels 0, 1, and 2
// (for y = 0) into a solid background color, which is randomized
// for every run.

in vec2 fragCoord;
uniform sampler2D input0;
out vec4 fragColor;

void main()
{
	float r = texelFetch(input0, ivec2(0, 0), 0).x;
	float g = texelFetch(input0, ivec2(1, 0), 0).x;
	float b = texelFetch(input0, ivec2(2, 0), 0).x;

	fragColor = vec4(r, g, b, 1);




	fragColor = texture(input0, fragCoord);
}