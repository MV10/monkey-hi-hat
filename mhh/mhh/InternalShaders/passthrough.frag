#version 450
precision highp float;

// Generic pass-through fragment shader for vertex-oriented visualizations.

in vec4 v_color;
out vec4 fragColor;  
  
void main()
{
    fragColor = v_color;
}
