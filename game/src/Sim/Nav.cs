using System;
namespace Starship.Physics;
public static class Nav {
    public static void Step(SimState sim, Vehicle v, double dt) {
        if (sim.Disp == null) return;
        double kp = Math.Exp(-dt / Const.NAV_POS_TAU), kv = Math.Exp(-dt / Const.NAV_VEL_TAU);
        double sp = Const.NAV_POS_SIG * Math.Sqrt(1 - kp * kp), sv = Const.NAV_VEL_SIG * Math.Sqrt(1 - kv * kv);
        v.NzH = v.NzH * kp + sp * Gauss(sim.NavRng);
        v.NzX = v.NzX * kp + sp * Gauss(sim.NavRng);
        v.NzVv = v.NzVv * kv + sv * Gauss(sim.NavRng);
        v.NzVh = v.NzVh * kv + sv * Gauss(sim.NavRng);
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
    private static double Gauss(Rng r) {
        double u = Math.Max(r.Next(), 1e-12), w = r.Next();
        return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * w);
    }
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
                double dm = Aero.Compute(v, vr, q, vr.Len / std.A).Drag;
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
    private static Vec2 Rel(Vec2 vn, double w, Vec2 e) => new(vn.X - w * e.X, vn.Y - w * e.Y);
    private static double HorForce(Vehicle v, Vec2 vn, double w, Vec2 e, Air at, double rhoK) {
        Vec2 vr = Rel(vn, w, e);
        double sp = vr.Len, q = 0.5 * at.Rho * rhoK * sp * sp;
        Vec2 f = Aero.World(v, Aero.Compute(v, vr, q, sp / at.A), vr);
        return f.X * e.X + f.Y * e.Y;
    }
}
