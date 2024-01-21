#version 450
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform sampler2D input0; // pass1-plasma
out vec4 fragColor;

#define fragCoord (fragCoord * resolution)
#define iResolution resolution

// Declarations for library multipass_desaturate.frag:
vec4 generic_desaturate(vec3 color, float factor);
vec4 photoshop_desaturate(vec3 color);

// comment out the algorithm you do not want to use
//#define USE_PHOTOSHOP_ALGORITHM
#define USE_GENERIC_ALGORITHM

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
