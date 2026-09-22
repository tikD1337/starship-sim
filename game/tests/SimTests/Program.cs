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
        BoosterSwing();
        ShipBellyFlop();
        ShipLanding();
        LightShip();
        LiveFlight();
        ArmCatch();
        Estimation();
        ConditionEvents();
        Fallback();
        TowerArms();
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
        UiAir2();
        UiTabs();
        UiArmGeom();
        UiReplay();
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
        Near("двигателями вперёд на M 3", Atmosphere.CdEngine(3.0), 2.00, 1e-9);
        Near("двигателями вперёд на дозвуке — донный срез как стакан, а не диск (пункт 48)",
             Atmosphere.CdEngine(0.5), 1.60, 1e-9);
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
        double jump = 0;
        for (double h = 50; h < 5000; h += 50) jump = Math.Max(jump, Math.Abs(Wind.Steady(12).At(h + 1) - Wind.Steady(12).At(h - 1)));
        True("приземный ветер с высотой меняется плавно, без скачка на 1200 м (пункт 58)", jump < 0.1,
             $"наибольший скачок за 2 м — {N(jump)} м/с");
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
        double litCoast = 0, hIgn = double.NaN, qDesc = 0, tIgn = double.NaN, vIgn = double.NaN;
        double gBurn = 0, t13 = 0;
        int nIgn = 0, nLast = 0;
        var counts = new System.Collections.Generic.SortedSet<int>();
        for (int i = 0; i < 200000 && !b.Landed && !b.Crashed && !b.Caught; i++) {
            Physics.Sim.Tick(sim, Const.DT);
            if (b.Mode == "boostback") boostbackSeen = true;
            else if (boostbackSeen && !b.IgnBurn) coasting = true;
            if (coasting && !b.IgnBurn && b.NRun > 0) litCoast += Const.DT;
            if (coasting && b.Q > qDesc) qDesc = b.Q;
            if (!b.IgnBurn || b.NRun == 0) continue;
            if (double.IsNaN(hIgn)) { hIgn = b.Alt; nIgn = b.NRun; tIgn = sim.T; vIgn = b.Speed; }
            gBurn = Math.Max(gBurn, b.Acc);
            if (b.NRun == 13) t13 += Const.DT;
            counts.Add(b.NRun);
            nLast = b.NRun;
        }
        True("между тормозным импульсом и жигой двигатели ускорителя молчат", litCoast == 0,
             $"работали {N(litCoast)} с");
        True("жига начинается ниже 2,5 км — не сжигает топливо раньше времени", hIgn < 2500, $"с высоты {N(hIgn)} м");
        True("жига начинается ниже 2 км (пункт 48)", hIgn < 2000, $"с высоты {N(hIgn)} м");
        True("жига начинается выше 500 м", hIgn > 500, $"с высоты {N(hIgn)} м");
        True("жига зажигает 13 двигателей", nIgn == 13, $"{nIgn}");
        True("жига кончается на трёх", nLast == 3, $"{nLast}");
        True("пик перегрузки на жиге не выше 6 g, как у пятого полёта (~5,5 g)", gBurn <= 6, $"{N(gBurn)} g");
        True("13 двигателей горят от 4 до 8 с (пятый полёт — чуть больше 5 с)", t13 >= 4 && t13 <= 8, $"{N(t13)} с");
        True("зажигание при 1100–1400 км/ч (пятый полёт — ~1250)", vIgn * 3.6 >= 1100 && vIgn * 3.6 <= 1400,
             $"{N(vIgn * 3.6)} км/ч");
        True("в жиге только 13 или 3 двигателя", counts.SetEquals(new[] { 13, 3 }),
             string.Join(", ", counts));
        True("ускоритель пойман", b.Caught, $"промах {N(sim.Downrange(b))} м");
        True("напор на спуске ниже предела конструкции", qDesc < 200e3, $"{N(qDesc / 1000)} кПа");
        double burn = b.Caught ? sim.T - tIgn : double.NaN;
        True("от зажигания жиги до захвата не больше 31 с", burn <= 31, $"{N(burn)} с");
        double tCatch = sim.T;
        True("время конца полёта ускорителя записано в события в момент захвата",
             sim.Events.TryGetValue("overБ", out double tOver) && Math.Abs(tOver - tCatch) < 0.05,
             sim.Events.ContainsKey("overБ") ? $"{N(sim.Events["overБ"])} при захвате {N(tCatch)}" : "нет события");
        while (sim.T < tCatch + 1000) Physics.Sim.Tick(sim, Const.DT);
        True("пойманный ускоритель через 1000 с стоит на месте относительно Земли", b.Speed < 0.5,
             $"{N(b.Speed * 3.6)} км/ч");
    }
    private static void BoosterSwing() {
        Head("Ускоритель на последних 300 м не раскачивается у башни");
        foreach ((string mission, Wind wind, string name) in new (string, Wind, string)[] {
                     ("orbital", null, "орбитальное"), ("orbital", Wind.Calm(), "штиль"),
                     ("orbital", Wind.Steady(8), "ветер 8 м/с"), ("orbital", Wind.Steady(-8), "ветер −8 м/с"),
                     ("high", null, "высокая орбита"), ("trans", null, "трансатмосферное") }) {
            (bool caught, double dr0, double drMax, int flips, int turns) = Swing(mission, wind);
            True($"{name}: пойман", caught);
            True($"{name}: промах меняет направление не больше одного раза", turns <= 1,
                 $"разворотов {turns}, на входе {N(dr0)} м, наибольший {N(drMax)} м");
            True($"{name}: корпус не перекладывается через вертикаль больше раза", flips <= 1,
                 $"перекладок {flips}");
        }
    }
    private static SimState Live(uint seed, bool disp, string mission = "orbital") {
        var sim = new SimState { Mission = mission, AnomOn = false, Disperse = disp };
        Physics.Sim.Reset(sim, seed);
        return sim;
    }
    private static (double X, double Y, double Prop) RunTo(SimState sim, double t) {
        while (sim.T < t) Physics.Sim.Tick(sim, Const.DT);
        Vehicle b = sim.Veh[0];
        return (b.X, b.Y, b.Prop);
    }
    private static double Spread(System.Collections.Generic.IEnumerable<double> xs, out double mean) {
        double s = 0, s2 = 0; int n = 0;
        foreach (double x in xs) { s += x; s2 += x * x; n++; }
        mean = s / Math.Max(n, 1);
        return Math.Sqrt(Math.Max(s2 / Math.Max(n, 1) - mean * mean, 0));
    }
    private static void LiveFlight() {
        Head("Живой полёт: одно зерно — один полёт, разные зёрна — разные полёты");
        var a = RunTo(Live(777, true), 120);
        var a2 = RunTo(Live(777, true), 120);
        True("одно зерно с разбросом повторяет полёт до бита", a == a2,
             $"{N(a.X - a2.X)} м по X");
        var c = RunTo(Live(778, true), 120);
        double dx = Math.Sqrt((a.X - c.X) * (a.X - c.X) + (a.Y - c.Y) * (a.Y - c.Y));
        True("другое зерно — другой полёт", dx > 5, $"к T+120 разошлись на {N(dx)} м");

        Head("Живой полёт: естественный разброс отдельно от отказов");
        SimState off = Live(777, false);
        bool flat = off.RhoK == 1 && off.Veh[0].Dry == Spec.Booster.Dry && off.Veh[1].Dry == Spec.Ship.Dry + off.Payload;
        foreach (Vehicle v in off.Veh) foreach (Engine e in v.Eng) flat &= e.P.Cf == 1;
        True("без разброса всё по таблице", flat);
        SimState on = Live(777, true);
        var cf = new System.Collections.Generic.List<double>();
        foreach (Engine e in on.Veh[0].Eng) cf.Add(e.P.Cf);
        double sd = Spread(cf, out double cfMean);
        double lo = double.MaxValue, hi = double.MinValue;
        foreach (double k in cf) { lo = Math.Min(lo, k); hi = Math.Max(hi, k); }
        True("у каждого двигателя своя тяга (±2 %)", sd > 0.003 && lo >= 0.98 && hi <= 1.02,
             $"разброс {N(sd * 100)} %, от {N(lo)} до {N(hi)}");
        True("плотность воздуха отличается от таблицы, но не больше чем на 5 %",
             on.RhoK != 1 && Math.Abs(on.RhoK - 1) <= 0.05, $"{N(on.RhoK)}");
        True("масса ступеней отличается от таблицы меньше чем на 1 %",
             on.Veh[0].Dry != Spec.Booster.Dry && Math.Abs(on.Veh[0].Dry / Spec.Booster.Dry - 1) < 0.01,
             $"{N(on.Veh[0].Dry / 1000)} т");

        Head("Живой полёт: порывы ветра");
        var gust = new System.Collections.Generic.List<double>();
        for (double t = 0; t < 900; t += 0.5) gust.Add(on.Wind.At(8e3, t));
        double gSd = Spread(gust, out double gMean);
        True("на 8 км ветер меняется со временем", gSd > 0.3 && gSd < 5, $"СКО {N(gSd)} м/с");
        True("порывы не сдвигают средний ветер", Math.Abs(gMean - on.Wind.At(8e3)) < 0.6,
             $"{N(gMean)} против {N(on.Wind.At(8e3))} м/с");
        var still = new System.Collections.Generic.List<double>();
        for (double t = 0; t < 300; t += 0.5) still.Add(off.Wind.At(8e3, t));
        True("без разброса ветер неподвижен", Spread(still, out _) == 0);
        var high = new System.Collections.Generic.List<double>();
        for (double t = 0; t < 300; t += 0.5) high.Add(on.Wind.At(45e3, t));
        True("выше 40 км порывов нет", Spread(high, out _) < 1e-9);

        Head("Живой полёт: с разбросом обе ступени ловятся");
        foreach (uint seed in new uint[] { 1, 2, 3 }) {
            SimState sim = Live(seed, true);
            Vehicle b = sim.Veh[0], s = sim.Veh[1];
            for (int i = 0; i < 1_400_000 && !(s.Landed || s.Crashed); i++) Physics.Sim.Tick(sim, Const.DT);
            True($"зерно {seed}: ускоритель и корабль пойманы", b.Caught && s.Caught, $"Б {b.Mode}, К {s.Mode}");
        }
        {
            SimState sim = Live(9, true);
            sim.Wind = Wind.Steady(15);
            sim.Disp.ApplyWind(sim);
            Vehicle s = sim.Veh[1];
            for (int i = 0; i < 1_400_000 && !(s.Landed || s.Crashed); i++) Physics.Sim.Tick(sim, Const.DT);
            True("порыв у рук не проводит корабль мимо захвата (ветер 15 м/с, зерно 9)", s.Caught, s.Mode);
        }
    }
    private static (double pitchH, double pitchT, int qFlips, double tMeco, double tIgn, double tSep, double bFracIgn, int litSep, string sepLog)
        Staging(SimState sim, Action<SimState> tweak = null) {
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        double pitchH = double.NaN, pitchT = double.NaN, tMeco = double.NaN, tIgn = double.NaN, tSep = double.NaN, frac = double.NaN;
        int flips = 0, lit = 0;
        bool qd = false;
        for (int i = 0; i < 40_000 && s.Attached; i++) {
            if (tweak != null && b.Mode == "ascent" && sim.T > 120) { tweak(sim); tweak = null; }
            Physics.Sim.Tick(sim, Const.DT);
            if (double.IsNaN(pitchH) && b.Mode == "ascent" && b.ThCmd != 0) { pitchH = b.Alt; pitchT = sim.T; }
            if (b.QDown != qd) { flips++; qd = b.QDown; }
            if (double.IsNaN(tMeco) && b.Mode == "meco") tMeco = sim.T;
            if (double.IsNaN(tIgn) && s.Ign) { tIgn = sim.T; frac = b.F / b.FRef; }
        }
        tSep = sim.T;
        foreach (Engine e in s.Eng) if (e.On && e.Pc >= Const.HOT_PC * Pump.PC_NOM) lit++;
        string log = "";
        foreach (LogEntry l in sim.Log) if (l.M.Contains("разделение по страховке") || l.M.Contains("на режиме")) log = l.M;
        return (pitchH, pitchT, flips, tMeco, tIgn, tSep, frac, lit, log);
    }
    private static SimState Scripted(string key, uint seed = 5) {
        var sim = new SimState { Mission = "orbital", AnomOn = true, AnomScript = new System.Collections.Generic.HashSet<string> { key } };
        Physics.Sim.Reset(sim, seed);
        return sim;
    }
    private static void Finish(SimState sim, Vehicle v, double tip = 0) {
        for (int i = 0; i < 1_400_000 && !(v.Landed || v.Crashed); i++) Physics.Sim.Tick(sim, Const.DT);
        for (double t = 0; t < tip; t += Const.DT) Physics.Sim.Tick(sim, Const.DT);
    }
    private static bool Logged(SimState sim, string part) {
        foreach (LogEntry l in sim.Log) if (l.M.Contains(part)) return true;
        return false;
    }
    private static void Fallback() {
        Head("Запасная посадка: площадка у башни или море, если что-то случилось");
        True("стол башни на нуле, земля и море на 16 м ниже", SimState.Surface(0) == 0 && SimState.Surface(200) == -Const.DECK_H);
        True("к востоку за берегом море, площадка на суше", SimState.Water(Const.COAST_DR + 10) && !SimState.Water(Const.PAD_DR) && !SimState.Water(0));
        {
            SimState sim = Live(12345, false);
            Finish(sim, sim.Veh[0]);
            True("без отказов опрос даёт GO и ускоритель ловится", Logged(sim, "GO на захват") && sim.Veh[0].Caught, sim.Veh[0].Mode);
        }
        {
            SimState sim = Scripted("copvLeak");
            Vehicle b = sim.Veh[0];
            Finish(sim, b, 30);
            double dr = sim.Downrange(b);
            True("утечка наддува: захват отменён до тормозного импульса, ускоритель уходит в море",
                 b.Site == "sea" && Logged(sim, "утечка газа наддува: уход в море"), b.Site);
            True("и мягко приводняется у точки в 6 км", b.Landed && !b.Crashed && b.Splash && Math.Abs(dr - Const.SEA_DR) < 150,
                 $"{b.Mode}, {N(dr)} м");
            True("на воде ступень заваливается набок", Math.Abs(Math.Abs(Vehicle.AngDiff(b.Th, 0)) - Math.PI / 2) < 0.05,
                 $"{N(Math.Abs(b.Th) * Const.R2D)}°");
        }
        {
            SimState sim = Scripted("relightB");
            Vehicle b = sim.Veh[0];
            Finish(sim, b);
            True("два двигателя не зажглись на жиге: ускоритель уходит от башни и приводняется",
                 b.Landed && !b.Crashed && b.Splash && !b.Caught && Logged(sim, "посадочных двигателей: уход в море"),
                 $"{b.Mode}, {N(sim.Downrange(b))} м");
        }
        {
            SimState sim = Scripted("relightS");
            Vehicle s = sim.Veh[1];
            Finish(sim, s);
            double dr = sim.Downrange(s);
            True("двигатель корабля не зажёгся на перевороте: уход на площадку у башни",
                 s.Site == "pad" && Logged(sim, "уход на площадку у башни"), s.Site);
            True("корабль садится на площадку, а не в руки", s.Landed && !s.Crashed && !s.Caught && Math.Abs(dr - Const.PAD_DR) < 15
                 && Math.Abs(s.Alt + Const.DECK_H) < 0.1, $"{s.Mode}, {N(dr - Const.PAD_DR)} м от центра, высота {N(s.Alt)} м");
        }
        {
            const int n = 400;
            string off = "";
            foreach (AnomalySpec spec in Anomalies.Table) {
                int hits = 0;
                for (uint sd = 1; sd <= n; sd++) {
                    var sim = new SimState { Mission = "orbital", AnomOn = true };
                    Physics.Sim.Reset(sim, sd);
                    if (sim.Anom.Has(spec.Key)) hits++;
                }
                if (hits < 0.4 * spec.P * n || hits > 2 * spec.P * n) off += $"{spec.Key} {hits}/{n} при {spec.P:P0}; ";
            }
            True("на подряд идущих зёрнах отказы выпадают с заявленной частотой (пункт 50)", off.Length == 0, off);
        }
        {
            int nb = 0, ns = 0, both = 0;
            const int seeds = 5000;
            for (uint sd = 1; sd <= seeds; sd++) {
                Dispersion d = Dispersion.Roll(sd);
                if (d.RelightB) nb++;
                if (d.RelightS) ns++;
                if (d.RelightB && d.RelightS) both++;
            }
            True("без отказов двигатели на посадку изредка не зажигаются: около 2 % у каждой ступени",
                 nb >= 0.015 * seeds && nb <= 0.025 * seeds && ns >= 0.015 * seeds && ns <= 0.025 * seeds && both < 0.002 * seeds,
                 $"ускоритель {N(100.0 * nb / seeds)} %, корабль {N(100.0 * ns / seeds)} %, оба {both}");
            uint sb = 1, ss = 1;
            while (sb < seeds && !Dispersion.Roll(sb).RelightB) sb++;
            while (ss < seeds && !(Dispersion.Roll(ss).RelightS && !Dispersion.Roll(ss).RelightB)) ss++;
            SimState simB = Live(sb, true);
            Vehicle bb = simB.Veh[0];
            Finish(simB, bb);
            True("естественный отказ на жиге ускорителя: захват отменён, приводнение",
                 bb.Splash && bb.Landed && !bb.Crashed && Logged(simB, "уход в море"), $"зерно {sb}: {bb.Mode}, {N(simB.Downrange(bb))} м");
            SimState simS = Live(ss, true);
            Vehicle sv = simS.Veh[1];
            Finish(simS, sv);
            True("естественный отказ на перевороте корабля: посадка на площадку у башни",
                 sv.Site == "pad" && sv.Landed && !sv.Crashed && Logged(simS, "уход на площадку у башни"),
                 $"зерно {ss}: {sv.Mode}, {N(simS.Downrange(sv) - Const.PAD_DR)} м от центра");
            SimState off = Live(sb, false);
            Finish(off, off.Veh[0]);
            True("без разброса того же зерна отказа нет", off.Veh[0].Caught, off.Veh[0].Mode);
        }
        {
            SimState sim = Live(12345, false);
            sim.Wind = Wind.Steady(22);
            Vehicle b = sim.Veh[0], s = sim.Veh[1];
            Finish(sim, s);
            True("ветер 22 м/с: обе ступени не идут к башне и приводняются",
                 b.Splash && s.Splash && !b.Crashed && !s.Crashed && Logged(sim, "ветер у башни"),
                 $"Б {b.Mode} {N(sim.Downrange(b))} м, К {s.Mode} {N(sim.Downrange(s))} м");
        }
    }
    private static void ConditionEvents() {
        Head("События по условиям: тангаж, дроссель у Max Q и горячее разделение без таймеров");
        var nom = Staging(Live(12345, false));
        True("тангаж начинается, когда ракета ушла от башни по высоте", nom.pitchH >= Const.ASC_CLEAR_H && nom.pitchH < Const.ASC_CLEAR_H + 2,
             $"H {N(nom.pitchH)} м на T+{N(nom.pitchT)}");
        True("дроссель у Max Q включается и снимается по напору один раз, без дребезга", nom.qFlips == 2, $"переключений {nom.qFlips}");
        True("корабль зажигается, когда тяга ускорителя спала (меньше 20 % от MECO)",
             nom.bFracIgn < 0.2 && nom.tIgn - nom.tMeco > 0.2 && nom.tIgn - nom.tMeco < Const.HOT_IGN_T,
             $"{N(nom.tIgn - nom.tMeco)} с после MECO, тяга {N(nom.bFracIgn * 100)} %");
        True("расцепка — когда все шесть двигателей корабля на режиме, а не по таймеру",
             nom.litSep == 6 && nom.tSep - nom.tIgn < Const.HOT_SEP_T - 1 && nom.sepLog.Contains("на режиме"),
             $"{N(nom.tSep - nom.tIgn)} с после зажигания, на режиме {nom.litSep}");
        var d1 = Staging(Live(21, true));
        var d2 = Staging(Live(22, true));
        True("с разбросом тангаж тоже по высоте ухода от башни",
             Math.Abs(d1.pitchH - Const.ASC_CLEAR_H) < 2 && Math.Abs(d2.pitchH - Const.ASC_CLEAR_H) < 2,
             $"H {N(d1.pitchH)} м на T+{N(d1.pitchT)} и {N(d2.pitchH)} м на T+{N(d2.pitchT)}");
        var fail = Staging(Live(12345, false), sim => { foreach (Engine e in sim.Veh[1].Eng) e.P.Tau = 5; });
        True("если двигатели корабля не выходят на режим, разделение по страховочному таймеру",
             fail.sepLog.Contains("страховке") && Math.Abs(fail.tSep - fail.tMeco - Const.HOT_SEP_T) < 0.05,
             $"через {N(fail.tSep - fail.tMeco)} с после MECO: {fail.sepLog}");
    }
    private static void Estimation() {
        Head("Наведение по оценке: навигация с ошибкой, ветер по сносу, плотность по торможению");
        {
            SimState off = Live(777, false);
            RunTo(off, 200);
            Vehicle ob = off.Veh[0];
            True("без разброса навигация точная", ob.NavH == 0 && ob.NavX == 0 && ob.NavVv == 0 && ob.NavVh == 0);
        }
        uint rs = 1;
        while (Math.Abs(Dispersion.Roll(rs).RhoK - 1) < 0.02) rs++;
        SimState sim = Live(rs, true);
        sim.Wind = Wind.Steady(10);
        sim.Disp.ApplyWind(sim);
        Vehicle b = sim.Veh[0];
        var navH = new System.Collections.Generic.List<double>();
        var navV = new System.Collections.Generic.List<double>();
        double rhoMeco = double.NaN, errEst = 0, errFc = 0, n = 0, lagSum = 0, lagN = 0;
        for (int i = 0; i < 1_000_000 && !b.Landed && !b.Crashed; i++) {
            Physics.Sim.Tick(sim, Const.DT);
            if (i % 100 == 0 && sim.T > 0 && Math.Abs(b.VVert) < 5) { navH.Add(b.NavH); navV.Add(b.NavVh); }
            if (b.Mode == "coastB" && b.VVert < -300) { lagSum += (b.NAlt - b.Alt) / -b.VVert; lagN++; }
            if (double.IsNaN(rhoMeco) && sim.T > 60 && b.Mode != "ascent") rhoMeco = b.RhoEst;
            if (b.Mode == "landB" && b.Alt < 1500) {
                errEst += Math.Abs(b.WindEst - b.WindE) * Const.DT;
                errFc += Math.Abs(sim.Wind.Forecast(b.Alt) - b.WindE) * Const.DT;
                n += Const.DT;
            }
        }
        double sH = Spread(navH, out double mH), sV = Spread(navV, out _);
        True("навигация запаздывает примерно на 0,1 с: на быстром спуске высота завышена на V·0,1",
             lagN > 100 && Math.Abs(lagSum / lagN - Const.NAV_LAG) < 0.02, $"{N(lagSum / Math.Max(lagN, 1))} с");
        True("ошибка навигации по высоте порядка метра", sH > 0.3 && sH < 1.6 && Math.Abs(mH) < 0.8,
             $"СКО {N(sH)} м, среднее {N(mH)} м");
        True("ошибка навигации по скорости — сотые доли м/с", sV > 0.02 && sV < 0.2, $"СКО {N(sV)} м/с");
        True("плотность оценена по торможению на подъёме точнее 1 %", Math.Abs(rhoMeco / sim.RhoK - 1) < 0.01,
             $"оценка {N(rhoMeco)}, настоящая {N(sim.RhoK)} (зерно {rs})");
        True("ниже 1,5 км оценка ветра ближе к настоящему, чем прогноз", n > 5 && errEst < errFc && errEst / n < 1.2,
             $"ошибка оценки {N(errEst / Math.Max(n, 1e-9))} м/с, прогноза {N(errFc / Math.Max(n, 1e-9))} м/с");
        True("ускоритель пойман, наводясь по оценкам", b.Caught, b.Mode);
    }
    private static void ArmCatch() {
        Head("Захват без телепорта: ступень ложится на рельсы и успокаивается");
        foreach (int idx in new[] { 0, 1 }) {
            string name = idx == 0 ? "ускоритель" : "корабль";
            SimState sim = Live(12345, false);
            Vehicle v = sim.Veh[idx];
            double jump = double.NaN, turn = double.NaN, speed = double.NaN, tCatch = double.NaN;
            double tilt2 = double.NaN, slide2 = double.NaN, stowAlt = double.NaN, sagV1 = double.NaN, sag1 = double.NaN, tilt1 = double.NaN;
            for (int i = 0; i < 1_600_000 && double.IsNaN(stowAlt); i++) {
                double x0 = v.X, y0 = v.Y, th0 = v.Th, vx0 = v.Vx, vy0 = v.Vy;
                bool was = v.Caught;
                Physics.Sim.Tick(sim, Const.DT);
                if (!was && v.Caught) {
                    tCatch = sim.T;
                    double px = x0 + vx0 * Const.DT, py = y0 + vy0 * Const.DT;
                    jump = Math.Sqrt((v.X - px) * (v.X - px) + (v.Y - py) * (v.Y - py));
                    turn = Math.Abs(Vehicle.AngDiff(v.Th, th0)) * Const.R2D;
                    speed = Math.Sqrt(vx0 * vx0 + vy0 * vy0);
                }
                if (!double.IsNaN(tCatch) && double.IsNaN(sagV1) && sim.T - tCatch >= 1.2) {
                    sagV1 = Math.Abs(sim.ArmSagV); sag1 = Math.Abs(sim.ArmSag - Const.ARM_SAG_REST);
                    tilt1 = Math.Abs(Vehicle.AngDiff(v.Th, 0)) * Const.R2D;
                }
                if (!double.IsNaN(tCatch) && double.IsNaN(tilt2) && sim.T - tCatch >= 2.5) {
                    tilt2 = Math.Abs(Vehicle.AngDiff(v.Th, 0)) * Const.R2D;
                    slide2 = Math.Abs(v.HeldVh);
                }
                if (v.Stowed) stowAlt = v.Alt;
            }
            True($"{name}: в кадре захвата положение не прыгает", jump < 0.1, $"{N(jump)} м");
            True($"{name}: в кадре захвата наклон не прыгает", turn < 0.2, $"{N(turn)}°");
            True($"{name}: через 1,2 с каретка погасила удар (скорость < 0,1 м/с, у положения покоя ±0,1 м)",
                 sagV1 < 0.1 && sag1 < 0.1, $"{N(sagV1)} м/с, {N(sag1)} м");
            True($"{name}: через 1,2 с корпус почти выпрямлен (< 1°)", tilt1 < 1, $"{N(tilt1)}°");
            True($"{name}: через 2,5 с корпус выпрямлен на рельсах", tilt2 < 0.3, $"{N(tilt2)}°");
            True($"{name}: через 2,5 с скольжение по рельсам погашено", slide2 < 0.05, $"{N(slide2)} м/с");
            True($"{name}: опущен ровно на стол", Math.Abs(stowAlt) < 0.05, $"высота {N(stowAlt)} м");
        }
    }
    private static void TowerArms() {
        Head("Руки башни сведены до подлёта и дожимают зазор, пока ступень проходит между ними");
        foreach ((string mission, int idx, string name) in new[] { ("orbital", 0, "ускоритель"), ("orbital", 1, "корабль") }) {
            var sim = new SimState { Mission = mission, AnomOn = false };
            Physics.Sim.Reset(sim, 12345);
            Vehicle v = sim.Veh[idx];
            double tReady = double.NaN, gapEnter = double.NaN, gapCatch = double.NaN, tCatch = double.NaN;
            double early = double.PositiveInfinity, sagMax = 0, sagLate = double.NaN, sagVLate = double.NaN;
            for (int i = 0; i < 1_400_000; i++) {
                Physics.Sim.Tick(sim, Const.DT);
                if (!v.Launched || v.Attached) continue;
                double rel = v.Alt - Const.CATCH_H;
                bool approach = !v.Caught && v.VVert < 0 && v.Alt < 8000;
                if (approach && double.IsNaN(tReady) && sim.ArmGap <= Const.ARM_GAP_READY + 0.05) tReady = sim.T;
                if (approach && rel > v.CatchPinY + 5) early = Math.Min(early, sim.ArmGap);
                if (approach && double.IsNaN(gapEnter) && rel < v.CatchPinY) gapEnter = sim.ArmGap;
                if (v.Caught && double.IsNaN(tCatch)) { tCatch = sim.T; gapCatch = sim.ArmGap; }
                if (!double.IsNaN(tCatch)) {
                    sagMax = Math.Max(sagMax, sim.ArmSag);
                    if (sim.T - tCatch >= 6 && double.IsNaN(sagLate)) { sagLate = sim.ArmSag; sagVLate = sim.ArmSagV; break; }
                }
            }
            True($"{name}: пойман", !double.IsNaN(tCatch));
            True($"{name}: руки сведены до рабочего зазора заранее, не меньше 8 с до захвата",
                 tCatch - tReady >= 8, $"за {N(tCatch - tReady)} с");
            True($"{name}: пока корпус выше рук, зазор не уже рабочего", early >= Const.ARM_GAP_READY - 0.05, $"{N(early)} м");
            True($"{name}: когда низ корпуса проходит рельсы, руки уже на рабочем зазоре",
                 gapEnter <= Const.ARM_GAP_READY + 0.05, $"{N(gapEnter)} м");
            True($"{name}: к захвату зазор дожат до касания", gapCatch <= 0.3, $"{N(gapCatch)} м");
            True($"{name}: каретка проседает от удара на 0,4–2 м", sagMax >= 0.4 && sagMax <= 2, $"{N(sagMax)} м");
            True($"{name}: через 6 с после захвата каретка успокоилась", Math.Abs(sagVLate) < 0.05, $"{N(sagVLate)} м/с");
        }
    }
    private static void ShipBellyFlop() {
        Head("Корабль ниже 15 км падает плашмя, а не носом вниз");
        foreach ((string mission, string name) in new[] { ("orbital", "орбитальное"), ("trans", "трансатмосферное") }) {
            var sim = new SimState { Mission = mission, AnomOn = false };
            Physics.Sim.Reset(sim, 12345);
            Vehicle s = sim.Veh[1];
            double worst = 0, atFlip = double.NaN;
            for (int i = 0; i < 1_200_000 && !s.Landed && !s.Crashed && !s.Caught; i++) {
                string was = s.Mode;
                Physics.Sim.Tick(sim, Const.DT);
                double dev = Vehicle.AngDiff(s.Th, Math.PI / 2) * Const.R2D;
                if (was == "entryS" && s.Mode == "flipS") atFlip = dev;
                if (s.Mode == "entryS" && s.Alt < 15e3) worst = Math.Max(worst, Math.Abs(dev));
            }
            True($"{name}: корпус держится в пределах 20° от горизонта", worst <= 20, $"наибольший уход {N(worst)}°");
            if (mission == "orbital")
                True($"{name}: переворот начинается почти с горизонтали", Math.Abs(atFlip) <= 20, $"{N(atFlip)}° от горизонта");
            True($"{name}: корабль {(mission == "orbital" ? "пойман башней" : "сел")}",
                 mission == "orbital" ? s.Caught : s.Landed && !s.Crashed, s.Mode);
        }
    }
    private static void ShipLanding() {
        Head("Посадка корабля как у настоящего: переворот за пару секунд, касание через ~20 с");
        foreach ((string mission, string name) in new[] { ("orbital", "орбитальное"), ("high", "высокая орбита") }) {
            var sim = new SimState { Mission = mission, AnomOn = false };
            Physics.Sim.Reset(sim, 12345);
            Vehicle s = sim.Veh[1];
            double t0 = double.NaN, tFlip = double.NaN, tilt = 0;
            for (int i = 0; i < 1_200_000 && !s.Landed && !s.Crashed && !s.Caught; i++) {
                Physics.Sim.Tick(sim, Const.DT);
                if (double.IsNaN(t0) && s.Mode == "flipS") t0 = sim.T;
                if (double.IsNaN(t0)) continue;
                double dev = Math.Abs(Vehicle.AngDiff(s.Th, 0)) * Const.R2D;
                if (double.IsNaN(tFlip) && dev <= 15) tFlip = sim.T - t0;
                if (!double.IsNaN(tFlip)) tilt = Math.Max(tilt, dev);
            }
            double burn = sim.T - t0;
            True($"{name}: корабль пойман башней", s.Caught, s.Mode);
            True($"{name}: переворот до 15° от вертикали не дольше 3 с после зажигания", tFlip <= 3, $"{N(tFlip)} с");
            True($"{name}: от зажигания до захвата не дольше 26 с (у пятого полёта 20 с)", burn <= 26, $"{N(burn)} с");
            True($"{name}: после переворота наклон не больше 30°", tilt <= 30, $"{N(tilt)}°");
        }
    }
    private static void LightShip() {
        Head("Корабль садится лёгким: груз выпущен, на вход только запас, переворот в точке (пункт 49)");
        double a90 = Flight.FlapCn(Math.PI / 2), a70 = Flight.FlapCn(70 * Const.D2R), a63 = Flight.FlapCn(Math.Atan(2));
        True("закрылки складываются вдоль корпуса: на 90° атаки власть наибольшая, а не нулевая", a90 > a70 && a90 > 0.6,
             $"на 90° {N(a90)}, на 70° {N(a70)}");
        True("на 63° власть та же, что у прежней модели руля", Math.Abs(a63 - 2 * Math.Sin(2 * Math.Atan(2)) * Math.Sin(Const.FLAP_DEF)) < 1e-9,
             $"{N(a63)}");
        True("в окне рук наведение не наклоняет ступень сильнее, чем берут руки", Const.CATCH_TILT_CMD < Const.CATCH_TILT,
             $"{N(Const.CATCH_TILT_CMD)}° при допуске {N(Const.CATCH_TILT)}°");
        foreach ((string mission, string name) in new[] { ("orbital", "орбитальное"), ("high", "высокая орбита") }) {
            var sim = new SimState { Mission = mission, AnomOn = false };
            Physics.Sim.Reset(sim, 12345);
            Vehicle s = sim.Veh[1];
            int sats = s.BayS.Sats, deoSats = -1;
            double deoPay = double.NaN, deoOpen = double.NaN, entryProp = double.NaN, flipMiss = double.NaN;
            string was = s.Mode;
            for (int i = 0; i < 1_400_000 && !s.Landed && !s.Crashed; i++) {
                Physics.Sim.Tick(sim, Const.DT);
                if (was != "deorbit" && s.Mode == "deorbit" && deoSats < 0) {
                    deoSats = s.BayS.Sats; deoOpen = s.BayS.Open; deoPay = s.Dry - Spec.Of(Kind.Ship).Dry;
                }
                if (was != "entryS" && s.Mode == "entryS" && double.IsNaN(entryProp)) entryProp = s.Prop;
                if (was != "flipS" && s.Mode == "flipS" && double.IsNaN(flipMiss)) flipMiss = sim.Downrange(s) + Const.FLIP_D - s.AimDr;
                was = s.Mode;
            }
            True($"{name}: на орбиту выведены спутники", sats > 0, $"{sats} Starlink");
            True($"{name}: груз выпущен и створка закрыта до схода с орбиты", deoSats == 0 && deoOpen < 0.01 && deoPay < 1,
                 $"в отсеке {deoSats}, створка {N(deoOpen)}, груза {N(deoPay / 1000)} т");
            True($"{name}: на вход только запас на посадку", entryProp <= Const.ENTRY_PROP + 500, $"{N(entryProp / 1000)} т");
            True($"{name}: переворот начинается в расчётной точке, а не с перелётом", Math.Abs(flipMiss) <= 30, $"{N(flipMiss)} м");
            True($"{name}: корабль пойман башней", s.Caught, s.Mode);
            True($"{name}: после захвата запас 25–40 т, а не полные баки и груз", s.Prop >= 25e3 && s.Prop <= 40e3,
                 $"{N(s.Prop / 1000)} т, груза {N((s.Dry - Spec.Of(Kind.Ship).Dry) / 1000)} т");
        }
        foreach (uint seed in new uint[] { 9, 14, 17 }) {
            SimState sim = Live(seed, true);
            sim.Wind = Wind.Steady(22);
            sim.Disp?.ApplyWind(sim);
            Vehicle s = sim.Veh[1];
            Finish(sim, s);
            True($"ветер 22 м/с, зерно {seed}: лёгкий корабль пойман или приводнился, а не разбит",
                 !s.Crashed && (s.Caught || s.Splash), $"{s.Mode}, {N(sim.Downrange(s))} м");
        }
    }
    private static (bool, double, double, int, int) Swing(string mission, Wind wind) {
        var sim = new SimState { Mission = mission, AnomOn = false };
        Physics.Sim.Reset(sim, 12345);
        if (wind != null) sim.Wind = wind;
        Vehicle b = sim.Veh[0];
        double dr0 = double.NaN, drMax = 0;
        int flips = 0, side = 0, turns = 0, trend = 0;
        double ext = double.NaN;
        for (int i = 0; i < 200000 && !b.Landed && !b.Crashed && !b.Caught; i++) {
            Physics.Sim.Tick(sim, Const.DT);
            if (b.Mode != "landB" || b.Alt - Const.CATCH_H > Const.LAND_DHPD) continue;
            double x = sim.Downrange(b), dr = Math.Abs(x), th = Vehicle.AngDiff(b.Th, 0) * Const.R2D;
            if (double.IsNaN(dr0)) { dr0 = dr; ext = x; }
            drMax = Math.Max(drMax, dr);
            if (trend >= 0 && x < ext - 0.5) { if (trend > 0) turns++; trend = -1; }
            else if (trend <= 0 && x > ext + 0.5) { if (trend < 0) turns++; trend = 1; }
            if (trend > 0) ext = Math.Max(ext, x); else if (trend < 0) ext = Math.Min(ext, x);
            int now = th > 1 ? 1 : th < -1 ? -1 : 0;
            if (now != 0 && side != 0 && now != side) flips++;
            if (now != 0) side = now;
        }
        return (b.Caught, dr0, drMax, flips, turns);
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
