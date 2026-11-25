#version 330 core

// P3: modele repetitive. Coordonatele UV se inmultesc cu uScale, deci pe panou
// incap uScale "celule"; fiecare functie intoarce 0 (culoarea A) sau 1 (culoarea B).

in vec3 vColor;
in vec2 vUV;

uniform bool uShowUV;
uniform vec3 uColorA;
uniform vec3 uColorB;
uniform int uPattern;       // 0 = dungi, 1 = tabla de sah, 2 = grila
uniform float uScale;       // cate celule pe latura panoului
uniform float uLineWidth;   // grosimea liniilor grilei, ca fractiune din celula

out vec4 FragColor;

// RASPUNS INTREBARE P3:
// Functia step(0.5, fract(p.x)) are o tranzitie brusca (discontinua: 0 sau 1, fara valori intermediare),
// iar fragment shaderul esantioneaza scena discret, doar in centrele pixelilor. La uScale = 20, latimea
// unei dungi pe ecran nu mai este un numar intreg de pixeli (ex. 4.3 pixeli), astfel ca frontiera 0.5
// cade diferit fata de grila de pixeli: unele dungi prind N pixeli pe latime, iar altele N + 1 pixeli
// (efect de aliasing / esantionare discreta). Legatura cu sectiunea 6.2: inlocuirea lui step cu
// smoothstep pe o banda ingusta (de ordinul unui pixel) amesteca culorile pe muchie (anti-aliasing)
// si elimina iluzia dungilor inegale.

// Dungi verticale: jumatate din fiecare celula A, jumatate B.
float stripes(vec2 p)
{
    return step(0.5, fract(p.x));
}

// Tabla de sah: celulele alterneaza pe ambele axe.
float checker(vec2 p)
{
    return mod(floor(p.x) + floor(p.y), 2.0);
}

// Grila: linii subtiri la marginea stanga si la marginea de jos a fiecarei celule.
float gridLines(vec2 p)
{
    vec2 f = fract(p);
    return 1.0 - step(uLineWidth, f.x) * step(uLineWidth, f.y);
}

void main()
{
    if (uShowUV)
    {
        FragColor = vec4(vUV, 0.0, 1.0);
        return;
    }

    vec2 p = vUV * uScale;
    float m;
    if (uPattern == 0)
        m = stripes(p);
    else if (uPattern == 1)
        m = checker(p);
    else
        m = gridLines(p);

    vec3 color = mix(uColorA, uColorB, m);
    FragColor = vec4(color * vColor, 1.0);
}
