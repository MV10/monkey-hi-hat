#version 450
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform float time;
uniform sampler2D video;
out vec4 fragColor;

// Not used here but also available:
// uniform vec2 video_resolution;
// uniform float video_duration;
// uniform float video_progress;

void main()
{
	vec2 uv = fragCoord;

    // a water effect
    //vec2 warp = texture( iChannel0, uv*0.1 + iTime*vec2(0.04,0.03) ).xz;

    float freq = 3.0 * sin(0.5 * time);
    vec2 warp = 0.5000 * cos(uv.xy * 1.0 * freq + vec2(0.0, 1.0) + time) +
                0.2500 * cos(uv.yx * 2.3 * freq + vec2(1.0, 2.0) + time) +
                0.1250 * cos(uv.xy * 4.1 * freq + vec2(5.0, 3.0) + time) +
                0.0625 * cos(uv.yx * 7.9 * freq + vec2(3.0, 4.0) + time);
    
	vec2 st = uv + warp * 0.5;

	fragColor = vec4(texture(video, st).xyz, 1.0);
}
