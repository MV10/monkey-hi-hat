#version 450
precision highp float;

in vec2 fragCoord;
uniform sampler2D input0;
uniform sampler2D video;
out vec4 fragColor;

// Not used here but also available:
// uniform vec2 video_resolution;
// uniform float video_duration;
// uniform float video_progress;

// For example, it would be possible to use video_progress (which is 0.0 to 1.0)
// to fade in the video at the start and fade it out at the end when the video
// loop is not a smooth transition.

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

	// mix them
	fragColor = vec4(viztexel, 0.75) + vec4(videotexel, 0.25); 
}