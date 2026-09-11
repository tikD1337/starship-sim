using System;
namespace Starship.Physics;
public static class Guide {
    public static void Separate(SimState sim) {
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        Vec2 ax = b.Axis;
        s.Attached = false; b.Stacked = false;
        double gap = b.Len + Const.SEP_GAP;
        s.X = b.X + ax.X * gap; s.Y = b.Y + ax.Y * gap;
        s.Vx = b.Vx + ax.X * 2.5; s.Vy = b.Vy + ax.Y * 2.5;
        b.Vx -= ax.X * 1.2; b.Vy -= ax.Y * 1.2;
        s.Th = b.Th; s.Om = b.Om * 0.5; s.ThCmd = b.Th;
        b.Om -= 0.02;
        s.Mode = "ascent2"; b.Mode = "flip"; b.Tmr = 0; b.Ign = false; b.NEng = 0;
        sim.LogMsg("РАЗДЕЛЕНИЕ СТУПЕНЕЙ", 2);
        sim.Focus = "ship";
        sim.OnSeparate?.Invoke();
    }
    public static void Step(SimState sim, Vehicle v, double dt) {
        if (v.Landed || !v.Alive) return;
        Orbit o = Guidance.Orb(v);
        double h = v.Alt, sp = v.Speed;
        switch (v.Mode) {
        case "ascent": {
            v.NEng = 33; v.Ign = true;
            v.ThCmd = sim.T < 7 ? 0 : Guidance.PitchProg(sp);
            v.Throttle = 1;
            if (v.Q > 32e3) v.Throttle = 0.74;
            if (v.Q > 25e3 && v.Q <= 32e3 && sim.T > 40) v.Throttle = 0.86;
            if (v.Q < 18e3 && sim.T > 75) v.Throttle = 1;
            if (v.Acc > 3.6) v.Throttle = Const.Clamp(v.Throttle * 3.6 / v.Acc, 0.4, 1);
            if (v.Q < 8e3 && sim.T > 60) sim.Once("maxq-pass");
            if (v.MaxQ > 1e3 && v.Q < v.MaxQ * 0.85)
                sim.Once("maxq", () => sim.LogMsg($"Max Q пройден — {(v.MaxQ / 1000):F1} кПа на H={(h / 1000):F1} км", 1));
            if (v.Prop <= sim.MecoFill * v.PropMax || sp > sim.MecoV) {
                v.Mode = "meco"; v.Tmr = 0;
                sim.LogMsg($"MECO: отсечка маршевых. V={sp:F0} м/с, H={(h / 1000):F1} км", 2);
            }
            break;
        }
        case "meco": {
            v.Tmr += dt; v.NEng = 3; v.Throttle = 0.4;
            Vehicle s = v.Mate;
            if (v.Tmr > 0.6 && !s.Ign) {
                s.Ign = true; s.NEng = 6; s.Throttle = 1;
                sim.LogMsg("Запуск двигателей корабля — горячее разделение", 1);
            }
            if (v.Tmr > 2.2) Separate(sim);
            break;
        }
        case "flip": {
            v.Ign = false; v.NEng = 0; v.Tmr += dt;
            v.ThCmd = Guidance.BoostbackAim(v);
            if (v.Tmr > 4 || Math.Abs(Vehicle.AngDiff(v.ThCmd, v.Th)) < 12 * Const.D2R) {
                v.Mode = "boostback"; v.Ign = true; v.NEng = 13; v.Throttle = 1;
                sim.LogMsg("Б: разворот выполнен, включение 13 двигателей — тормозной импульс", 1);
            }
            break;
        }
        case "boostback": {
            double near = double.IsNaN(v.MissPred) ? 1e9 : Math.Abs(v.MissPred);
            v.PredAcc += dt;
            if (v.PredAcc > (near < 40e3 ? 0.05 : 0.25) || double.IsNaN(v.MissPred)) {
                v.PredAcc = 0; v.MissPred = Guidance.BoosterMiss(sim, v, 0);
            }
            v.ThCmd = Guidance.BoostbackAim(v);
            if (near < 25e3) { v.NEng = 3; v.Throttle = near < 4e3 ? 0.4 : 1; }
            if (near < 250 || v.Prop < 0.025 * v.PropMax) {
                v.Ign = false; v.NEng = 0; v.Throttle = 1; v.Mode = "coastB";
                sim.LogMsg($"Б: конец тормозного импульса, топливо {(v.Prop / 1000):F0} т, расчётный промах {Mp(v):F0} м", 1);
            }
            break;
        }
        case "coastB": {
            v.Ign = false;
            DescentB(sim, v, dt);
            if (h < 40e3) v.Mode = "landB";
            break;
        }
        case "landB": {
            if (v.SeekPad && h < 3500 && Math.Abs(sim.Downrange(v)) > 2000) {
                v.SeekPad = false;
                sim.LogMsg($"Б: башня недосягаема ({(sim.Downrange(v) / 1000):F1} км) — посадка вне площадки", 2);
            }
            if (!v.IgnBurn) DescentB(sim, v, dt);
            Guidance.LandingBurn(sim, v, dt, v.Spec.NLand);
            break;
        }
        case "ascent2": {
            v.Ign = true; v.NEng = 6; v.Throttle = 1;
            if (v.Acc > 3.6) v.Throttle = Const.Clamp(3.6 / v.Acc, 0.45, 1);
            double aV = Const.Clamp(Const.ASC_KA * (sim.TargetApo - o.Apo) - Const.ASC_KV * v.VVert,
                                    -Const.ASC_AVMAX, Const.ASC_AVMAX);
            double aMax = Math.Max(v.F / v.Mass, 0.1);
            double vhIn = (v.X * v.Vy - v.Y * v.Vx) / v.R;
            double gEff = Const.MU / (v.R * v.R) - vhIn * vhIn / v.R;
            double cmd = Math.Acos(Const.Clamp((gEff + aV) / aMax, -1, 1));
            cmd = Const.Clamp(cmd, 25 * Const.D2R, Const.ASC_THMAX * Const.D2R);
            v.ThCmd += Const.Clamp(cmd - v.ThCmd, -0.05 * dt, 0.05 * dt);
            double periRate = v.PeriPrev < -1e11 ? 0 : (o.Peri - v.PeriPrev) / Math.Max(dt, 1e-6);
            v.PeriPrev = o.Peri;
            double periLead = o.Peri + Math.Max(0, periRate) * Const.SECO_LEAD;
            if (periLead >= sim.SecoPeri || v.Prop < 0.045 * v.PropMax) {
                v.Ign = false; v.NEng = 0; v.Mode = "coastS"; v.Throttle = 0;
                double vt = Math.Sqrt(v.Vx * v.Vx + v.Vy * v.Vy);
                sim.LogMsg($"SECO-1: {(o.Peri / 1000):F0} x {(o.Apo / 1000):F0} км, V={vt:F0} м/с", 2);
            }
            break;
        }
        case "coastS": {
            v.Ign = false; v.ThCmd = Guidance.AimPro(v);
            double vrIn = v.Vx * v.Up.X + v.Vy * v.Up.Y;
            if (vrIn < 0 && h < 200e3) Guidance.VentProp(sim, v, dt);
            if (sim.Mission == "trans") {
                if (vrIn < 0 && h < 130e3) {
                    v.Mode = "entryS";
                    sim.LogMsg("К: трансатмосферный профиль — заход на вход в атмосферу", 2);
                }
                break;
            }
            if (o.Peri >= sim.TargetPeri - 8e3) {
                v.Mode = "orbit";
                break;
            }
            if (vrIn < 5 && h > 100e3) {
                v.Mode = "circ"; v.Ign = true; v.NEng = 3; v.Throttle = 1; v.PeriPrev = -1e12;
                sim.LogMsg("Круговое довыведение: включение трёх вакуумных двигателей", 1);
            }
            if (h < 100e3 && vrIn < 0) {
                v.Mode = "entryS";
                sim.LogMsg("К: апогея не хватило — переход к входу в атмосферу", 2);
            }
            break;
        }
        case "circ": {
            Vec2 e = v.East;
            double s2 = Math.Sign(v.VHor != 0 ? v.VHor : 1);
            v.ThCmd = Guidance.PitchOf(new Vec2(e.X * s2, e.Y * s2), v);
            double periLeft = sim.TargetPeri - 8e3 - o.Peri;
            if (periLeft < Const.CIRC_ONE) v.NEng = 1;
            v.Throttle = Const.Clamp(periLeft / Const.CIRC_TAPER, 0.4, 1);
            if (o.Peri >= sim.TargetPeri - 8e3 || v.Prop < 0.03 * v.PropMax) {
                v.Ign = false; v.NEng = 0; v.Mode = "orbit";
            }
            break;
        }
        case "orbit": {
            v.Ign = false;
            if (v.F < 1e3)
                sim.Once("orbMsg" + v.Tag, () => sim.LogMsg(
                    $"ВЫХОД НА ОРБИТУ {(o.Peri / 1000):F0} × {(o.Apo / 1000):F0} км, период {(o.Per / 60):F1} мин", 1));
            v.ThCmd = (v.SeekPad && !double.IsNaN(v.DeoMiss) && Math.Abs(v.DeoMiss) < 500e3)
                      ? Guidance.AimRetro(v) : Guidance.AimPro(v);
            if (!v.SeekPad) break;
            v.DeoAcc += dt;
            if (v.DeoAcc > 3) {
                v.DeoAcc = 0;
                double dv = Guidance.DeorbitDv(v, Const.RE + 35e3);
                EntryPred p = Guidance.PredictEntry(sim, v, dv, 62, Const.ENTRY_BANK0);
                double prev = v.DeoMiss;
                v.DeoMiss = p.Miss;
                if (!double.IsNaN(prev) && Math.Abs(p.Miss) < 400e3 &&
                    (Math.Sign(prev) != Math.Sign(p.Miss) || Math.Abs(p.Miss) < 6e3)) {
                    v.Mode = "deorbit"; v.DeoAcc = 0;
                    v.DeoMiss = double.NaN; v.DeoLeft = double.NaN; v.DeoAuto = true;
                    sim.LogMsg($"К: окно схода с орбиты — расчётная точка входа в {(p.Miss / 1000):F0} км от башни, полёт {(p.T / 60):F0} мин", 2);
                }
            }
            break;
        }
        case "deorbit": {
            v.ThCmd = Guidance.AimRetro(v);
            if (Math.Abs(Vehicle.AngDiff(v.ThCmd, v.Th)) > 12 * Const.D2R && !(v.DvBurn > 0)) {
                v.Ign = false; v.NEng = 0;
                sim.Once("deoTurn" + v.Tag,
                    () => sim.LogMsg("К: разворот хвостом вперёд перед тормозным импульсом", 1));
                break;
            }
            if (double.IsNaN(v.DeoLeft)) {
                v.DeoLeft = v.DeoAuto ? Guidance.DeorbitSolve(sim, v) : Guidance.DeorbitDv(v, Const.RE + 35e3);
                v.DvBurn = 0; v.DeoAcc = 0;
                sim.LogMsg($"К: тормозной импульс {v.DeoLeft:F1} м/с" +
                           (v.DeoAuto ? " — наведение на башню" : " — ручной сход, перигей 35 км"), 1);
            }
            v.Ign = true; v.NEng = 3;
            v.DvBurn += v.F / v.Mass * dt;
            v.DeoLeft -= v.F / v.Mass * dt;
            v.DeoAcc += dt;
            if (v.DeoAuto && v.DeoAcc > 0.5 && v.DeoLeft > 2) { v.DeoAcc = 0; v.DeoLeft = Guidance.DeorbitSolve(sim, v); }
            v.Throttle = v.DeoLeft > 25 ? 0.6 : (v.DeoLeft > 6 ? 0.25 : 0.1);
            if (v.DeoLeft <= 0 || o.Peri < 12e3 || v.Prop < 0.01 * v.PropMax) {
                v.Ign = false; v.NEng = 0; v.Mode = "coastD"; v.DeoLeft = double.NaN;
                v.DeoMiss = Guidance.PredictEntry(sim, v, 0, 62, Const.ENTRY_BANK0).Miss;
                sim.LogMsg($"К: тормозной импульс завершён — {v.DvBurn:F1} м/с, перигей {(o.Peri / 1000):F0} км, расчётный промах {(v.DeoMiss / 1000):F0} км", 2);
            }
            break;
        }
        case "coastD": {
            v.Ign = false;
            v.ThCmd = Guidance.AimLift(v, 62, Guidance.LiftSign(v, "up", Guidance.LiftRef(v).Up));
            v.BankCmd = Const.ENTRY_BANK0 * Const.D2R;
            Guidance.VentProp(sim, v, dt);
            if (h < 120e3) {
                v.Mode = "entryS";
                sim.LogMsg("К: вход в атмосферу — «брюхом» к потоку, α≈60°", 2);
            }
            break;
        }
        case "entryS": {
            v.Ign = false;
            if (h > 60e3) Guidance.VentProp(sim, v, dt);
            v.EntAcc += dt;
            if (v.EntAcc > Const.ENTRY_PRED_DT && v.SeekPad && h > Const.GLIDE_H) {
                v.EntAcc = 0;
                double qq = 0.5 * Atmosphere.At(h, sim.RhoK).Rho * sp * sp;
                if (qq > 200) {
                    double aA = Math.Abs(v.Alpha), sa = Math.Sin(aA), ca = Math.Abs(Math.Cos(aA));
                    double cnm = 2 * Math.Abs(sa * ca) + 1.15 * (v.FullLen * v.Dia / v.A) * sa * sa;
                    double cdMod = cnm * sa + (0.2 * ca * ca * 2.6 + 0.06) * ca;
                    double cdAct = v.Drag / Math.Max(qq * v.A, 1);
                    v.EntK = Const.Clamp(0.85 * v.EntK + 0.15 * (cdAct / Math.Max(cdMod, 0.1)), 0.4, 2.5);
                }
                double bk0 = v.Bank * Const.R2D;
                double aDeg = Math.Abs(v.Alpha * Const.R2D);
                v.EntMiss = Guidance.PredictEntry(sim, v, 0, aDeg != 0 ? aDeg : 62, bk0).Miss;
                double bWant = Const.Clamp(bk0 + Em(v) * Const.ENTRY_KB, 0, 120) * Const.D2R;
                v.BankCmd += Const.BANK_SMOOTH * (bWant - v.BankCmd);
            }
            double trim = Const.Clamp(Em(v) / Const.ENTRY_KT, Const.ENTRY_TRIM_LO, Const.ENTRY_TRIM_HI);
            double aPrev = v.AlphaCmd;
            v.AlphaCmd = Const.Clamp(62 + trim, 45, 74);
            LiftRefV rf = Guidance.LiftRef(v);
            if (h > Const.GLIDE_H || !v.SeekPad) {
                if (!v.SeekPad && h <= Const.GLIDE_H) { v.AlphaCmd = 58; v.BankCmd = 0; }
                else v.AlphaCmd = h > 60e3 ? v.AlphaCmd : Const.Clamp(70 + trim, 45, 74);
                v.ThCmd = Guidance.AimLift(v, v.AlphaCmd, Guidance.LiftSign(v, "up", rf.Up));
            }
            else {
                double miss = sim.Downrange(v) + Guidance.FlipDrift(v);
                v.AlphaCmd = Const.GLIDE_KA > 0
                    ? Const.Clamp(58 + miss / Const.GLIDE_KA, Const.GLIDE_A_LO, Const.GLIDE_A_HI)
                    : 58;
                double aa = v.AlphaCmd * Const.D2R, sa2 = Math.Sin(aa), ca2 = Math.Cos(aa);
                double cn2 = 2 * sa2 * ca2 + 1.15 * (v.FullLen * v.Dia / v.A) * sa2 * sa2;
                double ca20 = Atmosphere.Cd0(v.Mach) * ca2 * ca2 + 0.06;
                double lacc = v.Q * v.A * Math.Max(0, cn2 * ca2 - ca20 * sa2) / v.Mass;
                v.BankCmd = Guidance.GlideBank(sim.Downrange(v) + Guidance.FlipDrift(v),
                                               v.VHor, h, v.VVert, lacc, Math.Abs(rf.East));
                sim.Once("glide" + v.Tag, () =>
                    sim.LogMsg($"{v.Tag}: терминальное наведение — гашение сноса креном, до башни {(-sim.Downrange(v) / 1000):F0} км", 2));
                v.ThCmd = Guidance.AimLift(v, v.AlphaCmd, Guidance.LiftSign(v, "east", rf.East));
            }
            if (aPrev > 0)
                v.AlphaCmd = aPrev + Const.Clamp(v.AlphaCmd - aPrev,
                    -Const.ALPHA_RATE * dt, Const.ALPHA_RATE * dt);
            if (v.Heat > v.MaxHeat) v.MaxHeat = v.Heat;
            if (v.SeekPad ? h < Const.FLIP_H && Guidance.StopAlt(v, 3) < Const.FLIP_STOP
                          : h < Const.FLIP_H_SEA) {
                if (v.SeekPad && Math.Abs(sim.Downrange(v)) > 3000) {
                    v.SeekPad = false;
                    sim.LogMsg($"К: башня недосягаема ({(sim.Downrange(v) / 1000):F0} км) — посадка вне площадки", 2);
                }
                v.Mode = "flipS"; v.Tmr = 0; v.Ign = true; v.NEng = 3; v.Throttle = 1;
                sim.LogMsg("К: переворот и посадочная жига", 2);
            }
            break;
        }
        case "flipS": {
            v.Tmr += dt; v.Ign = true; v.NEng = 3;
            Guidance.LandingBurn(sim, v, dt, 3);
            v.ThCmd = 0;
            if (Math.Abs(Vehicle.AngDiff(v.Th, 0)) < 30 * Const.D2R || v.Tmr > 4) {
                v.Mode = "landS";
                v.Ign = false; v.NEng = 0; v.Throttle = 0;
            }
            break;
        }
        case "landS":
            Guidance.LandingBurn(sim, v, dt, 3);
            break;
        }
    }
    private static void DescentB(SimState sim, Vehicle v, double dt) {
        v.PredAcc += dt;
        if (v.Alt > Const.BOOST_STRAIGHT_H) {
            if (v.PredAcc > 0.3 || double.IsNaN(v.MissPred)) {
                v.PredAcc = 0;
                v.BankCmd = Math.Acos(Guidance.BoosterLift(sim, v));
                v.MissPred = Guidance.BoosterMiss(sim, v, Math.Cos(v.Bank));
            }
            v.ThCmd = Guidance.AimRetro(v) + Const.BOOST_AOA * Const.D2R;
            return;
        }
        v.BankCmd = 0;
        if (v.PredAcc > 0.3) { v.PredAcc = 0; v.MissPred = Guidance.BoosterMiss(sim, v, 1); }
        double miss = Mp(v);
        double dl = Const.Clamp(Math.Abs(miss) / 1500, 0, 1) * 16 * Const.D2R;
        v.ThCmd = Guidance.AimRetro(v) + (miss > 0 ? dl : -dl);
    }
    private static double Mp(Vehicle v) => double.IsNaN(v.MissPred) ? 0 : v.MissPred;
    private static double Em(Vehicle v) => double.IsNaN(v.EntMiss) ? 0 : v.EntMiss;
}
