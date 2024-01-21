#version 450
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform float time;
uniform float frame;
uniform sampler2D noise;
uniform sampler2D inputB;
uniform sampler2D inputC;
out vec4 fragColor;

#define fragCoord (fragCoord * resolution)
#define iChannel0 inputB
#define iChannel1 noise
#define iChannel2 inputC

// Adapted from https://www.shadertoy.com/view/Msd3W2

void main()
{
    const float _K0 = -20.0/6.0; // center weight
    const float _K1 = 4.0/6.0; // edge-neighbors
    const float _K2 = 1.0/6.0; // vertex-neighbors
    const float cs = -0.1; // curl scale
    const float ls = 0.3; // laplacian scale
    const float ps = -0.05; // laplacian of divergence scale
    const float ds = 0.05; // divergence scale
    const float is = 0.01; // image derivative scale
    const float pwr = 1.0; // power when deriving rotation angle from curl
    const float amp = 1.0; // self-amplification
    const float sq2 = 0.7; // diagonal weight

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
    
    // sobel filter
    vec3 im = texture(iChannel2, vUv).xyz;
    vec3 im_n = texture(iChannel2, vUv+n).xyz;
    vec3 im_e = texture(iChannel2, vUv+e).xyz;
    vec3 im_s = texture(iChannel2, vUv+s).xyz;
    vec3 im_w = texture(iChannel2, vUv+w).xyz;
    vec3 im_nw = texture(iChannel2, vUv+nw).xyz;
    vec3 im_sw = texture(iChannel2, vUv+sw).xyz;
    vec3 im_ne = texture(iChannel2, vUv+ne).xyz;
    vec3 im_se = texture(iChannel2, vUv+se).xyz;

    float dx = 3.0 * (length(im_e) - length(im_w)) + (length(im_ne) + length(im_se) - length(im_sw) - length(im_nw));
    float dy = 3.0 * (length(im_n) - length(im_s)) + (length(im_nw) + length(im_ne) - length(im_se) - length(im_sw));

    // vector field neighbors
    vec3 uv =    texture(iChannel0, vUv).xyz;
    vec3 uv_n =  texture(iChannel0, vUv+n).xyz;
    vec3 uv_e =  texture(iChannel0, vUv+e).xyz;
    vec3 uv_s =  texture(iChannel0, vUv+s).xyz;
    vec3 uv_w =  texture(iChannel0, vUv+w).xyz;
    vec3 uv_nw = texture(iChannel0, vUv+nw).xyz;
    vec3 uv_sw = texture(iChannel0, vUv+sw).xyz;
    vec3 uv_ne = texture(iChannel0, vUv+ne).xyz;
    vec3 uv_se = texture(iChannel0, vUv+se).xyz;
    
    // uv.x and uv.y are our x and y components, uv.z is divergence 

    // laplacian of all components
    vec3 lapl  = _K0*uv + _K1*(uv_n + uv_e + uv_w + uv_s) + _K2*(uv_nw + uv_sw + uv_ne + uv_se);
    float sp = ps * lapl.z;
    
    // calculate curl
    // vectors point clockwise about the center point
    float curl = uv_n.x - uv_s.x - uv_e.y + uv_w.y + sq2 * (uv_nw.x + uv_nw.y + uv_ne.x - uv_ne.y + uv_sw.y - uv_sw.x - uv_se.y - uv_se.x);
    
    // compute angle of rotation from curl
    float sc = cs * sign(curl) * pow(abs(curl), pwr);
    
    // calculate divergence
    // vectors point inwards towards the center point
    float div  = uv_s.y - uv_n.y - uv_e.x + uv_w.x + sq2 * (uv_nw.x - uv_nw.y - uv_ne.x - uv_ne.y + uv_sw.x + uv_sw.y + uv_se.y - uv_se.x);
    float sd = ds * div;

    vec2 norm = normalize(uv.xy);
    
    // temp values for the update rule
    float ta = amp * uv.x + ls * lapl.x + norm.x * sp + uv.x * sd + is * dx;
    float tb = amp * uv.y + ls * lapl.y + norm.y * sp + uv.y * sd + is * dy;

    // rotate
    float a = ta * cos(sc) - tb * sin(sc);
    float b = ta * sin(sc) + tb * cos(sc);
    
    // initialize with noise
    if(frame<10) {
        fragColor = -0.5 + texture(iChannel1, fragCoord.xy / resolution.xy);
    } else {
        fragColor = clamp(vec4(a,b,div,1), -1., 1.);
    }
}