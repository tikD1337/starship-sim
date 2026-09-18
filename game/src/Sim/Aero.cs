using System;
namespace Starship.Physics;
public readonly struct AeroForce {
    public readonly double Alpha, CA, CN, Fax, Fsd, Drag;
    public AeroForce(double alpha, double ca, double cn, double fax, double fsd, double drag) {
        Alpha = alpha; CA = ca; CN = cn; Fax = fax; Fsd = fsd; Drag = drag;
    }
}
public static class Aero {
    public static AeroForce Compute(Vehicle v, double q, double mach) => Compute(v, v.VAir, q, mach);
    public static AeroForce Compute(Vehicle v, Vec2 vr, double q, double mach) {
        Vec2 ax = v.Axis, sd = v.Side;
        double sp = vr.Len;
        double va = 1, vs = 0, alpha = 0;
        if (sp > 1) {
            va = (vr.X * ax.X + vr.Y * ax.Y) / sp;
            vs = (vr.X * sd.X + vr.Y * sd.Y) / sp;
            alpha = Math.Atan2(vs, va);
        }
        double l = v.FullLen, a = v.A;
        double sa = Math.Sin(alpha), ca = Math.Cos(alpha);
        double CA = Atmosphere.CdAxial(mach, va) * ca * ca + 0.06;
        double CN = 2 * sa * Math.Abs(ca) + 1.15 * (l * v.Dia / a) * sa * Math.Abs(sa);
        double fax = -q * a * CA * Math.Sign(va != 0 ? va : 1);
        double fsd = -q * a * CN;
        return new AeroForce(alpha, CA, CN, fax, fsd, Math.Sqrt(fax * fax + fsd * fsd));
    }
    public static Vec2 World(Vehicle v, in AeroForce f, Vec2 vr) {
        Vec2 ax = v.Axis, sd = v.Side;
        double fx = f.Fax * ax.X + f.Fsd * sd.X, fy = f.Fax * ax.Y + f.Fsd * sd.Y, sp = vr.Len;
        if (v.Bank > 0.01 && sp > 1) {
            double dx = vr.X / sp, dy = vr.Y / sp;
            double alng = fx * dx + fy * dy;
            double cb = Math.Cos(v.Bank);
            fx = alng * dx + (fx - alng * dx) * cb; fy = alng * dy + (fy - alng * dy) * cb;
        }
        return new Vec2(fx, fy);
    }
    public static double Heat(double rho, double speed)
        => 1.83e-4 * Math.Sqrt(rho / 4.5) * Math.Pow(speed, 3) / 1000;
}
