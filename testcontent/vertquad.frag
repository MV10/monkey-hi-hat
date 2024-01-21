#version 450
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform float time;
uniform sampler2D eyecandyShadertoy;
out vec4 fragColor;

// These empty function declarations tell the compiler
// the functions are defined in external shader files
vec3 rgb2hsv(vec3 c);
vec3 hsv2rgb(vec3 c);

void main()
{
 	vec2 uv = fragCoord - 0.5;
    uv.y *= (resolution.y / resolution.x);

    float beat = texture(eyecandyShadertoy, vec2(0.1,0.25)).g * 0.5;
    
    vec2 rd = vec2(atan(uv.x,uv.y), length(uv));
    
    rd = vec2(rd.x+time*0.0, rd.y*(1.4-beat));
    
    float ljud = texture(eyecandyShadertoy, vec2(rd.y*0.75, 0.25)).g * 0.5;
  
    vec2 xygrid = 1.0-clamp(abs(sin(vec2(sin(rd.x),cos(rd.x))*rd.y*3.14159*10.0))*4.0, 0.0,1.0);
    vec2 rdgrid = 1.0-clamp(abs(sin(rd*vec2(0.5,1.0)*16.0))*16.0*min(1.0,rd.y), 0.0,1.0);
    
	fragColor.xyz = vec3(1.0-step(ljud, abs(cos(rd.x)*rd.y)))*ljud*ljud*8.0;

	fragColor.x += rdgrid.x+rdgrid.y;
    fragColor.y += xygrid.x+xygrid.y;
    
    vec2 srd = vec2(ivec2(sin(rd.x*(1.0+float(int(mod(time*3.0,4.0)))))*3.14159*1.0+time*4.0));
    vec3 rgb = vec3(sin(srd.x),cos(srd.x),-sin(srd.x));

    // This does nothing except ensure the library functions are used. While the compiler
    // might be sophisticated enough to optimize these away, it's still enough to guarantee
    // they get linked, which serves the purpose in this test shader.
    vec3 hsv = rgb2hsv(rgb);
    fragColor.xyz += hsv2rgb(hsv);

    fragColor.a = 0.0;
}
