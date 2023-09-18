#version 460

// This is the pass-through vertex shader for FX fragment shaders.

layout(location = 0) in vec3 vertices;
layout(location = 1) in vec2 vertexTexCoords;
out vec2 fragCoord;

void main(void)
{
    fragCoord = vertexTexCoords;
    gl_Position = vec4(vertices, 1.0);
}
