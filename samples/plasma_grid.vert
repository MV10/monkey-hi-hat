#version 320 es

layout (location = 0) in float vertexId;
uniform vec2 resolution;
uniform float vertexCount;
uniform float time;
uniform sampler2D sound;
out vec4 v_color;

// Adapted from: https://www.vertexshaderart.com/art/eMAJ5HydzdzbnzduF

void main() {
  float pixels = resolution.x * resolution.y;
  float pxGrid = pixels / vertexCount;
  float size = sqrt(pxGrid);
  
  float across = ceil(resolution.x / size);
  float down = floor(vertexCount / across);
  float x = mod(vertexId+0.01, across);
  float y = floor((vertexId+0.01) / across);
  

  float u = (x + 0.5) / across;
  float v = (y + 0.5) / down;

  
  float ux = u * 2. - 1.;
  float vy = v * 2. - 1.;
  
  
  gl_Position = vec4(ux, vy, 0, 1);
  
  
  float snd = pow(texture(sound, vec2(u * 0.01 + 0.001, v * 0.02)).g, 4.);


  
  gl_PointSize = max(resolution.x / across, resolution.y / down) + 1.;
  float r = snd * pow(cos(time * 1.92 + u * 2.9) * sin(-time * 1.63 + v * 2.9) * 0.5 + 0.5, 4.);
  float g = snd * pow(cos(time * 1.74 - u * 2.41) * sin(time * 1.34 - v * 3.4) * 0.5 + 0.5, 4.);
  float b = snd * pow(cos(time * 1.21 + u * 1.5) * sin(time * 1.53 + v * 1.41) * 0.5 + 0.5, 4.);
  v_color = vec4(r, g, b, 1);
}