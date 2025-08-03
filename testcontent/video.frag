#version 450
precision highp float;

in vec2 fragCoord;
uniform sampler2D video;
out vec4 fragColor;

// Not used here but also available:
// uniform vec2 video_resolution;
// uniform float video_duration;
// uniform float video_progress;

void main()
{
    vec3 texel = texture(video, fragCoord).rgb;
    fragColor = vec4(texel, 1.0);
}
