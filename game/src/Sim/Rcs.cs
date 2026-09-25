using System;
namespace Starship.Physics;
public static class Rcs {
    public static double Nominal(Vehicle v) => v.Kind == Kind.Booster ? 2.4e6 : 1.1e6;
    public static double Span(Vehicle v) {
        (double oz, _) = Propellant.TankOf(v, true);
        (double fz, double fh) = Propellant.TankOf(v, false);
        return fz + fh - oz;
    }
    private static double Choke(double g) => Math.Sqrt(g) * Math.Pow(2 / (g + 1), (g + 1) / (2 * (g - 1)));
    public static double Cstar(Tank t) => Math.Sqrt(t.R * Const.GAS_T) / Choke(t.G);
    public static double Cf(Tank t) {
        double g = t.G, pr = Math.Pow(2 / (g + 1), g / (g - 1));
        return Choke(g) * Math.Sqrt(2 * g / (g - 1) * (1 - Math.Pow(pr, (g - 1) / g))) + pr;
    }
    public static double Isp(Tank t) => Cstar(t) * Cf(t) / Const.G0;
    public static double Thrust(Tank t, double nominal, double pa)
        => nominal * Math.Max(0, Cf(t) * t.P - pa) / (Cf(t) * t.P0);
    public static double Flow(Tank t, double force, double pa) {
        double net = Cf(t) * t.P - pa;
        return force <= 0 || net <= 0 ? 0 : force * t.P / (Cstar(t) * net);
    }
    public static double Auth(Vehicle v) {
        if (!v.Rcs) return 0;
        double f = Nominal(v) / Span(v);
        return Math.Min(Thrust(v.Tanks.F, f, v.Pa), Thrust(v.Tanks.O, f, v.Pa)) * Span(v) * v.RcsK;
    }
    public static double UllageF(Vehicle v) => v.Rcs ? Thrust(v.Tanks.O, Nominal(v) / v.Len, v.Pa) * v.RcsK : 0;
    public static void Draw(Vehicle v, double torque, double ull, double dt) {
        double f = Math.Abs(torque) / Span(v);
        double mf = Math.Min(Flow(v.Tanks.F, f, v.Pa) * dt, v.Tanks.F.Mg);
        double mo = Math.Min((Flow(v.Tanks.O, f, v.Pa) + Flow(v.Tanks.O, ull, v.Pa)) * dt, v.Tanks.O.Mg);
        v.Tanks.F.Mg -= mf; v.Tanks.O.Mg -= mo;
        v.RcsGas += mf + mo;
    }
}
