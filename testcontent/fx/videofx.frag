#version 450
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform sampler2D input0;
uniform sampler2D video;
out vec4 fragColor;

#define fragCoord (fragCoord * resolution)

#define rlum 0.299
#define glum 0.587
#define blum 0.114

void main()
{
	// get the texels
	vec3 viztexel = texture(input0, fragCoord).rgb;
	vec3 videotexel = texture(video, fragCoord).rgb;

	// make the video grayscale
	//videotexel *= vec3(rlum, glum, blum);

	// adjust the luminance of the viz texel color
	//viztexel = (viztexel * 0.75);

	fragColor = (fragCoord.x < 0.5) ? vec4(viztexel, 1.0) : vec4(videotexel, 1.0);
}