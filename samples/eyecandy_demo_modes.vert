#version 320 es

layout (location = 0) in float vertexId;
uniform vec2 resolution;
uniform float vertexCount;
uniform float time;
out vec4 v_color;

// As seen here:
// https://www.vertexshaderart.com/art/W9WTPxA9AXtFxZRqq

#define PI 3.14159
#define NUM_SEGMENTS 21.0
#define NUM_POINTS (NUM_SEGMENTS * 2.0)
#define STEP 5.0

// lolengine.net/blog/2013/07/27/rgb-to-hsv-in-glsl
vec3 hsv2rgb(vec3 c)
{
    c = vec3(c.x, clamp(c.yz, 0.0, 1.0));
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void main() {  
  float traces = 5.0;
  float trace = mod(vertexId, traces);
  float x = -1.0 + 2.0 * vertexId / vertexCount;
  
  float speed = 1.0 * time;
  float amp = sin(time) * (1.0 + trace) / traces;
  float y = amp * sin(speed + PI * x);
  
  gl_Position = vec4(x, y, 0, 1);
  float c = trace / traces;
  v_color = vec4(hsv2rgb(vec3(x, 0.5, 1)), 1);

  gl_PointSize = 3.0;
}