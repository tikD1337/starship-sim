using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Starship.Physics;
namespace Starship.Check;
internal static class Program {
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const char TAB = '\t', NL = '\n';
    private static int Main(string[] args) {
        string what = args.Length > 0 ? args[0] : "atmo";
        switch (what) {
            case "atmo": DumpAtmo(); return 0;
            case "aero": DumpAero(); return 0;
            case "eng": DumpEng(); return 0;
            case "step": DumpStep(); return 0;
            case "guid": DumpGuid(); return 0;
            case "mission": DumpMission(args); return 0;
            default:
                Console.Error.WriteLine($"неизвестный раздел сверки: {what}");
                return 2;
        }
    }
    private static void DumpAtmo() {
        var sb = new StringBuilder();
        Row(sb, "h", "rho", "p", "T", "a");
        foreach (double h in Heights()) {
            Air air = Atmosphere.At(h);
            Row(sb, F(h), F(air.Rho), F(air.P), F(air.T), F(air.A));
        }
        sb.Append(NL);
        Row(sb, "M", "cd0");
        for (double m = 0; m <= 26.0001; m += 0.25)
            Row(sb, F(m), F(Atmosphere.Cd0(m)));
        Console.Out.Write(sb.ToString());
    }
    private static IEnumerable<double> Heights() {
        for (double h = -500; h <= 140000; h += 250) yield return h;
        foreach (double h in new double[] { 11000, 20000, 32000, 47000, 51000, 71000, 84852, 86000, 130000 })
        { yield return h - 1; yield return h; yield return h + 1; }
    }
    private static void DumpAero() {
        var sb = new StringBuilder();
        Row(sb, "kind", "stacked", "alt", "vx", "vy", "th", "fill",
                "mass", "fullLen", "cm", "cp", "inertia", "speed", "vVert", "vHor",
                "q", "mach", "alpha", "CA", "CN", "drag");
        double[] alts = { 1500, 4000, 8000, 12000, 30000, 60000, 80000, 100000 };
        double[] ths = { 0, 0.3, 1.0, 1.6, 2.4, 3.0, -0.7, -2.0 };
        double[] fills = { 1.0, 0.62, 0.19, 0.03 };
        (double vx, double vy)[] vels = { (0, 0), (120, 900), (1800, 2400), (-400, -1500), (5200, 900), (300, -260) };
        foreach (Kind kind in new[] { Kind.Booster, Kind.Ship })
        foreach (bool stacked in new[] { false, true }) {
            if (stacked && kind == Kind.Ship) continue;
            int i = 0;
            foreach (double alt in alts)
            foreach (double th in ths) {
                var (vx, vy) = vels[i % vels.Length];
                double fill = fills[i % fills.Length];
                i++;
                var v = new Vehicle(kind, 60e3);
                if (stacked) { v.Mate = new Vehicle(Kind.Ship, 60e3); v.Stacked = true; }
                v.X = 0;
                v.Y = Const.RE + alt;
                v.Vx = vx;
                v.Vy = vy;
                v.Th = th;
                v.Prop = v.PropMax * fill;
                Air air = Atmosphere.At(v.Alt);
                double sp = v.Speed;
                double mach = sp / air.A;
                double q = 0.5 * air.Rho * sp * sp;
                AeroForce af = Aero.Compute(v, q, mach);
                v.Alpha = af.Alpha;
                Row(sb, kind == Kind.Booster ? "booster" : "ship", stacked ? "1" : "0",
                    F(alt), F(vx), F(vy), F(th), F(fill),
                    F(v.Mass), F(v.FullLen), F(v.Cm), F(v.Cp), F(v.Inertia),
                    F(sp), F(v.VVert), F(v.VHor), F(q), F(mach),
                    F(af.Alpha), F(af.CA), F(af.CN), F(af.Drag));
            }
        }
        Console.Out.Write(sb.ToString());
    }
    private static void DumpEng() {
        var sb = new StringBuilder();
        Row(sb, "kind", "step", "t", "prop", "F", "mdot", "run", "pF", "pO", "copv",
                "rpmF", "dPF", "cavF", "TF", "vibF", "etaF",
                "rpmO", "dPO", "cavO", "TO", "vibO", "pc0", "spool0", "F0");
        foreach (Kind kind in new[] { Kind.Booster, Kind.Ship }) {
            var v = new Vehicle(kind, 60e3);
            v.Ign = true;
            v.NEng = kind == Kind.Booster ? 33 : 6;
            v.Throttle = 1;
            double pa = 101325;
            double dt = 0.02;
            for (int i = 0; i <= 3000; i++) {
                if (i == 1200) v.Throttle = 0.55;
                if (i == 2000) { v.NEng = kind == Kind.Booster ? 3 : 1; v.Throttle = 0.9; }
                if (i == 2600) pa = 0;
                Pressurant.Step(v, dt);
                EngineSet.Update(v, dt, pa);
                EngStats st = EngineSet.Stats(v);
                v.F = st.F; v.Mdot = st.Md; v.NRun = st.Run;
                v.Prop = Math.Max(0, v.Prop - st.Md * dt);
                if (i % 25 != 0) continue;
                Engine e0 = v.Eng[0];
                Row(sb, kind == Kind.Booster ? "booster" : "ship",
                    i.ToString(Inv), F(i * dt), F(v.Prop), F(v.F), F(v.Mdot), st.Run.ToString(Inv),
                    F(v.Tanks.F.P), F(v.Tanks.O.P), F(v.Tanks.Copv),
                    F(e0.Pf.Rpm), F(e0.Pf.DP), F(e0.Pf.Cav), F(e0.Pf.T), F(e0.Pf.Vib), F(e0.Pf.Eta),
                    F(e0.Po.Rpm), F(e0.Po.DP), F(e0.Po.Cav), F(e0.Po.T), F(e0.Po.Vib),
                    F(e0.Pc), F(e0.Spool), F(e0.F));
            }
        }
        Console.Out.Write(sb.ToString());
    }
    private static void DumpStep() {
        var sb = new StringBuilder();
        Row(sb, "case", "i", "t", "x", "y", "vx", "vy", "th", "om", "prop", "F", "q", "mach",
                "alpha", "drag", "gimbal", "flap", "fin", "finDep", "bank", "acc", "alt",
                "speed", "vVert", "vHor", "cm", "cp", "mode");
        for (int cs = 0; cs < 3; cs++) {
            var sim = new SimState { T = 0, RhoK = 1 };
            Kind kind = cs == 0 ? Kind.Booster : Kind.Ship;
            var v = new Vehicle(kind, 60e3);
            sim.Veh.Add(v);
            if (cs == 0) {
                v.Mate = new Vehicle(Kind.Ship, 60e3);
                v.Stacked = true;
                v.Mode = "ascent";
                v.Y = Const.RE + 2000;
                v.Vx = -Const.W * v.Y + 60;
                v.Vy = 210;
                v.Ign = true; v.NEng = 33; v.Throttle = 1;
            }
            else if (cs == 1) {
                v.Mode = "ascent2";
                v.Y = Const.RE + 70000;
                v.Vx = -Const.W * v.Y + 2400;
                v.Vy = 900;
                v.Prop = v.PropMax * 0.8;
                v.Ign = true; v.NEng = 6; v.Throttle = 1;
                v.Th = 1.1;
            }
            else {
                v.Mode = "entryS";
                v.Y = Const.RE + 62000;
                v.Vx = -Const.W * v.Y + 6800;
                v.Vy = -260;
                v.Prop = 60e3;
                v.Th = 1.2; v.BankCmd = 50 * Const.D2R;
                v.Ign = false; v.NEng = 0;
            }
            double dt = 0.01;
            for (int i = 0; i <= 20000; i++) {
                if (cs == 0) v.ThCmd = Math.Min(1.2, 0.0002 * i);
                if (cs == 1) v.ThCmd = 1.1 + 0.00002 * i;
                if (cs == 2) { v.ThCmd = 1.05; if (i == 8000) v.BankCmd = 95 * Const.D2R; }
                if (cs == 0 && i == 12000) { v.Throttle = 0.6; v.NEng = 13; }
                Flight.StepVehicle(sim, v, dt);
                sim.T += dt;
                if (i % 200 != 0) continue;
                Row(sb, cs.ToString(Inv), i.ToString(Inv), F(sim.T),
                    F(v.X), F(v.Y), F(v.Vx), F(v.Vy), F(v.Th), F(v.Om), F(v.Prop), F(v.F),
                    F(v.Q), F(v.Mach), F(v.Alpha), F(v.Drag), F(v.Gimbal), F(v.Flap), F(v.Fin),
                    F(v.FinDep), F(v.Bank), F(v.Acc), F(v.Alt), F(v.Speed), F(v.VVert), F(v.VHor),
                    F(v.Cm), F(v.Cp), v.Mode);
            }
        }
        Console.Out.Write(sb.ToString());
    }
    private static void DumpGuid() {
        var sb = new StringBuilder();
        Row(sb, "case", "kind", "alt", "vx", "vy", "th", "fill", "seekPad", "simT",
                "pitchProg", "apo", "peri", "ecc", "aimRetro", "aimPro", "aimA60",
                "liftRefX", "liftRefUp", "flipDrift", "stopAlt3", "landAim",
                "bbNeed", "bbAim", "predMiss",
                "deorbDv60", "entryMiss", "entryT", "glideBank");
        var sim = new SimState { RhoK = 1 };
        int cs = 0;
        foreach (Kind kind in new[] { Kind.Booster, Kind.Ship })
        foreach (double alt in new double[] { 3000, 40000, 80000, 190000 })
        foreach (double th in new double[] { 0.2, 1.4, 2.6 })
        foreach (bool seek in new[] { true, false }) {
            sim.T = 120 + cs * 7;
            var v = new Vehicle(kind, 60e3);
            double fill = 0.05 + 0.3 * ((cs % 3) / 3.0);
            v.X = 0; v.Y = Const.RE + alt;
            v.Vx = -Const.W * v.Y + (cs % 2 == 0 ? 900 : 5400);
            v.Vy = cs % 4 < 2 ? -420 : 640;
            v.Th = th;
            v.Prop = v.PropMax * fill;
            v.SeekPad = seek;
            v.MissPred = 1800 - cs * 130;
            v.Mode = kind == Kind.Booster ? "landB" : "entryS";
            Air at = Atmosphere.At(v.Alt);
            double sp = v.Speed;
            v.Q = 0.5 * at.Rho * sp * sp; v.Mach = sp / at.A;
            v.Alpha = Aero.Compute(v, v.Q, v.Mach).Alpha;
            Orbit o = Guidance.Orb(v);
            LiftRefV lr = Guidance.LiftRef(v);
            EntryPred ep = Guidance.PredictEntry(sim, v, 0, 62, Const.ENTRY_BANK0);
            Row(sb, cs.ToString(Inv), kind == Kind.Booster ? "booster" : "ship",
                F(alt), F(v.Vx), F(v.Vy), F(th), F(fill), seek ? "1" : "0", F(sim.T),
                F(Guidance.PitchProg(sp)), F(o.Apo), F(o.Peri), F(o.E),
                F(Guidance.AimRetro(v)), F(Guidance.AimPro(v)), F(Guidance.AimAlpha(v, 60)),
                F(lr.X), F(lr.Up), F(Guidance.FlipDrift(v)), F(Guidance.StopAlt(v, 3)),
                F(Guidance.LandAim(sim, v, 40 * Const.D2R, 0)),
                F(Guidance.BbNeed(sim, v)), F(Guidance.BoostbackAim(v)),
                F(Guidance.BoosterMiss(sim, v, 0)),
                F(Guidance.DeorbitDv(v, Const.RE + 60e3)), F(ep.Miss), F(ep.T),
                F(Guidance.GlideBank(2500 - cs * 300, 40, v.Alt, -180, 12, 0.6)));
            cs++;
        }
        Console.Out.Write(sb.ToString());
    }
    private static void DumpMission(string[] args) {
        string mission = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : "orbital";
        double pay = double.NaN, fill = double.NaN, vsep = double.NaN;
        bool quiet = false, deploy = false, anom = false, showLog = false;
        string anomKeys = null;
        uint seed = 12345u;
        int every = 2000;
        bool entDbg = false;
        bool ascDbg = false;
        bool lndDbg = false;
        double secoPeri = double.NaN, windSurf = double.NaN;
        for (int k = 1; k < args.Length; k++) {
            if (args[k] == "--quiet") quiet = true;
            if (args[k] == "--ent") entDbg = true;
            if (args[k] == "--asc") ascDbg = true;
            if (args[k] == "--lnd") lndDbg = true;
            if (args[k] == "--ascka") Const.ASC_KA = double.Parse(args[k + 1], Inv);
            if (args[k] == "--ascmax") Const.ASC_AVMAX = double.Parse(args[k + 1], Inv);
            if (args[k] == "--ascth") Const.ASC_THMAX = double.Parse(args[k + 1], Inv);
            if (args[k] == "--hdrz") Const.HDR_Z = double.Parse(args[k + 1], Inv);
            if (args[k] == "--fliph") Const.FLIP_H = double.Parse(args[k + 1], Inv);
            if (args[k] == "--stoph") Const.LAND_STOP_S = double.Parse(args[k + 1], Inv);
            if (args[k] == "--flipstop") Const.FLIP_STOP = double.Parse(args[k + 1], Inv);
                        if (args[k] == "--omacc") Const.OM_ACC_MAX = double.Parse(args[k + 1], Inv);
            if (args[k] == "--omflip") Const.OM_ACC_FLIP = double.Parse(args[k + 1], Inv);
            if (args[k] == "--secolead") Const.SECO_LEAD = double.Parse(args[k + 1], Inv);
            if (args[k] == "--circtaper") Const.CIRC_TAPER = double.Parse(args[k + 1], Inv);
            if (args[k] == "--circone") Const.CIRC_ONE = double.Parse(args[k + 1], Inv);
            if (args[k] == "--secoperi") secoPeri = double.Parse(args[k + 1], Inv);
            if (args[k] == "--entprop") Const.ENTRY_PROP = double.Parse(args[k + 1], Inv);
            if (args[k] == "--glka") Const.GLIDE_KA = double.Parse(args[k + 1], Inv);
            if (args[k] == "--driftk") Const.FLIP_DRIFT_K = double.Parse(args[k + 1], Inv);
            if (args[k] == "--alat") Const.LAND_ALAT = double.Parse(args[k + 1], Inv);
            if (args[k] == "--windk") Const.LAND_WIND_K = double.Parse(args[k + 1], Inv);
            if (args[k] == "--kdamp") Const.LAND_KDAMP = double.Parse(args[k + 1], Inv);
            if (args[k] == "--wind") windSurf = double.Parse(args[k + 1], Inv);
            if (args[k] == "--tiltend") Const.LAND_TILT_END = double.Parse(args[k + 1], Inv);
            if (args[k] == "--dhend") Const.LAND_DH_END = double.Parse(args[k + 1], Inv);
            if (args[k] == "--klat") Const.LAND_KLAT = double.Parse(args[k + 1], Inv);
            if (args[k] == "--anomkey") anomKeys = args[k + 1];
            if (args[k] == "--asckv") Const.ASC_KV = double.Parse(args[k + 1], Inv);
            if (args[k] == "--log") showLog = true;
            if (args[k] == "--deploy") deploy = true;
            if (args[k] == "--anom") anom = true;
            if (k + 1 >= args.Length) continue;
            if (args[k] == "--pay") pay = double.Parse(args[k + 1], Inv);
            if (args[k] == "--vsep") vsep = double.Parse(args[k + 1], Inv);
            if (args[k] == "--fill") fill = double.Parse(args[k + 1], Inv);
            if (args[k] == "--seed") seed = uint.Parse(args[k + 1], Inv);
            if (args[k] == "--every") every = int.Parse(args[k + 1], Inv);
            if (args[k] == "--kb") Const.ENTRY_KB = double.Parse(args[k + 1], Inv);
            if (args[k] == "--deoaim") Const.DEO_AIM = double.Parse(args[k + 1], Inv);
            if (args[k] == "--hold") Const.HOLD_MAX = double.Parse(args[k + 1], Inv);
            if (args[k] == "--tlag") Const.LAND_TLAG = double.Parse(args[k + 1], Inv);
            if (args[k] == "--klat") Const.LAND_KLAT = double.Parse(args[k + 1], Inv);
            if (args[k] == "--dhpd") Const.LAND_DHPD = double.Parse(args[k + 1], Inv);
            if (args[k] == "--tiltn") Const.LAND_TILT_NEAR = double.Parse(args[k + 1], Inv);
            if (args[k] == "--trimk") Const.ENTRY_KT = double.Parse(args[k + 1], Inv);
            if (args[k] == "--trimhi") Const.ENTRY_TRIM_HI = double.Parse(args[k + 1], Inv);
            if (args[k] == "--trimlo") Const.ENTRY_TRIM_LO = double.Parse(args[k + 1], Inv);
            if (args[k] == "--proj") Const.LAND_PROJ = double.Parse(args[k + 1], Inv);
            if (args[k] == "--thrmin") Const.LAND_THR_MIN = double.Parse(args[k + 1], Inv);
        }
        Log.Sink = (m, lv) => Console.Error.WriteLine("ENG " + m);
        var sim = new SimState { Mission = mission, AnomOn = anom || anomKeys != null };
        if (anomKeys != null)
            sim.AnomScript = new System.Collections.Generic.HashSet<string>(anomKeys.Split(','));
        if (mission == "high") { sim.TargetApo = 520e3; sim.TargetPeri = 500e3; }
        else if (mission == "trans") { sim.TargetApo = 180e3; sim.TargetPeri = 160e3; }
        Physics.Sim.Reset(sim, seed);
        if (anom)
            Console.Error.WriteLine("ANOM seed=" + seed + " list=" + string.Join(" | ", sim.Anom.List));
        if (!double.IsNaN(secoPeri)) sim.SecoPeri = secoPeri;
        if (!double.IsNaN(windSurf)) sim.Wind = Wind.Steady(windSurf);
        if (!double.IsNaN(fill)) sim.MecoFill = fill;
        if (!double.IsNaN(vsep)) sim.MecoV = vsep;
        if (!double.IsNaN(pay)) { sim.Payload = pay; Physics.Sim.MakeVehicles(sim); }
        var sb = new StringBuilder();
        Row(sb, "i", "t", "bMode", "bAlt", "bDR", "bV", "bProp", "bTh", "bQ",
                "sMode", "sAlt", "sDR", "sV", "sProp", "sTh", "sQ", "sTile");
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        const double dt = 0.01;
        double sepV = double.NaN, apoMax = 0, qPeak = 0, aPeak = 0, bkPeak = 0, cmPeak = 0, mPeak = 0;
        double orbPeri = double.NaN, orbApo = double.NaN;
        double burnT = 0, holdT = 0, sBurnT = 0, omMax = 0, omSum = 0, devMax = 0;
        double finMax = 0, flapMax = 0;
        double sTouch = double.NaN, bTouch = double.NaN, sVvPrev = 0, bVvPrev = 0;
        double sVhFlip = 0, sHoverT = 0, sTiltMax = 0;
        int sCut = 0;
        var cuts = new List<string>();
        double errMax = 0, errSum = 0, errT = 0; int omSign = 0, flips = 0;
        double tbMax = 0, vbMax = 0, wrMax = 0, pcMin = 1e9, pfMin = 1e9, poMin = 1e9, copvMin = 1e9;
        int i = 0;
        for (; i < 1400000; i++) {
            if ((b.Landed || b.Crashed) && (s.Landed || s.Crashed)) break;
            Physics.Sim.Tick(sim, dt);
            if (deploy && s.Mode == "orbit" && s.BayS != null && s.BayS.Sats > 0) {
                if (s.BayS.Want < 0.5) Physics.Sim.BaySet(sim, s, true);
                else if (s.BayS.Open >= 0.75) Physics.Sim.DeploySat(sim, s, s.BayS.Sats);
            }
            if (double.IsNaN(sepV) && !s.Attached) sepV = b.Speed;
            if (s.Mode == "entryS" && Math.Abs(s.Flap) > flapMax) flapMax = Math.Abs(s.Flap);
            if (b.Mode == "landB" && b.Ign) burnT += dt;
            if (b.Mode == "coastB" || b.Mode == "landB") {
                double om = Math.Abs(b.Om) * Const.R2D;
                if (om > omMax) omMax = om;
                if (Math.Abs(b.Fin) > finMax) finMax = Math.Abs(b.Fin);
                omSum += Math.Abs(b.Om) * dt * Const.R2D;
                double dev = Math.Abs(Vehicle.AngDiff(b.Th, Math.PI)) * Const.R2D;
                if (dev > devMax) devMax = dev;
                if (Math.Sign(b.Om) != 0 && Math.Sign(b.Om) != omSign) { omSign = Math.Sign(b.Om); flips++; }
                double e2 = Math.Abs(Vehicle.AngDiff(b.ThCmd, b.Th)) * Const.R2D;
                if (e2 > errMax) errMax = e2;
                errSum += e2 * dt; errT += dt;
            }
            if (b.Mode == "landB" && b.Alt - Const.CATCH_H < 40 && !b.Landed) holdT += dt;
            if (s.Mode == "landS" && s.Ign) sBurnT += dt;
            if (s.Mode == "flipS" || s.Mode == "landS") {
                double vh = Math.Abs(s.VHor);
                if (vh > sVhFlip) sVhFlip = vh;
                double tl = Math.Abs(Vehicle.AngDiff(s.Th, 0)) * Const.R2D;
                if (s.Alt < 400 && tl > sTiltMax) sTiltMax = tl;
            }
            if (s.Mode == "landS" && s.Alt - Const.CATCH_H < 60 && !s.Landed) sHoverT += dt;
            if (s.Mode == "landS" && s.Ign && s.NRun != sCut && s.NRun > 0) {
                sCut = s.NRun;
                cuts.Add($"{s.NRun}дв@T+{sim.T:F0}/H{s.Alt:F0}");
            }
            if (s.Landed && double.IsNaN(sTouch)) sTouch = sVvPrev;
            if (!s.Landed && !s.Crashed) sVvPrev = s.VVert;
            if (b.Landed && double.IsNaN(bTouch)) bTouch = bVvPrev;
            if (!b.Landed && !b.Crashed) bVvPrev = b.VVert;
            if (!s.Attached && s.Q > qPeak) {
                qPeak = s.Q; aPeak = s.Alpha * Const.R2D; bkPeak = s.Bank * Const.R2D;
                cmPeak = s.Cm; mPeak = s.Mass;
            }
            if (!s.Attached && s.Alt > 60e3) {
                Orbit ob = Guidance.Orb(s);
                if (ob.Apo > apoMax && ob.Apo < 5e7) apoMax = ob.Apo;
                if (s.Mode == "orbit" && s.F < 1e3 && double.IsNaN(orbPeri)) { orbPeri = ob.Peri; orbApo = ob.Apo; }
            }
            if (ascDbg && (s.Mode == "ascent2" || s.Mode == "coastS" || s.Mode == "circ") && i % 500 == 0) {
                Orbit oo = Guidance.Orb(s);
                Console.Error.WriteLine($"ASC t={sim.T:F0} {s.Mode} h={(s.Alt/1000):F1} v={s.Speed:F0}"
                    + $" peri={(oo.Peri/1000):F0} apo={(oo.Apo/1000):F0} prop={(s.Prop/1000):F1}"
                    + $" th={(s.Th*Const.R2D):F0} vv={s.VVert:F0}");
            }
            if (entDbg && s.Mode == "entryS" && i % every == 0)
                Console.Error.WriteLine($"ENT t={sim.T:F0} h={(s.Alt/1000):F1} v={s.Speed:F0}"
                    + $" q={(s.Q/1000):F1} a={(s.Alpha*Const.R2D):F1} ac={s.AlphaCmd:F1}"
                    + $" bk={(s.Bank*Const.R2D):F0} bc={(s.BankCmd*Const.R2D):F0}"
                    + $" em={(s.EntMiss/1000):F0} dr={(sim.Downrange(s)/1000):F0} cm={s.Cm:F2} k={s.EntK:F3}"
                    + $" flap={s.Flap:F2}");
            if (lndDbg && b.Mode == "landB" && i % 50 == 0)
                Console.Error.WriteLine($"LNDB t={sim.T:F1} h={b.Alt:F0} vv={b.VVert:F1}"
                    + $" vh={b.VHor:F1} dr={sim.Downrange(b):F1} th={(b.Th*Const.R2D):F2}"
                    + $" tc={(b.ThCmd*Const.R2D):F2} n={b.NRun} thr={b.Throttle:F2}"
                    + $" wind={b.WindE:F1} q={(b.Q/1000):F1} aoa={(b.AoaDev*Const.R2D):F1}"
                    + $" bank={(b.Bank*Const.R2D):F0} fin={b.Fin:F2} v={b.Speed:F0}");
            if (lndDbg && (s.Mode == "entryS" && s.Alt < 6000 || s.Mode == "flipS" || s.Mode == "landS")
                && i % 50 == 0)
                Console.Error.WriteLine($"LND t={sim.T:F1} {s.Mode} h={s.Alt:F0} vv={s.VVert:F1}"
                    + $" vh={s.VHor:F1} dr={sim.Downrange(s):F0} th={(s.Th*Const.R2D):F1}"
                    + $" tc={(s.ThCmd*Const.R2D):F1} n={s.NRun} thr={s.Throttle:F2}"
                    + $" a={(s.Alpha*Const.R2D):F0} q={(s.Q/1000):F1}");
            if (i % 100 == 0)
                foreach (Vehicle v in sim.Veh) {
                    if (v.Tanks == null) continue;
                    pfMin = Math.Min(pfMin, v.Tanks.F.P / 1000);
                    poMin = Math.Min(poMin, v.Tanks.O.P / 1000);
                    if (v.Tanks.Copv0 > 0) copvMin = Math.Min(copvMin, v.Tanks.Copv / v.Tanks.Copv0 * 100);
                    foreach (Engine e in v.Eng) {
                        if (!e.On) continue;
                        tbMax = Math.Max(tbMax, e.Pf.T);
                        vbMax = Math.Max(vbMax, Math.Max(e.Pf.Vib, e.Po.Vib));
                        wrMax = Math.Max(wrMax, Math.Max(e.Pf.Wear, e.Po.Wear));
                        pcMin = Math.Min(pcMin, e.Pc);
                    }
                }
            if (i % every != 0) continue;
            Row(sb, i.ToString(Inv), F(sim.T),
                b.Mode, F(b.Alt), F(sim.Downrange(b)), F(b.Speed), F(b.Prop), F(b.Th), F(b.Q),
                s.Mode, F(s.Alt), F(sim.Downrange(s)), F(s.Speed), F(s.Prop), F(s.Th), F(s.Q), F(s.TTile));
        }
        Row(sb, "END", F(sim.T), b.Mode, F(b.Alt), F(sim.Downrange(b)), F(b.Speed), F(b.Prop), F(b.Th), F(b.Q),
                s.Mode, F(s.Alt), F(sim.Downrange(s)), F(s.Speed), F(s.Prop), F(s.Th), F(s.Q), F(s.TTile));
        Console.Error.WriteLine($"{mission}: шагов {i}, Б {b.Mode} {sim.Downrange(b):F0} м, К {s.Mode} {(sim.Downrange(s) / 1000):F0} км");
        Console.Error.WriteLine($"SUM pay={sim.Payload / 1000:F1}t vsep={sim.MecoV:F0} fill={sim.MecoFill:F3} " +
                                $"sepV={sepV:F0} apoMax={apoMax / 1000:F0}km " +
                                (double.IsNaN(orbPeri) ? "" : $"orbit={orbPeri / 1000:F0}x{orbApo / 1000:F0}km ") +
                                $"bProp={b.Prop / 1000:F1}t bMaxQ={b.MaxQ / 1000:F0}kPa bMaxG={b.MaxG:F1} " +
                                $"sProp={s.Prop / 1000:F1}t sMaxQ={s.MaxQ / 1000:F0}kPa sMaxTile={s.MaxTile:F0}K " +
                                $"sMaxLee={s.MaxLee:F0}K sDmg={s.Dmg:F3} sEntK={s.EntK:F3} " +
                                $"bMode={b.Mode} bMiss={sim.Downrange(b):F1}m sMode={s.Mode} sMiss={sim.Downrange(s) / 1000:F1}km");
        Console.Error.WriteLine($"PEAK sQ={qPeak / 1000:F1}kPa alpha={aPeak:F1}deg bank={bkPeak:F1}deg " +
                                $"cm={cmPeak:F2}m mass={mPeak / 1000:F1}t sDry={s.Dry / 1000:F1}t sats={s.BayS?.Sats}");
        Console.Error.WriteLine($"BURN bBurn={burnT:F1}s bHold={holdT:F1}s sBurn={sBurnT:F1}s " +
                                $"bTouch={Math.Abs(bTouch):F2}m/s sTouch={Math.Abs(sTouch):F2}m/s");
        Console.Error.WriteLine($"FLIP sVhMax={sVhFlip:F1}m/s sHover={sHoverT:F1}s sTiltMax={sTiltMax:F1}deg");
        Console.Error.WriteLine($"ZONE tb={tbMax:F0}K vib={vbMax:F2}g wear={wrMax:F3} pc={pcMin:F1}MPa "
                              + $"pf={pfMin:F0}kPa po={poMin:F0}kPa copv={copvMin:F1}%");
        Console.Error.WriteLine("CUTS " + (cuts.Count > 0 ? string.Join(" ", cuts) : "нет"));
        Console.Error.WriteLine($"SPIN bOmMax={omMax:F1}deg/s bTurnTotal={omSum:F0}deg bTiltMax={devMax:F0}deg " +
                                $"flips={flips} errMax={errMax:F0}deg errAvg={(errT > 0 ? errSum / errT : 0):F1}deg " +
                                $"finMax={finMax:F2} flapMax={flapMax:F2}");
        if (showLog)
            for (int k = sim.Log.Count - 1; k >= 0; k--)
                Console.Error.WriteLine($"LOG T+{sim.Log[k].T:F0} {sim.Log[k].M}");
        if (!quiet) Console.Out.Write(sb.ToString());
    }
    private static void Row(StringBuilder sb, params string[] cols) {
        for (int i = 0; i < cols.Length; i++) {
            if (i > 0) sb.Append(TAB);
            sb.Append(cols[i]);
        }
        sb.Append(NL);
    }
    private static string F(double v) => v.ToString("R", Inv);
}
