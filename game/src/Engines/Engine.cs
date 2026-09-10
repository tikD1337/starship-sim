using System;
using System.Collections.Generic;
namespace Starship.Physics;
public sealed class Engine {
    public Vehicle Veh;
    public int Id;
    public string Name;
    public bool IsVac;
    public string Ring;
    public EngineSpec Spec;
    public double MdotNom;
    public EngineParams P = new();
    public Pump Pf, Po;
    public bool On, Failed, Sep;
    public string Reason = "";
    public double Spool, Pc, F, Md, Thr;
    public double TWall = 290, QWall, Burn;
    private const double CHAN_AREA = 0.003, CHAN_DH = 0.0025, COOL_T = 150;
    private const double WALL_LIMIT = 950, BURN_ENG = 2.0e-7;
    public string Anom;
    public Engine(Vehicle veh, int id, string name, bool isVac, string ring) {
        Veh = veh; Id = id; Name = name; IsVac = isVac; Ring = ring;
        Spec = isVac ? Physics.Spec.RaptorVac : Physics.Spec.RaptorSL;
        MdotNom = Spec.Mdot;
        Pf = new Pump(true, this, 1 / (1 + Pump.MR));
        Po = new Pump(false, this, Pump.MR / (1 + Pump.MR));
    }
    private EngineSpec _noz;
    private double _nozK = double.NaN;
    private EngineSpec Nozzle() {
        if (_nozK != P.AeK) {
            _noz = Spec.WithExitArea(P.AeK);
            _nozK = P.AeK;
        }
        return _noz;
    }
    private void Cooling(double dt, double pcPa) {
        if (pcPa < 1e5 || Md < 1) {
            TWall += (290 - TWall) * Math.Min(1, dt / 12);
            QWall = 0;
            return;
        }
        double hg = ChamberHeat.GasSide(Spec.At > 0 ? 2 * Math.Sqrt(Spec.At / Math.PI) : 0.24,
                                        pcPa, EngineSpec.CSTAR);
        double fuel = Md / (1 + Pump.MR);
        double hc = ChamberHeat.CoolantSide(fuel, CHAN_AREA, CHAN_DH, P.Cool);
        double tr = ChamberHeat.Recovery(ChamberHeat.GAS_TC, EngineSpec.GAMMA, ChamberHeat.GAS_PR, 1.0);
        TWall = hg + hc > 1 ? (hg * tr + hc * COOL_T) / (hg + hc) : tr;
        QWall = hg * (tr - TWall);
        double over = TWall - WALL_LIMIT;
        if (over <= 0) return;
        Burn += BURN_ENG * over * over * dt;
        if (Burn >= 1 && !Failed)
            Fail("прогар стенки камеры, " + TWall.ToString("F0") + " K");
    }
    public void Fail(string reason) {
        if (Failed) return;
        Failed = true; On = false; Reason = reason;
        Log.Msg(Veh.Tag + " · " + Name + ": аварийное выключение — " + reason, 3);
    }
    public void Update(double dt, double pa, double thrCmd, bool want) {
        bool run = want && !Failed && Veh.Prop > 0;
        On = run;
        Thr = run ? Const.Clamp(thrCmd, 0, 1) : 0;
        double tgt = run ? Math.Max(P.MinThr / 100, Thr) : 0;
        Spool += (tgt - Spool) * (1 - Math.Exp(-dt / Math.Max(0.05, P.Tau)));
        if (Spool < 0.005) Spool = 0;
        Pf.Update(dt, Spool, run);
        Po.Update(dt, Spool, run);
        double rf = Pf.DP / Pump.NomF.DP, ro = Po.DP / Pump.NomO.DP;
        double q = Const.Clamp(Math.Min(rf, ro), 0, 1.25);
        Pc = Pump.PC_NOM * q;
        double gate = (run || Spool > 0) ? 1 : 0;
        double pcPa = Pc * 1e6;
        EngineSpec noz = Nozzle();
        Sep = noz.Separated(pcPa, pa) && Spool > 0;
        double cf = noz.CfAt(pcPa, pa);
        F = Math.Max(0, cf * pcPa * noz.At * P.Cf) * gate;
        Md = Spec.Mdot * q * gate;
        if (Spool <= 0) { F = 0; Md = 0; Pc = 0; }
        Cooling(dt, Pc * 1e6);
    }
}
public static class ChamberHeat {
    public const double GAS_MU = 1.0e-4, GAS_CP = 2000, GAS_PR = 0.5, GAS_TC = 3500;
    public const double COOL_K = 0.10, COOL_MU = 1.0e-4, COOL_CP = 3500;
    private static readonly double GasProp = Math.Pow(GAS_MU, 0.2) * GAS_CP / Math.Pow(GAS_PR, 0.6);
    private static readonly double CoolPr = Math.Pow(COOL_CP * COOL_MU / COOL_K, 0.4);
    public static double GasSide(double throatD, double pcPa, double cstar) {
        if (pcPa <= 0 || throatD <= 0) return 0;
        double flux = pcPa / cstar;
        return 0.026 / Math.Pow(throatD, 0.2) * GasProp * Math.Pow(flux, 0.8) * 0.8;
    }
    public static double CoolantSide(double fuelFlow, double area, double dh, double valve) {
        double flow = fuelFlow * Math.Max(valve, 0);
        if (flow <= 0 || area <= 0) return 0;
        double g = flow / area;
        double re = g * dh / COOL_MU;
        return 0.023 * Math.Pow(re, 0.8) * CoolPr * COOL_K / dh;
    }
    public static double Recovery(double chamberT, double gamma, double pr, double mach) {
        double k = (gamma - 1) / 2 * mach * mach;
        return chamberT * (1 + Math.Cbrt(pr) * k) / (1 + k);
    }
}
public readonly struct EngStats {
    public readonly double F, Md;
    public readonly int Run, Fail;
    public EngStats(double f, double md, int run, int fail) { F = f; Md = md; Run = run; Fail = fail; }
}
public static class EngineSet {
    public static List<Engine> Make(Vehicle v) {
        var outp = new List<Engine>();
        if (v.Kind == Kind.Booster) {
            for (int i = 0; i < 33; i++) {
                string ring = i < 3 ? "центральный" : (i < 13 ? "средний" : "внешний");
                outp.Add(new Engine(v, i, "S1-" + (i + 1), false, ring));
            }
        }
        else {
            for (int i = 0; i < 3; i++) outp.Add(new Engine(v, i, "S2-" + (i + 1), false, "центральный"));
            for (int i = 0; i < 3; i++) outp.Add(new Engine(v, i + 3, "S2-V" + (i + 1), true, "вакуумный"));
        }
        return outp;
    }
    public static void Update(Vehicle v, double dt, double pa) {
        int n = v.Ign ? v.NEng : 0;
        double thr = v.Ign ? Const.Clamp(v.Throttle, 0, 1) : 0;
        int live = 0;
        foreach (Engine e in v.Eng) {
            bool want = !e.Failed && live < n && v.Ign;
            if (want) live++;
            e.Update(dt, pa, thr, want);
        }
    }
    public static EngStats Stats(Vehicle v) {
        double f = 0, md = 0;
        int run = 0, fail = 0;
        foreach (Engine e in v.Eng) {
            f += e.F; md += e.Md;
            if (e.On) run++;
            if (e.Failed) fail++;
        }
        return new EngStats(f, md, run, fail);
    }
}
