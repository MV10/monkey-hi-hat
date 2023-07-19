#version 320 es

// Normally the primary processing for VisualizerFragmentQuad happens
// in the frag shader and this default vert shader always applies.

layout(location = 0) in vec3 vertices;
layout(location = 1) in vec2 vertexTexCoords;
out vec2 fragCoord;

void main(void)
{
    fragCoord = vertexTexCoords;
    gl_Position = vec4(vertices, 1.0);
}
