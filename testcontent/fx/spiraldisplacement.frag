#version 460
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform float time;
uniform sampler2D input0;
out vec4 fragColor;

#define fragCoord (fragCoord * resolution)

// Adapted from https://www.shadertoy.com/view/tdcczN

const float PI = 3.14159265359;
const float TWO_PI = 6.28318530718;
const float PI_OVER_TWO = 1.57079632679;

void main()
{
    vec2 uv_screen = fragCoord / resolution.xy; //The texture coordinates normalised to [0,1]
    vec2 uv = (fragCoord - resolution.xy * 0.5) / resolution.y; //Gives a square aspect ratio
    
    //PARAMETERS TO EDIT
    float time_frequency = 1.; //hz
    float spiral_frequency = 10.; //hz (# ripple peaks over vertical span)
    float displacement_amount = 0.02;
    
    //Spiral (based on polar coordinates fed through a sinusoidal function
    vec2 uv_spiral = sin(vec2(-TWO_PI * time * time_frequency + //causes change over time
                              atan(uv.x, uv.y) + //creates the spiral
                              length(uv) * spiral_frequency * TWO_PI, //creates the ripples
                              0.));

    //Displace a texture by the spiral value
    fragColor = vec4(texture(input0, uv_screen + uv_spiral * displacement_amount));
}