#version 320 es
precision highp float;

// Normally the primary processing for VisualizerVertexIntegerArray
// happens in the vert shader and this default frag shader always applies.

in vec4 v_color;
out vec4 fragColor;  
  
void main()
{
    fragColor = v_color;
}
