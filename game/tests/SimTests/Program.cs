using System;
using System.Globalization;
using Starship.Physics;
namespace Starship.Tests;
internal static partial class Program {
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static int _ok, _fail, _todo;
    private static void Head(string s) {
        Console.WriteLine();
        Console.WriteLine("— " + s);
    }
    private static string N(double v) =>
        Math.Abs(v) >= 1e-3 && Math.Abs(v) < 1e6 ? v.ToString("G6", Inv) : v.ToString("G4", Inv);
    private static void Near(string name, double got, double want, double rel, string unit = "") {
        bool ok = Math.Abs(got - want) <= Math.Abs(want) * rel + 1e-12;
        if (ok) _ok++; else _fail++;
        Console.WriteLine($"{(ok ? "  ok  " : "ПРОВАЛ")} {name}: {N(got)} против {N(want)}{unit}"
                          + (ok ? "" : $"   (расхождение {100 * Math.Abs(got - want) / Math.Abs(want):F2} %, допуск {100 * rel:F2} %)"));
    }
    private static void True(string name, bool cond, string detail = "") {
        if (cond) _ok++; else _fail++;
        Console.WriteLine($"{(cond ? "  ok  " : "ПРОВАЛ")} {name}"
                          + (detail.Length > 0 ? "   " + detail : ""));
    }
    private static void Todo(int task, string name, bool cond, string detail = "") {
        _todo++;
        Console.WriteLine($"{(cond ? " ГОТОВО" : " ждёт ")} задание {task}: {name}"
                          + (detail.Length > 0 ? "   " + detail : ""));
    }
    private static int Main() {
        Console.WriteLine("Проверка физики по функциям. Задания — из PHYSICS_GAP.md.");
        Atmosphere_();
        Drag();
        AeroForces();
        EngineThrust();
        ChamberCooling();
        PumpCavitation();
        CenterOfMass();
        HeatShield();
        WindProfile();
        CatchInWind();
        BoosterReturn();
        FlightMarks();
        OrbitElements();
        MassBalance();
        BadNumbers();
        UiNumbers();
        UiThemes();
        UiEngineLayout();
        UiParams();
        UiEngine();
        UiParse();
        UiAir();
        Console.WriteLine();
        Console.WriteLine($"итог: пройдено {_ok}, провалов {_fail}, ждут заданий {_todo}");
        return _fail == 0 ? 0 : 1;
    }
    private static void Atmosphere_() {
        Head("Атмосфера: опорные высоты USSA-76");
        (double H, double Rho, double T)[] nodes = {
            (0, 1.2250, 288.15),
            (11000, 0.36392, 216.65),
            (20000, 0.088035, 216.65),
            (32000, 0.013225, 228.65),
            (47000, 0.0014275, 270.65),
            (71000, 6.4211e-5, 214.65),
            (84852, 6.9578e-6, 186.95),
        };
        foreach ((double h, double rho, double t) in nodes) {
            Air a = Atmosphere.At(h);
            Near($"плотность на {h / 1000:F1} км", a.Rho, rho, 0.01, " кг/м³");
            Near($"температура на {h / 1000:F1} км", a.T, t, 0.005, " К");
        }
        Air sl = Atmosphere.At(0);
        Near("давление на уровне моря", sl.P, 101325, 0.001, " Па");
        Near("скорость звука на уровне моря", sl.A, 340.29, 0.005, " м/с");
        Head("Атмосфера: термосфера и экзосфера");
        (double H, double Rho)[] high = {
            (100e3, 5.6040e-7), (130e3, 8.1520e-9), (200e3, 2.789e-10),
            (400e3, 3.725e-12), (1000e3, 3.019e-15),
        };
        foreach ((double h, double rho) in high)
            Near($"плотность на {h / 1000:F0} км", Atmosphere.At(h).Rho, rho, 0.01, " кг/м³");
        Air below = Atmosphere.At(90e3 - 0.1), above = Atmosphere.At(90e3 + 0.1);
        Near("плотность непрерывна на стыке 90 км", above.Rho, below.Rho, 0.01, " кг/м³");
        Near("температура непрерывна на стыке 90 км", above.T, below.T, 0.01, " К");
        bool falls = true;
        double prev = double.MaxValue;
        for (double h = 90e3; h <= 1200e3; h += 5e3) {
            double r = Atmosphere.At(h).Rho;
            if (r <= 0 || r >= prev) { falls = false; break; }
            prev = r;
        }
        True("плотность убывает и остаётся положительной до 1200 км", falls);
        Air exo = Atmosphere.At(1200e3);
        True("выше таблицы плотность продолжается экспонентой", exo.Rho > 0 && exo.Rho < 3.019e-15,
             $"на 1200 км {N(exo.Rho)} кг/м³");
        Near("температура экзосферы стремится к 1000 К", Atmosphere.At(600e3).T, 1000, 0.01, " К");
    }
    private static void Drag() {
        Head("Коэффициент сопротивления по числу Маха");
        double peak = Atmosphere.Cd0(1.05);
        True("трансзвуковой пик выше дозвука", peak > Atmosphere.Cd0(0.6),
             $"{N(peak)} против {N(Atmosphere.Cd0(0.6))}");
        True("трансзвуковой пик выше сверхзвука", peak > Atmosphere.Cd0(2.0),
             $"{N(peak)} против {N(Atmosphere.Cd0(2.0))}");
        True("после M 1,3 кривая монотонно падает",
             Atmosphere.Cd0(2) > Atmosphere.Cd0(3) && Atmosphere.Cd0(3) > Atmosphere.Cd0(5)
             && Atmosphere.Cd0(5) > Atmosphere.Cd0(8),
             $"{N(Atmosphere.Cd0(2))} → {N(Atmosphere.Cd0(3))} → {N(Atmosphere.Cd0(5))} → {N(Atmosphere.Cd0(8))}");
        Near("двигателями вперёд на M 3", Atmosphere.CdEngine(3.0), 1.75, 1e-9);
        Near("двигателями вперёд на дозвуке", Atmosphere.CdEngine(0.5), 1.00, 1e-9);
        True("выше M 5 кривая выполаживается",
             Atmosphere.CdEngine(5) == Atmosphere.CdEngine(12),
             $"{N(Atmosphere.CdEngine(5))} на M 5 и M 12");
        True("тупой торец тормозит сильнее носа на всём сверхзвуке",
             Atmosphere.CdEngine(2) > Atmosphere.Cd0(2) && Atmosphere.CdEngine(8) > Atmosphere.Cd0(8),
             $"на M 8: {N(Atmosphere.CdEngine(8))} против {N(Atmosphere.Cd0(8))}");
        Near("носом вперёд берётся носовая кривая", Atmosphere.CdAxial(3, 1), Atmosphere.Cd0(3), 1e-9);
        Near("хвостом вперёд берётся кривая торца", Atmosphere.CdAxial(3, -1), Atmosphere.CdEngine(3), 1e-9);
    }
    private static Vehicle AtSpeed(Kind kind, double alt, double vx, double vy, double th) {
        var v = new Vehicle(kind, 60e3);
        v.X = 0;
        v.Y = Const.RE + alt;
        v.Vx = vx;
        v.Vy = vy;
        v.Th = th;
        return v;
    }
    private static void AeroForces() {
        Head("Аэродинамические силы");
        Vehicle up = AtSpeed(Kind.Booster, 8000, -Const.W * (Const.RE + 8000), 400, 0);
        Air air = Atmosphere.At(up.Alt);
        double sp = up.Speed;
        AeroForce f0 = Aero.Compute(up, 0.5 * air.Rho * sp * sp, sp / air.A);
        Near("угол атаки при вертикальном подъёме", f0.Alpha * Const.R2D, 0, 0, "°");
        Near("нормальная сила при α = 0", f0.CN, 0, 0);
        True("осевая сила тормозит", f0.Fax < 0, $"Fax = {N(f0.Fax)} Н");
        var sim = new SimState { T = 100, RhoK = 1 };
        Vehicle v = AtSpeed(Kind.Booster, 12000, -Const.W * (Const.RE + 12000) + 300, -180, 2.2);
        v.Prop = v.PropMax * 0.2;
        sim.Veh.Add(v);
        Air a2 = Atmosphere.At(v.Alt);
        double sp2 = v.Speed;
        AeroForce ref2 = Aero.Compute(v, 0.5 * a2.Rho * sp2 * sp2, sp2 / a2.A);
        Flight.StepVehicle(sim, v, Const.DT);
        Near("Aero.Compute и Flight.StepVehicle дают одно сопротивление",
             v.Drag, ref2.Drag, 1e-9, " Н");
    }
    private static Engine Spun(Kind kind, double pa, double seconds) {
        var v = new Vehicle(kind, 60e3);
        v.Ign = true;
        v.NEng = kind == Kind.Booster ? 33 : 6;
        v.Throttle = 1;
        const double dt = 0.02;
        for (int i = 0; i * dt < seconds; i++) {
            Pressurant.Step(v, dt);
            EngineSet.Update(v, dt, pa);
            EngStats st = EngineSet.Stats(v);
            v.F = st.F;
            v.Mdot = st.Md;
            v.Prop = Math.Max(0, v.Prop - st.Md * dt);
        }
        return v.Eng[0];
    }
    private static void EngineThrust() {
        Head("Двигатель: высотная характеристика");
        Engine sea = Spun(Kind.Booster, Const.P0, 8);
        Engine vac = Spun(Kind.Booster, 0, 8);
        True("двигатель вышел на режим", sea.Spool > 0.99 && sea.F > 1e6,
             $"режим {N(sea.Spool)}, тяга {N(sea.F / 1e3)} кН");
        Near("прибавка тяги в вакууме равна Ae·P₀",
             vac.F - sea.F, sea.Spec.Ae * Const.P0, 0.005, " Н");
        Near("тяга в вакууме сходится с паспортной",
             vac.F, Spec.RaptorSL.Fv, 0.02, " Н");
        Head("Сопло: геометрия из паспортных чисел");
        EngineSpec sl = Spec.RaptorSL, vc = Spec.RaptorVac;
        Near("диаметр горловины", 2 * Math.Sqrt(sl.At / Math.PI), 0.240, 0.02, " м");
        Near("диаметр среза", 2 * Math.Sqrt(sl.Ae / Math.PI), 1.365, 0.02, " м");
        True("степень расширения у земного сопла около 32", sl.Eps > 30 && sl.Eps < 35, $"{N(sl.Eps)}");
        True("у вакуумного сопла расширение заметно больше", vc.Eps > sl.Eps * 1.5,
             $"{N(vc.Eps)} против {N(sl.Eps)}");
        True("множитель реальности близок к единице", sl.Real > 1.0 && sl.Real < 1.06,
             $"{N(sl.Real)} — идеальная газодинамика недобирает к паспорту");
        True("число Маха на срезе сверхзвуковое и растёт с расширением",
             EngineSpec.ExitMach(vc.Eps) > EngineSpec.ExitMach(sl.Eps) && EngineSpec.ExitMach(sl.Eps) > 3,
             $"M {N(EngineSpec.ExitMach(sl.Eps))} и {N(EngineSpec.ExitMach(vc.Eps))}");
        Near("в вакууме коэффициент тяги равен идеальному", sl.CfAt(EngineSpec.PC_PA, 0),
             sl.CfVac * sl.Real, 1e-12);
        True("внешнее давление съедает коэффициент тяги",
             sl.CfAt(EngineSpec.PC_PA, Const.P0) < sl.CfAt(EngineSpec.PC_PA, 0));
        True("земное сопло у земли не срывается", !sl.Separated(EngineSpec.PC_PA, Const.P0),
             $"давление на срезе {N(sl.Pe / 1000)} кПа против порога {N(0.4 * Const.P0 / 1000)} кПа");
        True("вакуумное сопло у земли срывается", vc.Separated(EngineSpec.PC_PA, Const.P0),
             $"давление на срезе {N(vc.Pe / 1000)} кПа");
        True("в вакууме не срывается ни то, ни другое",
             !sl.Separated(EngineSpec.PC_PA, 0) && !vc.Separated(EngineSpec.PC_PA, 0));
        True("на глубоком дросселировании земное сопло срывается",
             sl.Separated(EngineSpec.PC_PA * 0.25, Const.P0),
             "давление в камере вчетверо ниже — скачок входит внутрь");
        EngineSpec wide = sl.WithExitArea(2);
        True("множитель площади единица оставляет сопло тем же", ReferenceEquals(sl.WithExitArea(1), sl));
        Near("удвоение среза удваивает расширение", wide.Eps, sl.Eps * 2, 1e-12);
        True("у длинного сопла давление на срезе ниже", wide.Pe < sl.Pe,
             $"{N(wide.Pe / 1000)} против {N(sl.Pe / 1000)} кПа");
        True("в пустоте оно даёт больше тяги", wide.CfAt(EngineSpec.PC_PA, 0) > sl.CfAt(EngineSpec.PC_PA, 0),
             $"Cf {N(wide.CfAt(EngineSpec.PC_PA, 0))} против {N(sl.CfAt(EngineSpec.PC_PA, 0))}");
        True("а у земли срывается там, где паспортное держит",
             wide.Separated(EngineSpec.PC_PA, Const.P0) && !sl.Separated(EngineSpec.PC_PA, Const.P0));
        True("у земли выигрыша уже нет", wide.CfAt(EngineSpec.PC_PA, Const.P0) < sl.CfAt(EngineSpec.PC_PA, Const.P0));
        True("на отрыве признак поднят", vc.Separated(EngineSpec.PC_PA, Const.P0));
        True("в пустоте отрыва нет", !sl.Separated(EngineSpec.PC_PA, 0));
        double cfWhole = vc.CfVac * vc.Real - Const.P0 * vc.Eps / (EngineSpec.PC_PA * 0.5);
        double cfSep = vc.CfAt(EngineSpec.PC_PA * 0.5, Const.P0);
        True("отрыв срезает перерасширенную часть и тяга выше идеальной", cfSep > cfWhole,
             $"Cf {N(cfSep)} против {N(cfWhole)} у целого сопла");
        True("чем глубже дроссель, тем больше выигрыш от отрыва",
             vc.CfAt(EngineSpec.PC_PA * 0.4, Const.P0)
             - (vc.CfVac * vc.Real - Const.P0 * vc.Eps / (EngineSpec.PC_PA * 0.4))
             > cfSep - cfWhole,
             $"на 40 % тяги против 50 %");
        True("работающая часть сопла короче полной",
             vc.CfAt(EngineSpec.PC_PA * 0.4, Const.P0) < vc.CfAt(EngineSpec.PC_PA * 0.4, 0),
             "в пустоте то же сопло даёт больше");
        var vh = new Vehicle(Kind.Booster, 0);
        vh.Ign = true; vh.NEng = 33; vh.Throttle = 1;
        Engine one = vh.Eng[0];
        for (int i = 0; i < 100; i++) one.Update(0.02, Const.P0, 1, true);
        int mismatch = 0, coast = 0;
        for (int i = 0; i < 200; i++) {
            one.Update(0.02, Const.P0, 0, false);
            if (one.Spool <= 0) break;
            coast++;
            if (one.Spec.Separated(one.Pc * 1e6, Const.P0) != one.Sep) mismatch++;
        }
        True("на выбеге признак отрыва не расходится с расчётом", mismatch == 0,
             $"{coast} шагов выбега, расхождений {mismatch}");
    }
    private static Engine Run(double seconds, double cool, double throttle = 1) {
        var v = new Vehicle(Kind.Booster, 60e3);
        v.Ign = true;
        v.NEng = 33;
        v.Throttle = throttle;
        foreach (Engine e in v.Eng) e.P.Cool = cool;
        const double dt = 0.02;
        for (int i = 0; i * dt < seconds; i++) {
            Pressurant.Step(v, dt);
            EngineSet.Update(v, dt, Const.P0);
            EngStats st = EngineSet.Stats(v);
            v.F = st.F;
            v.Mdot = st.Md;
            v.Prop = Math.Max(0, v.Prop - st.Md * dt);
        }
        return v.Eng[0];
    }
    private static void ChamberCooling() {
        Head("Охлаждение камеры и прогар стенки");
        Engine nom = Run(20, 1.0);
        True("стенка держится в рабочем диапазоне", nom.TWall > 700 && nom.TWall < 950,
             $"{N(nom.TWall)} К при пределе 950 К");
        True("поток в стенку того же порядка, что у настоящих камер",
             nom.QWall > 50e6 && nom.QWall < 160e6, $"{N(nom.QWall / 1e6)} МВт/м²");
        Near("на номинале прогар не копится", nom.Burn, 0, 0);
        True("двигатель жив", !nom.Failed);
        Engine half = Run(20, 0.6);
        True("прикрытый наполовину клапан греет стенку выше предела", half.TWall > 950,
             $"{N(half.TWall)} К");
        True("но за двадцать секунд прогореть не успевает", !half.Failed,
             $"повреждение {N(half.Burn)}");
        Engine shut = Run(40, 0.3);
        True("клапан на треть прогорает за десятки секунд", shut.Failed,
             $"стенка {N(shut.TWall)} К, отказ: {shut.Reason}");
        double hFull = ChamberHeat.GasSide(0.24, 30e6, EngineSpec.CSTAR);
        double hHalf = ChamberHeat.GasSide(0.24, 15e6, EngineSpec.CSTAR);
        Near("теплоотдача газовой стороны идёт как p^0,8", hHalf / hFull, Math.Pow(0.5, 0.8), 1e-9);
        Engine low = Run(20, 1.0, 0.5);
        Near("на дросселе равновесие стенки то же", low.TWall, nom.TWall, 0.02, " К");
        True("но поток в стенку падает", low.QWall < nom.QWall * 0.85,
             $"{N(low.QWall / 1e6)} против {N(nom.QWall / 1e6)} МВт/м²");
        Near("температура восстановления ниже температуры газа",
             ChamberHeat.Recovery(3500, EngineSpec.GAMMA, 0.5, 1.0), 3434, 0.01, " К");
        var idle = new Vehicle(Kind.Booster, 60e3);
        idle.Ign = true;
        idle.NEng = 33;
        idle.Throttle = 1;
        for (int i = 0; i < 400; i++) {
            Pressurant.Step(idle, 0.02);
            EngineSet.Update(idle, 0.02, Const.P0);
            EngStats st = EngineSet.Stats(idle);
            idle.Mdot = st.Md;
            idle.Prop = Math.Max(0, idle.Prop - st.Md * 0.02);
        }
        idle.Ign = false;
        idle.NEng = 0;
        for (int i = 0; i < 300; i++) EngineSet.Update(idle, 0.02, Const.P0);
        Engine off = idle.Eng[0];
        True("после выключения стенка остывает, а не прогорает",
             !off.Failed && off.TWall < 900, $"{N(off.TWall)} К, повреждение {N(off.Burn)}");
    }
    private static void PumpCavitation() {
        Head("Турбонасос: кавитация при просадке на входе");
        var v = new Vehicle(Kind.Booster, 60e3);
        v.Ign = true;
        v.NEng = 33;
        v.Throttle = 1;
        const double dt = 0.02;
        for (int i = 0; i < 400; i++) {
            Pressurant.Step(v, dt);
            EngineSet.Update(v, dt, Const.P0);
            EngStats st = EngineSet.Stats(v);
            v.F = st.F;
            v.Mdot = st.Md;
            v.Prop = Math.Max(0, v.Prop - st.Md * dt);
        }
        Pump pf = v.Eng[0].Pf;
        double cavGood = pf.Cav, headGood = pf.Head;
        True("на штатном наддуве кавитации нет", cavGood > 0.999, $"запас {N(cavGood)}");
        v.Tanks.F.P = 40e3;
        v.Tanks.F.Mg = v.Tanks.F.P * Math.Max(v.Tanks.F.V * 0.02, 1) / (v.Tanks.F.R * 270);
        for (int i = 0; i < 60; i++) {
            EngineSet.Update(v, dt, Const.P0);
            v.Tanks.F.P = 40e3;
        }
        True("при срезанном наддуве насос кавитирует", pf.Cav < cavGood && pf.Cav < 1.0,
             $"{N(cavGood)} → {N(pf.Cav)}");
        True("напор просел вслед за кавитацией", pf.Head < headGood,
             $"{N(headGood)} → {N(pf.Head)} м");
    }
    private static void CenterOfMass() {
        Head("Центр масс по мере выработки");
        var v = new Vehicle(Kind.Booster, 60e3);
        v.Prop = v.PropMax;
        double full = v.Cm;
        v.Prop = v.PropMax * 0.5;
        double half = v.Cm;
        v.Prop = v.PropMax * 0.15;
        double low = v.Cm;
        v.Prop = 0;
        double dry = v.Cm;
        True("на основной выработке центр масс опускается", full > half && half > low,
             $"{N(full)} → {N(half)} → {N(low)} м от донного среза");
        True("на сухой ступени центр масс возвращается к конструкции", dry > low,
             $"{N(low)} → {N(dry)} м, конструкция 0,38·L = {N(0.38 * v.Len)} м");
        Near("центр масс сухой ступени равен центру конструкции", dry, 0.38 * v.Len, 1e-9, " м");
        True("центр масс не выходит за корпус", dry > 0 && full < v.Len,
             $"корпус 0…{N(v.Len)} м");
        var st = new Vehicle(Kind.Booster, 60e3) { Mate = new Vehicle(Kind.Ship, 60e3) };
        st.Stacked = true;
        True("состыкованный пакет поднимает центр масс выше отдельной ступени",
             st.Cm > full, $"{N(full)} → {N(st.Cm)} м");
    }
    private static void OrbitElements() {
        Head("Орбитальные элементы");
        double r = Const.RE + 200e3;
        double vc = Math.Sqrt(Const.MU / r);
        var v = new Vehicle(Kind.Ship, 0) { X = 0, Y = r, Vx = -vc, Vy = 0 };
        Orbit o = Guidance.Orb(v);
        Near("круговая орбита: апогей", o.Apo, 200e3, 1e-6, " м");
        Near("круговая орбита: перигей", o.Peri, 200e3, 1e-6, " м");
        Near("эксцентриситет круговой орбиты", o.E, 0, 1e-9);
        Near("период круговой орбиты 200 км", o.Per, 5301.0, 0.001, " с");
        double ra = Const.RE + 400e3;
        double vp = Math.Sqrt(2 * Const.MU * ra / (r * (r + ra)));
        var e = new Vehicle(Kind.Ship, 0) { X = 0, Y = r, Vx = -vp, Vy = 0 };
        Orbit oe = Guidance.Orb(e);
        Near("эллипс 200 × 400: апогей", oe.Apo, 400e3, 1e-6, " м");
        Near("эллипс 200 × 400: перигей", oe.Peri, 200e3, 1e-6, " м");
    }
    private static double Soak(Vehicle v, double heatKw, double alphaDeg, double seconds) {
        var sim = new SimState { T = 0 };
        sim.Veh.Add(v);
        v.Heat = heatKw;
        v.Alpha = alphaDeg * Const.D2R;
        const double dt = 0.05;
        for (int i = 0; i * dt < seconds; i++) {
            v.Heat = heatKw;
            v.Alpha = alphaDeg * Const.D2R;
            Physics.Sim.Thermal(sim, v, dt);
            sim.T += dt;
        }
        return v.Dmg;
    }
    private static void HeatShield() {
        Head("Теплозащита: две стороны и прогар");
        var ok = new Vehicle(Kind.Ship, 0);
        Soak(ok, 305, 67, 400);
        True("плитки выходят на рабочую температуру", ok.TTile > 1400 && ok.TTile < 1600,
             $"{N(ok.TTile)} К при пределе {N(Const.TILE_LIMIT)} К");
        True("подветренная сталь остаётся холодной", ok.TLee < 900,
             $"{N(ok.TLee)} К при пределе {N(Const.SKIN_LIMIT)} К");
        Near("штатный вход не копит прогар", ok.Dmg, 0, 0);
        var flip = new Vehicle(Kind.Ship, 0);
        double dmg = Soak(flip, 305, -67, 400);
        True("развёрнутый спиной корабль прогорает", dmg >= 1 && flip.Crashed,
             $"повреждение {N(dmg)}, сталь дошла до {N(flip.MaxLee)} К");
        True("на развороте плитки остаются целыми", flip.TTile < Const.TILE_LIMIT,
             $"{N(flip.TTile)} К — они в тени");
        var mild = new Vehicle(Kind.Ship, 0);
        Soak(mild, 40, -67, 400);
        True("слабый поток разворот прощает", !mild.Crashed && mild.Dmg < 1,
             $"сталь {N(mild.MaxLee)} К, повреждение {N(mild.Dmg)}");
        var boost = new Vehicle(Kind.Booster, 0);
        Soak(boost, 305, 67, 400);
        True("голая сталь под тем же потоком горячее плитки",
             boost.TTile > ok.TTile, $"{N(boost.TTile)} К против {N(ok.TTile)} К");
        True("ускоритель под орбитальным потоком прогорел бы", boost.Crashed,
             $"предел стали {N(Const.SKIN_LIMIT)} К");
        var real = new Vehicle(Kind.Booster, 0);
        Soak(real, 20, 67, 400);
        True("на своём входе ускоритель не прогорает", !real.Crashed && real.TTile < Const.SKIN_LIMIT,
             $"{N(real.TTile)} К");
    }
    private static void WindProfile() {
        Head("Ветер: профиль и его след на выведении");
        var rng = new Rng();
        rng.Seed(12345);
        Wind w = Wind.Roll(rng);
        True("на земле ветер есть", Math.Abs(w.At(0)) > 0.5, $"{N(w.At(0))} м/с");
        True("струйное течение сильнее приземного",
             Math.Abs(w.At(11e3)) > Math.Abs(w.At(0)),
             $"{N(w.At(11e3))} м/с на 11 км против {N(w.At(0))} у земли");
        Near("выше 60 км ветра нет", w.At(70e3), 0, 0, " м/с");
        Near("на самой границе ветер уже нулевой", w.At(Wind.TopAlt), 0, 0, " м/с");
        True("к границе профиль сходит плавно", Math.Abs(w.At(55e3)) < Math.Abs(w.At(30e3)),
             $"{N(w.At(55e3))} против {N(w.At(30e3))} м/с");
        True("сдвиг по высоте заметен на участке максимального напора",
             w.MaxShear(6e3, 16e3) > 1.0, $"{N(w.MaxShear(6e3, 16e3))} м/с на км");
        var rng2 = new Rng();
        rng2.Seed(12345);
        Wind same = Wind.Roll(rng2);
        var rng3 = new Rng();
        rng3.Seed(777);
        Wind other = Wind.Roll(rng3);
        Near("одно зерно даёт тот же профиль", same.At(9e3), w.At(9e3), 1e-12, " м/с");
        True("разные зёрна дают разный профиль", Math.Abs(other.At(9e3) - w.At(9e3)) > 0.5,
             $"{N(w.At(9e3))} против {N(other.At(9e3))} м/с");
        var v = new Vehicle(Kind.Ship, 0) { X = 0, Y = Const.RE + 9e3, Vx = -Const.W * (Const.RE + 9e3), Vy = 300 };
        Vec2 ground = v.VRel;
        v.WindE = 30;
        Vec2 air = v.VAir;
        double diff = Math.Sqrt(Math.Pow(air.X - ground.X, 2) + Math.Pow(air.Y - ground.Y, 2));
        Near("воздушная скорость отличается от земной ровно на ветер", diff, 30, 1e-9, " м/с");
        Near("ветер не трогает земную скорость", v.VRel.Len, ground.Len, 1e-12, " м/с");
        True("в штиль ветер всюду конечен и нулевой",
             !double.IsNaN(Wind.Calm().At(0)) && Wind.Calm().At(0) == 0
             && Wind.Calm().At(9e3) == 0, $"{N(Wind.Calm().At(0))} м/с у земли");
        True("постоянный ветер даёт заданное у земли и затухает выше",
             Math.Abs(Wind.Steady(12).At(0) - 12) < 1e-9
             && Math.Abs(Wind.Steady(12).At(30e3)) < 0.5,
             $"{N(Wind.Steady(12).At(0))} м/с у земли, {N(Wind.Steady(12).At(30e3))} на 30 км");
        double aCalm = AscentAoA(Wind.Calm());
        double aWind = AscentAoA(w);
        True("без ветра угол атаки на 8…14 км мал", aCalm < 5, $"{N(aCalm)}°");
        True("со сдвигом ветра угол атаки заметно растёт", aWind > aCalm + 1.0,
             $"{N(aCalm)}° → {N(aWind)}°");
    }
    private static void FlightMarks() {
        Head("Вехи полёта для разбора записи");
        var sim = new SimState { Mission = "orbital", AnomOn = false };
        Physics.Sim.Reset(sim, 12345);
        True("до старта уже есть веха предстартовой подготовки", sim.Marks.Count == 1,
             sim.Marks.Count > 0 ? sim.Marks[0].M : "нет вех");
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        for (int i = 0; i < 1_200_000 && !(s.Landed || s.Crashed); i++)
            Physics.Sim.Tick(sim, Const.DT);
        True("вехи набрались", sim.Marks.Count > 8, $"{sim.Marks.Count} штук");
        bool sorted = true;
        for (int i = 1; i < sim.Marks.Count; i++)
            if (sim.Marks[i].T < sim.Marks[i - 1].T) sorted = false;
        True("вехи идут по возрастанию времени", sorted);
        True("отсчёт секунд в вехи не попадает",
             !sim.Marks.Exists(m => m.M.StartsWith("Отсчёт")),
             $"вех {sim.Marks.Count}, строк журнала {sim.Log.Count}");
        string all = string.Join(" | ", sim.Marks.ConvertAll(m => m.M));
        foreach (string want in new[] { "башню", "разделение", "Max Q", "ЗАХВАТ", "ОРБИТУ", "жига" })
            True($"среди вех есть «{want}»", all.Contains(want));
        True("последняя веха не позже конца полёта", sim.Marks[^1].T <= sim.T + 1e-9,
             $"T+{sim.Marks[^1].T:F0} при полёте {sim.T:F0} с");
    }
    private static void CatchInWind() {
        Head("Ловля ускорителя при приземном ветре");
        (bool caught, double miss, double drift) calm = LandInWind(Wind.Calm());
        True("в штиль ускоритель пойман", calm.caught, $"промах {N(calm.miss)} м");
        foreach (double surf in new[] { 8.0, -8.0 }) {
            (bool caught, double miss, double drift) r = LandInWind(Wind.Steady(surf));
            True($"при ветре {N(surf)} м/с у земли ускоритель пойман", r.caught,
                 $"промах {N(r.miss)} м, снос по воздуху {N(r.drift)} м/с");
            True($"при ветре {N(surf)} м/с промах не хуже полутора метров сверх штиля",
                 Math.Abs(r.miss) < Math.Abs(calm.miss) + 1.5,
                 $"{N(r.miss)} против {N(calm.miss)} м");
        }
        (bool caught, double miss, double drift) hard = LandInWind(Wind.Steady(15));
        True("ловля держит приземный ветер 15 м/с", hard.caught,
             $"промах {N(hard.miss)} м при допуске {N(Const.CATCH_DR)} м");
    }
    private static (bool, double, double) LandInWind(Wind wind) {
        var sim = new SimState { Mission = "orbital", AnomOn = false };
        Physics.Sim.Reset(sim, 12345);
        sim.Wind = wind;
        Vehicle b = sim.Veh[0];
        double drift = 0;
        for (int i = 0; i < 200000 && !b.Landed && !b.Crashed && !b.Caught; i++) {
            Physics.Sim.Tick(sim, Const.DT);
            if (b.Alt < 3e3 && b.Alt > 100) drift = Math.Max(drift, Math.Abs(b.WindE));
        }
        return (b.Caught, sim.Downrange(b), drift);
    }
    private static void BoosterReturn() {
        Head("Возврат ускорителя: без импульса входа, жига 13 → 3");
        var sim = new SimState { Mission = "orbital", AnomOn = false };
        Physics.Sim.Reset(sim, 12345);
        Vehicle b = sim.Veh[0];
        bool boostbackSeen = false, coasting = false;
        double litCoast = 0, hIgn = double.NaN, qDesc = 0, tIgn = double.NaN;
        int nIgn = 0, nLast = 0;
        var counts = new System.Collections.Generic.SortedSet<int>();
        for (int i = 0; i < 200000 && !b.Landed && !b.Crashed && !b.Caught; i++) {
            Physics.Sim.Tick(sim, Const.DT);
            if (b.Mode == "boostback") boostbackSeen = true;
            else if (boostbackSeen && !b.IgnBurn) coasting = true;
            if (coasting && !b.IgnBurn && b.NRun > 0) litCoast += Const.DT;
            if (coasting && b.Q > qDesc) qDesc = b.Q;
            if (!b.IgnBurn || b.NRun == 0) continue;
            if (double.IsNaN(hIgn)) { hIgn = b.Alt; nIgn = b.NRun; tIgn = sim.T; }
            counts.Add(b.NRun);
            nLast = b.NRun;
        }
        True("между тормозным импульсом и жигой двигатели ускорителя молчат", litCoast == 0,
             $"работали {N(litCoast)} с");
        True("жига начинается ниже 2 км", hIgn < 2000, $"с высоты {N(hIgn)} м");
        True("жига начинается выше 500 м", hIgn > 500, $"с высоты {N(hIgn)} м");
        True("жига зажигает 13 двигателей", nIgn == 13, $"{nIgn}");
        True("жига кончается на трёх", nLast == 3, $"{nLast}");
        True("в жиге только 13 или 3 двигателя", counts.SetEquals(new[] { 13, 3 }),
             string.Join(", ", counts));
        True("ускоритель пойман", b.Caught, $"промах {N(sim.Downrange(b))} м");
        True("напор на спуске ниже предела конструкции", qDesc < 200e3, $"{N(qDesc / 1000)} кПа");
        double burn = b.Caught ? sim.T - tIgn : double.NaN;
        True("от зажигания жиги до захвата не больше 30 с", burn <= 30, $"{N(burn)} с");
    }
    private static double AscentAoA(Wind wind) {
        var sim = new SimState { Mission = "orbital", AnomOn = false };
        Physics.Sim.Reset(sim, 12345);
        sim.Wind = wind;
        Vehicle b = sim.Veh[0];
        double worst = 0;
        for (int i = 0; i < 200000 && b.Alt < 16e3; i++) {
            Physics.Sim.Tick(sim, Const.DT);
            if (b.Alt < 8e3 || b.Alt > 14e3) continue;
            double a = Math.Abs(b.Alpha) * Const.R2D;
            if (a > worst) worst = a;
        }
        return worst;
    }
    private static void MassBalance() {
        Head("Баланс массы за 100 с работы");
        var sim = new SimState { T = 0, RhoK = 1 };
        var v = new Vehicle(Kind.Booster, 60e3);
        v.Mode = "ascent";
        v.Y = Const.RE + 2000;
        v.Vx = -Const.W * v.Y + 60;
        v.Vy = 210;
        v.Ign = true;
        v.NEng = 33;
        v.Throttle = 1;
        v.ThCmd = 0.2;
        sim.Veh.Add(v);
        double prop0 = v.Prop, spent = 0;
        const double dt = Const.DT;
        for (int i = 0; i < 10000; i++) {
            Flight.StepVehicle(sim, v, dt);
            sim.T += dt;
            spent += v.Mdot * dt;
        }
        True("топливо действительно израсходовано", prop0 - v.Prop > 100e3,
             $"{N((prop0 - v.Prop) / 1000)} т за 100 с");
        Near("убыль в баке равна интегралу расхода", prop0 - v.Prop, spent, 1e-9, " кг");
    }
    private static void BadNumbers() {
        Head("Негодные числа не расходятся по расчёту");
        Air bad = Atmosphere.At(double.NaN);
        True("атмосфера на негодной высоте даёт числа",
             !double.IsNaN(bad.Rho) && !double.IsNaN(bad.P) && !double.IsNaN(bad.A),
             $"плотность {N(bad.Rho)} кг/м³");
        True("сопротивление на негодном числе Маха конечно",
             !double.IsNaN(Atmosphere.Cd0(double.NaN)) && !double.IsNaN(Atmosphere.CdEngine(double.NaN)),
             $"{N(Atmosphere.Cd0(double.NaN))} носом вперёд и {N(Atmosphere.CdEngine(double.NaN))} двигателями");
        True("ветер на негодной высоте нулевой",
             Wind.Roll(new Rng()).At(double.NaN) == 0);
    }
}
