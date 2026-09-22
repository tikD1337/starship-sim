using System;
using System.Linq;
using Starship.Game;
using Starship.Game.Ui;
using Starship.Physics;
namespace Starship.Tests;
internal static partial class Program {
    private static void Cases(string name, params (string What, string Got, string Want)[] rows) {
        var bad = rows.Where(r => r.Got != r.Want).Select(r => $"{r.What}: «{r.Got}» вместо «{r.Want}»").ToArray();
        True(name, bad.Length == 0, bad.Length == 0 ? $"{rows.Length} случаев" : string.Join("; ", bad));
    }
    private static void UiNumbers() {
        Head("Интерфейс: числа");
        string t = NumFmt.Thin.ToString(), m = NumFmt.Minus.ToString();
        Cases("запись чисел, часов и расстояний",
              ("тысячи узким пробелом", NumFmt.F(32394, 0), "32" + t + "394"), ("четыре знака", NumFmt.F(3564, 0), "3" + t + "564"),
              ("три знака не делятся", NumFmt.F(981, 0), "981"), ("десятичная запятая", NumFmt.F(30.04, 1), "30,0"),
              ("дробь с тысячами", NumFmt.F(1253.6, 1), "1" + t + "253,6"), ("миллионы", NumFmt.F(1234567, 0), "1" + t + "234" + t + "567"),
              ("настоящий минус", NumFmt.F(-0.1, 1), m + "0,1"), ("округлённый ноль без минуса", NumFmt.F(-0.04, 1), "0,0"),
              ("не число — прочерк", NumFmt.F(double.NaN, 1), "—"), ("часы полёта", NumFmt.Clock(101.1), "T+01:41,1"),
              ("отсчёт до старта", NumFmt.Clock(-10), "T" + m + "00:10,0"), ("секунды не доходят до 60", NumFmt.Clock(59.97), "T+01:00,0"),
              ("часы борта", NumFmt.Hms(270), "T+00:04:30"), ("часы борта до старта", NumFmt.Hms(-10), "T" + m + "00:00:10"),
              ("метры", NumFmt.Dist(850, out string u1) + " " + u1, "850 м"), ("километры с десятыми", NumFmt.Dist(30912, out string u2) + " " + u2, "30,9 км"),
              ("далеко — целые км", NumFmt.Dist(212400, out string u3) + " " + u3, "212 км"));
        Group("разбор введённого числа", "",
              ("узкий пробел и запятая", NumFmt.TryParse("32" + NumFmt.Thin + "394,5", out double a) && a == 32394.5),
              ("обычный пробел", NumFmt.TryParse("1 000", out double b) && b == 1000),
              ("минус из пульта", NumFmt.TryParse(NumFmt.Minus + "0,25", out double c) && c == -0.25),
              ("точка тоже годится", NumFmt.TryParse("2.5", out double d) && d == 2.5),
              ("мусор и пустое не проходят", !NumFmt.TryParse("abc", out _) && !NumFmt.TryParse("", out _) && !NumFmt.TryParse(null, out _)),
              ("NaN и бесконечность не число (пункт 51)", new[] { "NaN", "Infinity", "-Infinity", "∞", "1e999" }.All(x => !NumFmt.TryParse(x, out _))));
    }
    private static void UiThemes() {
        Head("Интерфейс: темы пульта");
        uint[][] spec = {
            new uint[] { 0x0D0F12, 0x15181C, 0x1C2026, 0x262B32, 0xF3F5F7, 0x8A929C, 0x7CC4FF, 0x232C37 },
            new uint[] { 0x000000, 0x0C0C0D, 0x151517, 0x232326, 0xFFFFFF, 0x7E7E85, 0xFFFFFF, 0xFFFFFF },
            new uint[] { 0x0B1417, 0x112027, 0x16292F, 0x21363E, 0xEEF6F7, 0x7C979E, 0x4FD1C5, 0x163A40 },
        };
        string[] what = { "фон", "панель", "поле", "линия", "значения", "подписи", "выделение", "диск" };
        var bad = Themes.All.Take(3).SelectMany((p, i) => new[] { p.Bg, p.Panel, p.Field, p.Line, p.Ink, p.Lab, p.Accent, p.Disc }
                     .Select((c, j) => (c, j)).Where(x => x.c != (spec[i][x.j] << 8 | 0xFF)).Select(x => $"{p.Key}: {what[x.j]} {x.c:X8}")).ToArray();
        Group("три темы как в спецификации", string.Join("; ", bad),
              ("три темы", Themes.All.Length == 3),
              ("восемь цветов каждой", bad.Length == 0),
              ("цифры на диске: светлые, чёрные, светлые", Themes.All[0].DiscInk == 0xF3F5F7FF && Themes.All[1].DiscInk == 0x000000FF && Themes.All[2].DiscInk == 0xEEF6F7FF),
              ("жёлтый и красный общие", Themes.Warn == 0xF2B544FF && Themes.Crit == 0xF0625AFF));
        Group("ключ --theme", "",
              ("b — вторая", Themes.Parse("b", 0) == 1), ("регистр и пробелы не важны", Themes.Parse(" C ", 0) == 2),
              ("чужое значение — как было", Themes.Parse("x", 1) == 1 && Themes.Parse("", 2) == 2 && Themes.Parse(null, 0) == 0 && Themes.Parse("ab", 1) == 1));
    }
    private static void UiEngineLayout() {
        Head("Интерфейс: схема двигателей");
        Spot[] b = EngineLayout.Booster(), s = EngineLayout.Ship();
        (double Gap, double Rx, double Ry, bool Hits) Scan(Spot[] set) {
            double gap = double.MaxValue, rx = 0, ry = 0;
            bool hits = true;
            for (int i = 0; i < set.Length; i++) {
                rx = Math.Max(rx, Math.Abs(set[i].X) + set[i].R);
                ry = Math.Max(ry, Math.Abs(set[i].Y) + set[i].R);
                hits &= EngineLayout.Hit(set, set[i].X, set[i].Y) == i;
                for (int j = i + 1; j < set.Length; j++)
                    gap = Math.Min(gap, Math.Sqrt(Math.Pow(set[i].X - set[j].X, 2) + Math.Pow(set[i].Y - set[j].Y, 2)) - set[i].R - set[j].R);
            }
            return (gap, rx, ry, hits);
        }
        var sb = Scan(b);
        var ss = Scan(s);
        double R(Spot p) => Math.Sqrt(p.X * p.X + p.Y * p.Y);
        Group("кружки двигателей", $"зазор {N(sb.Gap)} и {N(ss.Gap)} px, размах {N(sb.Rx)} × {N(sb.Ry)} px",
              ("33 у ускорителя и 6 у корабля", b.Length == 33 && s.Length == 6),
              ("не касаются друг друга", sb.Gap >= 4 && ss.Gap >= 4),
              ("влезают в 470 × 412", sb.Rx <= 235 && sb.Ry <= 206 && ss.Rx <= 235 && ss.Ry <= 206),
              ("щелчок по кружку выбирает его, мимо — ничего", sb.Hits && ss.Hits && EngineLayout.Hit(b, 0, 0) == -1 && EngineLayout.Hit(b, 300, 300) == -1),
              ("номера как в Vehicle.Eng: первый сверху, 3 + 10 + 20 по кольцам", Math.Abs(b[0].X) < 1e-9 && b[0].Y < 0
               && Math.Abs(R(b[2]) - 40) < 1e-9 && Math.Abs(R(b[3]) - 108) < 1e-9 && Math.Abs(R(b[12]) - 108) < 1e-9 && Math.Abs(R(b[13]) - 180) < 1e-9));
    }
    private static void UiParams() {
        Head("Интерфейс: параметры и зоны");
        ParamRow tb = ParamDefs.Row("_tb");
        var z = ParamDefs.Bands(tb, 314);
        var p = ParamDefs.Bands(ParamDefs.Row("_pf"), 350);
        Table("шкалы зон",
              ("подшипники: жёлтая с 550 из 700 K", z.W0, 550.0 / 700, 1e-9), ("подшипники: красная с 650 K", z.C0, 650.0 / 700, 1e-9),
              ("подшипники: жёлтая до красной", z.W1, z.C0, 1e-9), ("подшипники: красная до конца", z.C1, 1, 1e-9),
              ("подшипники: отметка 314 K", z.Mark, 314.0 / 700, 1e-9),
              ("наддув: красная с нуля (+1)", p.C0 + 1, 1, 1e-9), ("наддув: красная до 200 кПа", p.C1, 0.4, 1e-9), ("наддув: жёлтая с 200", p.W0, 0.4, 1e-9), ("наддув: жёлтая до 280", p.W1, 0.56, 1e-9),
              ("наддув: отметка 350 кПа", p.Mark, 0.7, 1e-9),
              ("отметка за шкалой сверху", ParamDefs.Bands(tb, 9000).Mark, 1, 1e-9), ("и снизу + 1", ParamDefs.Bands(tb, -5).Mark + 1, 1, 1e-9));
        string miss = string.Concat(ParamDefs.Groups.SelectMany(g => g.Sections).SelectMany(s => s.Rows)
                                                   .Where(r => !double.IsNaN(r.Crit) && !(r.Max > 0)).Select(r => r.Key + " "));
        string cols = string.Concat(ParamDefs.Groups.Where(g => { int k = ParamDefs.Split(g); return k <= 0 || k >= g.Sections.Count; }).Select(g => g.Name + " "));
        Group("реестр параметров", "",
              ($"у каждой строки с зонами есть шкала ({miss})", miss.Length == 0),
              ("наддув правится у ступени, обороты — у одного двигателя", ParamDefs.Row("pTankF").Stage && ParamDefs.Row("pTankOx").Stage && !ParamDefs.Row("rpmSet").Stage),
              ("неизвестный ключ — пусто", ParamDefs.Row("нет такого") == null),
              ($"в каждой группе обе колонки не пустые ({cols})", cols.Length == 0));
        var sim = new SimState { Mission = "orbital", AnomOn = false };
        Physics.Sim.Reset(sim, 12345);
        Vehicle v = sim.Veh[0];
        ParamRow rpm = ParamDefs.Row("rpmSet"), tank = ParamDefs.Row("pTankF");
        double rpm0 = v.Eng[0].P.RpmSet;
        bool nan = !ParamDefs.Apply(rpm, v, v.Eng[0], double.NaN) && !ParamDefs.Apply(rpm, v, v.Eng[0], double.PositiveInfinity) && v.Eng[0].P.RpmSet == rpm0;
        bool one = ParamDefs.Apply(rpm, v, v.Eng[0], 1e9) && v.Eng[0].P.RpmSet == rpm.Hi && v.Eng[1].P.RpmSet == rpm0;
        bool all = ParamDefs.Apply(tank, v, v.Eng[0], 500) && v.Eng[0].P.PTankF == 500 && v.Eng[32].P.PTankF == 500;
        Group("правка параметра", "", ("NaN и бесконечность не доходят до двигателя (пункт 51)", nan),
              ("число правит один двигатель и зажимается в пределы", one), ("параметр ступени правит все двигатели", all));
    }
    private static void UiEngine() {
        Head("Интерфейс: показания двигателя");
        var sim = new SimState { Mission = "orbital", AnomOn = false };
        Physics.Sim.Reset(sim, 12345);
        Engine e = sim.Veh[0].Eng[0];
        bool off = EngineState.Of(e) == EngLook.Off;
        while (sim.T < 20) Physics.Sim.Tick(sim, Const.DT);
        PumpRow[] t = PumpTable.Of(e);
        Group("таблица турбонасоса", $"обороты {t[0].Fuel} и {t[0].Ox}",
              ("до зажигания стоит, на T+20 работает", off && EngineState.Of(e) == EngLook.On),
              ("пять строк", t.Length == 5),
              ("обороты каждого насоса свои (пункт 47)", t[0].Fuel == NumFmt.F(e.Pf.Rpm, 0) && t[0].Ox == NumFmt.F(e.Po.Rpm, 0) && t[0].Fuel != t[0].Ox),
              ("расход и номинал напора из насосов", t[2].Fuel == NumFmt.F(e.Pf.Q * Pump.RHO_F, 1) && t[1].Ox.Split(" / ")[^1] == NumFmt.F(Pump.NomO.DP / 1e6, 1)));
        int worst = EngineState.Worst(sim.Veh[0]);
        double cav = e.Pf.Cav, tb = e.Pf.T;
        e.Pf.Cav = 0.5;
        PumpRow low = PumpTable.Of(e)[4];
        e.Pf.Cav = cav;
        e.Pf.T = 600;
        bool warn = EngineState.Of(e) == EngLook.Warn;
        e.Pf.T = 680;
        bool crit = EngineState.Of(e) == EngLook.Crit;
        e.Pf.T = tb;
        sim.Veh[0].Eng[7].Pf.T = 600;
        bool hot7 = EngineState.Worst(sim.Veh[0]) == 7;
        e.Failed = true;
        Group("подсветка отклонений", $"ближе всех к пределу № {worst + 1}",
              ("кавитация горючего подсвечена, окислителя нет", low.FuelWarn && !low.OxWarn),
              ("600 K в подшипниках — жёлтый, 680 K — красный", warn && crit),
              ("отказ виден сразу", EngineState.Of(e) == EngLook.Failed),
              ("перегретый подшипник выводит свой двигатель вперёд", worst >= 0 && worst < 33 && hot7));
    }
    private static (double Lo, double Hi, double Wide) Span(Part[] parts) {
        var pts = parts.SelectMany(p => p.Pts).ToArray();
        return (pts.Min(q => q.Y), pts.Max(q => q.Y), pts.Max(q => Math.Abs(q.X)));
    }
    private static void UiAir() {
        Head("Эфир: палитра, силуэт, фазы");
        Part[] stack = RocketArt.Stack(), ship = RocketArt.Ship(), boost = RocketArt.Booster();
        var st = Span(stack);
        var sh = Span(ship);
        var bo = Span(boost);
        Pt[] flame = RocketArt.Flame(42, 0);
        Group("палитра и силуэт", $"связка {N(st.Hi - st.Lo)} м, корабль {N(sh.Hi - sh.Lo)}, ускоритель {N(bo.Hi - bo.Lo)}",
              ("палитра эфира из спецификации", OnAir.Sky == 0x0A1426FF && OnAir.Band == 0x1C3656FF && OnAir.Ink == 0xF2F5F8FF && OnAir.Rail == 0x8C98A6FF && OnAir.Strip == 0x16191EFF),
              ("связка — корабль и ускоритель встык, нос в нуле", stack.Length == ship.Length + boost.Length && Math.Abs(st.Hi - st.Lo - 125.7) < 1e-7 && Math.Abs(st.Lo) < 1e-9),
              ("ничего не торчит дальше 9 м от оси", st.Wide <= 9),
              ("корабль с кольцом — 54,6 м, ускоритель с раструбами — 71 м от своего нуля",
               Math.Abs(sh.Hi - sh.Lo - 54.6) < 0.55 && Math.Abs(bo.Lo) < 1e-9 && Math.Abs(bo.Hi - bo.Lo - 71) < 0.36),
              ("факел от среза вниз на заданную длину, короткая тяга — короткий", flame.Length > 4 && Math.Abs(flame.Min(q => q.Y)) < 1e-9
               && Math.Abs(flame.Max(q => q.Y) - 42) < 1e-9 && RocketArt.Flame(10, 0)[^1].Y <= 10 + 1e-9));
        Cases("подписи фаз",
              ("захват корабля", Phases.Air("caught", true), "Корабль пойман башней"), ("захват ускорителя", Phases.Air("caught", false), "Ускоритель пойман башней"),
              ("эфир на выведении", Phases.Air("ascent"), "Работа первой ступени"), ("пульт на выведении", Phases.Console("ascent"), "Выведение"),
              ("незнакомый режим — как есть", Phases.Air("нет такого"), "нет такого"));
    }
    private static void UiDirector() {
        Head("Борт: дуга вех, наборы камер, режиссёр");
        var arc = new Arc("orbital");
        string[] n = arc.Names;
        Group("дуга вех орбитального задания", string.Join(", ", n),
              ("от старта до захвата корабля", n[0] == "старт" && n[^1] == "захват корабля"),
              ("после захвата ускорителя — орбита, сход и вход", Array.IndexOf(n, "захват ускорителя") < Array.IndexOf(n, "орбита")
               && Array.IndexOf(n, "орбита") < Array.IndexOf(n, "сход с орбиты") && Array.IndexOf(n, "сход с орбиты") < Array.IndexOf(n, "вход")),
              ("видно шесть вех с начала, до старта метка в нуле", arc.Shown == 6 && arc.First == 0 && arc.Frac(-10) == 0),
              ("у трансатмосферного последняя — приводнение", new Arc("trans").Names[^1] == "приводнение"));
        Group("наборы камер по событиям", "",
              ("на старте — стол, при отрыве — башня сверху", Array.IndexOf(CamPlan.For("idle"), 7) >= 0 && CamPlan.For("ascent")[0] == 8),
              ("на выведении есть «Факел», на разделении — петля заднего закрылка", Array.IndexOf(CamPlan.For("ascent"), 6) >= 0 && Array.IndexOf(CamPlan.For("meco"), 2) >= 0),
              ("на входе корабля первая — петля переднего закрылка", CamPlan.For("entryS")[0] == 1),
              ("на захвате и посадке корабля — ловильные руки", CamPlan.For("caught")[0] == 10 && Array.IndexOf(CamPlan.For("landS"), 10) >= 0),
              ("на жиге ускорителя ведём его, пока корабль разгоняется — корабль", !CamPlan.Ship("landB", "orbit") && CamPlan.Ship("coastB", "ascent2")),
              ("после захвата ускорителя 12 с показываем захват, потом корабль", !CamPlan.Ship("caught", "coastS", 3) && CamPlan.Ship("caught", "coastS", 13)
               && CamPlan.Ship("caught", "landS", 300) && CamPlan.Ship("crashed", "orbit", 20)));
        var dull = new Director.Shot(3, 0.1, 0, 0, 90, 0.9, false, 0, 0);
        var nice = new Director.Shot(4, 0.8, 0.9, 0, 90, 0.3, false, 0, 20);
        Director.Shot[] set = { dull, nice };
        Group("оценка кадра", $"{N(Director.Score(nice))} против {N(Director.Score(dull))}",
              ("красивый выше пустого", Director.Score(nice) > Director.Score(dull)),
              ("солнце в центре портит", Director.Score(nice with { Sun = 5 }) < Director.Score(nice)),
              ("плазма поднимает", Director.Score(dull with { Plasma = 0.9 }) > Director.Score(dull)),
              ("ночь без огня — плохо", Director.Score(nice with { Night = true, Flame = 0 }) < Director.Score(dull)));
        Group("правила монтажа", "",
              ("план не выдержан — не режем", Director.Pick(set, 3, 2, false) == 3),
              ("на событии режем сразу", Director.Pick(set, 3, 2, true) == 4),
              ("через 15 с меняем и без события", Director.Pick(set, 3, 16, false) == 4),
              ("одну камеру дважды подряд не берём", Director.Pick(set, 4, 16, true) == 3),
              ("нет выбора — остаёмся", Director.Pick(new[] { nice }, 4, 16, true) == 4),
              ("без заметного перевеса остаёмся", Director.Pick(new[] { dull, dull with { Cam = 4, Earth = 0.13 } }, 3, 8, false) == 3),
              ("при равных — первая камера набора", Director.Pick(new[] { dull with { Cam = 5 }, dull with { Cam = 6 } }, int.MinValue, 0, true) == 5));
    }
    private static void UiFallbackMarks() {
        Head("Запасная посадка: вехи и зачёт цели");
        var sim = new SimState { Mission = "orbital", AnomOn = false };
        Physics.Sim.Reset(sim, 12345);
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        string tower = Arc.NamesOf("orbital", sim)[5];
        b.Site = "sea"; b.AimDr = Const.SEA_DR;
        string sea = Arc.NamesOf("orbital", sim)[5];
        s.Site = "pad"; s.AimDr = Const.PAD_DR;
        string pad = Arc.NamesOf("orbital", sim)[^1];
        b.Crashed = true;
        Cases("вехи по исходу",
              ("к башне", tower, "захват ускорителя"), ("в море", sea, "приводнение ускорителя"), ("на площадку", pad, "посадка корабля на площадку"),
              ("разбился", Arc.NamesOf("orbital", sim)[5], "потеря ускорителя"), ("трансатмосферное", Arc.NamesOf("trans", sim)[^1], "приводнение"));
        var f = new SimState { Mission = "orbital", AnomOn = false };
        Physics.Sim.Reset(f, 12345);
        Vehicle v = f.Veh[0];
        Aim wait = MissionRules.Catch(v);
        v.Caught = true; v.Landed = true;
        Aim done = MissionRules.Catch(v);
        v.Caught = false; v.Site = "sea"; v.Splash = true;
        Aim wet = MissionRules.Catch(v);
        string seaText = MissionRules.Where(v);
        v.Splash = false; v.Site = "pad";
        Aim onPad = MissionRules.Catch(v);
        string padText = MissionRules.Where(v);
        v.Site = "tower"; v.SeekPad = false;
        Aim stray = MissionRules.Catch(v);
        v.Crashed = true;
        Group("зачёт цели «поймать башней»", $"{seaText}; {padText}",
              ("летит — ждёт, пойман — выполнена", wait == Aim.Wait && done == Aim.Done),
              ("море и площадка по плану — наполовину, с подписью", wet == Aim.Part && onPad == Aim.Part
               && seaText == "запасная посадка в море" && padText == "запасная посадка на площадке у башни"),
              ("где попало или разбился — провалена", stray == Aim.Fail && MissionRules.Catch(v) == Aim.Fail),
              ("очки: половина, полные с бонусом, ноль", MissionRules.Score(Aim.Part, 400, 0) == 200 && MissionRules.Score(Aim.Done, 400, 150) == 550
               && MissionRules.Score(Aim.Fail, 400, 0) == 0 && MissionRules.Score(Aim.Wait, 400, 0) == 0));
    }
    private static void UiTabs() {
        Head("Интерфейс: экраны");
        True("Tab ходит по кругу: сводка → эфир → пульт → сводка", Tabs.Next(1) == 2 && Tabs.Next(2) == 3 && Tabs.Next(3) == 1,
             $"{Tabs.Next(1)}, {Tabs.Next(2)}, {Tabs.Next(3)}");
    }
    private static void UiArmGeom() {
        Head("Руки башни: угол от зазора до обшивки");
        Table("рельс стоит ровно на заданном зазоре от обшивки",
              (from g in new[] { 0.0, 0.7, ArmGeom.Ready, 5, ArmGeom.Park }
               select ($"зазор {N(g)} м", ArmGeom.RailDist(ArmGeom.Angle(g)) - ArmGeom.VehR - ArmGeom.RailR + 1, g + 1, 1e-9)).ToArray());
        double ready = ArmGeom.Angle(ArmGeom.Ready) * 180 / Math.PI, park = ArmGeom.Angle(ArmGeom.Park) * 180 / Math.PI;
        Group("углы", $"касание {N(ArmGeom.Angle(0) * 180 / Math.PI)}°, рабочий {N(ready)}°, стоянка {N(park)}°",
              ("при касании руки параллельны", Math.Abs(ArmGeom.Angle(0)) < 1e-9), ("рабочий зазор — пара градусов", ready > 1 && ready < 4),
              ("на стоянке раскрыты широко", park > 35 && park < 65));
    }
    private static void UiReplay() {
        Head("Запись прогона и рекорды");
        string[] keys = { "orbital", "trans", "high" };
        ReplayText.Data ok = ReplayText.Parse(new[] { "mission high", "seed 777", "anom 1", "script engine-out", "12.50 pitch 1", "13.00 sep 1" }, keys);
        bool threw = false;
        ReplayText.Data bad = default;
        try { bad = ReplayText.Parse(new[] { "seed abc", "seed 99999999999", "seed -1" }, keys); } catch (Exception) { threw = true; }
        ReplayText.Data nan = ReplayText.Parse(new[] { "NaN pitch 1", "1.00 thr Infinity", "2.00 bank 1e999", "3.00 thr -1e999", "4.00 pitch 0.5" }, keys);
        Group("разбор чужого файла записи", $"{ok.Mission}, зерно {ok.Seed}, событий {ok.Ev.Count}",
              ("нормальная запись читается целиком", ok.Mission == "high" && ok.Seed == 777 && ok.Anom && ok.Script == "engine-out" && ok.Ev.Count == 2
                                                    && ok.Ev[1].T == 13 && ok.Ev[1].K == "sep"),
              ("разброс в заголовке читается", !ok.Disp && ReplayText.Parse(new[] { "mission high", "seed 5", "disp 1" }, keys).Disp),
              ("задание не из списка — орбитальное", ReplayText.Parse(new[] { "mission ../../../../Users/Public/x" }, keys).Mission == "orbital"),
              ("битое зерно не роняет разбор", !threw && bad.Seed == 12345u),
              ("события с NaN и бесконечностью отброшены", nan.Ev.Count == 1 && nan.Ev[0].A == 0.5));
        var r = new Starship.Game.Replay();
        r.Head("orbital", 5, false, null, true);
        for (int i = 0; i < 1000; i++) { double t = i * 0.016; r.Put(t, "pitch", 0); r.Put(t, "thr", 0); r.Put(t, "bank", 0); }
        int still = r.Ev.Count;
        r.Put(20, "pitch", 1); r.Put(20, "thr", 0); r.Put(21, "pitch", 0);
        int moved = r.Ev.Count;
        r.Put(30, "sat", 1); r.Put(31, "sat", 1); r.Put(32, "rcs", 0); r.Put(33, "rcs", 0);
        var t1 = new DateTime(2026, 9, 18, 10, 0, 0);
        string n1 = r.FileName(t1), n2 = r.FileName(t1.AddSeconds(7));
        string newest = Starship.Game.Replay.Newest(new (string, ulong)[] { ("orbital-195412.txt", 100), ("orbital-0003.txt", 300), ("high-0009.txt", 200), ("notes.md", 900) });
        var c = r.Copy();
        c.Keep(21);
        int kept = c.Ev.Count;
        c.Put(22, "pitch", 0);
        Group("запись без потерь и раздувания (пункты 54–57)", $"событий {still} → {moved} → {r.Ev.Count}, в копии {kept}; {n1}",
              ("неподвижные оси не пишутся каждый кадр", still == 3),
              ("изменение оси и повторные действия записаны", moved == 5 && r.Ev.Count == 9),
              ("в имени время: два полёта не затирают друг друга", n1 != n2 && n1.StartsWith("orbital-") && n1.EndsWith(".txt")),
              ("F10 берёт самую свежую по времени", newest == "orbital-0003.txt"),
              ("копия режется для перемотки, оригинал цел, повтор не дублируется", kept == 5 && r.Ev.Count == 9 && c.Seed == 5 && c.Disp && c.Ev.Count == 5));
        var live = new SimState { Mission = "orbital", AnomOn = false };
        var play = new SimState { Mission = "orbital", AnomOn = false };
        Physics.Sim.Reset(live, 12345); Physics.Sim.Reset(play, 12345);
        var rec = new Starship.Game.Replay();
        rec.Head("orbital", 12345, false, null, false);
        while (live.T < 50) Physics.Sim.Tick(live, Const.DT);
        double thrAuto = live.Veh[0].Throttle;
        live.ManPitchAxis = 1; rec.Put(live.T, "pitch", 1);
        rec.Put(live.T, "man", 1); Physics.Sim.SetManual(live, true);
        rec.Put(live.T, Starship.Game.Replay.ParamKey("rpmSet", 0, 5), 30000);
        ParamDefs.Apply(ParamDefs.Row("rpmSet"), live.Veh[0], live.Veh[0].Eng[5], 30000);
        while (live.T < 60) Physics.Sim.Tick(live, Const.DT);
        rec.Rewind();
        while (play.T < 60) { rec.Apply(play); Physics.Sim.Tick(play, Const.DT); }
        Group("повтор совпадает с живым полётом (пункты 52–53)", $"расхождение {N(Math.Abs(live.Veh[0].X - play.Veh[0].X))} м",
              ("ручной режим берёт тягу автомата", live.ManThr == thrAuto && thrAuto < 1 && play.ManThr == live.ManThr),
              ("правка параметра пульта повторена", play.Veh[0].Eng[5].P.RpmSet == 30000),
              ("положение до бита", live.Veh[0].X == play.Veh[0].X && live.Veh[0].Y == play.Veh[0].Y));
        var recs = RecordsText.Parse("{\"orbital\": {\"s\": 1234, \"g\": \"A\", \"t\": \"16.09.2026\"}, \"trans\": 800}");
        string[] broken = {
            "{\"orbital\": {\"g\": \"A\"}, \"high\": {\"s\": 700}}", "{\"orbital\": {\"s\": \"много\"}, \"high\": {\"s\": 700, \"g\": 5}}",
            "{\"orbital\": {\"s\": 1234, \"g\": \"A\"", "[1, 2, 3]", "", "не json", "{\"high\": {\"s\": 1e30}}",
        };
        string err = "";
        int saved = 0;
        foreach (string text in broken) {
            try { if (RecordsText.Parse(text).TryGetValue("high", out var h) && h.Score == 700) saved++; }
            catch (Exception e) { err += e.GetType().Name + " "; }
        }
        Group("рекорды: повреждённый файл не роняет таблицу (пункт 59)", $"уцелело {saved} из 2 {err}",
              ("целый файл и старый формат читаются", recs.Count == 2 && recs["orbital"].Score == 1234 && recs["orbital"].Grade == "A" && recs["trans"].Score == 800),
              ("битые файлы не бросают исключений", err.Length == 0),
              ("целые записи из частично битого сохраняются", saved == 2));
    }
}
