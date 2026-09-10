using System;
namespace Starship.Physics;
public enum Kind { Booster, Ship }
public sealed class EngineSpec {
    public double Fv, Isp, IspSL, Mdot, Ae, At, Eps, CfVac, Real, Pe;
    public const double GAMMA = 1.20, CSTAR = 1800.0, PC_PA = 30e6;
    private static double AreaRatio(double m) {
        const double g = GAMMA;
        return 1 / m * Math.Pow(2 / (g + 1) * (1 + (g - 1) / 2 * m * m), (g + 1) / (2 * (g - 1)));
    }
    public static double ExitMach(double eps) {
        double lo = 1.0001, hi = 20;
        for (int i = 0; i < 80; i++) {
            double mid = (lo + hi) / 2;
            if (AreaRatio(mid) < eps) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }
    private static void Expand(EngineSpec e) {
        const double g = GAMMA;
        e.Eps = e.Ae / e.At;
        double me = ExitMach(e.Eps);
        e.Pe = PC_PA * Math.Pow(1 + (g - 1) / 2 * me * me, -g / (g - 1));
        e.CfVac = Math.Sqrt(2 * g * g / (g - 1) * Math.Pow(2 / (g + 1), (g + 1) / (g - 1))
                            * (1 - Math.Pow(e.Pe / PC_PA, (g - 1) / g))) + e.Pe / PC_PA * e.Eps;
    }
    public static EngineSpec Make(double fv, double isp, double ispSL) {
        var e = new EngineSpec { Fv = fv, Isp = isp, IspSL = ispSL };
        e.Mdot = e.Fv / (Const.G0 * e.Isp);
        e.Ae = (e.Fv - e.Mdot * Const.G0 * e.IspSL) / Const.P0;
        e.At = e.Mdot * CSTAR / PC_PA;
        Expand(e);
        e.Real = e.Fv / (e.CfVac * PC_PA * e.At);
        return e;
    }
    public EngineSpec WithExitArea(double k) {
        if (!(k > 0) || k == 1) return this;
        var e = new EngineSpec {
            Fv = Fv, Isp = Isp, IspSL = IspSL, Mdot = Mdot, At = At, Ae = Ae * k, Real = Real,
        };
        Expand(e);
        return e;
    }
    public double CfAt(double pcPa, double pa) {
        double pc = Math.Max(pcPa, 1);
        if (!Separated(pcPa, pa)) return CfVac * Real - pa * Eps / pc;
        const double g = GAMMA;
        double pSep = Math.Min(SEP_PR * pa, pc * 0.99);
        double mSep = Math.Max(Math.Sqrt(Math.Max(2 / (g - 1)
                               * (Math.Pow(pc / pSep, (g - 1) / g) - 1), 0)), 1.0001);
        double epsSep = Const.Clamp(AreaRatio(mSep), 1, Eps);
        double cfSep = Math.Sqrt(2 * g * g / (g - 1) * Math.Pow(2 / (g + 1), (g + 1) / (g - 1))
                                 * (1 - Math.Pow(pSep / pc, (g - 1) / g)))
                       + pSep / pc * epsSep;
        return cfSep * Real - pa * epsSep / pc;
    }
    public const double SEP_PR = 0.4;
    public bool Separated(double pcPa, double pa) => pa > 1 && Pe * (pcPa / PC_PA) < SEP_PR * pa;
}
public sealed class Spec {
    public string Name;
    public double Dry, Prop, Len, Dia;
    public EngineSpec Eng;
    public int NEng, NVac, NLand;
    public double Gimbal;
    public static readonly EngineSpec RaptorSL = EngineSpec.Make(2.596e6, 350, 330);
    public static readonly EngineSpec RaptorVac = EngineSpec.Make(2.70e6, 380, 340);
    public static readonly Spec Booster = new() {
        Name = "Super Heavy (B)", Dry = 275e3, Prop = 3650e3, Len = 72.3, Dia = 9,
        Eng = RaptorSL, NEng = 33, NVac = 0, NLand = 3, Gimbal = 13 * Const.D2R,
    };
    public static readonly Spec Ship = new() {
        Name = "Starship (S)", Dry = 85e3, Prop = 1500e3, Len = 52.1, Dia = 9,
        Eng = RaptorSL, NEng = 3, NVac = 3, NLand = 3, Gimbal = 15 * Const.D2R,
    };
    public static Spec Of(Kind k) => k == Kind.Booster ? Booster : Ship;
}
