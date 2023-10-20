#version 460
precision highp float;

// This produces a melting or smearing effect. Every few frames, the primary shader is
// mixed into this buffer. In order to avoid large blank areas for completely overpowering
// any melting effect in progress, it's possible to specify a color key, and those areas
// will not be mixed at all. Mixing uses a perceptual color model so visually similar colors
// can be excluded within the specified tolerance (similarity is 0-1, 0 means an exact match
// to the requested color key RGB).

uniform float option_mix_frame = 4.;       // mix primary content every Nth frame
uniform float option_mix_factor = 0.15;    // how much of the primary buffer gets mixed in
uniform float option_key_tolerance = 0.05; // allowable color-key variance (perceptual similarity)
uniform float option_key_r = 0.;           // color-key red channel
uniform float option_key_g = 255.;         // color-key green channel
uniform float option_key_b = 0.;           // color-key blue channel


in vec2 fragCoord;
uniform vec2 resolution;
uniform float frame;
uniform sampler2D input0;
uniform sampler2D inputB;
out vec4 fragColor;

#define iChannel0 inputB
#define iChannel1 input0
#define iResolution resolution
#define U (fragCoord * resolution)
#define O fragColor
int iFrame = int(frame);
int mix_frame = int(option_mix_frame);

// See libraries\color_conversions.hlsl in Volt's Laboratory for links to credits
// and comments relating to the OKLab color space perceptual similarity code.

float __cube_root(float x)
{
    float y = sign(x) * uintBitsToFloat(floatBitsToUint(abs(x)) / 3u + 0x2a514067u);
    for( int i = 0; i < 4; ++i )
    {
        float y3 = y * y * y;
        y *= ( y3 + 2. * x ) / ( 2. * y3 + x );

        // Newton's method looks like this but requires more loops for the same error level
        //y = (2.0 * y + x / (y * y)) * 0.333333333;
    }
    return y;
}

// https://bottosson.github.io/posts/oklab/
// https://www.shadertoy.com/view/wts3RX (faster, slightly less accurate)
vec3 rgb2oklab(vec3 c)
{
    float l = 0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b;
	float a = 0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b;
	float b = 0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b;

    float lr = __cube_root(l); // slower real cube root: lr = pow(l, 1.0 / 3.0)
    float ar = __cube_root(a);
    float br = __cube_root(b);

    return vec3 (
        0.2104542553 * lr + 0.7936177850 * ar - 0.0040720468 * br,
        1.9779984951 * lr - 2.4285922050 * ar + 0.4505937099 * br,
        0.0259040371 * lr + 0.7827717662 * ar - 0.8086757660 * br);
}

// https://bottosson.github.io/posts/oklab/
vec3 oklab2rgb(vec3 c)
{
    // vec3 x,y,z == L,a,b,
    float lr = c.x + 0.3963377774 * c.y + 0.2158037573 * c.z;
    float ar = c.x - 0.1055613458 * c.y - 0.0638541728 * c.z;
    float br = c.x - 0.0894841775 * c.y - 1.2914855480 * c.z;

    float l = pow(lr, 3.0);
    float a = pow(ar, 3.0);
    float b = pow(br, 3.0);

    return vec3(
        +4.0767416621 * l - 3.3077115913 * a + 0.2309699292 * b,
		-1.2684380046 * l + 2.6097574011 * a - 0.3413193965 * b,
		-0.0041960863 * l - 0.7034186147 * a + 1.7076147010 * b);
}

// demo https://www.shadertoy.com/view/cdcBDs
// 0.0 = identical, 1.0 = maximally different (pure white vs pure black)
float color_difference(vec3 rgb1, vec3 rgb2)
{
    vec3 oklab1 = rgb2oklab(rgb1);
    vec3 oklab2 = rgb2oklab(rgb2);
    return distance(oklab1, oklab2);
}

#define C(x,y) textureLod(iChannel0, t*(U+float(1<<s)*vec2(x,y)),float(s))

void main()
{
    O = O-O;
    vec2 t = 1./iResolution.xy, q = U*t - .5;
    int s = 10;
    for (; s > 0; s--)
        O.xy -= 2.0 * vec2(C(0,1).x + C(0,-1).x, C(1,0).y + C(-1,0).y)
            -4.0 * C(0,0).xy + (C(1,-1) - C(1,1) - C(-1,-1) + C(-1,1)).yx;
    O = (C(O.x,O.y) + vec4(5e-4*q / (dot(q,q)+.01),0,0));
    
    // mix in some new content every few frames
	if(iFrame % mix_frame == 0)
    {
        vec4 primary_buffer = texture(iChannel1, U*t);
        vec4 effect_buffer = texture(iChannel0, U*t);
        vec3 key = rgb2oklab(vec3(option_key_r, option_key_g, option_key_b));
        vec3 pixel = rgb2oklab(primary_buffer.rgb);
        float diff = color_difference(key, pixel);
        float mix_factor = (diff - option_key_tolerance <= 0.) ? 0.0 : option_mix_factor;
        O = mix(effect_buffer, primary_buffer, mix_factor);
    }
}
