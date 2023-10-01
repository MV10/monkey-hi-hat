#version 460
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform sampler2D input0;
uniform sampler2D inputB;
out vec4 fragColor;

#define fragCoord (fragCoord * resolution)

// Adapted from https://www.shadertoy.com/view/MdlBDn

void main()
{
    vec2 res = resolution.xy;
    vec2 tc = fragCoord.xy / res;
    vec2 uv = tc;
    
    uv *= 0.998;
    
    vec4 sum = texture(inputB, uv);
    vec4 src = texture(input0, tc);
    
    sum.rgb = mix(sum.rbg, src.rgb, 0.01);
    fragColor = sum;
}
