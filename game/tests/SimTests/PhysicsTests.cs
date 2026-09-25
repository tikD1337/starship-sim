using System;
using System.Collections.Generic;
using System.Linq;
using Starship.Physics;
namespace Starship.Tests;
internal static partial class Program {
    private static void Atmosphere_() {
        Head("Атмосфера");
        (double H, double Rho, double T)[] nodes = {
            (0, 1.2250, 288.15), (11000, 0.36392, 216.65), (20000, 0.088035, 216.65), (32000, 0.013225, 228.65),
            (47000, 0.0014275, 270.65), (71000, 6.4211e-5, 214.65), (84852, 6.9578e-6, 186.95),
        };
        var rows = new List<(string, double, double, double)>();
        foreach ((double h, double rho, double t) in nodes) {
            Air a = Atmosphere.At(h);
            rows.Add(($"плотность на {h / 1000:F1} км", a.Rho, rho, 0.01));
            rows.Add(($"температура на {h / 1000:F1} км", a.T, t, 0.005));
        }
        Air sl = Atmosphere.At(0);
        rows.Add(("давление у моря", sl.P, 101325, 0.001));
        rows.Add(("скорость звука у моря", sl.A, 340.29, 0.005));
        Table("USSA-76 на опорных высотах", rows.ToArray());
        Table("термосфера и экзосфера по таблице",
              (from x in new (double H, double Rho)[] { (100e3, 5.6040e-7), (130e3, 8.1520e-9), (200e3, 2.789e-10), (400e3, 3.725e-12), (1000e3, 3.019e-15) }
               select ($"плотность на {x.H / 1000:F0} км", Atmosphere.At(x.H).Rho, x.Rho, 0.01)).ToArray());
        Air below = Atmosphere.At(90e3 - 0.1), above = Atmosphere.At(90e3 + 0.1), exo = Atmosphere.At(1200e3);
        bool falls = true;
        double prev = double.MaxValue;
        for (double h = 90e3; h <= 1200e3; h += 5e3) {
            double r = Atmosphere.At(h).Rho;
            if (r <= 0 || r >= prev) { falls = false; break; }
            prev = r;
        }
        Group("выше 90 км атмосфера гладкая и конечная", $"на 1200 км {N(exo.Rho)} кг/м³, экзосфера {N(Atmosphere.At(600e3).T)} К",
              ("стык 90 км непрерывен", Math.Abs(above.Rho / below.Rho - 1) < 0.01 && Math.Abs(above.T / below.T - 1) < 0.01),
              ("плотность убывает и положительна до 1200 км", falls),
              ("за таблицей экспонента", exo.Rho > 0 && exo.Rho < 3.019e-15),
              ("температура экзосферы ~1000 К", Math.Abs(Atmosphere.At(600e3).T / 1000 - 1) < 0.01));
    }
    private static void Drag() {
        Head("Сопротивление по числу Маха");
        double peak = Atmosphere.Cd0(1.05);
        Group("носом вперёд: пик на трансзвуке, дальше спад", $"пик {N(peak)}, на M 2 {N(Atmosphere.Cd0(2))}, на M 8 {N(Atmosphere.Cd0(8))}",
              ("пик выше дозвука", peak > Atmosphere.Cd0(0.6)),
              ("пик выше сверхзвука", peak > Atmosphere.Cd0(2)),
              ("после M 1,3 монотонно", Atmosphere.Cd0(2) > Atmosphere.Cd0(3) && Atmosphere.Cd0(3) > Atmosphere.Cd0(5) && Atmosphere.Cd0(5) > Atmosphere.Cd0(8)));
        Group("двигателями вперёд: донный срез как стакан (пункт 48)", $"M 0,5 {N(Atmosphere.CdEngine(0.5))}, M 3 {N(Atmosphere.CdEngine(3))}",
              ("1,60 на дозвуке", Math.Abs(Atmosphere.CdEngine(0.5) - 1.60) < 1e-9),
              ("2,00 на M 3", Math.Abs(Atmosphere.CdEngine(3) - 2.00) < 1e-9),
              ("выше M 5 полка", Atmosphere.CdEngine(5) == Atmosphere.CdEngine(12)),
              ("торец тормозит сильнее носа на сверхзвуке", Atmosphere.CdEngine(2) > Atmosphere.Cd0(2) && Atmosphere.CdEngine(8) > Atmosphere.Cd0(8)),
              ("кривая выбирается по знаку осевой скорости", Atmosphere.CdAxial(3, 1) == Atmosphere.Cd0(3) && Atmosphere.CdAxial(3, -1) == Atmosphere.CdEngine(3)));
    }
    private static Vehicle AtSpeed(Kind kind, double alt, double vx, double vy, double th) =>
        new(kind, 60e3) { X = 0, Y = Const.RE + alt, Vx = vx, Vy = vy, Th = th };
    private static void AeroForces() {
        Head("Аэродинамические силы");
        Vehicle up = AtSpeed(Kind.Booster, 8000, -Const.W * (Const.RE + 8000), 400, 0);
        Air air = Atmosphere.At(up.Alt);
        AeroForce f0 = Aero.Compute(up, 0.5 * air.Rho * up.Speed * up.Speed, up.Speed / air.A);
        Group("вертикальный подъём: α = 0, нормальной силы нет, осевая тормозит", $"Fax {N(f0.Fax)} Н",
              ("α = 0", f0.Alpha == 0), ("CN = 0", f0.CN == 0), ("Fax < 0", f0.Fax < 0));
        var sim = new SimState { T = 100, RhoK = 1 };
        Vehicle v = AtSpeed(Kind.Booster, 12000, -Const.W * (Const.RE + 12000) + 300, -180, 2.2);
        v.Prop = v.PropMax * 0.2;
        sim.Veh.Add(v);
        Air a2 = Atmosphere.At(v.Alt);
        AeroForce ref2 = Aero.Compute(v, 0.5 * a2.Rho * v.Speed * v.Speed, v.Speed / a2.A);
        Flight.StepVehicle(sim, v, Const.DT);
        Near("Aero.Compute и шаг полёта дают одно сопротивление", v.Drag, ref2.Drag, 1e-9, " Н");
        double a90 = Surfaces.FlapCn(Math.PI / 2), a70 = Surfaces.FlapCn(70 * Const.D2R);
        True("закрылки складываются вдоль корпуса: на 90° атаки сила наибольшая (пункт 49)", a90 > a70 && a90 > 0.6, $"на 90° {N(a90)}, на 70° {N(a70)}");
    }
    private static Vehicle Burning(Kind kind, double seconds, double pa = Const.P0, double cool = 1, double throttle = 1) {
        var v = new Vehicle(kind, 60e3) { Ign = true, NEng = kind == Kind.Booster ? 33 : 6, Throttle = throttle };
        foreach (Engine e in v.Eng) e.P.Cool = cool;
        const double dt = 0.02;
        for (int i = 0; i * dt < seconds; i++) {
            Pressurant.Step(v, dt);
            EngineSet.Update(v, dt, pa);
            EngStats st = EngineSet.Stats(v);
            v.F = st.F;
            v.Mdot = st.Md;
            v.Prop = Math.Max(0, v.Prop - st.Md * dt);
        }
        return v;
    }
    private static void EngineThrust() {
        Head("Двигатель и сопло");
        Engine sea = Burning(Kind.Booster, 8).Eng[0], vac = Burning(Kind.Booster, 8, 0).Eng[0];
        Group("высотная характеристика", $"у земли {N(sea.F / 1e3)} кН, в пустоте {N(vac.F / 1e3)} кН",
              ("вышел на режим", sea.Spool > 0.99 && sea.F > 1e6),
              ("прибавка в пустоте равна Ae·P₀", Math.Abs((vac.F - sea.F) / (sea.Spec.Ae * Const.P0) - 1) < 0.005),
              ("тяга в пустоте по паспорту", Math.Abs(vac.F / Spec.RaptorSL.Fv - 1) < 0.02));
        EngineSpec sl = Spec.RaptorSL, vc = Spec.RaptorVac;
        double pc = EngineSpec.PC_PA;
        Group("геометрия из паспортных чисел", $"горловина {N(2 * Math.Sqrt(sl.At / Math.PI))} м, срез {N(2 * Math.Sqrt(sl.Ae / Math.PI))} м, ε {N(sl.Eps)} и {N(vc.Eps)}",
              ("горловина 0,24 м", Math.Abs(2 * Math.Sqrt(sl.At / Math.PI) / 0.240 - 1) < 0.02),
              ("срез 1,365 м", Math.Abs(2 * Math.Sqrt(sl.Ae / Math.PI) / 1.365 - 1) < 0.02),
              ("расширение земного ~32, вакуумного в 1,5 раза больше", sl.Eps > 30 && sl.Eps < 35 && vc.Eps > sl.Eps * 1.5),
              ("множитель реальности 1…1,06", sl.Real > 1.0 && sl.Real < 1.06),
              ("на срезе M > 3 и растёт с расширением", EngineSpec.ExitMach(vc.Eps) > EngineSpec.ExitMach(sl.Eps) && EngineSpec.ExitMach(sl.Eps) > 3),
              ("в пустоте Cf идеальный, внешнее давление его съедает", sl.CfAt(pc, 0) == sl.CfVac * sl.Real && sl.CfAt(pc, Const.P0) < sl.CfAt(pc, 0)));
        Group("отрыв потока по Саммерфилду", $"давление на срезе {N(sl.Pe / 1000)} и {N(vc.Pe / 1000)} кПа",
              ("земное у земли держится", !sl.Separated(pc, Const.P0)),
              ("вакуумное у земли срывается", vc.Separated(pc, Const.P0)),
              ("в пустоте не срывается ни одно", !sl.Separated(pc, 0) && !vc.Separated(pc, 0)),
              ("на глубоком дросселе срывается и земное", sl.Separated(pc * 0.25, Const.P0)));
        EngineSpec wide = sl.WithExitArea(2);
        Group("длинное сопло: в пустоте выигрывает, у земли срывается", $"Cf в пустоте {N(wide.CfAt(pc, 0))} против {N(sl.CfAt(pc, 0))}",
              ("множитель 1 — то же сопло", ReferenceEquals(sl.WithExitArea(1), sl)),
              ("удвоение среза удваивает расширение", Math.Abs(wide.Eps - 2 * sl.Eps) < 1e-9),
              ("давление на срезе ниже", wide.Pe < sl.Pe),
              ("в пустоте тяга больше", wide.CfAt(pc, 0) > sl.CfAt(pc, 0)),
              ("у земли срыв и выигрыша нет", wide.Separated(pc, Const.P0) && wide.CfAt(pc, Const.P0) < sl.CfAt(pc, Const.P0)));
        double gain5 = vc.CfAt(pc * 0.5, Const.P0) - (vc.CfVac * vc.Real - Const.P0 * vc.Eps / (pc * 0.5));
        double gain4 = vc.CfAt(pc * 0.4, Const.P0) - (vc.CfVac * vc.Real - Const.P0 * vc.Eps / (pc * 0.4));
        Group("отрыв укорачивает сопло и прибавляет тягу", $"выигрыш Cf {N(gain5)} на 50 % и {N(gain4)} на 40 %",
              ("тяга выше, чем у целого перерасширенного сопла", gain5 > 0),
              ("чем глубже дроссель, тем выигрыш больше", gain4 > gain5),
              ("работающая часть короче полной", vc.CfAt(pc * 0.4, Const.P0) < vc.CfAt(pc * 0.4, 0)));
        var vh = new Vehicle(Kind.Booster, 0) { Ign = true, NEng = 33, Throttle = 1 };
        Engine one = vh.Eng[0];
        for (int i = 0; i < 100; i++) one.Update(0.02, Const.P0, 1, true);
        int mismatch = 0, coast = 0;
        for (int i = 0; i < 200; i++) {
            one.Update(0.02, Const.P0, 0, false);
            if (one.Spool <= 0) break;
            coast++;
            if (one.Spec.Separated(one.Pc * 1e6, Const.P0) != one.Sep) mismatch++;
        }
        True("на выбеге признак отрыва не расходится с расчётом", mismatch == 0, $"{coast} шагов выбега, расхождений {mismatch}");
    }
    private static void ChamberCooling() {
        Head("Охлаждение камеры и прогар стенки");
        Engine nom = Burning(Kind.Booster, 20).Eng[0], low = Burning(Kind.Booster, 20, throttle: 0.5).Eng[0];
        Group("на номинале стенка в рабочем диапазоне", $"{N(nom.TWall)} К, {N(nom.QWall / 1e6)} МВт/м², на дросселе {N(low.QWall / 1e6)}",
              ("700…950 К", nom.TWall > 700 && nom.TWall < 950),
              ("поток 50…160 МВт/м², как у настоящих камер", nom.QWall > 50e6 && nom.QWall < 160e6),
              ("прогар не копится, двигатель жив", nom.Burn == 0 && !nom.Failed),
              ("на дросселе стенка та же, поток ниже", Math.Abs(low.TWall / nom.TWall - 1) < 0.02 && low.QWall < nom.QWall * 0.85));
        Engine half = Burning(Kind.Booster, 20, cool: 0.6).Eng[0], shut = Burning(Kind.Booster, 40, cool: 0.3).Eng[0];
        Group("без охлаждения стенка прогорает", $"наполовину: {N(half.TWall)} К; на треть: {shut.Reason}",
              ("наполовину прикрытый клапан — выше предела", half.TWall > 950),
              ("но за 20 с не прогорает", !half.Failed),
              ("на треть — прогар за десятки секунд", shut.Failed));
        double hFull = ChamberHeat.GasSide(0.24, 30e6, EngineSpec.CSTAR), hHalf = ChamberHeat.GasSide(0.24, 15e6, EngineSpec.CSTAR);
        Group("формулы теплоотдачи", $"восстановление {N(ChamberHeat.Recovery(3500, EngineSpec.GAMMA, 0.5, 1.0))} К",
              ("газовая сторона ∝ p^0,8", Math.Abs(hHalf / hFull - Math.Pow(0.5, 0.8)) < 1e-9),
              ("температура восстановления ниже газа, ~3434 К", Math.Abs(ChamberHeat.Recovery(3500, EngineSpec.GAMMA, 0.5, 1.0) / 3434 - 1) < 0.01));
        Vehicle idle = Burning(Kind.Booster, 8);
        idle.Ign = false;
        idle.NEng = 0;
        for (int i = 0; i < 300; i++) EngineSet.Update(idle, 0.02, Const.P0);
        True("после выключения стенка остывает, а не прогорает", !idle.Eng[0].Failed && idle.Eng[0].TWall < 900,
             $"{N(idle.Eng[0].TWall)} К, повреждение {N(idle.Eng[0].Burn)}");
    }
    private static void PumpCavitation() {
        Head("Турбонасос");
        Vehicle v = Burning(Kind.Booster, 8);
        Pump pf = v.Eng[0].Pf;
        double cavGood = pf.Cav, headGood = pf.Head;
        v.Tanks.F.P = 40e3;
        v.Tanks.F.Mg = v.Tanks.F.P * Math.Max(v.Tanks.F.V * 0.02, 1) / (v.Tanks.F.R * 270);
        for (int i = 0; i < 60; i++) {
            EngineSet.Update(v, 0.02, Const.P0);
            v.Tanks.F.P = 40e3;
        }
        double cavLow = pf.Cav, headLow = pf.Head;
        v.AAx = 1.5 * Const.G0;
        for (int i = 0; i < 60; i++) {
            EngineSet.Update(v, 0.02, Const.P0);
            v.Tanks.F.P = 40e3;
        }
        Group("просадка наддува на входе даёт кавитацию и провал напора", $"запас {N(cavGood)} → {N(cavLow)}, напор {N(headGood)} → {N(headLow)} м; при 1,5 g запас {N(pf.Cav)}",
              ("на штатном наддуве кавитации нет", cavGood > 0.999),
              ("при срезанном наддуве насос кавитирует", cavLow < cavGood && cavLow < 1),
              ("напор просел", headLow < headGood),
              ("под перегрузкой 1,5 g столб держит вход без кавитации", pf.Cav > 0.999));
    }
    private static void Settling() {
        Head("Осадка топлива");
        var v = new Vehicle(Kind.Ship, 0) { Prop = 150e3 };
        for (int i = 0; i < 100; i++) Propellant.Settle(v, 0, 0.01);
        double free = v.Settled;
        for (int i = 0; i < 800; i++) Propellant.Settle(v, Const.G0, 0.01);
        double g1 = v.Settled;
        double tRcs = Propellant.SettleTau(v, Const.ULLAGE_A);
        Group("жидкость всплывает в невесомости и оседает под тягой", $"за 1 с невесомости {N(free)}, за 8 с при 1 g {N(g1)}, τ при мягкой осадке {N(tRcs)} с",
              ("за секунду невесомости всплывает", free < 0.1), ("за 8 с при 1 g оседает", g1 > 0.95),
              ("от ДМТ оседает десятки секунд", tRcs > 20 && tRcs < 80));
        Vehicle wet = Burning(Kind.Ship, 3), dry = Unsettled(3);
        Group("запуск на неосевшем топливе — авария", $"осевшее: {wet.Eng.Count(e => e.On)} работают; всплывшее: {dry.Eng.Count(e => e.Failed)} выключены — «{dry.Eng[0].Reason}»",
              ("на осевшем запускаются", wet.Eng.All(e => e.On && !e.Failed)),
              ("на всплывшем все выключены", dry.Eng.All(e => e.Failed)),
              ("причина — газ на входе", dry.Eng[0].Reason.Contains("газ")));
    }
    private static (Vehicle V, double Push) InOrbit(bool rcs) {
        var sim = new SimState { T = 0, RhoK = 1 };
        var v = new Vehicle(Kind.Ship, 0) {
            Mode = "man", Prop = 120e3, Settled = 0, Rcs = rcs, Launched = true,
            Ign = true, NEng = 3, Throttle = 1, Th = Math.PI / 2, ThCmd = Math.PI / 2,
        };
        v.Y = Const.RE + 200e3;
        v.Vx = Math.Sqrt(Const.MU / v.Y);
        sim.Veh.Add(v);
        double push = 0;
        for (int i = 0; i < 30000 && !(v.NRun > 0 && !v.Ullage && v.F > 1e5); i++) {
            Flight.StepVehicle(sim, v, Const.DT);
            sim.T += Const.DT;
            if (v.Ullage && v.NRun == 0) push = Math.Max(push, v.AAx);
        }
        return (v, push);
    }
    private static void Ullage() {
        Head("Запуск в невесомости");
        (Vehicle on, double push) = InOrbit(true);
        Vehicle off = InOrbit(false).V;
        Group("перед запуском ДМТ мягко осаживают топливо, без них запуск срывается",
              $"с ДМТ: {on.NRun} работают, осадка {N(on.Settled)}, ускорение {N(push)} м/с²; без ДМТ: {off.Eng.Count(e => e.Failed)} выключены",
              ("с ДМТ двигатели работают", on.NRun >= 3 && on.Eng.All(e => !e.Failed)),
              ("запуск после осадки", on.Settled >= Const.SETTLE_GO),
              ("ДМТ толкают мягко", push > 0 && push <= Const.ULLAGE_A * 1.25),
              ("без ДМТ выключены по газу", off.Eng.Where(e => !e.IsVac).All(e => e.Failed) && off.Eng[0].Reason.Contains("газ")));
    }
    private static void Slosh() {
        Head("Плескание");
        var v = new Vehicle(Kind.Booster, 0) { Prop = 0.2 * Spec.Booster.Prop, AAx = Const.G0 };
        double w = Propellant.Omega(v, true), dt = 0.01, last = 0, t0 = double.NaN, t1 = double.NaN, a0 = 0, a1 = 0;
        int cross = 0;
        for (int i = 0; i < 6000; i++) {
            Propellant.Slosh(v, v.Cm, i < 10 ? 1 : 0, dt);
            double y = v.SloshY[0];
            if (i > 10 && last < 0 && y >= 0) {
                cross++;
                if (cross == 1) t0 = i * dt;
                if (cross == 6) t1 = i * dt;
            }
            if (cross == 1) a0 = Math.Max(a0, Math.Abs(y));
            if (cross == 6) a1 = Math.Max(a1, Math.Abs(y));
            last = y;
        }
        double per = (t1 - t0) / 5, zeta = Math.Log(a0 / a1) / (2 * Math.PI * 5);
        for (int j = 0; j < 2; j++) {
            double wj = Propellant.Omega(v, j == 0);
            v.SloshY[j] = -2 / (wj * wj);
            v.SloshV[j] = 0;
        }
        double shift = Const.G0 * (Propellant.SloshMass(v, true) * v.SloshY[0] + Propellant.SloshMass(v, false) * v.SloshY[1]);
        double steady = Propellant.Slosh(v, v.Cm, 2, dt);
        v.AAx = 0;
        v.SloshY[0] = 1;
        double free = Math.Abs(Propellant.Slosh(v, v.Cm, 1, dt));
        Group("первый тон: период и затухание; в равновесии — только сдвиг центра масс, в невесомости момента нет",
              $"период {N(per)} с при расчётных {N(2 * Math.PI / w)}, ζ {N(zeta)}, момент в равновесии {N(steady)} Н·м при сдвиге ЦМ {N(shift)}",
              ("период по ω² = 1,84·a·th(1,84h/R)/R", w > 0 && Math.Abs(per * w / (2 * Math.PI) - 1) < 0.03),
              ("затухание по перегородкам", zeta > 0.02 && zeta < 0.04),
              ("в равновесии момент равен тяге на сдвиг центра масс", shift != 0 && Math.Abs(steady / shift - 1) < 1e-6),
              ("в невесомости момента нет", free == 0));
        var calm = SloshHover(0);
        var wave = SloshHover(1.5);
        Group("волна в полупустом баке качает ступень, автомат гасит", $"без волны {N(calm.Peak)}°, с волной {N(wave.Peak)}°, к концу {N(wave.End)}°; момент волны до {N(wave.TMax / 1e6)} МН·м при инерции {N(wave.I)} кг·м²",
              ("волна заметно качает", wave.Peak > 0.15 && wave.Peak > 100 * calm.Peak),
              ("затухает, а не раскачивается", wave.End < 0.5 * wave.Peak));
    }
    private static (double Peak, double End, double TMax, double I) SloshHover(double y0) {
        var sim = new SimState { T = 0, RhoK = 1 };
        var v = new Vehicle(Kind.Booster, 0) {
            Mode = "landB", Ign = true, NEng = 3, ThCmd = 0, Launched = true, Kp = Const.BOOST_KP, Kd = Const.BOOST_KD,
        };
        v.Prop = 0.1 * v.PropMax;
        foreach (Tank t in new[] { v.Tanks.F, v.Tanks.O }) t.Mg = t.P * t.V * (1 - v.Fill) / (t.R * 270);
        v.Y = Const.RE + 1000;
        v.Vx = -Const.W * v.Y;
        sim.Veh.Add(v);
        double peak = 0, end = 0, tMax = 0;
        for (int i = 0; i < 3200; i++) {
            v.Throttle = Const.Clamp(v.Mass * Const.G0 / (3 * 2.45e6), 0.4, 1);
            if (i < 200) v.Settled = 1;
            if (i == 200) v.SloshY[0] = y0;
            Flight.StepVehicle(sim, v, Const.DT);
            sim.T += Const.DT;
            double th = Math.Abs(Vehicle.AngDiff(v.Th, 0)) * Const.R2D;
            if (i > 200) peak = Math.Max(peak, th);
            if (i > 2700) end = Math.Max(end, th);
            tMax = Math.Max(tMax, Math.Abs(v.SloshT));
        }
        return (peak, end, tMax, v.Inertia);
    }
    private static Vehicle Unsettled(double seconds) {
        var v = new Vehicle(Kind.Ship, 60e3) { Ign = true, NEng = 6, Throttle = 1, Settled = 0 };
        for (int i = 0; i * 0.02 < seconds; i++) {
            Pressurant.Step(v, 0.02);
            EngineSet.Update(v, 0.02, 0);
        }
        return v;
    }
    private static void CenterOfMass() {
        Head("Центр масс");
        var v = new Vehicle(Kind.Booster, 60e3);
        double Cm(double k) { v.Prop = v.PropMax * k; return v.Cm; }
        double full = Cm(1), half = Cm(0.5), low = Cm(0.15), dry = Cm(0);
        var st = new Vehicle(Kind.Booster, 60e3) { Mate = new Vehicle(Kind.Ship, 60e3), Stacked = true };
        Group("по мере выработки центр масс опускается, сухая ступень — центр конструкции",
              $"{N(full)} → {N(half)} → {N(low)} → {N(dry)} м, пакет {N(st.Cm)} м",
              ("опускается на выработке", full > half && half > low),
              ("сухая ступень — ровно 0,38·L", Math.Abs(dry - 0.38 * v.Len) < 1e-9 && dry > low),
              ("не выходит за корпус", dry > 0 && full < v.Len),
              ("пакет со второй ступенью выше", st.Cm > full));
    }
    private static void MomentOfInertia() {
        Head("Момент инерции по частям");
        var v = new Vehicle(Kind.Booster, 60e3);
        double Ratio(double k) { v.Prop = v.PropMax * k; double l = v.FullLen; return v.Inertia / (v.Mass * l * l / 12); }
        double full = Ratio(1), empty = Ratio(0);
        var s = new Vehicle(Kind.Ship, 0) { Prop = 0 };
        double shipEmpty = s.Inertia / (s.Mass * s.Len * s.Len / 12);
        Group("против однородного стержня: полная ступень легче в развороте, пустая тяжелее",
              $"ускоритель полный {N(full)}, пустой {N(empty)}, корабль пустой {N(shipEmpty)} от стержня",
              ("полный — топливо в середине, меньше 0,75 стержня", full < 0.75),
              ("пустой — двигатели и рули по концам, больше 1,2 стержня", empty > 1.2),
              ("пустой корабль тоже тяжелее стержня", shipEmpty > 1.1));
    }
    private static void OrbitElements() {
        Head("Орбитальные элементы");
        double r = Const.RE + 200e3, ra = Const.RE + 400e3;
        Orbit o = Guidance.Orb(new Vehicle(Kind.Ship, 0) { X = 0, Y = r, Vx = -Math.Sqrt(Const.MU / r), Vy = 0 });
        Orbit oe = Guidance.Orb(new Vehicle(Kind.Ship, 0) { X = 0, Y = r, Vx = -Math.Sqrt(2 * Const.MU * ra / (r * (r + ra))), Vy = 0 });
        Table("круговая 200 км и эллипс 200 × 400 км",
              ("круговая: апогей", o.Apo, 200e3, 1e-6), ("круговая: перигей", o.Peri, 200e3, 1e-6),
              ("круговая: 1 + e", 1 + o.E, 1, 1e-9), ("круговая: период", o.Per, 5301.0, 0.001),
              ("эллипс: апогей", oe.Apo, 400e3, 1e-6), ("эллипс: перигей", oe.Peri, 200e3, 1e-6));
    }
    private static Vehicle Soak(Kind kind, double heatKw, double alphaDeg, double seconds) {
        var v = new Vehicle(kind, 0);
        var sim = new SimState { T = 0 };
        sim.Veh.Add(v);
        for (double t = 0; t < seconds; t += 0.05) {
            v.Heat = heatKw;
            v.Alpha = alphaDeg * Const.D2R;
            Physics.Sim.Thermal(sim, v, 0.05);
            sim.T += 0.05;
        }
        return v;
    }
    private static void HeatShield() {
        Head("Теплозащита");
        Vehicle ok = Soak(Kind.Ship, 305, 67, 400), flip = Soak(Kind.Ship, 305, -67, 400), mild = Soak(Kind.Ship, 40, -67, 400);
        Group("корабль брюхом к потоку", $"плитки {N(ok.TTile)} К, подветренная сталь {N(ok.TLee)} К",
              ("плитки 1400…1600 К", ok.TTile > 1400 && ok.TTile < 1600),
              ("подветренная сталь холодная", ok.TLee < 900),
              ("прогар не копится", ok.Dmg == 0));
        Group("спиной к потоку корабль прогорает", $"сталь до {N(flip.MaxLee)} К; слабый поток — {N(mild.MaxLee)} К",
              ("прогар", flip.Dmg >= 1 && flip.Crashed),
              ("плитки в тени целы", flip.TTile < Const.TILE_LIMIT),
              ("слабый поток разворот прощает", !mild.Crashed && mild.Dmg < 1));
        Vehicle boost = Soak(Kind.Booster, 305, 67, 400), real = Soak(Kind.Booster, 20, 67, 400);
        Group("ускоритель без плиток", $"под орбитальным потоком {N(boost.TTile)} К, на своём входе {N(real.TTile)} К",
              ("голая сталь горячее плитки", boost.TTile > ok.TTile),
              ("орбитальный поток прожёг бы", boost.Crashed),
              ("на своём входе не прогорает", !real.Crashed && real.TTile < Const.SKIN_LIMIT));
    }
    private static double AscentAoA(Wind wind) {
        var sim = new SimState { Mission = "orbital", AnomOn = false };
        Physics.Sim.Reset(sim, 12345);
        sim.Wind = wind;
        Vehicle b = sim.Veh[0];
        double worst = 0;
        for (int i = 0; i < 200000 && b.Alt < 16e3; i++) {
            Physics.Sim.Tick(sim, Const.DT);
            if (b.Alt >= 8e3 && b.Alt <= 14e3) worst = Math.Max(worst, Math.Abs(b.Alpha) * Const.R2D);
        }
        return worst;
    }
    private static Wind Rolled(uint seed) {
        var rng = new Rng();
        rng.Seed(seed);
        return Wind.Roll(rng);
    }
    private static void WindProfile() {
        Head("Ветер");
        Wind w = Rolled(12345), same = Rolled(12345), other = Rolled(777);
        Group("профиль: у земли есть, струйное течение сильнее, выше 60 км ноль", $"у земли {N(w.At(0))}, на 11 км {N(w.At(11e3))} м/с, сдвиг {N(w.MaxShear(6e3, 16e3))} м/с на км",
              ("у земли больше 0,5 м/с", Math.Abs(w.At(0)) > 0.5),
              ("на 11 км сильнее", Math.Abs(w.At(11e3)) > Math.Abs(w.At(0))),
              ("выше 60 км и на границе ноль", w.At(70e3) == 0 && w.At(Wind.TopAlt) == 0),
              ("к границе сходит плавно", Math.Abs(w.At(55e3)) < Math.Abs(w.At(30e3))),
              ("сдвиг на участке Max Q больше 1 м/с на км", w.MaxShear(6e3, 16e3) > 1.0));
        Group("зерно задаёт ветер", $"на 9 км {N(w.At(9e3))} и {N(other.At(9e3))} м/с",
              ("одно зерно — тот же профиль", same.At(9e3) == w.At(9e3)),
              ("другое — другой", Math.Abs(other.At(9e3) - w.At(9e3)) > 0.5));
        var v = new Vehicle(Kind.Ship, 0) { X = 0, Y = Const.RE + 9e3, Vx = -Const.W * (Const.RE + 9e3), Vy = 300 };
        Vec2 ground = v.VRel;
        v.WindE = 30;
        Vec2 air = v.VAir;
        double diff = Math.Sqrt(Math.Pow(air.X - ground.X, 2) + Math.Pow(air.Y - ground.Y, 2));
        True("воздушная скорость отличается от земной ровно на ветер, земную ветер не трогает",
             Math.Abs(diff - 30) < 1e-9 && v.VRel.Len == ground.Len, $"{N(diff)} м/с");
        double jump = 0;
        for (double h = 50; h < 5000; h += 50) jump = Math.Max(jump, Math.Abs(Wind.Steady(12).At(h + 1) - Wind.Steady(12).At(h - 1)));
        Group("штиль и постоянный ветер", $"наибольший скачок приземного за 2 м — {N(jump)} м/с",
              ("в штиль всюду ноль, не NaN", Wind.Calm().At(0) == 0 && Wind.Calm().At(9e3) == 0),
              ("постоянный: 12 у земли, затухает выше", Math.Abs(Wind.Steady(12).At(0) - 12) < 1e-9 && Math.Abs(Wind.Steady(12).At(30e3)) < 0.5),
              ("приземный слой без скачка на 1200 м (пункт 58)", jump < 0.1),
              ("ниже стола башни ветер как у стола, а не ноль (пункт 62)", Wind.Steady(12).At(-Const.DECK_H) == Wind.Steady(12).At(0) && w.At(-5) == w.At(0)));
        double aCalm = AscentAoA(Wind.Calm()), aWind = AscentAoA(w);
        Group("сдвиг ветра даёт угол атаки на выведении", $"{N(aCalm)}° → {N(aWind)}° на 8…14 км",
              ("без ветра меньше 5°", aCalm < 5), ("со сдвигом больше на градус", aWind > aCalm + 1));
    }
    private static SimState Live(uint seed, bool disp, string mission = "orbital") {
        var sim = new SimState { Mission = mission, AnomOn = false, Disperse = disp };
        Physics.Sim.Reset(sim, seed);
        return sim;
    }
    private static (double X, double Y) At(SimState sim, double t) {
        while (sim.T < t) Physics.Sim.Tick(sim, Const.DT);
        return (sim.Veh[0].X, sim.Veh[0].Y);
    }
    private static double Spread(IEnumerable<double> xs, out double mean) {
        double s = 0, s2 = 0;
        int n = 0;
        foreach (double x in xs) { s += x; s2 += x * x; n++; }
        mean = s / Math.Max(n, 1);
        return Math.Sqrt(Math.Max(s2 / Math.Max(n, 1) - mean * mean, 0));
    }
    private static void Dispersion_() {
        Head("Разброс и порывы");
        var a = At(Live(777, true), 120);
        var a2 = At(Live(777, true), 120);
        var c = At(Live(778, true), 120);
        double dx = Math.Sqrt((a.X - c.X) * (a.X - c.X) + (a.Y - c.Y) * (a.Y - c.Y));
        Group("одно зерно — один полёт, другое — другой", $"к T+120 разошлись на {N(dx)} м",
              ("то же зерно повторяет полёт до бита", a == a2), ("другое расходится", dx > 5));
        SimState off = Live(777, false), on = Live(777, true);
        bool flat = off.RhoK == 1 && off.Veh[0].Dry == Spec.Booster.Dry && off.Veh[1].Dry == Spec.Ship.Dry + off.Payload
                    && off.Veh.All(v => v.Eng.All(e => e.P.Cf == 1));
        double[] cf = on.Veh[0].Eng.Select(e => e.P.Cf).ToArray();
        double sd = Spread(cf, out _);
        Group("естественный разброс отдельно от отказов", $"тяга ±{N(sd * 100)} %, плотность ×{N(on.RhoK)}, ускоритель {N(on.Veh[0].Dry / 1000)} т",
              ("без разброса всё по таблице", flat),
              ("у каждого двигателя своя тяга в пределах ±2 %", sd > 0.003 && cf.Min() >= 0.98 && cf.Max() <= 1.02),
              ("плотность другая, но в пределах 5 %", on.RhoK != 1 && Math.Abs(on.RhoK - 1) <= 0.05),
              ("масса другая, но в пределах 1 %", on.Veh[0].Dry != Spec.Booster.Dry && Math.Abs(on.Veh[0].Dry / Spec.Booster.Dry - 1) < 0.01));
        double gSd = Spread(Enumerable.Range(0, 1800).Select(i => on.Wind.At(8e3, i * 0.5)), out double gMean);
        Group("порывы", $"на 8 км СКО {N(gSd)} м/с, среднее {N(gMean)} против {N(on.Wind.At(8e3))}",
              ("на 8 км ветер меняется со временем", gSd > 0.3 && gSd < 5),
              ("средний ветер порывы не сдвигают", Math.Abs(gMean - on.Wind.At(8e3)) < 0.6),
              ("без разброса ветер неподвижен", Spread(Enumerable.Range(0, 600).Select(i => off.Wind.At(8e3, i * 0.5)), out _) == 0),
              ("выше 40 км порывов нет", Spread(Enumerable.Range(0, 600).Select(i => on.Wind.At(45e3, i * 0.5)), out _) < 1e-9));
        int nb = 0, ns = 0, both = 0;
        const int seeds = 5000;
        for (uint s = 1; s <= seeds; s++) {
            Dispersion d = Dispersion.Roll(s);
            if (d.RelightB) nb++;
            if (d.RelightS) ns++;
            if (d.RelightB && d.RelightS) both++;
        }
        True("двигатели на посадку изредка не зажигаются сами: около 2 % у каждой ступени",
             nb >= 0.015 * seeds && nb <= 0.025 * seeds && ns >= 0.015 * seeds && ns <= 0.025 * seeds && both < 0.002 * seeds,
             $"ускоритель {N(100.0 * nb / seeds)} %, корабль {N(100.0 * ns / seeds)} %, оба {both}");
        var hits = new Dictionary<string, int>();
        const int n = 400;
        for (uint s = 1; s <= n; s++) {
            var sim = new SimState { Mission = "orbital", AnomOn = true };
            Physics.Sim.Reset(sim, s);
            foreach (AnomalySpec spec in Anomalies.Table)
                if (sim.Anom.Has(spec.Key)) hits[spec.Key] = hits.GetValueOrDefault(spec.Key) + 1;
        }
        string skew = string.Concat(Anomalies.Table.Where(spec => hits.GetValueOrDefault(spec.Key) < 0.4 * spec.P * n || hits.GetValueOrDefault(spec.Key) > 2 * spec.P * n)
                                    .Select(spec => $"{spec.Key} {hits.GetValueOrDefault(spec.Key)}/{n} при {spec.P:P0}; "));
        True("на подряд идущих зёрнах отказы выпадают с заявленной частотой (пункт 50)", skew.Length == 0, skew);
    }
    private static (double pitchH, int qFlips, double tMeco, double tIgn, double tSep, double bFracIgn, int litSep, string sepLog)
        Staging(SimState sim, Action<SimState> tweak = null) {
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        double pitchH = double.NaN, tMeco = double.NaN, tIgn = double.NaN, frac = double.NaN;
        int flips = 0, lit = 0;
        bool qd = false;
        for (int i = 0; i < 40_000 && s.Attached; i++) {
            if (tweak != null && b.Mode == "ascent" && sim.T > 120) { tweak(sim); tweak = null; }
            Physics.Sim.Tick(sim, Const.DT);
            if (double.IsNaN(pitchH) && b.Mode == "ascent" && b.ThCmd != 0) pitchH = b.Alt;
            if (b.QDown != qd) { flips++; qd = b.QDown; }
            if (double.IsNaN(tMeco) && b.Mode == "meco") tMeco = sim.T;
            if (double.IsNaN(tIgn) && s.Ign) { tIgn = sim.T; frac = b.F / b.FRef; }
        }
        foreach (Engine e in s.Eng) if (e.On && e.Pc >= Const.HOT_PC * Pump.PC_NOM) lit++;
        string log = sim.Log.LastOrDefault(l => l.M.Contains("разделение по страховке") || l.M.Contains("на режиме"))?.M ?? "";
        return (pitchH, flips, tMeco, tIgn, sim.T, frac, lit, log);
    }
    private static void ConditionEvents() {
        Head("События выведения по условиям, а не по таймерам");
        var nom = Staging(Live(12345, false));
        Group("номинал", $"тангаж с {N(nom.pitchH)} м, корабль зажжён через {N(nom.tIgn - nom.tMeco)} с после MECO при {N(nom.bFracIgn * 100)} % тяги, расцепка через {N(nom.tSep - nom.tIgn)} с",
              ("тангаж — когда ракета ушла от башни", nom.pitchH >= Const.ASC_CLEAR_H && nom.pitchH < Const.ASC_CLEAR_H + 2),
              ("дроссель у Max Q включается и снимается один раз", nom.qFlips == 2),
              ("корабль зажигается, когда тяга ускорителя спала ниже 20 %", nom.bFracIgn < 0.2 && nom.tIgn - nom.tMeco > 0.2 && nom.tIgn - nom.tMeco < Const.HOT_IGN_T),
              ("расцепка — когда все шесть на режиме", nom.litSep == 6 && nom.tSep - nom.tIgn < Const.HOT_SEP_T - 1 && nom.sepLog.Contains("на режиме")));
        var d1 = Staging(Live(21, true));
        var d2 = Staging(Live(22, true));
        var fail = Staging(Live(12345, false), sim => { foreach (Engine e in sim.Veh[1].Eng) e.P.Tau = 5; });
        Group("с разбросом и с вялыми двигателями корабля", $"тангаж с {N(d1.pitchH)} и {N(d2.pitchH)} м; страховка через {N(fail.tSep - fail.tMeco)} с",
              ("с разбросом тангаж тоже по высоте", Math.Abs(d1.pitchH - Const.ASC_CLEAR_H) < 2 && Math.Abs(d2.pitchH - Const.ASC_CLEAR_H) < 2),
              ("двигатели не вышли на режим — разделение по страховке", fail.sepLog.Contains("страховке") && Math.Abs(fail.tSep - fail.tMeco - Const.HOT_SEP_T) < 0.05));
    }
    private static void MassBalance() {
        Head("Баланс массы");
        var sim = new SimState { T = 0, RhoK = 1 };
        var v = new Vehicle(Kind.Booster, 60e3) { Mode = "ascent", Ign = true, NEng = 33, Throttle = 1, ThCmd = 0.2 };
        v.Y = Const.RE + 2000;
        v.Vx = -Const.W * v.Y + 60;
        v.Vy = 210;
        sim.Veh.Add(v);
        double prop0 = v.Prop, spent = 0;
        for (int i = 0; i < 10000; i++) {
            Flight.StepVehicle(sim, v, Const.DT);
            sim.T += Const.DT;
            spent += v.Mdot * Const.DT;
        }
        True("за 100 с убыль в баке равна интегралу расхода", prop0 - v.Prop > 100e3 && Math.Abs((prop0 - v.Prop) / spent - 1) < 1e-9,
             $"{N((prop0 - v.Prop) / 1000)} т");
    }
    private static (double G, double Th, double Cm) Hover(int n) {
        var sim = new SimState { T = 0, RhoK = 1 };
        var v = new Vehicle(Kind.Ship, 30e3) { Mode = "landS", Ign = true, NEng = n, Throttle = 0.5, ThCmd = 0 };
        v.Y = Const.RE + 1000;
        v.Vx = -Const.W * v.Y;
        sim.Veh.Add(v);
        for (int i = 0; i < 300; i++) {
            Flight.StepVehicle(sim, v, Const.DT);
            sim.T += Const.DT;
        }
        return (v.Gimbal, Vehicle.AngDiff(v.Th, 0), v.Cm);
    }
    private static double GimbalRate() {
        var sim = new SimState { T = 0, RhoK = 1 };
        var v = new Vehicle(Kind.Ship, 30e3) { Mode = "landS", Ign = true, NEng = 1, Throttle = 0.45, Kp = 1.4, Kd = 2.8 };
        v.Y = Const.RE + 1000;
        v.Vx = -Const.W * v.Y;
        sim.Veh.Add(v);
        for (int i = 0; i < 200; i++) { Flight.StepVehicle(sim, v, Const.DT); sim.T += Const.DT; }
        v.ThCmd = 20 * Const.D2R;
        double g0 = v.Gimbal, rate = 0;
        for (int i = 0; i < 500; i++) {
            Flight.StepVehicle(sim, v, Const.DT);
            sim.T += Const.DT;
            rate = Math.Max(rate, Math.Abs(v.Gimbal - g0) / Const.DT);
            g0 = v.Gimbal;
        }
        return rate * Const.R2D;
    }
    private static (double Peak, double Settle) Step(double deg, double kp, double kd) {
        var sim = new SimState { T = 0, RhoK = 1 };
        var v = new Vehicle(Kind.Ship, 30e3) { Mode = "landS", Ign = true, NEng = 1, Throttle = 0.45, Kp = kp, Kd = kd };
        v.Y = Const.RE + 1000;
        v.Vx = -Const.W * v.Y;
        sim.Veh.Add(v);
        for (int i = 0; i < 200; i++) { Flight.StepVehicle(sim, v, Const.DT); sim.T += Const.DT; }
        v.ThCmd = deg * Const.D2R;
        double peak = 0, settle = double.NaN;
        for (int i = 0; i < 2000; i++) {
            Flight.StepVehicle(sim, v, Const.DT);
            sim.T += Const.DT;
            double th = Vehicle.AngDiff(v.Th, 0) * Const.R2D;
            peak = Math.Max(peak, th);
            if (Math.Abs(th - deg) > 0.5) settle = double.NaN;
            else if (double.IsNaN(settle)) settle = i * Const.DT;
        }
        return (peak, settle);
    }
    private static void EngineOut() {
        Head("Несимметричная тяга и привод сопел");
        var soft = Step(20, 1.4, 2.8);
        var stiff = Step(20, 4, 3);
        Group("разворот на 20° без перелёта при любом усилении", $"мягкий: пик {N(soft.Peak)}°, {N(soft.Settle)} с; жёсткий: пик {N(stiff.Peak)}°, {N(stiff.Settle)} с",
              ("мягкий не перелетает", soft.Peak < 21), ("жёсткий не перелетает", stiff.Peak < 21),
              ("жёсткий успокаивается за 10 с", stiff.Settle < 10));
        double gr = GimbalRate();
        Group("сопло перекладывается не быстрее привода", $"до {N(gr)}°/с",
              ("не быстрее 30°/с", gr <= 30 + 1e-6), ("но перекладывается", gr > 1));
        var one = Hover(1);
        var three = Hover(3);
        double want = -Math.Asin(0.87 / one.Cm);
        Group("один центральный двигатель корабля стоит в 0,87 м от оси — сопло парирует момент",
              $"сопло {N(one.G * Const.R2D)}° при расчётных {N(want * Const.R2D)}°, корпус {N(one.Th * Const.R2D)}°; на трёх сопло {N(three.G * Const.R2D)}°",
              ("сопло отклонено на −asin(0,87 / плечо до центра масс)", Math.Abs(one.G - want) < 0.05 * Math.Abs(want)),
              ("корпус не уводит", Math.Abs(one.Th) < 0.2 * Const.D2R),
              ("на трёх тяга симметрична — сопло прямо", Math.Abs(three.G) < 0.01 * Math.Abs(want)));
    }
    private static Vehicle DeepSpace(Kind kind) {
        var v = new Vehicle(kind, 0) { Mode = "man", Direct = true, Launched = true };
        v.Y = 1e9;
        return v;
    }
    private static void Fly(SimState sim, Vehicle v, double seconds) {
        for (int i = 0; i * Const.DT < seconds; i++) {
            Flight.StepVehicle(sim, v, Const.DT);
            sim.T += Const.DT;
        }
    }
    private static (double Got, double Want) Spin(Vehicle v, double arm) {
        var sim = new SimState { T = 0, RhoK = 1 };
        Fly(sim, v, 1.5);
        double om = v.Om, f = v.F, i = v.Inertia, c = v.Cm;
        Fly(sim, v, 1);
        double fa = (f + v.F) / 2, ia = (i + v.Inertia) / 2, ca = (c + v.Cm) / 2;
        double want = double.IsNaN(arm) ? -fa * Math.Sin(v.GimCmd) * ca / ia : -fa * arm / ia;
        return (v.Om - om, want);
    }
    private static void OpenLoop() {
        Head("Физика без автомата");
        var sim = new SimState { T = 0, RhoK = 1 };
        Vehicle spin = DeepSpace(Kind.Ship);
        spin.Om = 2 * Const.D2R;
        Fly(sim, spin, 60);
        Group("в пустоте без двигателей вращение сохраняется", $"через 60 с {N(spin.Om * Const.R2D)}°/с из 2, повернулся на {N(spin.Th * Const.R2D)}° из 120",
              ("угловая скорость та же", Math.Abs(spin.Om / (2 * Const.D2R) - 1) < 1e-3),
              ("угол равен ω·t", Math.Abs(spin.Th * Const.R2D - 120) < 0.5));
        Vehicle gim = DeepSpace(Kind.Ship);
        gim.Ign = true; gim.NEng = 3; gim.Throttle = 1; gim.GimCmd = 2 * Const.D2R;
        Vehicle one = DeepSpace(Kind.Ship);
        one.Ign = true; one.NEng = 1; one.Throttle = 1;
        var g = Spin(gim, double.NaN);
        var o = Spin(one, 0.87);
        Vehicle turn = DeepSpace(Kind.Ship);
        turn.Direct = false; turn.Kp = Const.SHIP_KP; turn.Kd = Const.SHIP_KD; turn.ThCmd = Math.PI / 2;
        double omPeak = 0;
        for (int i = 0; i * Const.DT < 90; i++) {
            Flight.StepVehicle(sim, turn, Const.DT);
            omPeak = Math.Max(omPeak, Math.Abs(turn.Om));
        }
        double turnErr = Math.Abs(Vehicle.AngDiff(turn.ThCmd, turn.Th)) * Const.R2D;
        Group("угловое ускорение без автомата — момент на инерцию", $"сопло 2°: {N(g.Got)} против F·sin δ·l/I {N(g.Want)} рад/с²; один двигатель: {N(o.Got)} против F·0,87/I {N(o.Want)}",
              ("сопло отклонено на 2°", Math.Abs(g.Got / g.Want - 1) < 0.02),
              ("один двигатель в 0,87 м от оси", Math.Abs(o.Got / o.Want - 1) < 0.02));
        Group("в пустоте автомат разворачивается экономно", $"пик {N(omPeak * Const.R2D)}°/с, через 90 с ошибка {N(turnErr)}°",
              ("не быстрее 1,5°/с", omPeak <= Const.OM_VAC * Const.D2R * 1.02), ("разворот на 90° закончен", turnErr < 1));
        Vehicle burn = DeepSpace(Kind.Ship);
        burn.Ign = true; burn.NEng = 3; burn.Throttle = 1;
        double m0 = burn.Mass;
        Fly(sim, burn, 60);
        double dv = Math.Sqrt(burn.Vx * burn.Vx + burn.Vy * burn.Vy), rocket = 350 * Const.G0 * Math.Log(m0 / burn.Mass);
        Near("ракетное уравнение в пустоте: 60 с на трёх, Isp 350 с", dv, rocket, 0.01, " м/с");
        var shut = Drop(false);
        var open = Drop(true);
        Group("падение двигателями вперёд с 8 до 5 км — как одномерный расчёт с тем же сопротивлением",
              $"рули сложены: {N(shut.Got)} против {N(shut.Want)} м/с; раскрыты: {N(open.Got)} против {N(open.Want)} м/с",
              ("рули сложены — сопротивление корпуса", Math.Abs(shut.Got / shut.Want - 1) < 0.01),
              ("рули раскрыты — корпус и рули", Math.Abs(open.Got / open.Want - 1) < 0.01),
              ("раскрытые рули тормозят", open.Got < shut.Got));
    }
    private static (double Got, double Want) Drop(bool fins) {
        var air = new SimState { T = 0, RhoK = 1, Wind = Wind.Calm() };
        var fall = new Vehicle(Kind.Booster, 0) { Mode = fins ? "man" : "ascent", Direct = true, Launched = true, Prop = 30e3, FinDep = fins ? 1 : 0 };
        fall.Y = Const.RE + 8000;
        fall.Vx = -Const.W * fall.Y;
        fall.Vy = -330;
        air.Veh.Add(fall);
        double h = 8000, vv = 330, m = fall.Mass, fin = fins ? 3 * 37.4 * 0.2 : 0;
        while (h > 5000) {
            Air at = Atmosphere.At(h);
            double gr = Const.MU / ((Const.RE + h) * (Const.RE + h)), cd = Atmosphere.CdEngine(vv / at.A) + 0.06;
            vv += (gr - 0.5 * at.Rho * vv * vv * (cd * fall.A + fin) / m) * Const.DT;
            h -= vv * Const.DT;
        }
        while (fall.Alt > 5000) {
            Flight.StepVehicle(air, fall, Const.DT);
            air.T += Const.DT;
        }
        return (-fall.VVert, vv);
    }
    private static (double Max, double End) FinFall(bool open, double defl) {
        var sim = new SimState { T = 0, RhoK = 1, Wind = Wind.Calm() };
        var v = new Vehicle(Kind.Booster, 0) {
            Mode = open ? "man" : "ascent", Direct = true, Launched = true, Prop = 30e3, FinDep = open ? 1 : 0,
            FinCmd = defl * Const.D2R, FinDefl = defl * Const.D2R, Th = 8 * Const.D2R,
        };
        v.Y = Const.RE + 25e3;
        v.Vx = -Const.W * v.Y;
        v.Vy = -900;
        sim.Veh.Add(v);
        double max = 0;
        for (int i = 0; i < 2000; i++) {
            Flight.StepVehicle(sim, v, Const.DT);
            sim.T += Const.DT;
            max = Math.Max(max, v.AoaDev * Const.R2D);
        }
        return (max, v.AoaDev * Const.R2D);
    }
    private static void Fins() {
        Head("Решётчатые рули без автомата");
        var open = FinFall(true, 0);
        var shut = FinFall(false, 0);
        var trim = FinFall(true, 10);
        Group("раскрытые рули ставят падающую ступень по потоку, сложенные — нет",
              $"раскрыты: до {N(open.Max)}°, через 20 с {N(open.End)}°; сложены: до {N(shut.Max)}°; руль на 10°: через 20 с {N(trim.End)}°",
              ("раскрыты — отклонение гаснет", open.End < 8 && open.Max < 9),
              ("сложены — корпус неустойчив", shut.Max > 15),
              ("руль на 10° — балансировка у 10…16°", trim.End > 10 && trim.End < 16));
    }
    private static (double Mean, double Early, double Late) FlapFall(double fwd, double aft) {
        var sim = new SimState { T = 0, RhoK = 1, Wind = Wind.Calm() };
        var v = new Vehicle(Kind.Ship, 0) {
            Mode = "man", Direct = true, Launched = true, Prop = 45e3, Th = 110 * Const.D2R,
            FlapFwdCmd = fwd * Const.D2R, FlapAftCmd = aft * Const.D2R, FlapFwd = fwd * Const.D2R, FlapAft = aft * Const.D2R,
        };
        v.Y = Const.RE + 15e3;
        v.Vx = -Const.W * v.Y;
        v.Vy = -250;
        sim.Veh.Add(v);
        double eLo = 1e9, eHi = -1e9, lLo = 1e9, lHi = -1e9, sum = 0;
        int n = 0;
        for (int i = 0; i < 3000; i++) {
            Flight.StepVehicle(sim, v, Const.DT);
            sim.T += Const.DT;
            double a = v.Alpha * Const.R2D;
            if (i >= 200 && i < 1200) { eLo = Math.Min(eLo, a); eHi = Math.Max(eHi, a); }
            if (i >= 2000) { lLo = Math.Min(lLo, a); lHi = Math.Max(lHi, a); }
            if (i >= 2500) { sum += a; n++; }
        }
        return (sum / n, eHi - eLo, lHi - lLo);
    }
    private static void Flaps() {
        Head("Закрылки корабля без автомата");
        var fwd = FlapFall(10, 60);
        var aft = FlapFall(40, 20);
        Group("закрылки задают балансировку падения брюхом, колебания гаснут сами",
              $"передние выпущены (10°), задние убраны (60°): {N(fwd.Mean)}°, размах {N(fwd.Early)} → {N(fwd.Late)}°; наоборот (40°/20°): {N(aft.Mean)}°, размах {N(aft.Early)} → {N(aft.Late)}°",
              ("балансировки различаются больше чем на 20°", Math.Abs(fwd.Mean - aft.Mean) > 20),
              ("передние выпущены больше — брюхом к потоку", fwd.Mean > 35 && fwd.Mean < 90),
              ("колебания гаснут", fwd.Late < 0.6 * fwd.Early && aft.Late < 0.6 * aft.Early));
    }
    private static void HotStaging() {
        Head("Горячее разделение без автомата");
        var sim = new SimState();
        Physics.Sim.Reset(sim, 1);
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        b.Vx = 1500; b.Vy = 800; b.Om = 0.01; b.Prop = 0.12 * b.PropMax;
        s.Vx = b.Vx; s.Vy = b.Vy; s.Om = b.Om;
        double m = b.Mass, px = m * b.Vx, py = m * b.Vy;
        Guide.Separate(sim);
        double mb = b.Dry + b.Prop, ms = s.Dry + s.Prop;
        double qx = mb * b.Vx + ms * s.Vx, qy = mb * b.Vy + ms * s.Vy;
        Group("разделение — внутреннее событие: импульс и вращение связки сохраняются",
              $"импульс до {N(Math.Sqrt(px * px + py * py) / 1e6)}, после {N(Math.Sqrt(qx * qx + qy * qy) / 1e6)} тыс. т·м/с; вращение {N(b.Om)} и {N(s.Om)} рад/с при 0,01",
              ("масса та же", Math.Abs(mb + ms - m) < 1e-6),
              ("импульс тот же", Math.Abs(qx - px) + Math.Abs(qy - py) < 1e-6 * Math.Abs(px)),
              ("обе ступени вращаются как связка", b.Om == 0.01 && s.Om == 0.01));
    }
    private static int OverArms(double prop, double f) {
        var sim = new SimState { T = 0, RhoK = 1 };
        var v = new Vehicle(Kind.Booster, 0) {
            Mode = "landB", SeekPad = true, Site = "tower", Launched = true, Ign = true, IgnBurn = true, BurnLogged = true,
            NEng = 3, NRun = 3, Throttle = Const.LAND_THR_MIN, Prop = prop, F = f,
        };
        v.Y = Const.RE + Guidance.CatchHT(v) + 3;
        v.Vx = -Const.W * v.Y;
        sim.Veh.Add(v);
        Guidance.LandingBurn(sim, v, Const.DT, 3);
        return v.NEng;
    }
    private static void LightHover() {
        Head("Лёгкий ускоритель над руками");
        int light = OverArms(10e3, 3e6), heavy = OverArms(45e3, 3e6);
        Group("тяга трёх на минимальном дросселе больше веса — один гасится", $"при 3 МН и 10 т топлива {light}, при 45 т {heavy}",
              ("лёгкий гасит один", light == 2), ("тяжёлый остаётся на трёх", heavy == 3));
    }
    private static void BadNumbers() {
        Head("Негодные числа");
        Air bad = Atmosphere.At(double.NaN);
        Group("не расходятся по расчёту", "",
              ("атмосфера на NaN даёт числа", !double.IsNaN(bad.Rho) && !double.IsNaN(bad.P) && !double.IsNaN(bad.A)),
              ("сопротивление на NaN конечно", !double.IsNaN(Atmosphere.Cd0(double.NaN)) && !double.IsNaN(Atmosphere.CdEngine(double.NaN))),
              ("ветер на NaN нулевой", Wind.Roll(new Rng()).At(double.NaN) == 0));
    }
}
