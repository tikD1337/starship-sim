using System;
using System.Collections.Generic;
using Godot;
namespace Starship.Game;
public static class CamCheck {
    public const float MaxGap = 0.45f;
    public static int Run(ModelLibrary lib) {
        int bad = 0;
        foreach (CamRig.Spot s in CamRig.Spots) {
            string[] names = s.On switch {
                CamRig.Carrier.FlapFwd => new[] { "flap_fwd" },
                CamRig.Carrier.FlapAft => new[] { "flap_aft" },
                _ => s.Ship ? new[] { "hull_ship", "tiles_ship" } : new[] { "hull_booster" },
            };
            var parts = new List<Vector3[]>();
            foreach (string n in names)
                if (lib.Meshes.TryGetValue(n, out Mesh m)) parts.Add(m.GetFaces());
            float gap = float.MaxValue;
            foreach (Vector3[] f in parts) gap = Mathf.Min(gap, Gap(f, s.Pos));
            float rCam = new Vector2(s.Pos.X, s.Pos.Z).Length();
            float rSkin = s.On == CamRig.Carrier.Body ? Outer(parts, s.Pos) : 0f;
            string verdict = parts.Count == 0 ? "нет детали " + names[0]
                : rCam < rSkin + 0.02f ? "внутри корпуса" : gap > MaxGap ? "висит в воздухе" : "ok";
            if (verdict != "ok") bad++;
            GD.Print($"CAMCHECK {s.Name}: зазор {gap:F2} м, обшивка на {rSkin:F2} м, камера на {rCam:F2} м — {verdict}");
        }
        GD.Print($"CAMCHECK_SUM плохих {bad}");
        return bad;
    }
    private static float Gap(Vector3[] f, Vector3 p) {
        float best = 20f;
        for (int i = 0; i + 2 < f.Length; i += 3) {
            Vector3 a = f[i], b = f[i + 1], c = f[i + 2];
            if (Mathf.Min(a.Y, Mathf.Min(b.Y, c.Y)) - p.Y > best) continue;
            if (p.Y - Mathf.Max(a.Y, Mathf.Max(b.Y, c.Y)) > best) continue;
            float d = p.DistanceTo(Closest(p, a, b, c));
            if (d < best) best = d;
        }
        return best;
    }
    private static float Outer(List<Vector3[]> parts, Vector3 p) {
        var o = new Vector3(0, p.Y, 0);
        var dir = new Vector3(p.X, 0, p.Z).Normalized();
        float far = 0f;
        foreach (Vector3[] f in parts)
            for (int i = 0; i + 2 < f.Length; i += 3)
                far = Mathf.Max(far, Ray(o, dir, f[i], f[i + 1], f[i + 2]));
        return far;
    }
    private static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b, Vector3 c) {
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        float d1 = ab.Dot(ap), d2 = ac.Dot(ap);
        if (d1 <= 0 && d2 <= 0) return a;
        Vector3 bp = p - b;
        float d3 = ab.Dot(bp), d4 = ac.Dot(bp);
        if (d3 >= 0 && d4 <= d3) return b;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
        Vector3 cp = p - c;
        float d5 = ab.Dot(cp), d6 = ac.Dot(cp);
        if (d6 >= 0 && d5 <= d6) return c;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
        float va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
            return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
        float den = 1f / (va + vb + vc);
        return a + ab * (vb * den) + ac * (vc * den);
    }
    private static float Ray(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c) {
        Vector3 e1 = b - a, e2 = c - a, h = d.Cross(e2);
        float det = e1.Dot(h);
        if (Mathf.Abs(det) < 1e-9f) return -1f;
        float inv = 1f / det;
        Vector3 s = o - a;
        float u = inv * s.Dot(h);
        if (u < 0 || u > 1) return -1f;
        Vector3 q = s.Cross(e1);
        float v = inv * d.Dot(q);
        if (v < 0 || u + v > 1) return -1f;
        float t = inv * e2.Dot(q);
        return t > 1e-4f ? t : -1f;
    }
}
