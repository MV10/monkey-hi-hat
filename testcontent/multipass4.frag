#version 450
precision highp float;

in vec2 fragCoord;
uniform sampler2D input0;  // pass1-plasma
uniform sampler2D input1;  // pass4-clouds
out vec4 fragColor;

void main()
{
    fragColor = texture(input0, fragCoord) * texture(input1, fragCoord);
}
