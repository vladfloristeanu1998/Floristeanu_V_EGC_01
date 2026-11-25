#version 330 core

in vec3 vColor;

uniform vec4 uTint;   // nuanta obiectului (alb = culoarea varfurilor ramane neschimbata)

out vec4 FragColor;

void main()
{
    FragColor = vec4(vColor * vec3(1.0, 0.6, 0.4), 1.0) * uTint;
}
