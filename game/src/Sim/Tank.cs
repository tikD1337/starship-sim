using System;
namespace Starship.Physics;
public sealed class Tank {
    public double V, Mg, P, R;
    public string Name;
}
public sealed class TankSet {
    public Tank F, O;
    public double Copv, Copv0;
    public string Src = "наддув";
    public static TankSet Make(Kind kind, double prop) {
        double vf = prop / (1 + 3.6) / 423;
        double vo = prop * 3.6 / (1 + 3.6) / 1141;
        var ts = new TankSet {
            F = new Tank { V = vf * 1.04, P = 350e3, R = 518, Name = "горючего" },
            O = new Tank { V = vo * 1.04, P = 380e3, R = 260, Name = "окислителя" },
            Copv = kind == Kind.Booster ? 900 : 420, Copv0 = kind == Kind.Booster ? 900 : 420,
        };
        foreach (Tank t in new[] { ts.F, ts.O })
            t.Mg = t.P * Math.Max(t.V * 0.02, 1) / (t.R * 270);
        return ts;
    }
}
public static class Pressurant {
    public static void Step(Vehicle v, double dt) {
        const double T = 270;
        double fill = Const.Clamp(v.Fill, 0, 1);
        EngineParams p0 = v.Eng[0].P;
        bool running = v.F > 1e3;
        string src = running ? "автогенный" : (v.Tanks.Copv > 0 ? "баллоны" : "подушка");
        if (v.OnHeader) src = "головные баки";
        for (int i = 0; i < 2; i++) {
            Tank t = i == 0 ? v.Tanks.F : v.Tanks.O;
            double setK = i == 0 ? p0.PTankF : p0.PTankOx;
            double frac = i == 0 ? 1.0 / (1 + 3.6) : 3.6 / (1 + 3.6);
            double vul = Math.Max(t.V * (1 - fill), t.V * 0.02);
            double target = setK * 1000;
            double need = target * vul / (t.R * T) - t.Mg;
            if (need > 0) {
                double auto = running ? 0.035 * v.Mdot * frac : 0;
                double copv = v.Tanks.Copv > 0 ? 1.6 * v.CopvK : 0;
                double add = Math.Min(need, (auto + copv) * dt);
                t.Mg += add;
                double fromCopv = Math.Max(0, add - auto * dt);
                if (fromCopv > 0) v.Tanks.Copv = Math.Max(0, v.Tanks.Copv - fromCopv);
            }
            else if (t.Mg > target * vul / (t.R * T) * 1.06) t.Mg -= Math.Min(-need, 3 * dt);
            t.P = t.Mg * t.R * T / vul;
        }
        v.Tanks.Src = src;
    }
}
