using System;
namespace Starship.Physics;
public static class Surfaces {
    public static double FinLocal(double vs, double va, double cm, double om)
        => Math.Atan2(vs + om * (Const.FIN_Y - cm), va);
    public static double FinDrag(Vehicle v, double q, double va)
        => -q * 3 * Const.FIN_S * Const.FIN_CD * v.FinDep * Math.Sign(va != 0 ? va : 1);
    public static double FinForce(Vehicle v, double q, double aLoc, double defl)
        => -q * Const.FIN_N * Const.FIN_S * Const.FIN_CN * Math.Sin(aLoc - defl) * v.FinDep;
    public static double FinFor(Vehicle v, double q, double aLoc, double force) {
        double k = q * Const.FIN_N * Const.FIN_S * Const.FIN_CN * v.FinDep;
        if (k < 1) return 0;
        double s = Math.Asin(Const.Clamp(-force / k, -1, 1));
        double d1 = Vehicle.AngDiff(aLoc - s, 0), d2 = Vehicle.AngDiff(aLoc - Math.PI + s, 0);
        double lim = Const.FIN_DEF * Const.D2R * v.CtrlK;
        return Const.Clamp(Math.Abs(d1) < Math.Abs(d2) ? d1 : d2, -lim, lim);
    }
    public static double FlapCn(double alpha) {
        double s = Math.Sin(alpha);
        return 2 * s * Math.Abs(s);
    }
    public static double FlapSpan(Vehicle v, double fwd, double aft) {
        double s = 0;
        for (int i = 0; i < 4; i++) s += Area(v, i, Ang(v, i, fwd, aft));
        return s;
    }
    private static double Ang(Vehicle v, int i, double fwd, double aft)
        => v.Flaps[i].Jammed ? v.Flaps[i].Ang : i < 2 ? fwd : aft;
    private static double Area(Vehicle v, int i, double ang)
        => (i < 2 ? Const.FLAP_S_FWD : Const.FLAP_S_AFT) * (1 - v.Flaps[i].Burn) * Math.Cos(ang);
    public static double BankRateK(Vehicle v) {
        double worst = 0;
        for (int p = 0; p < 4; p += 2) {
            double a = Math.Max(0, Area(v, p, v.Flaps[p].Ang)), b = Math.Max(0, Area(v, p + 1, v.Flaps[p + 1].Ang));
            if (a + b > 1e-9) worst = Math.Max(worst, Math.Abs(a - b) / (a + b));
        }
        return 1 - worst;
    }
    public static double FlapCnA(Vehicle v, double alpha, double fwd, double aft)
        => FlapCn(Math.Abs(alpha)) * FlapSpan(v, fwd, aft) / v.A;
    public static double FinTrim(double cp, double cm) => 1 - (cp - cm) / (Const.FIN_Y - cm);
    public static (double F, double T) FlapForce(Vehicle v, double q, double alpha, double cm, double fwd, double aft) {
        double c = -q * FlapCn(alpha), f = 0, t = 0;
        for (int i = 0; i < 4; i++) {
            double fi = c * Area(v, i, Ang(v, i, fwd, aft));
            f += fi;
            t += fi * ((i < 2 ? Const.FLAP_Y_FWD : Const.FLAP_Y_AFT) - cm);
        }
        return (f, t);
    }
    public static (double Fwd, double Aft) FlapBase(Vehicle v) => v.Mode switch {
        "entryS" => (40 * Const.D2R, 55 * Const.D2R),
        "coastD" => (24 * Const.D2R, 34 * Const.D2R),
        "flipS" or "landS" => (14 * Const.D2R, 20 * Const.D2R),
        _ => (0, 0),
    };
    private static (double Fwd, double Aft) FlapAt(Vehicle v, double d) {
        (double bf, double ba) = FlapBase(v);
        return (Const.Clamp(bf + d, 0, Const.FLAP_FWD_MAX * Const.D2R), Const.Clamp(ba - d, 0, Const.FLAP_AFT_MAX * Const.D2R));
    }
    public static bool FlapsLive(Vehicle v, double q) => !v.Attached &&
        (v.Mode is "coastD" or "entryS" or "flipS" or "landS" || v.Mode == "man" && q > 100);
    public static double FlapAuth(Vehicle v, double q, double alpha, double cm) {
        if (!FlapsLive(v, q)) return 0;
        double span = Const.FLAP_SPAN * Const.D2R * v.CtrlK;
        (double f1, double a1) = FlapAt(v, span);
        (double f2, double a2) = FlapAt(v, -span);
        return Math.Abs(FlapForce(v, q, alpha, cm, f1, a1).T - FlapForce(v, q, alpha, cm, f2, a2).T) / 2;
    }
    public static (double Fwd, double Aft) FlapFor(Vehicle v, double q, double alpha, double cm, double torque) {
        double lo = -Const.FLAP_SPAN * Const.D2R * v.CtrlK, hi = -lo;
        double T(double d) { (double f, double a) = FlapAt(v, d); return FlapForce(v, q, alpha, cm, f, a).T; }
        bool up = T(hi) > T(lo);
        for (int i = 0; i < 30; i++) {
            double mid = (lo + hi) / 2;
            if (T(mid) < torque == up) lo = mid; else hi = mid;
        }
        return FlapAt(v, (lo + hi) / 2);
    }
}
