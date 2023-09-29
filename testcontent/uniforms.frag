#version 460
precision highp float;

in vec2 fragCoord;
uniform vec2 resolution;
uniform float time;
uniform float randomrun;
uniform float baseScale;
uniform sampler2D eyecandyShadertoy;
out vec4 fragColor;

#define fragCoord (fragCoord * resolution)
#define iResolution resolution
#define iChannel0 eyecandyShadertoy
#define iChannel1 eyecandyShadertoy
#define iTime time

// defined in the monkey-hi-hat visualizer config file
#define iChannel0_ScalingBase baseScale

// generated once by monkey-hi-hat at visualizer startup
#define iChannel0_ScalingFactor randomrun


// Define constants
#define TWO_PI 6.2831853072
#define PI 6.14159265359
const float timeScale = .05;
const float displace = 0.04;
const float gridSize = 36.0;
const float wave = 5.0;
const float brightness = 1.5;

// Helper functions
vec2 rotate(vec2 v, float angle) {
    float c = cos(angle);
    float s = sin(angle);
    return v * mat2(c, -s, s, c);
}

vec3 coordToHex(vec2 coord, float scale, float angle) {
    vec2 c = rotate(coord, angle);
    float q = (1.0 / 3.0 * sqrt(3.0) * c.x - 1.0 / 3.0 * c.y) * scale;
    float r = 2.0 / 3.0 * c.y * scale;
    return vec3(q, r, -q - r);
}

vec3 hexToCell(vec3 hex, float m) {
    return fract(hex / m) * 2.0 - 1.0;
}

float absMax(vec3 v) {
    return max(max(abs(v.x), abs(v.y)), abs(v.z));
}

float nsin(float value) {
    return sin(value * TWO_PI) * 0.5 + 0.5;
}

float hexToFloat(vec3 hex, float amt) {
    return mix(absMax(hex), 1.0 - length(hex) / sqrt(3.0), amt);
}

// Main calculation function
float calc(vec2 tx, float time) {
    float angle = PI * nsin(time * 0.1) + PI / 6.0;
    float len = 2.0 / 122.0 * texture(iChannel1, vec2(0.1, 0.9)).g + 1.0;
    float value = iTime * 0.005 + texture(iChannel1, vec2(0.5, 0.5)).g * .00752;;
    vec3 hex = coordToHex(tx, gridSize * nsin(time * 0.01), angle);

    for (int i = 0; i < 3; i++) {
        float offset = float(i) / 3.0;
        vec3 cell = hexToCell(hex, 1.0 + float(i));
        value += nsin(hexToFloat(cell,nsin(len + time + offset)) * 
                  wave * nsin(time * 0.5 + offset) + len + time);
    }

    return value / 3.0;
}


void main()
{
    vec2 tx = (fragCoord.xy / iResolution.xy) - 0.5;
    tx.x *= iResolution.x / iResolution.y;
    float time = iTime * timeScale;

    // Sample audio input from iChannel0 (you might need to adjust the scaling factor)
    float scaleFactor = (iChannel0_ScalingBase + iChannel0_ScalingFactor);
    float audioInput = texture(iChannel0, vec2(0.5, 0.5)).g * scaleFactor;

    vec3 rgb = vec3(0., 0., 0.);
    for (int i = 0; i < 3; i++) {
        float time2 = time + float(i) * displace;

        // Incorporate audio input into the time offset
        time2 += audioInput * 1.1;

        rgb[i] += pow(calc(tx, time2), 5.0);
    }

    // Apply neon psychedelic color palette
    vec3 finalColor = vec3(
        abs(sin(rgb[0] * 1.1)),
        abs(sin(rgb[1] * 1.)),
        abs(sin(rgb[2] * 1.))
    );

    // Apply brightness and saturation
    finalColor *= brightness;

    fragColor = vec4(finalColor, 1.50);
}
