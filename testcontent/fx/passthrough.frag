#version 460
precision highp float;

// This is a passthrough FX shader. It's useful to create
// a doublebuffered version of the primary visualizer so
// that other FX shaders can reference it as a backbuffer.

in vec4 v_color;
out vec4 fragColor;  
  
void main()
{
    fragColor = v_color;
}
