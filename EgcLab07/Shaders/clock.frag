#version 330 core

in vec3 vColor;
in vec2 vUV;

uniform bool uShowUV;
uniform float uHourAngle;
uniform float uMinuteAngle;
uniform float uSecondAngle;
uniform float uNightMix;      // 0.0 = zi, 1.0 = noapte
uniform float uBezelWidth;    // grosimea ramei exterioare
uniform float uTickScale;     // lungimea gradatiilor
uniform int uSubticks;        // 1 = toate cele 60 gradatii, 0 = doar cele 12 ore
uniform vec3 uSecondColor;    // culoarea secundarului

out vec4 FragColor;

const float PI = 3.14159265;
const float TWO_PI = 6.28318530;

// Distanta de la punctul p la segmentul [a, b]
float sdSegment(vec2 p, vec2 a, vec2 b)
{
    vec2 pa = p - a;
    vec2 ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h);
}

// Directia unitate pentru un unghi masurat de la ora 12 in sensul acelor de ceasornic
vec2 clockDir(float angle)
{
    return vec2(sin(angle), cos(angle));
}

void main()
{
    if (uShowUV)
    {
        FragColor = vec4(vUV, 0.0, 1.0);
        return;
    }

    // Coordonate centrate in [-1, 1], cu (0, 0) in centrul cadranului
    vec2 p = (vUV - 0.5) * 2.0;
    float r = length(p);
    float aa = max(fwidth(r), 0.002);

    // 1. Discul exterior si transparenta in afara ceasului
    float outerRadius = 0.96;
    float innerRadius = outerRadius - uBezelWidth;
    float alpha = 1.0 - smoothstep(outerRadius - aa, outerRadius + aa, r);
    if (alpha <= 0.001) discard;

    // Paleta zi / noapte (interpolata lin prin uNightMix)
    vec3 dialColor  = mix(vec3(0.95, 0.93, 0.88), vec3(0.11, 0.13, 0.18), uNightMix);
    vec3 bezelBase  = mix(vec3(0.72, 0.58, 0.34), vec3(0.28, 0.33, 0.42), uNightMix);
    vec3 tickColor  = mix(vec3(0.14, 0.15, 0.18), vec3(0.86, 0.89, 0.95), uNightMix);
    vec3 handColor  = mix(vec3(0.12, 0.13, 0.16), vec3(0.92, 0.94, 0.98), uNightMix);

    // Efect subtil de reflexie pe rama metalica
    float bezelGrad = 0.85 + 0.15 * cos(atan(p.x, p.y) - 0.6);
    vec3 bezelColor = bezelBase * bezelGrad;

    // Bordura interioara fina intre rama si cadran
    float bezelMask = smoothstep(innerRadius - aa, innerRadius + aa, r);
    float rimLine = 1.0 - smoothstep(0.0, aa * 1.5, abs(r - innerRadius));
    vec3 color = mix(dialColor, bezelColor, bezelMask);
    color = mix(color, tickColor * 0.6, rimLine * 0.5);

    // 2. Cele 60 de gradatii
    float angle = atan(p.x, p.y);
    if (angle < 0.0) angle += TWO_PI;

    float sector = TWO_PI / 60.0;
    float idx = floor(angle / sector + 0.5);
    float tickAngle = idx * sector;
    bool isHourTick = mod(idx, 5.0) < 0.5;

    if (isHourTick || uSubticks == 1)
    {
        vec2 tDir = clockDir(tickAngle);
        float tickOuter = innerRadius - 0.035;
        float tickLen = (isHourTick ? 0.11 : 0.05) * uTickScale;
        float tickHalfW = isHourTick ? 0.013 : 0.0055;

        float dTick = sdSegment(p, tDir * (tickOuter - tickLen), tDir * tickOuter) - tickHalfW;
        float tickMask = 1.0 - smoothstep(-aa, aa, dTick);
        color = mix(color, tickColor, tickMask);
    }

    // 3. Acele ceasului (cu o umbra discreta pentru adancime)
    vec2 hDir = clockDir(uHourAngle);
    vec2 mDir = clockDir(uMinuteAngle);
    vec2 sDir = clockDir(uSecondAngle);

    vec2 hA = -hDir * 0.08, hB = hDir * (innerRadius * 0.50);
    vec2 mA = -mDir * 0.10, mB = mDir * (innerRadius * 0.76);
    vec2 sA = -sDir * 0.17, sB = sDir * (innerRadius * 0.85);

    // Umbra acelor (deplasata usor dreapta-jos)
    vec2 sh = vec2(0.012, -0.014);
    float dShadowH = sdSegment(p - sh, hA, hB) - 0.022;
    float dShadowM = sdSegment(p - sh, mA, mB) - 0.014;
    float dShadowS = sdSegment(p - sh, sA, sB) - 0.006;
    float dShadow = min(dShadowH, min(dShadowM, dShadowS));
    float shadowMask = (1.0 - smoothstep(-aa * 2.0, aa * 2.0, dShadow)) * 0.22;
    color = mix(color, vec3(0.0), shadowMask);

    // Orarul
    float dHour = sdSegment(p, hA, hB) - 0.021;
    color = mix(color, handColor, 1.0 - smoothstep(-aa, aa, dHour));

    // Minutarul
    float dMinute = sdSegment(p, mA, mB) - 0.013;
    color = mix(color, handColor, 1.0 - smoothstep(-aa, aa, dMinute));

    // Secundarul (rosu)
    float dSecond = sdSegment(p, sA, sB) - 0.0055;
    color = mix(color, uSecondColor, 1.0 - smoothstep(-aa, aa, dSecond));

    // 4. Axul / capacul central
    float capOuter = 1.0 - smoothstep(0.038 - aa, 0.038 + aa, r);
    float capInner = 1.0 - smoothstep(0.015 - aa, 0.015 + aa, r);
    color = mix(color, uSecondColor, capOuter);
    color = mix(color, bezelBase, capInner);

    FragColor = vec4(color * vColor, alpha);
}