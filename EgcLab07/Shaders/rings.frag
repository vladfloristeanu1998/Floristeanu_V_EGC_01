#version 330 core

// P4: un disc cu margine neteda si unde concentrice animate.

in vec3 vColor;
in vec2 vUV;

uniform bool uShowUV;
uniform float uTime;           // timpul de joc, in secunde (se opreste in pauza)
uniform vec2 uCenter;          // centrul discului, in coordonate UV
uniform float uRadius;         // raza discului, in coordonate UV
uniform float uSoftness;       // latimea marginii netede
uniform float uFrequency;      // cate unde pe unitatea de distanta
uniform float uSpeed;          // viteza undelor
uniform vec3 uColorInside;     // culoarea undelor
uniform vec3 uColorOutside;    // culoarea din afara discului

out vec4 FragColor;

void main()
{
    if (uShowUV)
    {
        FragColor = vec4(vUV, 0.0, 1.0);
        return;
    }

    // Distanta fragmentului fata de centrul discului (0 in centru, ~0.5 la marginea panoului).
    float d = distance(vUV, uCenter);

 // RASPUNS INTREBARE P4:
    // 1. Daca inlocuim "-" cu "+" (d * uFrequency + uTime * uSpeed * 0.25), se inverseaza sensul de
    //    propagare: pentru ca faza sinusoidei sa ramana constanta pe masura ce uTime creste, raza d
    //    trebuie sa scada, deci undele merg dinspre marginea discului catre centru (converg in uCenter).
    // 2. Daca inlocuim distance(vUV, uCenter) cu abs(vUV.x - uCenter.x), distanta ignora coordonata Y
    //    si masoara doar departarea orizontala fata de axa verticala x = uCenter.x. Discul devine o
    //    banda verticala (de latime 2 * uRadius), iar inelele concentrice devin dungi verticale
    //    simetrice care se propaga orizontal spre stanga si spre dreapta.

    // TODO 7.5 (a): discul cu margine neteda (anti-aliasing pe intervalul [uRadius - uSoftness, uRadius + uSoftness])
    float inside = 1.0 - smoothstep(uRadius - uSoftness, uRadius + uSoftness, d);

    // TODO 7.5 (b): undele concentrice animate (2 * pi = 6.2831853)
    float wave = 0.5 + 0.5 * sin(6.2831853 * (d * uFrequency - uTime * uSpeed * 0.25));
    vec3 ringColor = mix(uColorInside * 0.35, uColorInside, wave);
    vec3 color = mix(uColorOutside, ringColor, inside);
    FragColor = vec4(color * vColor, 1.0);
}
