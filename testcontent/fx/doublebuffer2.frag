#version 460
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform sampler2D inputB;
out vec4 fragColor;

#define fragCoord (fragCoord * resolution)

// Adapted from https://www.shadertoy.com/view/MdlBDn

void main()
{
	vec2 uv = fragCoord.xy / resolution.xy;
	fragColor = texture(inputB, uv);
}


/*
// Adapted from https://www.shadertoy.com/view/XtGBzy

void main()
{
    vec2 q = fragCoord.xy / resolution.xy;

    vec3 e = vec3(vec2(1.) / resolution.xy, 0.);
    float f = 10.0;
    float p10 = texture(inputB, q - e.zy).z;
    float p01 = texture(inputB, q - e.xz).z;
    float p21 = texture(inputB, q + e.xz).z;
    float p12 = texture(inputB, q + e.zy).z;
    
    vec4 w = texture(inputB, q);
    
    // Totally fake displacement and shading:
    vec3 grad = normalize(vec3(p21 - p01, p12 - p10, 0.5));

    //vec2 uv = fragCoord.xy*2./iChannelResolution[1].xy + grad.xy*.35;
    // in mhh terms, this would be the resolution of the primary shader...
    vec2 uv = fragCoord.xy*2. / resolution.xy + grad.xy * .35;

    uv = uv * 0.5;
    vec4 c = texture(input0, uv);
    c += c * 0.5;
    c += c * w * (0.5 - distance(q, vec2(0.5)));
    vec3 lightDir = vec3(0.2, -0.5, 0.7);
    vec3 light = normalize(lightDir);
    
    float diffuse = dot(grad, light);
    float spec = pow(max(0., -reflect(light, grad).z), 32.);
    fragColor = mix(c, vec4(.7, .8, 1., 1.), .25) * max(diffuse, 0.) + spec;
}
*/
