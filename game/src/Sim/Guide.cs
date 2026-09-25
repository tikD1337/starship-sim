using System;
namespace Starship.Physics;
public static class Guide {
    public static void Separate(SimState sim) {
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        Vec2 ax = b.Axis;
        s.Attached = false; b.Stacked = false;
        double gap = b.Len + Const.SEP_GAP;
        s.X = b.X + ax.X * gap; s.Y = b.Y + ax.Y * gap;
        s.Vx = b.Vx; s.Vy = b.Vy;
        s.Th = b.Th; s.Om = b.Om; s.ThCmd = b.Th;
        s.Mode = "ascent2"; b.Mode = "flip"; b.Tmr = 0; b.Ign = true; b.NEng = 3;
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
            v.ThCmd = h < Const.ASC_CLEAR_H ? 0 : Guidance.PitchProg(sp);
            if (v.Q > Const.ASC_Q_IN) v.QDown = true;
            else if (v.Q < Const.ASC_Q_OUT) v.QDown = false;
            v.Throttle = !v.QDown ? 1 : v.Q > 32e3 ? 0.74 : 0.86;
            if (v.Acc > 3.6) v.Throttle = Const.Clamp(v.Throttle * 3.6 / v.Acc, 0.4, 1);
            if (v.Q < 8e3 && sim.T > 60) sim.Once("maxq-pass");
            if (v.MaxQ > 1e3 && v.Q < v.MaxQ * 0.85)
                sim.Once("maxq", () => sim.LogMsg($"Max Q пройден — {(v.MaxQ / 1000):F1} кПа на H={(h / 1000):F1} км", 1));
            if (v.Prop <= sim.MecoFill * v.PropMax || sp > sim.MecoV) {
                v.Mode = "meco"; v.Tmr = 0; v.FRef = v.F;
                sim.LogMsg($"MECO: отсечка маршевых. V={sp:F0} м/с, H={(h / 1000):F1} км", 2);
            }
            break;
        }
        case "meco": {
            v.Tmr += dt; v.NEng = 3; v.Throttle = 0.4;
            Vehicle s = v.Mate;
            if (!s.Ign && (v.F < Const.HOT_IGN_F * v.FRef || v.Tmr > Const.HOT_IGN_T)) {
                s.Ign = true; s.NEng = 6; s.Throttle = 1;
                sim.LogMsg("Запуск двигателей корабля — горячее разделение", 1);
            }
            if (!s.Ign) break;
            int need = 0, lit = 0;
            foreach (Engine e in s.Eng) {
                if (!e.Failed) need++;
                if (e.On && e.Pc >= Const.HOT_PC * Pump.PC_NOM) lit++;
            }
            if (lit >= Math.Min(s.NEng, need) && need > 0) {
                sim.LogMsg($"Корабль на режиме: {lit} двигателей, давление в камерах выше {Const.HOT_PC * Pump.PC_NOM:F0} МПа", 1);
                Separate(sim);
            }
            else if (v.Tmr > Const.HOT_SEP_T) {
                sim.LogMsg($"Корабль не вышел на режим ({lit} из {Math.Min(s.NEng, need)}) — разделение по страховке", 2);
                Separate(sim);
            }
            break;
        }
        case "flip": {
            v.Ign = true; v.NEng = 3; v.Throttle = 0.4; v.Tmr += dt;
            v.ThCmd = Guidance.BoostbackAim(v);
            if (v.Tmr > 4 || Math.Abs(Vehicle.AngDiff(v.ThCmd, v.Th)) < 12 * Const.D2R) {
                PollBooster(sim, v);
                v.Mode = "boostback"; v.Ign = true; v.NEng = 13; v.Throttle = 1;
                sim.LogMsg("Б: разворот на трёх центральных выполнен, включение ещё 10 — тормозной импульс", 1);
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
                if (v.Catch && Math.Abs(Mp(v)) > Const.GO_MISS_B) Divert(sim, v, Const.SEA_DR, $"расчётный промах {Mp(v):F0} м");
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
            if (v.SeekPad && h < 3500 && Math.Abs(sim.Downrange(v) - v.AimDr) > 2000) {
                v.SeekPad = false;
                sim.LogMsg($"Б: {(v.Site == "tower" ? "башня недосягаема" : "цель недосягаема")} ({((sim.Downrange(v) - v.AimDr) / 1000):F1} км) — снижение без точки прицеливания", 2);
            }
            if (!v.IgnBurn) DescentB(sim, v, dt);
            else sim.Once("reach" + v.Tag, () => Reach(sim, v, Const.REACH_B));
            Guidance.LandingBurn(sim, v, dt, v.Spec.NLand);
            LatePoll(sim, v, dt);
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
            if (v.SeekPad && Sim.DeployStep(sim, v, dt)) { v.ThCmd = Guidance.AimPro(v); break; }
            v.ThCmd = (v.SeekPad && !double.IsNaN(v.DeoMiss) && Math.Abs(v.DeoMiss) < Const.DEO_TURN)
                      ? Guidance.AimRetro(v) : Guidance.AimPro(v);
            if (!v.SeekPad) break;
            v.DeoAcc += dt;
            if (v.DeoAcc > 3) {
                v.DeoAcc = 0;
                double dv = Guidance.DeorbitDv(v, Const.RE + 35e3);
                EntryPred p = Guidance.PredictEntry(sim, v, dv, 62, Const.ENTRY_BANK0, Propellant.SettleLeft(v));
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
                if (v.SeekPad) PollShip(sim, v, "на входе");
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
                double qq = 0.5 * Atmosphere.At(h, v.RhoEst).Rho * sp * sp;
                if (qq > 200) {
                    double aA = Math.Abs(v.Alpha), sa = Math.Sin(aA), ca = Math.Abs(Math.Cos(aA));
                    double cnm = 2 * Math.Abs(sa * ca) + 1.15 * (v.FullLen * v.Dia / v.A) * sa * sa + Surfaces.FlapCnA(v, aA, v.FlapFwd, v.FlapAft);
                    double cdMod = cnm * sa + (0.2 * ca * ca * 2.6 + 0.06) * ca;
                    double cdAct = v.Drag / Math.Max(qq * v.A, 1);
                    v.EntK = Const.Clamp(0.85 * v.EntK + 0.15 * (cdAct / Math.Max(cdMod, 0.1)), 0.4, 2.5);
                }
                double bk0 = v.Bank * Const.R2D;
                double aDeg = Math.Abs(v.Alpha * Const.R2D);
                double a0 = aDeg != 0 ? aDeg : 62;
                v.EntMiss = Guidance.PredictEntry(sim, v, 0, a0, bk0).Miss;
                v.EntSA = Math.Abs(Guidance.PredictEntry(sim, v, 0, a0 + 1, bk0).Miss - v.EntMiss);
                double sB = Math.Abs(Guidance.PredictEntry(sim, v, 0, a0, bk0 + 2).Miss - v.EntMiss) / 2;
                double kb = Math.Min(Const.ENTRY_KB, Const.ENTRY_GAIN / Math.Max(sB, 1));
                double bWant = Const.Clamp(bk0 + Em(v) * kb, 0, 120) * Const.D2R;
                v.BankCmd += Const.BANK_SMOOTH * (bWant - v.BankCmd);
            }
            double trim = Const.Clamp(Em(v) / Math.Max(Const.ENTRY_KT, v.EntSA / Const.ENTRY_GAIN), Const.ENTRY_TRIM_LO, Const.ENTRY_TRIM_HI);
            double aPrev = v.AlphaCmd;
            v.AlphaCmd = Const.Clamp(62 + trim, 45, 74);
            LiftRefV rf = Guidance.LiftRef(v);
            if (h > Const.GLIDE_H || !v.SeekPad) {
                if (!v.SeekPad && h <= Const.GLIDE_H) { v.AlphaCmd = 58; v.BankCmd = 0; }
                else v.AlphaCmd = h > 60e3 ? v.AlphaCmd : Const.Clamp(70 + trim, 45, 74);
                v.AlphaCmd = RateAlpha(aPrev, v.AlphaCmd, dt);
                v.ThCmd = Guidance.AimLift(v, v.AlphaCmd, Guidance.LiftSign(v, "up", rf.Up));
            }
            else {
                double miss = sim.Downrange(v) + Const.FLIP_D - v.AimDr;
                double vp = Guidance.AimPro(v) * Const.R2D;
                double flat = Const.Clamp((Math.Abs(vp) - Const.BELLY_VP0) / (Const.BELLY_VP1 - Const.BELLY_VP0), 0, 1);
                double lead = miss + Const.BELLY_LEAD;
                double aGlide = Const.GLIDE_KA > 0
                    ? Const.Clamp(58 + lead / Const.GLIDE_KA, Const.GLIDE_A_LO, Const.GLIDE_A_HI)
                    : 58;
                double thBelly = Math.PI / 2 + Guidance.BellyTilt(v, miss, v.VHor, Guidance.FlipTgo(v, FlipStop(v)));
                double thGlide = Guidance.AimLift(v, aGlide, Guidance.LiftSign(v, "east", rf.East));
                v.AlphaCmd = aGlide + (Math.Abs(Vehicle.AngDiff(vp * Const.D2R, thBelly)) * Const.R2D - aGlide) * flat;
                double aa = aGlide * Const.D2R, sa2 = Math.Sin(aa), ca2 = Math.Cos(aa);
                double cn2 = 2 * sa2 * ca2 + 1.15 * (v.FullLen * v.Dia / v.A) * sa2 * sa2 + Surfaces.FlapCnA(v, aa, v.FlapFwd, v.FlapAft);
                double ca20 = Atmosphere.Cd0(v.Mach) * ca2 * ca2 + 0.06;
                double lacc = v.Q * v.A * Math.Max(0, cn2 * ca2 - ca20 * sa2) / v.Mass;
                v.BankCmd = (1 - flat) * Guidance.GlideBank(lead, v.VHor, h, v.VVert, lacc, Math.Abs(rf.East));
                sim.Once("glide" + v.Tag, () =>
                    sim.LogMsg($"{v.Tag}: терминальное наведение — гашение сноса, до {(v.Site == "sea" ? "точки приводнения" : "башни")} {((v.AimDr - sim.Downrange(v)) / 1000):F0} км", 2));
                v.ThCmd = thGlide + Vehicle.AngDiff(thBelly, thGlide) * flat;
                v.AlphaCmd = RateAlpha(aPrev, v.AlphaCmd, dt);
            }
            if (v.Heat > v.MaxHeat) v.MaxHeat = v.Heat;
            if (h < Const.GLIDE_H && v.SeekPad) sim.Once("poll" + v.Tag, () => PollShip(sim, v, "на 25 км"));
            if (h < Const.GO_POLL_H && v.SeekPad) sim.Once("poll3" + v.Tag, () => PollShip(sim, v, "на 3 км"));
            if (v.SeekPad ? h < Const.FLIP_H && Guidance.StopAlt(v, 3) < FlipStop(v)
                          : h < Const.FLIP_H_SEA) {
                if (v.SeekPad && Math.Abs(sim.Downrange(v) - v.AimDr) > 3000) {
                    v.SeekPad = false;
                    sim.LogMsg($"К: башня недосягаема ({(sim.Downrange(v) / 1000):F0} км) — снижение без точки прицеливания", 2);
                }
                Reach(sim, v, Const.REACH_S);
                v.Mode = "flipS"; v.Tmr = 0; v.Ign = true; v.NEng = 3; v.Throttle = 1;
                sim.LogMsg("К: переворот и посадочная жига", 2);
            }
            break;
        }
        case "flipS": {
            v.Tmr += dt; v.Ign = true; v.NEng = 3;
            Guidance.LandingBurn(sim, v, dt, 3);
            LatePoll(sim, v, dt);
            v.ThCmd = 0;
            if (Math.Abs(Vehicle.AngDiff(v.Th, 0)) < Const.FLIP_END * Const.D2R
                && Math.Abs(v.Om) < Const.FLIP_OM_END * Const.D2R || v.Tmr > 4) {
                v.Mode = "landS";
                v.Ign = false; v.NEng = 0; v.Throttle = 0;
            }
            break;
        }
        case "landS":
            Guidance.LandingBurn(sim, v, dt, 3);
            LatePoll(sim, v, dt);
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
    private static double FlipStop(Vehicle v) => Const.FLIP_STOP + (v.Catch ? 0 : SimState.Surface(v.AimDr) - Const.CATCH_H);
    public static void Divert(SimState sim, Vehicle v, double aim, string why) {
        v.Site = "sea";
        v.AimDr = aim;
        v.HoldT = 0; v.WaitT = 0; v.ShipHold = false;
        sim.LogMsg($"{v.Tag}: захват отменён — {why}: уход в море", 2);
    }
    private static void Reach(SimState sim, Vehicle v, double r) {
        if (!v.SeekPad || v.Site != "sea") return;
        double dr = sim.Downrange(v);
        double aim = Math.Max(Const.Clamp(v.AimDr, dr - r, dr + r), Const.COAST_DR + 300);
        if (Math.Abs(aim - v.AimDr) < 1) return;
        v.AimDr = aim;
        sim.LogMsg($"{v.Tag}: точка приводнения перенесена ближе — {aim / 1000:F1} км от башни", 1);
    }
    private static int Dead(Vehicle v, int n) {
        int d = 0;
        for (int i = 0; i < n && i < v.Eng.Count; i++) if (v.Eng[i].Failed) d++;
        return d;
    }
    private static void PollBooster(SimState sim, Vehicle v) {
        if (!v.Catch) return;
        int dead = Dead(v, 13);
        double wind = Math.Abs(sim.Wind.Forecast(Const.CATCH_H));
        string why = dead >= 2 ? $"{dead} из 13 посадочных двигателей неисправны"
            : v.CopvK < 1 ? "утечка газа наддува"
            : v.CtrlK < 1 ? "заедание решётчатого руля"
            : v.Prop < Const.GO_PROP_B ? $"топлива на посадку {v.Prop / 1000:F0} т"
            : wind > Const.GO_WIND ? $"ветер у башни {wind:F0} м/с"
            : null;
        if (why == null) sim.LogMsg("Б: опрос перед тормозным импульсом — GO на захват башней", 1);
        else Divert(sim, v, Const.SEA_DR, why);
    }
    private static void PollShip(SimState sim, Vehicle v, string at) {
        if (!v.Catch) return;
        double wind = Math.Abs(sim.Wind.Forecast(Const.CATCH_H) + v.WindBias);
        string why = v.Dmg > Const.GO_DMG_S ? $"повреждение теплозащиты {v.Dmg * 100:F0} %"
            : v.CtrlK < 1 ? "заедание привода закрылка"
            : v.Prop < Const.GO_PROP_S ? $"топлива на посадку {v.Prop / 1000:F0} т"
            : wind > Const.GO_WIND ? $"ветер у башни {wind:F0} м/с"
            : null;
        if (why == null) sim.LogMsg($"К: опрос {at} — GO на захват башней, ветер у башни {wind:F0} м/с", 1);
        else Divert(sim, v, Const.SEA_DR, why);
    }
    private static void LatePoll(SimState sim, Vehicle v, double dt) {
        if (!v.Catch || !v.Ign || v.Landed) return;
        bool booster = v.Kind == Kind.Booster;
        double aim = Math.Max(Const.COAST_DR + 200, sim.Downrange(v));
        int dead = booster ? Dead(v, 13) : Dead(v, 3);
        if (booster && dead >= 2) {
            Divert(sim, v, aim, $"не зажглись {dead} из 13 посадочных двигателей");
            return;
        }
        if (!booster && dead >= 1)
            sim.Once("dead" + v.Tag, () => sim.LogMsg($"{v.Tag}: не зажглись {dead} из 3 посадочных двигателей — захват на оставшихся", 2));
        double dh = v.Alt - Const.CATCH_H;
        if (dh < 3 && dh > -Const.CATCH_WIN) v.WaitT += dt;
        if (v.WaitT > Const.WAIT_MAX)
            Divert(sim, v, aim, $"не удалось войти в окно захвата за {Const.WAIT_MAX:F0} с");
        else if (dh <= -Const.CATCH_WIN)
            Divert(sim, v, aim, "ступень прошла ниже рук");
    }
    private static double Em(Vehicle v) => double.IsNaN(v.EntMiss) ? 0 : v.EntMiss;
    private static double RateAlpha(double prev, double want, double dt)
        => prev > 0 ? prev + Const.Clamp(want - prev, -Const.ALPHA_RATE * dt, Const.ALPHA_RATE * dt) : want;
}
