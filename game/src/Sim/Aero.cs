using System;
namespace Starship.Physics;
public readonly struct AeroForce {
    public readonly double Alpha, CA, CN, Fax, Fsd, Drag;
    public AeroForce(double alpha, double ca, double cn, double fax, double fsd, double drag) {
        Alpha = alpha; CA = ca; CN = cn; Fax = fax; Fsd = fsd; Drag = drag;
    }
}
public static class Aero {
    public static AeroForce Compute(Vehicle v, double q, double mach) {
        Vec2 ax = v.Axis, sd = v.Side, vr = v.VAir;
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
    public static double Heat(double rho, double speed)
        => 1.83e-4 * Math.Sqrt(rho / 4.5) * Math.Pow(speed, 3) / 1000;
}
