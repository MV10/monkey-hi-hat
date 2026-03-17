
// Source:
// http://stackoverflow.com/questions/9320953/what-algorithm-does-photoshop-use-to-desaturate-an-image

// Generic algorithm to desaturate images used in most game engines
vec4 generic_desaturate(vec3 color, float factor)
{
	vec3 lum = vec3(0.299, 0.587, 0.114);
	vec3 gray = vec3(dot(lum, color));
	return vec4(mix(color, gray, factor), 1.0);
}

// Algorithm employed by photoshop to desaturate the input
vec4 photoshop_desaturate(vec3 color)
{
    float bw = (min(color.r, min(color.g, color.b)) + max(color.r, max(color.g, color.b))) * 0.5;
    return vec4(bw, bw, bw, 1.0);
}
