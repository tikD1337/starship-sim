using System;
using System.Collections.Generic;
using System.Linq;
using Starship.Game;
using Starship.Game.Ui;
using Starship.Physics;
namespace Starship.Tests;
internal sealed class Run {
    public readonly SimState Sim;
    private readonly List<Action> _each = new();
    private Func<bool> _done = () => true;
    public Run(SimState sim) { Sim = sim; }
    public Vehicle B => Sim.Veh[0];
    public Vehicle S => Sim.Veh[1];
    public Run Each(Action a) { _each.Add(a); return this; }
    public Run Until(Func<bool> done) { _done = done; return this; }
    public void Go() {
        for (int i = 0; i < 1_800_000 && !_done(); i++) {
            Physics.Sim.Tick(Sim, Const.DT);
            foreach (Action a in _each) a();
        }
    }
    public static bool Over(Vehicle v) => v.Landed || v.Crashed || v.Caught;
}
internal static partial class Program {
    private static SimState Scripted(string key, uint seed = 5) {
        var sim = new SimState { Mission = "orbital", AnomOn = true, AnomScript = new HashSet<string> { key } };
        Physics.Sim.Reset(sim, seed);
        return sim;
    }
    private static bool Logged(SimState sim, string part) => sim.Log.Any(l => l.M.Contains(part));
    private static SimState Windy(uint seed, bool disp, double surf) {
        SimState sim = Live(seed, disp);
        sim.Wind = Wind.Steady(surf);
        sim.Disp?.ApplyWind(sim);
        return sim;
    }
    private static void FlightPlan(List<Run> runs, List<Action> checks) {
        Run Add(SimState sim, Func<Run, bool> done) {
            var r = new Run(sim);
            r.Until(() => done(r));
            runs.Add(r);
            return r;
        }
        Run nom = Add(Live(12345, false), r => r.S.Stowed || r.S.Crashed || r.S.Landed && !r.S.Caught);
        Run high = Add(Live(12345, false, "high"), r => Run.Over(r.S));
        Run trans = Add(Live(12345, false, "trans"), r => Run.Over(r.S));
        var winds = new (string Name, double Surf)[] { ("штиль", 0), ("ветер 8 м/с", 8), ("ветер −8 м/с", -8), ("ветер 15 м/с", 15) };
        Run[] windRuns = winds.Select(w => {
            SimState sim = Live(12345, false);
            sim.Wind = w.Surf == 0 ? Wind.Calm() : Wind.Steady(w.Surf);
            return Add(sim, r => Run.Over(r.B));
        }).ToArray();

        checks.Add(() => Head("Орбитальное задание без разброса: полёт целиком"));
        Marks(nom, checks);
        BoosterReturn(nom, checks);
        ShipDescent(nom, "орбитальное", checks);
        ArmsAndCatch(nom, checks);
        Rails(nom, checks);
        checks.Add(() => Head("Высокая орбита и трансатмосферное"));
        ShipDescent(high, "высокая орбита", checks);
        ShipDescent(trans, "трансатмосферное", checks);

        checks.Add(() => Head("Ускоритель у башни: ветер и раскачка"));
        var swings = new List<(string, Func<(bool, int, int, double)>)> {
            ("орбитальное", Swing(nom)), ("высокая орбита", Swing(high)), ("трансатмосферное", Swing(trans)),
        };
        for (int i = 0; i < 3; i++) swings.Add((winds[i].Name, Swing(windRuns[i])));
        checks.Add(() => {
            var res = swings.Select(s => (s.Item1, R: s.Item2())).ToList();
            Group("на последних 650 м промах меняет направление и корпус перекладывается не больше раза",
                  string.Join(", ", res.Select(x => $"{x.Item1} {x.R.Item2}/{x.R.Item3}")),
                  res.Select(x => ($"{x.Item1}: пойман {x.R.Item1}, разворотов {x.R.Item2}, перекладок {x.R.Item3}", x.R.Item1 && x.R.Item2 <= 1 && x.R.Item3 <= 1)).ToArray());
            double calm = Math.Abs(windRuns[0].Sim.Downrange(windRuns[0].B));
            Group("ловля при приземном ветре", string.Join(", ", windRuns.Select((r, k) => $"{winds[k].Name} {N(r.Sim.Downrange(r.B))} м")),
                  windRuns.Select((r, k) => ($"{winds[k].Name}: пойман, промах не хуже штиля + 1,5 м",
                                              r.B.Caught && (k == 3 || Math.Abs(r.Sim.Downrange(r.B)) < calm + 1.5))).ToArray());
        });

        Estimation(nom, runs, checks);
        LiveCatches(runs, checks);
        Fallbacks(runs, checks);
    }
    private static void Marks(Run n, List<Action> checks) {
        int before = n.Sim.Marks.Count;
        checks.Add(() => {
            var m = n.Sim.Marks;
            string all = string.Join(" | ", m.Select(x => x.M));
            string[] miss = new[] { "башню", "разделение", "Max Q", "ЗАХВАТ", "ОРБИТУ", "жига" }.Where(k => !all.Contains(k)).ToArray();
            Group("вехи для разбора записи", $"{m.Count} вех",
                  ("до старта одна веха подготовки", before == 1),
                  ("набралось больше восьми", m.Count > 8),
                  ("идут по времени", m.Zip(m.Skip(1)).All(p => p.Second.T >= p.First.T)),
                  ("отсчёт секунд в вехи не попадает", !m.Exists(x => x.M.StartsWith("Отсчёт"))),
                  ($"есть ключевые (нет: {string.Join(", ", miss)})", miss.Length == 0),
                  ("последняя не позже конца полёта", m[^1].T <= n.Sim.T + 1e-9));
        });
    }
    private static void BoosterReturn(Run n, List<Action> checks) {
        Vehicle b = n.B;
        SimState sim = n.Sim;
        bool back = false, coast = false, over = false;
        double lit = 0, hIgn = double.NaN, vIgn = double.NaN, tIgn = double.NaN, qDesc = 0, g = 0, t13 = 0, tCatch = double.NaN, v1000 = double.NaN, miss = double.NaN;
        int nIgn = 0, nLast = 0;
        var counts = new SortedSet<int>();
        n.Each(() => {
            if (over) {
                if (double.IsNaN(v1000) && sim.T >= tCatch + 1000) v1000 = b.Speed;
                return;
            }
            if (b.Mode == "boostback") back = true;
            else if (back && !b.IgnBurn) coast = true;
            if (coast && !b.IgnBurn && b.NRun > 0) lit += Const.DT;
            if (coast) qDesc = Math.Max(qDesc, b.Q);
            if (b.IgnBurn && b.NRun > 0) {
                if (double.IsNaN(hIgn)) { hIgn = b.Alt; nIgn = b.NRun; tIgn = sim.T; vIgn = b.Speed; }
                g = Math.Max(g, b.Acc);
                if (b.NRun == 13) t13 += Const.DT;
                counts.Add(b.NRun);
                nLast = b.NRun;
            }
            if (Run.Over(b)) { over = true; tCatch = sim.T; miss = sim.Downrange(b); }
        });
        checks.Add(() => {
            Group("ускоритель: после тормозного импульса двигатели молчат до жиги", $"напор на спуске до {N(qDesc / 1000)} кПа",
                  ("опрос перед импульсом даёт GO", Logged(sim, "GO на захват")),
                  ("между импульсом и жигой не работал ни один двигатель", lit == 0),
                  ("напор на спуске ниже предела конструкции", qDesc < 200e3));
            Group("посадочная жига ускорителя как у пятого полёта (пункт 48)",
                  $"с {N(hIgn)} м при {N(vIgn * 3.6)} км/ч, 13 двигателей {N(t13)} с, {N(g)} g, до захвата {N(tCatch - tIgn)} с",
                  ("зажигание на 500…2000 м", hIgn > 500 && hIgn < 2000),
                  ("при 1100…1400 км/ч (пятый полёт ~1250)", vIgn * 3.6 >= 1100 && vIgn * 3.6 <= 1400),
                  ("13 двигателей, потом 3 — и только они", nIgn == 13 && nLast == 3 && counts.SetEquals(new[] { 13, 3 })),
                  ("13 горят 4…8 с (пятый полёт — чуть больше 5)", t13 >= 4 && t13 <= 8),
                  ("перегрузка не выше 6 g", g <= 6),
                  ("от зажигания до захвата не больше 31 с", tCatch - tIgn <= 31));
            Group("ускоритель пойман и стоит", $"промах {N(miss)} м, через 1000 с {N(v1000 * 3.6)} км/ч",
                  ("пойман", b.Caught),
                  ("конец полёта записан в события в момент захвата", sim.Events.TryGetValue("overБ", out double t) && Math.Abs(t - tCatch) < 0.05),
                  ("через 1000 с стоит на месте относительно Земли", v1000 < 0.5));
        });
    }
    private static void ShipDescent(Run r, string name, List<Action> checks) {
        Vehicle s = r.S;
        SimState sim = r.Sim;
        bool catchJob = sim.Mission != "trans";
        int sats = s.BayS?.Sats ?? 0, deoSats = -1;
        string was = s.Mode;
        double deoPay = double.NaN, deoOpen = double.NaN, entryProp = double.NaN, flipMiss = double.NaN, belly = 0, atFlip = double.NaN;
        double t0 = double.NaN, tFlip = double.NaN, tilt = 0, tEnd = double.NaN;
        r.Each(() => {
            if (!double.IsNaN(tEnd)) return;
            if (was != "deorbit" && s.Mode == "deorbit" && deoSats < 0) { deoSats = s.BayS.Sats; deoOpen = s.BayS.Open; deoPay = s.Dry - Spec.Ship.Dry; }
            if (was != "entryS" && s.Mode == "entryS" && double.IsNaN(entryProp)) entryProp = s.Prop;
            double flat = Vehicle.AngDiff(s.Th, Math.PI / 2) * Const.R2D;
            if (s.Mode == "entryS" && s.Alt < 15e3) belly = Math.Max(belly, Math.Abs(flat));
            if (was == "entryS" && s.Mode == "flipS") { atFlip = flat; flipMiss = sim.Downrange(s) + Const.FLIP_D - s.AimDr; t0 = sim.T; }
            if (!double.IsNaN(t0)) {
                double dev = Math.Abs(Vehicle.AngDiff(s.Th, 0)) * Const.R2D;
                if (double.IsNaN(tFlip) && dev <= 15) tFlip = sim.T - t0;
                if (!double.IsNaN(tFlip)) tilt = Math.Max(tilt, dev);
            }
            was = s.Mode;
            if (Run.Over(s)) tEnd = sim.T;
        });
        checks.Add(() => {
            if (!catchJob) {
                Group($"{name}: падение плашмя и приводнение", $"уход от горизонта до {N(belly)}°, {s.Mode}, остаток {N(s.Prop / 1000)} т",
                      ("ниже 15 км корпус в 20° от горизонта", belly <= 20), ("сел на воду целым", s.Landed && !s.Crashed));
                return;
            }
            Group($"{name}: груз выпущен на орбите, на вход только запас (пункт 49)",
                  $"{sats} Starlink; к сходу в отсеке {deoSats}, груза {N(deoPay / 1000)} т; на вход {N(entryProp / 1000)} т",
                  ("спутники были выведены", sats > 0),
                  ("все выпущены и створка закрыта до схода", deoSats == 0 && deoOpen < 0.01 && deoPay < 1),
                  ("на вход не больше запаса", entryProp <= Const.ENTRY_PROP + 500));
            Group($"{name}: падение плашмя приводит точно в точку переворота",
                  $"уход от горизонта до {N(belly)}°, на перевороте {N(atFlip)}°, промах точки {N(flipMiss)} м, переворот {N(tFlip)} с",
                  ("ниже 15 км корпус в 20° от горизонта", belly <= 20),
                  ("переворот начинается почти с горизонтали", Math.Abs(atFlip) <= 20),
                  ("точка переворота в 30 м от расчётной", Math.Abs(flipMiss) <= 30),
                  ("до 15° от вертикали не дольше 3 с", tFlip <= 3));
            Group($"{name}: посадка на руки за ~20 с, с запасом, а не полными баками",
                  $"от зажигания до захвата {N(tEnd - t0)} с, наклон до {N(tilt)}°, остаток {N(s.Prop / 1000)} т",
                  ("пойман башней", s.Caught),
                  ("не дольше 24 с (пятый полёт — 20)", tEnd - t0 <= 24),
                  ("после переворота наклон не больше 30°", tilt <= 30),
                  ("после захвата 25…40 т", s.Prop >= 25e3 && s.Prop <= 40e3));
        });
    }
    private static void ArmsAndCatch(Run n, List<Action> checks) {
        foreach ((int idx, string name) in new[] { (0, "ускоритель"), (1, "корабль") }) {
            SimState sim = n.Sim;
            Vehicle v = sim.Veh[idx];
            double x0 = v.X, y0 = v.Y, th0 = v.Th, vx0 = v.Vx, vy0 = v.Vy;
            bool was = v.Caught, done = false;
            double jump = double.NaN, turn = double.NaN, tCatch = double.NaN, sagV1 = double.NaN, sag1 = double.NaN, tilt1 = double.NaN;
            double tilt2 = double.NaN, slide2 = double.NaN, stowAlt = double.NaN;
            double tReady = double.NaN, gapEnter = double.NaN, gapCatch = double.NaN, early = double.PositiveInfinity, sagMax = 0, sagVLate = double.NaN;
            n.Each(() => {
                if (done) return;
                if (!was && v.Caught) {
                    tCatch = sim.T;
                    gapCatch = sim.ArmGap;
                    double px = x0 + vx0 * Const.DT, py = y0 + vy0 * Const.DT;
                    jump = Math.Sqrt((v.X - px) * (v.X - px) + (v.Y - py) * (v.Y - py));
                    turn = Math.Abs(Vehicle.AngDiff(v.Th, th0)) * Const.R2D;
                }
                if (v.Launched && !v.Attached && !v.Caught && v.VVert < 0 && v.Alt < 8000) {
                    double rel = v.Alt - Const.CATCH_H;
                    if (double.IsNaN(tReady) && sim.ArmGap <= Const.ARM_GAP_READY + 0.05) tReady = sim.T;
                    if (rel > v.CatchPinY + 5) early = Math.Min(early, sim.ArmGap);
                    if (double.IsNaN(gapEnter) && rel < v.CatchPinY) gapEnter = sim.ArmGap;
                }
                if (!double.IsNaN(tCatch)) {
                    double dt = sim.T - tCatch;
                    if (dt <= 6) sagMax = Math.Max(sagMax, sim.ArmSag);
                    if (double.IsNaN(sagV1) && dt >= 1.2) {
                        sagV1 = Math.Abs(sim.ArmSagV); sag1 = Math.Abs(sim.ArmSag - Const.ARM_SAG_REST);
                        tilt1 = Math.Abs(Vehicle.AngDiff(v.Th, 0)) * Const.R2D;
                    }
                    if (double.IsNaN(tilt2) && dt >= 2.5) { tilt2 = Math.Abs(Vehicle.AngDiff(v.Th, 0)) * Const.R2D; slide2 = Math.Abs(v.HeldVh); }
                    if (double.IsNaN(sagVLate) && dt >= 6) sagVLate = sim.ArmSagV;
                }
                if (v.Stowed) { stowAlt = v.Alt; done = true; }
                x0 = v.X; y0 = v.Y; th0 = v.Th; vx0 = v.Vx; vy0 = v.Vy; was = v.Caught;
            });
            checks.Add(() => {
                Group($"{name}: руки сведены заранее и дожимают зазор, пока корпус проходит рельсы",
                      $"рабочий зазор за {N(tCatch - tReady)} с до захвата; выше рук не уже {N(early)} м, у рельсов {N(gapEnter)}, на захвате {N(gapCatch)} м",
                      ("пойман", !double.IsNaN(tCatch)),
                      ("рабочий зазор не меньше чем за 8 с", tCatch - tReady >= 8),
                      ("пока корпус выше рук, не уже рабочего", early >= Const.ARM_GAP_READY - 0.05),
                      ("когда низ проходит рельсы — рабочий", gapEnter <= Const.ARM_GAP_READY + 0.05),
                      ("к захвату дожат до касания", gapCatch <= 0.3));
                Group($"{name}: захват без телепорта — ложится на рельсы и успокаивается",
                      $"скачок {N(jump)} м и {N(turn)}°, просадка до {N(sagMax)} м, через 1,2 с {N(sagV1)} м/с и {N(tilt1)}°, через 2,5 с {N(tilt2)}°, на столе {N(stowAlt)} м",
                      ("в кадре захвата положение и наклон не прыгают", jump < 0.1 && turn < 0.2),
                      ("каретка проседает на 0,4…2 м", sagMax >= 0.4 && sagMax <= 2),
                      ("через 1,2 с удар погашен (< 0,1 м/с и ±0,1 м)", sagV1 < 0.1 && sag1 < 0.1),
                      ("через 1,2 с корпус почти прям (< 1°)", tilt1 < 1),
                      ("через 2,5 с прям и не скользит", tilt2 < 0.3 && slide2 < 0.05),
                      ("через 6 с каретка стоит", Math.Abs(sagVLate) < 0.05),
                      ("опущен ровно на стол", Math.Abs(stowAlt) < 0.05));
            });
        }
    }
    private static void Rails(Run n, List<Action> checks) {
        SimState sim = n.Sim;
        var arc = new Arc("orbital");
        Rail.Mark[] r100 = null, r600 = null;
        bool[] p140 = null;
        double f140 = double.NaN, f470 = double.NaN;
        int first470 = -1;
        bool caught470 = false;
        n.Each(() => {
            if (sim.T > 700) return;
            arc.Track(sim);
            if (r100 == null && sim.T >= 100) r100 = Rail.Of(sim);
            if (r600 == null && sim.T >= 600) r600 = Rail.Of(sim);
            if (p140 == null && sim.T >= 140) { p140 = (bool[])arc.Passed.Clone(); f140 = arc.Frac(sim.T); }
            if (first470 < 0 && sim.T >= 470) {
                f470 = arc.Frac(sim.T);
                first470 = arc.First;
                caught470 = arc.Passed[Array.IndexOf(arc.Names, "захват ускорителя")];
            }
        });
        checks.Add(() => {
            int sc = Array.IndexOf(arc.Names, "сход с орбиты");
            Group("лента и дуга эфира идут за полётом", $"T+140: метка {N(f140)}; T+470: {N(f470)}, вехи с {first470}; после орбиты: {string.Join(", ", r600.Select(m => m.Name + (m.Past ? "✓" : "")))}",
                  ("на ленте четыре вехи, на T+100 пройдены старт и max Q, следующая — разделение",
                   r100.Length == 4 && r100[0].Past && r100[1].Past && !r100[2].Past && r100[2].Next && !r100[3].Past && !r100[3].Next),
                  ("после выхода на орбиту следующая — сход с орбиты", r600.Any(m => m.Name == "сход с орбиты" && !m.Past)),
                  ("на T+140 дуга прошла старт, max Q и разделение, но не SECO", p140[0] && p140[1] && p140[2] && !p140[4] && !p140[5]),
                  ("метка на T+140 внутри дуги", f140 > 0.35 && f140 < 0.8),
                  ("на T+470 ускоритель пойман, но дуга не кончилась", caught470 && f470 < 0.9),
                  ("окно дуги сдвинулось к сходу с орбиты", first470 > 0 && first470 + arc.Shown > sc));
        });
    }
    private static Func<(bool, int, int, double)> Swing(Run r) {
        Vehicle b = r.B;
        SimState sim = r.Sim;
        double dr0 = double.NaN, ext = double.NaN;
        int flips = 0, side = 0, turns = 0, trend = 0;
        r.Each(() => {
            if (b.Mode != "landB" || b.Alt - Const.CATCH_H > Const.LAND_DHPD) return;
            double x = sim.Downrange(b), th = Vehicle.AngDiff(b.Th, 0) * Const.R2D;
            if (double.IsNaN(dr0)) { dr0 = Math.Abs(x); ext = x; }
            if (trend >= 0 && x < ext - 0.5) { if (trend > 0) turns++; trend = -1; }
            else if (trend <= 0 && x > ext + 0.5) { if (trend < 0) turns++; trend = 1; }
            if (trend > 0) ext = Math.Max(ext, x); else if (trend < 0) ext = Math.Min(ext, x);
            int now = th > 1 ? 1 : th < -1 ? -1 : 0;
            if (now != 0 && side != 0 && now != side) flips++;
            if (now != 0) side = now;
        });
        return () => (b.Caught, turns, flips, dr0);
    }
    private static void Estimation(Run nom, List<Run> runs, List<Action> checks) {
        bool exact = false, seen = false;
        nom.Each(() => {
            if (seen || nom.Sim.T < 200) return;
            seen = true;
            Vehicle b = nom.B;
            exact = b.NavH == 0 && b.NavX == 0 && b.NavVv == 0 && b.NavVh == 0;
        });
        uint rs = 1;
        while (Math.Abs(Dispersion.Roll(rs).RhoK - 1) < 0.02) rs++;
        SimState sim = Windy(rs, true, 10);
        var r = new Run(sim);
        r.Until(() => r.B.Landed || r.B.Crashed);
        runs.Add(r);
        Vehicle bb = r.B;
        var navH = new List<double>();
        var navV = new List<double>();
        double rhoMeco = double.NaN, errEst = 0, errFc = 0, n = 0, lagSum = 0, lagN = 0;
        int tick = 0;
        r.Each(() => {
            tick++;
            if (tick % 100 == 0 && Math.Abs(bb.VVert) < 5) { navH.Add(bb.NavH); navV.Add(bb.NavVh); }
            if (bb.Mode == "coastB" && bb.VVert < -300) { lagSum += (bb.NAlt - bb.Alt) / -bb.VVert; lagN++; }
            if (double.IsNaN(rhoMeco) && sim.T > 60 && bb.Mode != "ascent") rhoMeco = bb.RhoEst;
            if (bb.Mode == "landB" && bb.Alt < 1500) {
                errEst += Math.Abs(bb.WindEst - bb.WindE) * Const.DT;
                errFc += Math.Abs(sim.Wind.Forecast(bb.Alt) - bb.WindE) * Const.DT;
                n += Const.DT;
            }
        });
        checks.Add(() => {
            Head("Наведение по оценке, а не по правде");
            double sH = Spread(navH, out double mH), sV = Spread(navV, out _);
            Group("навигация с шумом и запаздыванием", $"запаздывание {N(lagSum / Math.Max(lagN, 1))} с, высота СКО {N(sH)} м (среднее {N(mH)}), скорость {N(sV)} м/с",
                  ("без разброса навигация точная", exact),
                  ("запаздывает на ~0,1 с", lagN > 100 && Math.Abs(lagSum / lagN - Const.NAV_LAG) < 0.02),
                  ("по высоте порядка метра", sH > 0.3 && sH < 1.6 && Math.Abs(mH) < 0.8),
                  ("по скорости — сотые м/с", sV > 0.02 && sV < 0.2));
            Group("оценки плотности и ветра", $"плотность {N(rhoMeco)} при настоящей {N(sim.RhoK)}; ветер у земли ошибается на {N(errEst / Math.Max(n, 1e-9))} м/с, прогноз — на {N(errFc / Math.Max(n, 1e-9))}",
                  ("плотность по торможению точнее 1 %", Math.Abs(rhoMeco / sim.RhoK - 1) < 0.01),
                  ("ниже 1,5 км оценка ветра лучше прогноза", n > 5 && errEst < errFc && errEst / n < 1.2),
                  ("ускоритель пойман, наводясь по оценкам", bb.Caught));
        });
    }
    private static void LiveCatches(List<Run> runs, List<Action> checks) {
        var cases = new List<(string Name, Run R)>();
        foreach (uint seed in new uint[] { 1, 2 }) cases.Add(($"разброс, зерно {seed}", new Run(Live(seed, true))));
        cases.Add(("разброс и ветер 15 м/с, зерно 9 (порыв у рук)", new Run(Windy(9, true, 15))));
        foreach (uint seed in new uint[] { 14, 17 }) cases.Add(($"разброс и ветер 22 м/с, зерно {seed}", new Run(Windy(seed, true, 22))));
        foreach ((string _, Run r) in cases) { r.Until(() => Run.Over(r.S)); runs.Add(r); }
        checks.Add(() => {
            Head("Живой полёт: разброс и ветер");
            Group("с разбросом обе ступени пойманы", string.Join(", ", cases.Take(3).Select(c => $"{c.Name}: Б {c.R.B.Mode}, К {c.R.S.Mode}")),
                  cases.Take(3).Select(c => (c.Name, c.R.B.Caught && c.R.S.Caught)).ToArray());
            Group("ветер 22 м/с выше допуска: корабль пойман или приводнился, но не разбит (пункты 63–64)",
                  string.Join(", ", cases.Skip(3).Select(c => $"{c.Name}: {c.R.S.Mode}")),
                  cases.Skip(3).Select(c => (c.Name, !c.R.S.Crashed && (c.R.S.Caught || c.R.S.Splash))).ToArray());
        });
    }
    private static void Fallbacks(List<Run> runs, List<Action> checks) {
        Run Add(SimState sim, Func<Run, bool> done) {
            var r = new Run(sim);
            r.Until(() => done(r));
            runs.Add(r);
            return r;
        }
        double tLand = double.NaN;
        Run copv = Add(Scripted("copvLeak"), r => {
            if (r.B.Landed || r.B.Crashed) { if (double.IsNaN(tLand)) tLand = r.Sim.T; return r.Sim.T >= tLand + 30; }
            return false;
        });
        Run relB = Add(Scripted("relightB"), r => Run.Over(r.B));
        Run relS = Add(Scripted("relightS"), r => Run.Over(r.S));
        uint sb = 1, ss = 1;
        while (!Dispersion.Roll(sb).RelightB) sb++;
        while (!(Dispersion.Roll(ss).RelightS && !Dispersion.Roll(ss).RelightB)) ss++;
        Run natB = Add(Live(sb, true), r => Run.Over(r.B));
        Run natS = Add(Live(ss, true), r => Run.Over(r.S));
        Run offB = Add(Live(sb, false), r => Run.Over(r.B));
        SimState gale = Live(12345, false);
        gale.Wind = Wind.Steady(22);
        Run storm = Add(gale, r => Run.Over(r.S));
        checks.Add(() => {
            Head("Запасная посадка: площадка у башни или море");
            Group("поверхность", "",
                  ("стол на нуле, земля и море на 16 м ниже", SimState.Surface(0) == 0 && SimState.Surface(200) == -Const.DECK_H),
                  ("за берегом море, площадка и стол на суше", SimState.Water(Const.COAST_DR + 10) && !SimState.Water(Const.PAD_DR) && !SimState.Water(0)));
            Vehicle b = copv.B;
            double dr = copv.Sim.Downrange(b);
            Group("утечка наддува: захват отменён до импульса, ускоритель приводняется", $"{b.Mode}, {N(dr)} м, на воде {N(Math.Abs(b.Th) * Const.R2D)}°",
                  ("отмена с уходом в море", b.Site == "sea" && Logged(copv.Sim, "утечка газа наддува: уход в море")),
                  ("мягко у точки в 6 км", b.Landed && !b.Crashed && b.Splash && Math.Abs(dr - Const.SEA_DR) < 150),
                  ("на воде заваливается набок", Math.Abs(Math.Abs(Vehicle.AngDiff(b.Th, 0)) - Math.PI / 2) < 0.05));
            Group("не зажглись двигатели на посадку: ускоритель в море, корабль на площадку",
                  $"Б {relB.B.Mode} {N(relB.Sim.Downrange(relB.B))} м; К {relS.S.Mode} {N(relS.Sim.Downrange(relS.S) - Const.PAD_DR)} м от центра площадки",
                  ("ускоритель уходит от башни и приводняется", relB.B.Landed && !relB.B.Crashed && relB.B.Splash && !relB.B.Caught && Logged(relB.Sim, "посадочных двигателей: уход в море")),
                  ("корабль уходит на площадку", relS.S.Site == "pad" && Logged(relS.Sim, "уход на площадку у башни")),
                  ("и садится на неё, а не в руки", relS.S.Landed && !relS.S.Crashed && !relS.S.Caught
                                                    && Math.Abs(relS.Sim.Downrange(relS.S) - Const.PAD_DR) < 15 && Math.Abs(relS.S.Alt + Const.DECK_H) < 0.1));
            Group("естественный отказ на посадку (2 %) ведёт туда же", $"зерно {sb}: Б {natB.B.Mode}; зерно {ss}: К {natS.S.Mode}",
                  ("ускоритель приводняется", natB.B.Splash && natB.B.Landed && !natB.B.Crashed && Logged(natB.Sim, "уход в море")),
                  ("корабль садится на площадку", natS.S.Site == "pad" && natS.S.Landed && !natS.S.Crashed && Logged(natS.Sim, "уход на площадку у башни")),
                  ("без разброса того же зерна отказа нет", offB.B.Caught));
            True("ветер 22 м/с без разброса: опрос уводит обе ступени в море",
                 storm.B.Splash && storm.S.Splash && !storm.B.Crashed && !storm.S.Crashed && Logged(storm.Sim, "ветер у башни"),
                 $"Б {storm.B.Mode} {N(storm.Sim.Downrange(storm.B))} м, К {storm.S.Mode} {N(storm.Sim.Downrange(storm.S))} м");
        });
    }
}
