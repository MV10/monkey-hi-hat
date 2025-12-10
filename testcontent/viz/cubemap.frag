#version 450
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform float time;
uniform samplerCube imageA;
out vec4 fragColor;

// these I just threw together over in http://shadertoy.com/view/wtVSDw
// but they seem to work, or at least seem to be inverses of each other.
int CubeFaceOfDir(vec3 d) // just the face id
{
    vec3 a = abs(d);
    int f = a.x >= a.y ? (a.x >= a.z ? 0 : 2) : (a.y >= a.z ? 1 : 2);
    int i = f + f;
    if (d[f] < 0.) ++i;
    return i;
}

// takes normalized direction vector, returns uv in c.xy and face id in c.z
vec3 DirToCubeUVFace(vec3 d)
{
    int i = CubeFaceOfDir(d),
        f = i >> 1;
    vec2 uv;
    switch (f) {
        case 0: uv = d.yz; break;
        case 1: uv = d.xz; break;
        case 2: uv = d.xy; break;
    }
    uv /= abs(d[f]); // project
    if ((i&1) != 0) // negative faces are odd indices
        uv.x = -uv.x; // flip u
    return vec3(uv, float(i));
}

// takes uv in c.xy and face id in c.z, returns unnormalized direction vector
vec3 CubeUVFaceToDir(vec3 c)
{
    int i = int(c.z); 
    vec3 d = vec3(c.xy, 1. - 2. * float(i & 1));
    d.x *= d.z; // only unflip u 
    switch (i >> 1) { // f
        case 0: d = d.zxy; break;
        case 1: d = d.xzy; break;
        case 2: d = d.xyz; break;
    }
    return d; // needs normalized probably but texture() doesn't mind.
}

ivec3 DirToCubeTexelFace(vec3 p)
{
    return ivec3(DirToCubeUVFace(p) * vec3(512,512,1));
}

// function to work with individual texels; takes texel index in xy and face id in z,
// returns unnormalized direction vector to center of texel in cubemap
// rory618 http://shadertoy.com/view/wdsBRn
vec3 CubeTexelFaceToDir(ivec3 p) // NOTE different face index order!!!  am I sure this is a match for DirToCubeTexelFace?!
{
    vec2 q = vec2(p - 512) + .5; vec3 r;
    switch (p.z) {
        case 0: r = vec3( 512,-q.y,-q.x); break;
        case 1: r = vec3( q.x, 512, q.y); break;
        case 2: r = vec3( q.x,-q.y, 512); break;
        case 3: r = vec3(-512,-q.y, q.x); break;
        case 4: r = vec3( q.x,-512,-q.y); break;
        case 5: r = vec3(-q.x,-q.y,-512); break;
    }
    return r;
} // apparently this part wasn't tested well enough.

// developed here on jt's toy:  http://shadertoy.com/view/WccXRM
vec3 volume_to_direction(ivec3 v) // aka CubeTexelFaceToDir
{
    float s = 1. - 2. * float(v.z & 1);
    vec3 d = vec3(vec2(v) - 511.5, s * 512.);
    return v.z < 2 ? d.zyx * vec3(1,-1, s)
         : v.z < 4 ? d.xzy * vec3(1, 1,-s)
         :           d.xyz * vec3(s,-1, 1);
}
// now I need the matching opposite conversion!
// but taking mainCubemap's arguments, volume is just ivec3(fragCoord, CubeFaceOfDir(rd))
// so don't tend to need to actually convert from dir back to index

// just for debugging so probably broken and imprecise.
// in fact it's a big ol' kludge atm.  what a mess!  I'll try to improve it as I get time.
vec4 Unwrap(samplerCube ch, vec2 q)
{
    vec2 uv = q * .5 + .5;
    uv *= 4.;
    uv -= vec2(.0,.5);
    int i = -1;
    if (uv.y >= 1. && uv.y < 2.) {
        int f = int(floor(uv.x));
        if (f >= 0 && f < 2) i = 3*f + 1;
     	else if (f >= 2 && f < 4) i = 5*f - 10;
        if (f == 2) uv = vec2(uv.y, -uv.x); // maybe rotate, different directions
        else if (f == 0) uv = vec2(-uv.y, uv.x);
    } else {
		if (int(uv.x) == 1) {
        	if (uv.y >= 0. && uv.y < 1.) { i = 3; uv.x = 0.-uv.x; }
        	else if (uv.y >= 2. && uv.y < 3.) { i = 2; uv.y = 0.-uv.y; }
    	}
    }
	if (!(i >= 0)) return vec4(vec3(.7),1);
    uv = fract(uv);
    vec3 d = CubeUVFaceToDir(vec3(uv * 2. - 1., float(i)));
//    d = CubeUVFaceToDir(DirToCubeUVFace(d)); // ensure can convert back&forth flawlessly
//    d = CubeUVFaceToDir(DirToCubeUVFace(d));
    vec4 c = textureLod(ch, d, 0.);
    //c.rgb = pow(c.rgb, vec3(2.2)); // gamma correction - skipping as it cancels out here
    return c;
} // result in srgb gamma atm

void main()
{
    vec2 p = fragCoord * resolution; // frickin shadertoy man
    vec2 R = resolution.xy;
    vec2 q = (p + p - R) / R.y;
    fragColor.rgb = Unwrap(imageA, q * .755).rgb;
    fragColor.a = 1.;
}
