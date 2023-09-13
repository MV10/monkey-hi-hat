#version 460
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform sampler2D input1; // pass2-desaturate
out vec4 fragColor;

// Derived from: https://www.shadertoy.com/view/wtVGzh

#define fragCoord (fragCoord * resolution)
#define iResolution resolution
#define iChannel0 input1

void main()
{
        
    //vec2 src_size = iChannelResolution[0].xy;
    vec2 src_size = resolution;

    vec2 uv = fragCoord / src_size;
    vec2 d = 1.0 / src_size;

    vec3 c = texture(input1, uv).xyz;
    vec3 u = (
           -1.0 * texture(iChannel0, uv + vec2(-d.x, -d.y)).xyz +
           -2.0 * texture(iChannel0, uv + vec2(-d.x,  0.0)).xyz + 
           -1.0 * texture(iChannel0, uv + vec2(-d.x,  d.y)).xyz +
           +1.0 * texture(iChannel0, uv + vec2( d.x, -d.y)).xyz +
           +2.0 * texture(iChannel0, uv + vec2( d.x,  0.0)).xyz + 
           +1.0 * texture(iChannel0, uv + vec2( d.x,  d.y)).xyz
           ) / 4.0;

    vec3 v = (
           -1.0 * texture(iChannel0, uv + vec2(-d.x, -d.y)).xyz + 
           -2.0 * texture(iChannel0, uv + vec2( 0.0, -d.y)).xyz + 
           -1.0 * texture(iChannel0, uv + vec2( d.x, -d.y)).xyz +
           +1.0 * texture(iChannel0, uv + vec2(-d.x,  d.y)).xyz +
           +2.0 * texture(iChannel0, uv + vec2( 0.0,  d.y)).xyz + 
           +1.0 * texture(iChannel0, uv + vec2( d.x,  d.y)).xyz
           ) / 4.0;

    fragColor = vec4(vec3(sqrt(dot(u, u) + dot(v, v))) * 3.0, 1.0);   
}
			