#version 460
precision highp float;

in vec2 fragCoord;
uniform float time;
uniform vec2 resolution;
uniform sampler2D inputA;  // buffer 0 from the previous frame
out vec4 fragColor;

#define fragCoord (fragCoord * resolution)
#define iResolution resolution

#define iChannel0 inputA
#define r resolution
#define t time

// Adapted from Dark Star on Shadertoy: https://www.shadertoy.com/view/DtBSWw

#define R(a) mat2(cos(a+vec4(0,11,33,0)))

vec4 Image(out vec4 O, vec2 u, sampler2D ch)
{
    vec2  p = (u+u-r) / r.y, q, n = r-r;
    float S = 6.,a=0.,i=a, d = dot(p,p), e = 2e2, s=a;
    p = p/( .7-d ) + t/3.14;
    for( O *= 0. ; i++ < e ; O += texture( ch, (u/r-.5)*i/e+.5 ) / e)
        p *= R(5.), n *= R(5.),
        a += dot( sin( q = p*S +i -abs(n)*R(t*.2) ) / S, r/r ),
        n += cos(q), 
        S *= 1.1;
    a = max( s, .9 -a*.2 -d );
    return pow( a+ a*vec4(8,4,1,0)/e , O+15. );  // was + 40.
}

void main()
{
    vec4 V;
    fragColor = Image(V, fragCoord, iChannel0);
    fragColor += V;

    // uncomment this to see what's in the backbuffer
    //vec2 uv = fragCoord / resolution;
    //fragColor = texture(iChannel0, uv);
}
