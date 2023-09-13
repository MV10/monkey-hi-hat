#version 460
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform sampler2D input0; // pass1-plasma
out vec4 fragColor;

// Derived from: https://www.shadertoy.com/view/lsdXDH

#define fragCoord (fragCoord * resolution)
#define iResolution resolution

// Source:
// http://stackoverflow.com/questions/9320953/what-algorithm-does-photoshop-use-to-desaturate-an-image

// comment out the algorithm you do not want to use below
//#define USE_PHOTOSHOP_ALGORITHM
#define USE_GENERIC_ALGORITHM

// Generic algorithm to desaturate images used in most game engines
vec4 generic_desaturate(vec3 color, float factor)
{
	vec3 lum = vec3(0.299, 0.587, 0.114);
	vec3 gray = vec3(dot(lum, color));
	return vec4(mix(color, gray, factor), 1.0);
}

// Algorithm employed by photoshop to desaturate the input
vec4 photoshop_desaturate(vec3 color)
{
    float bw = (min(color.r, min(color.g, color.b)) + max(color.r, max(color.g, color.b))) * 0.5;
    return vec4(bw, bw, bw, 1.0);
}

void main()
{
	vec2 uv = fragCoord.xy / iResolution.xy;
    fragColor = texture(input0, uv);
    
#	ifdef USE_GENERIC_ALGORITHM
	fragColor = generic_desaturate(texture(input0, uv).rgb, 1.0);
#	endif
    
#	ifdef USE_PHOTOSHOP_ALGORITHM
    fragColor = photoshop_desaturate(texture(input0, uv).rgb);
#	endif
}
