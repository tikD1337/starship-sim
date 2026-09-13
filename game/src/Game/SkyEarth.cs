using System;
using Godot;
using Starship.Physics;
namespace Starship.Game;
public sealed class SkyEarth {
    private ShaderMaterial _mat;
    public Sky Sky;
    private const string Code = @"
shader_type sky;
uniform float alt_m = 0.0;
uniform float travel = 0.0;
uniform vec3 sun_dir = vec3(-0.76, 0.46, 0.21);
uniform float clock = 0.0;
uniform float sky_gain = 1.15;
uniform float earth_gain = 1.15;
uniform float cloud_gain = 1.0;
uniform float star_gain = 1.0;
uniform int dbg = 0;
const float RE_M = 6371000.0;
const float H_M = 8500.0;
const vec3 TAU_R = vec3(0.049, 0.115, 0.281);
const vec3 POLE = vec3(0.0, 0.438, 0.899);
float hash13(vec3 p) {
    p = fract(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}
float vnoise(vec3 x) {
    vec3 i = floor(x);
    vec3 f = fract(x);
    f = f * f * (3.0 - 2.0 * f);
    float a = mix(mix(hash13(i), hash13(i + vec3(1.0, 0.0, 0.0)), f.x),
                  mix(hash13(i + vec3(0.0, 1.0, 0.0)), hash13(i + vec3(1.0, 1.0, 0.0)), f.x), f.y);
    float b = mix(mix(hash13(i + vec3(0.0, 0.0, 1.0)), hash13(i + vec3(1.0, 0.0, 1.0)), f.x),
                  mix(hash13(i + vec3(0.0, 1.0, 1.0)), hash13(i + vec3(1.0, 1.0, 1.0)), f.x), f.y);
    return mix(a, b, f.z);
}
float fbm(vec3 p, int oct) {
    float s = 0.0;
    float a = 0.5;
    float n = 0.0;
    for (int k = 0; k < 8; k++) {
        if (k >= oct) break;
        s += a * vnoise(p);
        n += a;
        p *= 2.03;
        a *= 0.5;
    }
    return s / max(n, 1e-5);
}
vec3 rot_z(vec3 v, float a) {
    float s = sin(a);
    float c = cos(a);
    return vec3(c * v.x - s * v.y, s * v.x + c * v.y, v.z);
}
vec3 rot_x(vec3 v, float a) {
    float s = sin(a);
    float c = cos(a);
    return vec3(v.x, c * v.y - s * v.z, s * v.y + c * v.z);
}
vec3 surface(vec3 n, vec3 d) {
    vec3 g = rot_x(rot_z(n, -travel), 0.55);
    float lat = dot(g, POLE);
    float h = fbm(g * 2.05, 6);
    h += (fbm(g * 9.5 + 17.0, 4) - 0.5) * 0.09;
    h += (fbm(g * 46.0 + 5.0, 5) - 0.5) * 0.052;
    h += (fbm(g * 260.0 + 31.0, 4) - 0.5) * 0.024;
    float sea = 0.505;
    float landness = smoothstep(sea, sea + 0.010, h);
    float ice = smoothstep(0.79, 0.905, abs(lat));

    vec3 ocean = mix(vec3(0.020, 0.058, 0.152), vec3(0.042, 0.150, 0.243),
                     smoothstep(sea - 0.10, sea, h));
    float dry = fbm(g * 5.3 + 11.0, 4) + (fbm(g * 74.0 + 3.0, 4) - 0.5) * 0.34
              + (abs(lat) < 0.28 ? -0.10 : 0.06);
    vec3 soil = mix(vec3(0.072, 0.121, 0.055), vec3(0.318, 0.253, 0.148),
                    smoothstep(0.40, 0.63, dry));
    soil = mix(soil, vec3(0.34, 0.31, 0.27), smoothstep(0.60, 0.75, h));
    soil *= 0.86 + 0.28 * fbm(g * 560.0 + 7.0, 4);
    vec3 base = mix(ocean, soil, landness);
    base = mix(base, vec3(0.87, 0.90, 0.94), ice);

    vec3 cg = rot_z(g, clock * 1.4e-5);
    float cl = fbm(cg * 3.2 + vec3(4.0), 5);
    cl = mix(cl, fbm(cg * 14.0 + vec3(9.0), 4), 0.34);
    cl = mix(cl, fbm(cg * 96.0 + vec3(21.0), 4), 0.30);
    cl = mix(cl, fbm(cg * 430.0 + vec3(37.0), 4), 0.34);
    cl = pow(clamp(cl, 0.0, 1.0), 1.25);
    float cov = smoothstep(0.505, 0.705, cl) * (0.94 - 0.30 * ice);
    float landsoft = smoothstep(sea, sea + 0.05, h);
    cov *= 1.0 - 0.34 * landsoft;
    float cshade = 0.78 + 0.44 * fbm(cg * 250.0 + vec3(53.0), 4);

    float sl = dot(n, sun_dir);
    float lam = max(sl, 0.0);
    float day = smoothstep(-0.11, 0.20, sl);

    vec3 hv = normalize(sun_dir - d);
    float spec = pow(max(dot(n, hv), 0.0), 220.0) * (1.0 - landness) * (1.0 - cov) * (1.0 - ice);

    base *= 1.0 - 0.30 * cov;
    vec3 lit = base * (lam * 1.30 + 0.015);
    lit = mix(lit, vec3(0.90, 0.93, 0.97) * cloud_gain * cshade * (lam * 0.88 + 0.020), cov);
    lit += vec3(1.0, 0.95, 0.86) * spec * 1.7;
    lit *= day * earth_gain;

    float cities = smoothstep(0.655, 0.735, fbm(g * 95.0, 3)) * landness * (1.0 - ice);
    cities *= 0.35 + 0.65 * smoothstep(0.52, 0.66, h);
    lit += vec3(1.0, 0.72, 0.38) * cities * 1.25 * (1.0 - day);

    float rim = 1.0 - clamp(dot(n, -d), 0.0, 1.0);
    lit += vec3(0.16, 0.34, 0.66) * pow(rim, 5.0) * day * earth_gain * 0.55;

    if (dbg == 1) return vec3(landness);
    if (dbg == 2) return vec3(cov);
    if (dbg == 3) return base;
    if (dbg == 4) return vec3(lam);
    if (dbg == 5) return vec3(rim);
    if (dbg == 7) return vec3(cshade);
    if (dbg == 8) return vec3(cov);
    if (dbg == 6) return vec3(h);
    return lit;
}
vec3 stars(vec3 d) {
    vec3 sp = d * 380.0;
    vec3 ci = floor(sp);
    float h1 = hash13(ci);
    if (h1 < 0.9958) return vec3(0.0);
    vec3 cen = ci + vec3(0.28, 0.34, 0.22) + 0.44 * vec3(hash13(ci + 1.7), hash13(ci + 3.1), hash13(ci + 5.9));
    float dd = length(sp - cen);
    float pt = pow(smoothstep(0.55, 0.0, dd), 3.0);
    float br = 0.25 + 0.75 * pow((h1 - 0.9958) / 0.0042, 2.2);
    vec3 tint = mix(vec3(0.72, 0.80, 1.0), vec3(1.0, 0.86, 0.68), hash13(ci + 9.3));
    return tint * pt * br * 1.4;
}
float erfcx(float z) {
    float t = 1.0 / (1.0 + 0.3275911 * z);
    return t * (0.254829592 + t * (-0.284496736 + t * (1.421413741 + t * (-1.453152027 + t * 1.061405429))));
}
float shell(float gh, float t0, float hs) {
    float z = -t0 / sqrt(2.0 * RE_M * hs);
    if (z >= 0.0) return 0.5 * exp(-max(alt_m, 0.0) / hs) * erfcx(z);
    return 0.5 * exp(-max(gh, 0.0) / hs) * (2.0 - exp(-z * z) * erfcx(-z));
}
vec3 space(vec3 d, vec3 L, float b, float cc) {
    vec3 col = stars(d) * star_gain;

    float sd = dot(d, sun_dir);
    col += vec3(1.0, 0.98, 0.94) * smoothstep(0.999984, 0.999993, sd) * 9.0;
    col += vec3(1.0, 0.94, 0.82) * pow(max(sd, 0.0), 3400.0) * 0.9;

    float per = sqrt(max(cc - b * b, 0.0));
    float gh = (per - 1.0) * RE_M;
    float t0 = b * RE_M;
    vec3 q = normalize(d * max(b, 0.0) - L);
    float lit = smoothstep(-0.28, 0.14, dot(q, sun_dir));
    float thin = shell(gh, t0, 13000.0);
    float dense = shell(gh, t0, 4200.0);
    vec3 rc = mix(vec3(0.22, 0.50, 1.0), vec3(0.62, 0.82, 1.0), dense);
    col += rc * thin * lit * 1.35 * smoothstep(3000.0, 20000.0, alt_m);
    return col;
}
vec3 air(vec3 col, vec3 d, float path_m, float hit) {
    float dens = exp(-max(alt_m, 0.0) / H_M);
    if (dens < 1e-4) return col;

    float airm = mix(dens * (1.0 / (max(d.y, 0.0) + 0.145)),
                     dens * min(path_m, 3.0e6) / H_M, hit);
    airm = min(airm, 26.0);
    vec3 tau = TAU_R * airm;
    float cs = dot(d, sun_dir);
    float ph = 0.75 * (1.0 + cs * cs);
    float slum = smoothstep(-0.09, 0.22, sun_dir.y);
    vec3 ins = (1.0 - exp(-tau)) * ph * vec3(1.0, 0.97, 0.93) * sky_gain * slum;
    ins += vec3(1.0, 0.92, 0.78) * pow(max(cs, 0.0), 9.0) * dens * 0.42 * slum;
    return col * exp(-tau) + ins;
}
void sky() {
    vec3 d = normalize(EYEDIR);
    float alt = max(alt_m, 0.0) / RE_M;
    vec3 L = vec3(0.0, -(1.0 + alt), 0.0);
    float b = dot(d, L);
    float cc = dot(L, L);
    float disc = b * b - (cc - 1.0);

    vec3 col;
    float path_m = 0.0;
    float hit = 0.0;
    if (disc > 0.0 && b > 0.0) {
        float tt = b - sqrt(disc);
        path_m = tt * RE_M;
        hit = 1.0;
        col = surface(normalize(d * tt - L), d);
    } else {
        col = space(d, L, b, cc);
    }
    COLOR = air(col, d, path_m, hit);
}
";
    public static SkyEarth Build() {
        var s = new SkyEarth();
        var sh = new Shader { Code = Code };
        s._mat = new ShaderMaterial { Shader = sh };
        s.Sky = new Sky {
            SkyMaterial = s._mat, ProcessMode = Sky.ProcessModeEnum.Incremental,
            RadianceSize = Sky.RadianceSizeEnum.Size128,
        };
        return s;
    }
    public void Update(double altM, double travelRad, Vector3 sunDir, double clock) {
        _mat.SetShaderParameter("alt_m", (float)altM);
        _mat.SetShaderParameter("travel", (float)travelRad);
        _mat.SetShaderParameter("sun_dir", sunDir);
        _mat.SetShaderParameter("clock", (float)clock);
        _mat.SetShaderParameter("star_gain", (float)Math.Clamp((altM - 26000.0) / 30000.0, 0.0, 1.0));
    }
    public void SetDbg(int n) => _mat.SetShaderParameter("dbg", n);
    public static Vector3 SunScene(Vec2 up, Vec2 east) {
        const double sigma = 1.0821, delta = 0.2094;
        double cx = Math.Cos(delta) * Math.Sin(sigma);
        double cy = Math.Cos(delta) * Math.Cos(sigma);
        double cz = Math.Sin(delta);
        var v = new Vector3(
            (float)(cx * east.X + cy * east.Y),
            (float)(cx * up.X + cy * up.Y),
            (float)cz);
        return v.Normalized();
    }
}
