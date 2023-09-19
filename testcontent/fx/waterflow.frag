#version 460
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform float time;
uniform sampler2D input0;
out vec4 fragColor;

// Adapted from https://www.shadertoy.com/view/WdjyzR

vec2 random(vec2 uv)
{
    return fract(vec2(dot(uv, vec2(435.6,534.3)), dot(uv, vec2(358.463,246.3))) );
}

float noise(vec2 uv)
{
    //get decimal and integer portions from position
    vec2 i = floor(uv);
    vec2 f = fract(uv);
    
    //randomize each position around current position
    vec2 a = random(i);
    vec2 b = random(i + vec2(1.0, 0.0));
    vec2 c = random(i + vec2(0.0, 1.0));
    vec2 d = random(i + vec2(1.0, 1.0));
    
    //interpolate decimal position
    vec2 m = smoothstep(0.0,1.0,f);
    
    //apply interpolation to 4 corners and return result.
    return mix(mix(dot(a,f), dot(b, f-vec2(1.0,0.0)), m.x),
               mix(dot(c,f - vec2(0.0, 1.0)), dot(d,f - vec2(1,1)), m.x), m.y);
}


float fbm(vec2 uv)
{
    //store the final value 
    float v = 0.;
    
    //store the current amplitude for the noise texture
    float a =.9;
    
    for(int i = 0; i < 6; i++)
    {
        v += a * noise(uv);
        a *= 0.5;
        uv *= 2.0;
    }
    
    return v;
}

vec2 rip(vec2 uv)
{
    return uv + fbm(uv + time);
}

void main()
{
    // Normalized pixel coordinates (from 0 to 1)
    vec2 uv = fragCoord;
	
    uv = rip(uv * 10.0)/10.;
    // Time varying pixel color
  	vec4 col = texture(input0, uv);

    // Output to screen
    fragColor = vec4(col);
}
