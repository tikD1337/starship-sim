using System;
namespace Starship.Physics;
public static class Nav {
    public static void Step(SimState sim, Vehicle v, double dt) {
        if (sim.Disp == null) return;
        double kp = Math.Exp(-dt / Const.NAV_POS_TAU), kv = Math.Exp(-dt / Const.NAV_VEL_TAU);
        double sp = Const.NAV_POS_SIG * Math.Sqrt(1 - kp * kp), sv = Const.NAV_VEL_SIG * Math.Sqrt(1 - kv * kv);
        v.NavH = v.NavH * kp + sp * Gauss(sim.NavRng);
        v.NavX = v.NavX * kp + sp * Gauss(sim.NavRng);
        v.NavVv = v.NavVv * kv + sv * Gauss(sim.NavRng);
        v.NavVh = v.NavVh * kv + sv * Gauss(sim.NavRng);
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
