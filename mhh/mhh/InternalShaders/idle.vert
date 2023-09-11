#version 460

layout (location = 0) in float vertexId;
uniform vec2 resolution;
uniform float vertexCount;
uniform float time;
out vec4 v_color;

// Adapted from https://www.vertexshaderart.com/art/MNGReAdLScFwNiZDg

void main() {
  float phi = vertexId / vertexCount * 3.14159265*2.0;
  
  float x = cos(phi);
  float y = sin(phi);
  float z = 0.0;
  
  float theta = sin(cos(phi)*3.1415192 + time);//(x+time * 0.3) * 3.141592*2.0;
  
  theta *= 10.0;
  
  y *= cos(theta);
  z = sin(theta) * sin(phi);
  
  vec4 pos = vec4(x, y, z, 1.0);
    
  vec3 eye = vec3(sin(time*0.3), 0, cos(time*0.3));
  vec3 right = vec3(-eye.z, 0, eye.x);
  vec3 look = -eye;

  mat4 L = mat4(vec4(right, 0),
                vec4(0, 1, 0, 0),
                vec4(look, 0),
                vec4(0, 0, 0, 1));
  
  gl_Position = (L * pos) * vec4((resolution.y / resolution.x) * 0.5, 0.5, 0.5, 1);
  v_color = vec4(sin(theta) * 0.5 + 0.5,
                 cos(phi) * 0.5 + 0.5,
                 sin(time) * 0.5 + 0.5,
                 1.0);
}