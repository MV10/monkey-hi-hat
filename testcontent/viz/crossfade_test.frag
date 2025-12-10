#version 450
precision highp float;

// Adapted from https://www.shadertoy.com/view/lsBGzh
// My revisions https://www.shadertoy.com/view/XcXSzf

in vec2 fragCoord;
uniform sampler2D oldBuffer;
uniform sampler2D newBuffer;
out vec4 fragColor;

uniform float fadeDuration;
uniform float time;
uniform vec2 resolution;
#define uv fragCoord

float dripLine(vec2 pp, out bool useNewBuffer, float t)
{
	pp.y += (
		 0.4 * sin(0.5 *  2.3 * pp.x + pp.y) +
		 0.2 * sin(0.5 *  5.5 * pp.x + pp.y) +
		 0.1 * sin(0.5 * 13.7 * pp.x) +
		0.06 * sin(0.5 * 23.0 * pp.x));
	
	pp += vec2(0.0, 0.4) * t;
	
	float threshold = 5.3;
	useNewBuffer = pp.y > threshold;
	float d = abs(pp.y - threshold);
	return d;
}

vec3 scene(in vec2 pp, in vec2 uv)
{
    // The untouched run-time is about 13.25 sec, so scale the current time
    // according to the target duration, then add 1.7 sec to skip the initial
    // delay in visibility.
    float t = (13.25 / fadeDuration) * time + 1.7;
	
	bool useNewBuffer;
	float d = dripLine(pp, useNewBuffer, t);
	
	if(!useNewBuffer)
	{
        vec3 txo = texture(oldBuffer, uv).rgb;

        // ao shading along drip-line (stronger than in the original code)
		float ao = clamp(smoothstep(0.0, 0.2, d), 0.0, 1.0);

        return mix(1.0, sqrt(ao), 0.75) * txo;
    }
	else
	{
        vec3 txn = texture(newBuffer, uv).rgb;
        
        // fake a height to ad a highlight along leading right edge
        float h = clamp(smoothstep(0.0, 0.25, d), 0.0, 1.0);
		h = 4.0 * pow(h, 0.2);

		// direction of the highlight
        vec3 N = normalize(vec3(-dFdx(h), 1.0, -dFdy(h)));
        
        // distance above the edge
		vec3 L = normalize(vec3(0.5, 0.7, -0.5));
        
        // 2.5 exaggerates the highlight (bad for video, good for music viz FX)
        return txn + pow(dot(N, L), 15.0) * vec3(2.5);
	}
}

void main()
{
    vec2 pp = vec2(uv.x / (resolution.y / resolution.x), uv.y) * 4.0;
	fragColor = vec4(scene(pp, uv), 1.0);
}
