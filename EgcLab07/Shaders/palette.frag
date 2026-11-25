#version 330 core

// P5: paleta cosinus (Inigo Quilez): o singura formula, patru vectori, o infinitate de palete.

in vec3 vColor;
in vec2 vUV;

uniform bool uShowUV;
uniform float uTime;
uniform vec3 uA;          // luminozitatea medie a fiecarui canal (r, g, b)
uniform vec3 uB;          // amplitudinea (cat de mult variaza canalul)
uniform vec3 uC;          // frecventa (de cate ori se repeta canalul cand t creste cu 1)
uniform vec3 uD;          // faza (decalajul canalelor intre ele)
uniform float uRepeat;    // de cate ori se repeta paleta de la polul sud la polul nord
uniform float uSpeed;     // viteza de "curgere" a culorilor

out vec4 FragColor;

// RASPUNS INTREBARE P5:
// Trebuie schimbat vectorul uC (c), deoarece acesta controleaza frecventa fiecarui canal RGB
// (de cate ori se repeta ciclul cosinusului cand parametrul t creste cu 1). Daca dublam
// componenta corespunzatoare din uC (de exemplu uC = vec3(2.0, 1.0, 1.0)), canalul rosu va
// oscila de doua ori mai des decat verdele si albastrul pe acelasi interval al lui t.
// (Nota: benzile coboara spre polul sud desi t creste in timp deoarece, pentru a pastra aceeasi
// culoare t = constant, cresterea lui uTime impune scaderea lui vUV.y dinspre 1 spre 0.)

vec3 palette(float t)
{
    // TODO 7.6 (a): formula Inigo Quilez pe toate cele 3 canale simultan (2 * pi = 6.2831853)
    return uA + uB * cos(6.2831853 * (uC * t + uD));
}

void main()
{
    if (uShowUV)
    {
        FragColor = vec4(vUV, 0.0, 1.0);
        return;
    }

    // TODO 7.6 (b): t creste de la polul sud (0) la polul nord (1) si curge in timp
    float t = vUV.y * uRepeat + uTime * uSpeed;
    vec3 color = palette(t);
    FragColor = vec4(color * vColor, 1.0);
}