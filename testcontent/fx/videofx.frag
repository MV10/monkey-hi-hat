#version 450
precision highp float;

in vec2 fragCoord;
uniform sampler2D input0;
uniform sampler2D video;
uniform float video_progress;
out vec4 fragColor;

// Not used here but also available:
// uniform vec2 video_resolution;
// uniform float video_duration;

// For example, it would be possible to use video_progress (which is 0.0 to 1.0)
// to fade in the video at the start and fade it out at the end when the video
// loop is not a smooth transition.

// perceptual luminosity (more or less)
#define rlum 0.299
#define glum 0.587
#define blum 0.114

void main()
{
	// get the texels
	vec3 viztexel = texture(input0, fragCoord).rgb;
	vec3 videotexel = texture(video, fragCoord).rgb;

	// make the video grayscale
	videotexel *= vec3(rlum, glum, blum);

	// since most videos don't loop seamlessly, this ramps a fade multiplier up over the
    // first 10th of the progress, and ramps it back to zero over the final 10th
    videotexel *= clamp(10.0 * video_progress, 0.0, 1.0) * clamp(10.0 * (1.0 - video_progress), 0.0, 1.0);

	// mix them
	fragColor = vec4(viztexel, 0.70) + vec4(videotexel, 0.30); 
}
