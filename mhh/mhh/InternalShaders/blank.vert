#version 450

layout (location = 0) in float vertexId;
out vec4 v_color;

void main() {
  gl_Position = vec4(0);
  v_color = vec4(0);
}