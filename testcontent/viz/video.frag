#version 450
precision highp float;

in vec2 fragCoord;
uniform sampler2D video;
uniform float video_progress;
out vec4 fragColor;

// Not used here but also available:
// uniform vec2 video_resolution;
// uniform float video_duration;

void main()
{
    vec3 texel = texture(video, fragCoord).rgb;

    // since most videos don't loop seamlessly, this ramps a fade multiplier up over the
    // first 10th of the progress, and ramps it back to zero over the final 10th
    texel *= clamp(10.0 * video_progress, 0.0, 1.0) * clamp(10.0 * (1.0 - video_progress), 0.0, 1.0);

    fragColor = vec4(texel, 1.0);
}
