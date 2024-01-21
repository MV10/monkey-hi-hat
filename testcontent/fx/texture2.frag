#version 450
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform float time;
uniform float frame;
uniform sampler2D input0;
uniform sampler2D inputB;
uniform sampler2D inputC;
out vec4 fragColor;

#define fragCoord (fragCoord * resolution)
#define iChannel0 input0
#define iChannel1 inputC
#define iChannel2 inputB

// Adapted from https://www.shadertoy.com/view/Msd3W2

void main()
{
    const float ds = 1.5; //0.4; // diffusion rate
    const float darken = 0.01; // darkening
    const float D1 = 0.2;  // edge neighbors
    const float D2 = 0.05; // vertex neighbors
    
    vec2 vUv = fragCoord.xy / resolution.xy;
    vec2 texel = 1. / resolution.xy;
    
    // 3x3 neighborhood coordinates
    float step_x = texel.x;
    float step_y = texel.y;
    vec2 n  = vec2(0.0, step_y);
    vec2 ne = vec2(step_x, step_y);
    vec2 e  = vec2(step_x, 0.0);
    vec2 se = vec2(step_x, -step_y);
    vec2 s  = vec2(0.0, -step_y);
    vec2 sw = vec2(-step_x, -step_y);
    vec2 w  = vec2(-step_x, 0.0);
    vec2 nw = vec2(-step_x, step_y);
    
    vec3 components = texture(iChannel2, vUv).xyz;
    
    float a = components.x;
    float b = components.y;
    
    vec3 im =    texture(iChannel1, vUv).xyz;
    vec3 im_n =  texture(iChannel1, vUv+n).xyz;
    vec3 im_e =  texture(iChannel1, vUv+e).xyz;
    vec3 im_s =  texture(iChannel1, vUv+s).xyz;
    vec3 im_w =  texture(iChannel1, vUv+w).xyz;
    vec3 im_nw = texture(iChannel1, vUv+nw).xyz;
    vec3 im_sw = texture(iChannel1, vUv+sw).xyz;
    vec3 im_ne = texture(iChannel1, vUv+ne).xyz;
    vec3 im_se = texture(iChannel1, vUv+se).xyz;

    float D1_e = D1 * a;
    float D1_w = D1 * -a;
    float D1_n = D1 * b;
    float D1_s = D1 * -b;
    float D2_ne = D2 * (b + a);
    float D2_nw = D2 * (b - a);
    float D2_se = D2 * (a - b);
    float D2_sw = D2 * (- a - b);

    vec3 diffusion_im = -darken * length(vec2(a, b)) * im + im_n*D1_n + im_ne*D2_ne + im_e*D1_e + im_se*D2_se + im_s*D1_s + im_sw*D2_sw + im_w*D1_w + im_nw*D2_nw;

    // initialize with image
    if(frame<10) {
        fragColor = texture(iChannel0, vUv);
    } else {
        fragColor = vec4(clamp(im + ds * diffusion_im, 0.0, 1.0), 0.0);
    }
}
