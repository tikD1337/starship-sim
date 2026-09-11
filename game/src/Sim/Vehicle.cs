using System;
namespace Starship.Physics;
public sealed class Vehicle {
    public Kind Kind;
    public Spec Spec;
    public string Name, Tag;
    public double Dry, Prop, PropMax, Len, Dia, A;
    public bool Stacked;
    public Vehicle Mate;
    public double X, Y, Vx, Vy;
    public double Th, Om, ThCmd;
    public double Bank, BankCmd;
    public double Throttle = 1;
    public int NEng;
    public bool Ign;
    public double Gimbal, Flap, Fin, FinDep;
    public bool Alive = true, Landed, Crashed, Launched, Attached;
    public string Mode = "idle";
    public double F, Mdot, Q, Mach, Acc, Alpha, Heat, Rho = 1.225, Pa = Const.P0, Drag;
    public double MaxQ, MaxG, MaxHeat, MaxAlt;
    public double Hdr, HdrLeft;
    public double PeriPrev = -1e12;
    public System.Collections.Generic.List<Engine> Eng;
    public TankSet Tanks;
    public double EngAcc;
    public int NRun;
    public double DryK = 1, CopvK = 1, CtrlK = 1, TileK = 1;
    public double EntK = Const.ENTRY_K0;
    public bool SeekPad, Caught;
    public bool Rcs = true;
    public double RcsK = 1, RcsUse, AoaDev, Tmr, HoldT, CutT;
    public bool Venting, BurnLogged, IgnBurn;
    public double MissPred = double.NaN, PredAcc;
    public System.Collections.Generic.Dictionary<string, double> Sgn = new();
    public double DeoAcc, DvBurn, EntAcc, AlphaCmd = 62;
    public double DeoMiss = double.NaN, DeoLeft = double.NaN, EntMiss = double.NaN;
    public bool DeoAuto;
    public double TTile = 290, TSkin = 290, TLee = 290, MaxTile, MaxLee, Dmg;
    public Bay BayS;
    public string Anom;
    public double GimLim = double.NaN;
    public double Kp = 1.1, Kd = 2.4;
    public Vehicle(Kind kind, double payload) {
        Kind = kind;
        Spec = Spec.Of(kind);
        Name = Spec.Name;
        Tag = kind == Kind.Booster ? "Б" : "К";
        Dry = Spec.Dry + (kind == Kind.Ship ? payload : 0);
        PropMax = Spec.Prop;
        Prop = Spec.Prop;
        Len = Spec.Len;
        Dia = Spec.Dia;
        A = Math.PI * Spec.Dia * Spec.Dia / 4;
        X = 0; Y = Const.RE;
        Hdr = kind == Kind.Ship ? 42e3 : 0;
        HdrLeft = Hdr;
        Tanks = TankSet.Make(kind, Spec.Prop);
        Eng = EngineSet.Make(this);
    }
    public bool OnHeader => Kind == Kind.Ship && HdrLeft > 0 && (Mode == "flipS" || Mode == "landS");
    public double Mass => Dry + Prop + (Stacked && Mate != null ? Mate.Dry + Mate.Prop : 0);
    public double FullLen => Len + (Stacked && Mate != null ? Mate.Len : 0);
    public double R => Math.Sqrt(X * X + Y * Y);
    public double Alt => R - Const.RE;
    public double Fill => Prop / PropMax;
    public Vec2 Up { get { double r = R; return new Vec2(X / r, Y / r); } }
    public Vec2 East { get { Vec2 u = Up; return new Vec2(-u.Y, u.X); } }
    public Vec2 Axis {
        get {
            Vec2 u = Up, e = East;
            double s = Math.Sin(Th), c = Math.Cos(Th);
            return new Vec2(u.X * c + e.X * s, u.Y * c + e.Y * s);
        }
    }
    public Vec2 Side {
        get {
            Vec2 u = Up, e = East;
            double s = Math.Sin(Th), c = Math.Cos(Th);
            return new Vec2(-u.X * s + e.X * c, -u.Y * s + e.Y * c);
        }
    }
    public double WindE;
    public Vec2 VRel => new(Vx + Const.W * Y, Vy - Const.W * X);
    public Vec2 VAir {
        get {
            if (WindE == 0) return VRel;
            Vec2 e = East, r = VRel;
            return new Vec2(r.X - WindE * e.X, r.Y - WindE * e.Y);
        }
    }
    public double Speed => VRel.Len;
    public double VVert { get { Vec2 u = Up, v = VRel; return v.X * u.X + v.Y * u.Y; } }
    public double VHor { get { Vec2 u = Up, v = VRel; return v.X * (-u.Y) + v.Y * u.X; } }
    public double Cm {
        get {
            double mp = Prop;
            double pay = Math.Max(0, Dry - Spec.Dry);
            double md = Dry - pay;
            double dryF = Kind == Kind.Booster ? 0.38 : 0.44;
            double hd = Math.Min(HdrLeft, mp), mn = mp - hd;
            double mfMax = Math.Max(PropMax - Hdr, 1);
            double num = md * dryF * Len + pay * 0.5 * (Const.BAY_Z0 + Const.BAY_Z1)
                       + mn * (0.12 * Len + 0.38 * Len * (mn / mfMax))
                       + hd * Const.HDR_Z * Len;
            double den = Dry + mp;
            if (Stacked && Mate != null) {
                double m2 = Mate.Dry + Mate.Prop;
                num += m2 * (Len + 0.45 * Mate.Len);
                den += m2;
            }
            return num / den;
        }
    }
    public double Cp {
        get { double c = Math.Cos(Alpha); return FullLen * (0.5 + 0.16 * c * Math.Abs(c)); }
    }
    public double Inertia { get { double l = FullLen; return Mass * l * l / 12; } }
    public int MaxEng => Kind == Kind.Booster ? 33 : 6;
    public bool Stowed;
    public double StowT = double.NaN;
    public bool Hauled;
    public double CatchPinY => Len - (Kind == Kind.Booster ? 13.0 : 8.0);
    public static double AngDiff(double a, double b) {
        double d = (a - b) % (2 * Math.PI);
        if (d > Math.PI) d -= 2 * Math.PI;
        if (d < -Math.PI) d += 2 * Math.PI;
        return d;
    }
}
