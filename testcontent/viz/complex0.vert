#version 450

// Expects three integers. Stores a random number into 
// pixels 0, 1, and 2 (for y = 0). These random values
// will be treated as RGB channel values by the frag
// shader in the next pass.

layout (location = 0) in float vertexId;
uniform float vertexCount;
uniform float time;
out vec4 v_color;

float color(float channel) 
{ 
	return 0.5 + 0.5 * cos(time + channel);
}

void main()
{
	int id = int(vertexId);
	float x = vertexId / vertexCount;

	// shadertoy New color: 0.5 + 0.5*cos(iTime+uv.xyx+vec3(0,2,4));
	float rgb[3];
	rgb[0] = color(0);
	rgb[1] = color(2);
	rgb[2] = color(4);

	gl_PointSize = 1.0;
	gl_Position = vec4(x, 0, 0, 1);
	v_color = vec4(rgb[id], 0, 0, 1);

	gl_PointSize = 15.0;
	gl_Position = vec4(x, x, 0, 1);
	v_color = vec4(rgb[id], rgb[id], rgb[id], 1);
}
