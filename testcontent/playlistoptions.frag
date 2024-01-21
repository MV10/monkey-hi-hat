#version 450
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform float time;
uniform sampler2D eyecandyShadertoy;
out vec4 fragColor;

#define fragCoord (fragCoord * resolution)
#define iResolution resolution

#define T time
#define PI 3.141592
#define FAR 100.0
#define EPS 0.005

// meant as a GLSL compiler hack to avoid constants... but
// this one uses constants anyway, so it's a bit silly
//#define ZERO (min(iFrame,0))
#define ZERO 0

#define ROOF 1.0
#define FLOOR 2.0
#define PILLAR 3.0
#define PILLAR_LIGHT 4.0
#define SPHERE 5.0

#define PARTITION_SIZE 30.0

#define CA vec3(0.5, 0.5, 0.5)
#define CB vec3(0.5, 0.5, 0.5)
#define CC vec3(1.0, 1.0, 1.0)
#define CD vec3(0.0, 0.33, 0.67)

struct Scene {
    float t; //distance to surface
    float id; //id of surface
    vec3 n; //surface normal
    float li; //light
    float em; //emissive
    float ref; //reflection
};

float rand(vec2 p) {return fract(sin(dot(p, vec2(12.9898,78.233))) * 43758.5453);}
mat2 rot(float x) {return mat2(cos(x), sin(x), -sin(x), cos(x));}
vec3 camPos() {return vec3(0.0, 0.0, T * 4.0);}
float sphereMotion(float z) {return sin(z * 0.1);}
//IQ cosine palattes
//https://iquilezles.org/articles/palettes
vec3 palette(float t, vec3 a, vec3 b, vec3 c, vec3 d) {return a + b * cos(6.28318 * (c * t + d));}
vec3 glowColour() {return palette(T * 0.1, CA, CB, CC, CD);}

//IQs noise
float noise(vec3 rp) {
    vec3 ip = floor(rp);
    rp -= ip; 
    vec3 s = vec3(7, 157, 113);
    vec4 h = vec4(0.0, s.yz, s.y + s.z) + dot(ip, s);
    rp = rp * rp * (3.0 - 2.0 * rp); 
    h = mix(fract(sin(h) * 43758.5), fract(sin(h + s.x) * 43758.5), rp.x);
    h.xy = mix(h.xz, h.yw, rp.y);
    return mix(h.x, h.y, rp.z); 
}


//IQs distance functions
float sphIntersect(vec3 ro, vec3 rd, vec4 sph) {
    vec3 oc = ro - sph.xyz;
    float b = dot(oc, rd);
    float c = dot(oc, oc) - sph.w * sph.w;
    float h = b * b - c;
    if (h < 0.0) return 0.0;
    h = sqrt(h);
    return -b - h;
}

