#version 460
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform float time;
uniform sampler2D input0;
out vec4 fragColor;

// Adapted from https://www.shadertoy.com/view/Xsl3zn

void main()
{
	vec2 uv = fragCoord;
    vec2 warp = texture(input0, uv * 0.1 + time * vec2(0.04, 0.03)).xz;
	vec2 st = uv + warp * 0.5;
	fragColor = vec4(texture(input0, st).xyz, 1.0);
}
