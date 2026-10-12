using System;
namespace Starship.Physics;
public static class Nav {
    public static void Step(SimState sim, Vehicle v, double dt) {
        if (sim.Disp == null) return;
        Noise(sim, v);
        double alt = v.Alt, dr = sim.Downrange(v), vv = v.VVert, vh = v.VHor;
        int len = v.LagH.Length, i = v.LagI;
        v.LagH[i] = alt; v.LagX[i] = dr; v.LagVv[i] = vv; v.LagVh[i] = vh;
        int j = ((i - Math.Min(v.LagN, (int)Math.Round(Const.NAV_LAG / dt))) % len + len) % len;
        v.NavH = v.NzH + v.LagH[j] - alt;
        v.NavX = v.NzX + v.LagX[j] - dr;
        v.NavVv = v.NzVv + v.LagVv[j] - vv;
        v.NavVh = v.NzVh + v.LagVh[j] - vh;
        v.LagI = (i + 1) % len;
        v.LagN = Math.Min(v.LagN + 1, len - 1);
    }
    private static void Noise(SimState sim, Vehicle v) {
        double at = sim.T / Const.NAV_NOISE_DT;
        long k = (long)Math.Floor(at);
        uint seed = sim.NavSeed ^ (v.Kind == Kind.Ship ? 0x9E3779B9u : 0u);
        double[] a = v.NzA, b = v.NzB;
        long warm = (long)(20 * Const.NAV_POS_TAU / Const.NAV_NOISE_DT);
        if (k < v.NzK || k - v.NzK > warm) {
            Array.Clear(b, 0, 4);
            v.NzK = k - warm - 1;
        }
        for (; v.NzK < k; v.NzK++)
            for (int c = 0; c < 4; c++) {
                double keep = Math.Exp(-Const.NAV_NOISE_DT / (c < 2 ? Const.NAV_POS_TAU : Const.NAV_VEL_TAU));
                a[c] = b[c];
                b[c] = a[c] * keep + Sig(c) * Math.Sqrt(1 - keep * keep) * Rng.Gauss(seed, v.NzK + 2, c);
            }
        double f = at - k;
        v.NzH = a[0] + (b[0] - a[0]) * f; v.NzX = a[1] + (b[1] - a[1]) * f;
        v.NzVv = a[2] + (b[2] - a[2]) * f; v.NzVh = a[3] + (b[3] - a[3]) * f;
    }
    private static double Sig(int c) => c < 2 ? Const.NAV_POS_SIG : Const.NAV_VEL_SIG;
    public static void Observe(SimState sim, Vehicle v, double fx, double fy, double drag, double h, double dt) {
        v.ObsAcc += dt;
        if (v.ObsAcc < Const.NAV_OBS_DT) return;
        double odt = v.ObsAcc;
        v.ObsAcc = 0;
        double wf = sim.Wind.Forecast(h);
        Vec2 e = v.East, u = v.Up, r = v.VRel;
        Vec2 vn = new(r.X + u.X * v.NavVv + e.X * v.NavVh, r.Y + u.Y * v.NavVv + e.Y * v.NavVh);
        Air std = Atmosphere.At(h);
        if (h < Const.NAV_RHO_H) {
            Vec2 vr = Rel(vn, wf + v.WindBias, e);
            double q = 0.5 * std.Rho * vr.Len * vr.Len;
            if (q > Const.NAV_RHO_Q) {
                double dm = Surfaces.Total(v, vr, q, vr.Len / std.A).Drag;
                if (dm > 1) v.RhoEst += (Const.Clamp(drag / dm, 0.7, 1.3) - v.RhoEst) * Math.Min(odt / Const.NAV_RHO_TAU, 1);
            }
        }
        if (h < Const.NAV_OBS_H) {
            double w = wf + v.WindBias, k = v.RhoEst;
            double f0 = HorForce(v, vn, w, e, std, k), s = HorForce(v, vn, w + 0.5, e, std, k) - HorForce(v, vn, w - 0.5, e, std, k);
            if (Math.Abs(s) > Const.NAV_OBS_SMIN * v.Mass) {
                double step = Const.Clamp((fx * e.X + fy * e.Y - f0) / s, -4, 4);
                v.WindBias = Const.Clamp(v.WindBias + step * Math.Min(odt / Const.NAV_WIND_TAU, 1), -30, 30);
                v.WindBiasAvg += (v.WindBias - v.WindBiasAvg) * Math.Min(odt / Const.NAV_POLL_TAU, 1);
            }
        }
        v.WindEst = wf + v.WindBias;
    }
    public static double AeroUp(Vehicle v, double h, double vh, double vv) {
        Vec2 e = v.East, u = v.Up;
        Vec2 vr = Rel(new Vec2(e.X * vh + u.X * vv, e.Y * vh + u.Y * vv), v.WindEst, e);
        Air at = Atmosphere.At(h);
        double sp = vr.Len, q = 0.5 * at.Rho * v.RhoEst * sp * sp;
        Vec2 f = Aero.World(v, Surfaces.Total(v, vr, q, sp / at.A), vr);
        return f.X * u.X + f.Y * u.Y;
    }
    private static Vec2 Rel(Vec2 vn, double w, Vec2 e) => new(vn.X - w * e.X, vn.Y - w * e.Y);
    private static double HorForce(Vehicle v, Vec2 vn, double w, Vec2 e, Air at, double rhoK) {
        Vec2 vr = Rel(vn, w, e);
        double sp = vr.Len, q = 0.5 * at.Rho * rhoK * sp * sp;
        Vec2 f = Aero.World(v, Surfaces.Total(v, vr, q, sp / at.A), vr);
        return f.X * e.X + f.Y * e.Y;
    }
}
