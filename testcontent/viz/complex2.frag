#version 450
precision highp float;

// Uses the previous pass as the background color
// and prints the actual values stored by the vert shader
// in the first pass.

in vec2 fragCoord;
uniform sampler2D input0;
uniform sampler2D input1;
out vec4 fragColor;

void main()
{
	//float r = texelFetch(input0, ivec2(0, 0), 0).x;
	//float g = texelFetch(input0, ivec2(1, 0), 0).x;
	//float b = texelFetch(input0, ivec2(2, 0), 0).x;

	fragColor = texture(input1, fragCoord);

	// TODO - load font library and output RGB values 
}
