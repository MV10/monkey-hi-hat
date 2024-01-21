#version 450
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform sampler2D inputB;
out vec4 fragColor;

#define fragCoord (fragCoord * resolution)

// Adapted from https://www.shadertoy.com/view/MdlBDn

void main()
{
	vec2 uv = fragCoord.xy / resolution.xy;
	fragColor = texture(inputB, uv);
}