float sdCapsule(vec3 p, vec3 a, vec3 b, float r) {
    vec3 pa = p - a, ba = b - a;
    float h = clamp(dot(pa,ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h) - r;
}

float sdBox(vec3 p, vec3 b) {
    vec3 d = abs(p) - b;
    return min(max(d.x, max(d.y, d.z)), 0.0) + length(max(d, 0.0));
}

//neat trick from Shane
vec2 nearest(vec2 a, vec2 b){ 
    float s = step(a.x, b.x);
    return s * a + (1. - s) * b;
}
vec4 nearest(vec4 a, vec4 b) {
   float s = step(a.w, b.w);
   return s * a + (1. - s) * b;       
}

//returns sphere center and distance
vec4 nearestSphere(vec3 rp) {
    
    vec4 near = vec4(vec3(0.0), FAR);

    float z = camPos().z; 
    z = z - mod(z, PARTITION_SIZE) - mod(T * 10.0, PARTITION_SIZE); //start partitioning behind camera
    
    for (int i=ZERO; i<5; i++) {
    	vec3 c = vec3(sphereMotion(z), -1.6, z);
        near = nearest(near, vec4(c, length(rp - c) - 0.8));
        z += PARTITION_SIZE;
    }
    
    return near;
}

vec3 map(vec3 p) {
     
    float sphere = nearestSphere(p).w;   
    
    p.xz = mod(p.xz,12.0) - 6.0;
    p = abs(p);
    
    float box = sdBox(p, vec3(2.9, 1.4, 2.9));
    float light = min(sdCapsule(p,vec3(3.0,1.5,3.0),vec3(0.0, 1.5,3.0),0.05),
                      sdCapsule(p,vec3(3.0,1.5,3.0),vec3(3.0, 1.5,0.0),0.05));
    light = min(light,sdCapsule(p,vec3(3.0,1.5,3.0),vec3(3.0,-1.5,3.0),0.05));

    vec2 near = nearest(vec2(box,PILLAR), vec2(light,PILLAR_LIGHT));
    near = nearest(near, vec2(sphere,SPHERE));
    
    return vec3(near,light);
}

vec3 normal(vec3 p) 
{  
    vec4 n = vec4(0.0);
    for (int i=ZERO; i<4; i++) 
    {
        vec4 s = vec4(p, 0.0);
        s[i] += EPS;
        n[i] = map(s.xyz).x;
    }
    return normalize(n.xyz-n.w);
}

vec3 bump(vec3 rp, vec3 n) {
    vec2 e = vec2(EPS, 0.0);
    float nz = noise(rp);
    vec3 d = vec3(noise(rp + e.xyy) - nz, noise(rp + e.yxy) - nz, noise(rp + e.yyx) - nz) / e.x;
    n = normalize(n - d * 0.2 / sqrt(0.1));
    return n;
}

float spherePattern(vec3 rp, vec3 bc) {
    rp -= bc;
    rp.xy *= rot(0.5 * sphereMotion(bc.z));
    rp.xz *= rot(0.5 * sphereMotion(bc.z + PI * PARTITION_SIZE));
    rp.yz *= rot(-T * 12.0);
    rp.xz = abs(rp.xz);  
    float pattern = step(0.4, rp.x) * step(rp.x, 0.6);
    pattern *= step(0.3, rp.z);
    return pattern;
}
   
vec3 march(vec3 ro, vec3 rd) {
 
    float t = 0.0;
    float id = 0.0;
    float li = 0.0;
    
    for (int i=ZERO; i<98; i++) {
        vec3 rp = ro + rd * t;
        vec3 ns = map(rp);
        if (abs(ns.x)<EPS) {
            id = ns.y;
            break;
        }
        
        li += 0.1/(1.0 + ns.z*ns.z*100.0);
        
        vec4 nearSphere = nearestSphere(rp);
        vec3 srd = normalize(nearSphere.xyz - rp);
        float st = sphIntersect(rp, srd, vec4(nearSphere.xyz, 0.8));
        vec3 srp = rp + srd * st;

        li += spherePattern(srp, nearSphere.xyz) * (0.1/(1.0 + st*st*6.0));
       
        t += ns.x * 0.8;
        if (t>FAR) break;
        
    }
    
    return vec3(t, id, li);
}

Scene drawScene(vec3 ro, vec3 rd) {
    
	float mint = FAR;
    vec3 minn = vec3(0.0);
    float id = 0.0;
    
    vec3 fo = vec3(0.0, -2.4, 0.0);
    vec3 fn = vec3(0.0, 1.0, 0.0);
    vec3 co = vec3(0.0, 2.4, 0.0);
    vec3 cn = vec3(0.0, -1.0, 0.0);
    
    float ft = dot(fo - ro,fn)/dot(rd,fn);
    float ct = dot(co - ro,cn)/dot(rd,cn);

    if (ft > 0.0 && ft < FAR) {
        mint = ft;
        minn = fn;
        id = FLOOR;
    }
    
    if (ct > 0.0 && ct < mint) {
        mint = ct;
        minn = cn;
        id = ROOF;
    }
    
    vec3 st = march(ro, rd);
    if (st.x > 0.0 && st.x < mint) {
        mint = st.x;
        minn = normal(ro + rd * st.x);
        id = st.y;
    }
    
    return Scene(mint, id, minn, st.z, 0.0, 0.0);
}

void surfaceDetail(vec3 ro, vec3 rd, inout Scene scene) {
 
    //ray surface intersection
    vec3 rp = ro + rd * scene.t;
    
    if (scene.id == ROOF) {        
        scene.n = bump((rp + T * 0.4) * 4.0, scene.n);
    }
    
    if (scene.id == SPHERE) {
        vec4 ns = nearestSphere(rp);
        if (spherePattern(rp, ns.xyz) > 0.0) {
            //light
            scene.em = 1.0;
        }        
    }
    
    if (scene.id == PILLAR_LIGHT) {   
        scene.em = 1.0;
    }
    
    if (scene.id == FLOOR || scene.id == ROOF || scene.id == PILLAR) {
        scene.ref = 0.003;    
    }
}

void setupCamera(vec2 uv, inout vec3 ro, inout vec3 rd) {

    ro = camPos();
    vec3 lookAt = ro + vec3(0.0, 0.0 , 6.0);
    
    float FOV = PI / 4.0;
    vec3 forward = normalize(lookAt - ro);
    vec3 right = normalize(vec3(forward.z, 0.0, -forward.x)); 
    vec3 up = cross(forward, right);

    rd = normalize(forward + FOV * uv.x * right + FOV * uv.y * up);
}

void main() {

    vec3 pc = vec3(0.0);
	vec3 gc = glowColour();
    
    vec2 uv = (fragCoord.xy - iResolution.xy * 0.5) / iResolution.y;
	vec3 ro, rd, roo, rdo;
    setupCamera(uv, ro, rd);

    roo = ro;
    rdo = rd;
    float to = 0.0;
    float tt = 0.0;
    float la = 0.0;
    float ref = 0.0;
    
    for (int i=ZERO; i<3; i++) {
        
        Scene scene = drawScene(ro, rd);

        tt += scene.t;
        
        if (i == 0) to = scene.t;
        
        if (scene.id == 0.0) break;

        la += scene.li; 
        
        surfaceDetail(ro, rd, scene);   
        
        if (scene.em == 1.0) {
            pc = vec3(1.) / (1. + tt * tt * ref); //light
            break;
        }
        
        //setup for next loop
        ref += scene.ref;
        ro = ro + rd * (scene.t - EPS); //pull back from surface
        rd = reflect(rd, scene.n); //reflect ray direction   
    }
    
    pc += gc * la;
    
    gc = mix(gc, gc.xzy, 0.25 - rdo.y * 0.25);
    pc = mix(gc, pc, 1.0 / (to * to / FAR + 1.0));
    
    fragColor = vec4(sqrt(clamp(pc * 1.0, 0.0, 1.0)), 1.0);
}