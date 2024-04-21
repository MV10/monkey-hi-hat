#version 450
precision highp float;

// A simple pass-through associated with the vert shader for this pass.

in vec4 v_color;
out vec4 fragColor;  

void main()
{
    fragColor = v_color;
}
