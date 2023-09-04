#version 460
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform sampler2D input0;  // pass1-plasma
uniform sampler2D input1;  // pass4-clouds
out vec4 fragColor;

#define fragCoord (fragCoord * resolution)
#define iResolution resolution

void main()
{
	vec2 uv = fragCoord.xy / iResolution.xy;
    fragColor = texture(input0, uv) * texture(input1, uv);
}
