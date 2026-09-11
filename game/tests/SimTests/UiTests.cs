using System;
using Starship.Game.Ui;
using Starship.Physics;
namespace Starship.Tests;
internal static partial class Program {
    private static void Same(string name, string got, string want) =>
        True(name, got == want, $"«{got}» против «{want}»");
    private static void UiNumbers() {
        Head("Интерфейс: запись чисел");
        string t = NumFmt.Thin.ToString(), m = NumFmt.Minus.ToString();
        Same("тысячи узким пробелом", NumFmt.F(32394, 0), "32" + t + "394");
        Same("четыре знака тоже делятся", NumFmt.F(3564, 0), "3" + t + "564");
        Same("три знака не делятся", NumFmt.F(981, 0), "981");
        Same("десятичная запятая", NumFmt.F(30.04, 1), "30,0");
        Same("дробь с тысячами", NumFmt.F(1253.6, 1), "1" + t + "253,6");
        Same("миллионы", NumFmt.F(1234567, 0), "1" + t + "234" + t + "567");
        Same("минус настоящий", NumFmt.F(-0.1, 1), m + "0,1");
        Same("округлённый ноль без минуса", NumFmt.F(-0.04, 1), "0,0");
        Same("не число — прочерк", NumFmt.F(double.NaN, 1), "—");
        Same("часы полёта", NumFmt.Clock(101.1), "T+01:41,1");
        Same("отсчёт до старта", NumFmt.Clock(-10), "T" + m + "00:10,0");
        Same("секунды не доходят до 60", NumFmt.Clock(59.97), "T+01:00,0");
        Same("метры", NumFmt.Dist(850, out string u1) + " " + u1, "850 м");
        Same("километры с десятыми", NumFmt.Dist(30912, out string u2) + " " + u2, "30,9 км");
        Same("далеко — целые км", NumFmt.Dist(212400, out string u3) + " " + u3, "212 км");
    }
    private static void UiThemes() {
        Head("Интерфейс: темы пульта");
        True("три темы", Themes.All.Length == 3);
        uint[][] spec = {
            new uint[] { 0x0D0F12, 0x15181C, 0x1C2026, 0x262B32, 0xF3F5F7, 0x8A929C, 0x7CC4FF, 0x232C37 },
            new uint[] { 0x000000, 0x0C0C0D, 0x151517, 0x232326, 0xFFFFFF, 0x7E7E85, 0xFFFFFF, 0xFFFFFF },
            new uint[] { 0x0B1417, 0x112027, 0x16292F, 0x21363E, 0xEEF6F7, 0x7C979E, 0x4FD1C5, 0x163A40 },
        };
        string[] what = { "фон", "панель", "поле", "линия", "значения", "подписи", "выделение", "диск" };
        for (int i = 0; i < 3 && i < Themes.All.Length; i++) {
            Palette p = Themes.All[i];
            uint[] got = { p.Bg, p.Panel, p.Field, p.Line, p.Ink, p.Lab, p.Accent, p.Disc };
            for (int j = 0; j < got.Length; j++)
                True($"тема {p.Key}: {what[j]} как в спецификации", got[j] == (spec[i][j] << 8 | 0xFF),
                     $"{got[j]:X8}");
        }
        True("цифры на диске: светлые, чёрные, светлые",
             Themes.All[0].DiscInk == 0xF3F5F7FF && Themes.All[1].DiscInk == 0x000000FF
             && Themes.All[2].DiscInk == 0xEEF6F7FF);
        True("жёлтый и красный общие", Themes.Warn == 0xF2B544FF && Themes.Crit == 0xF0625AFF);
        True("--theme b", Themes.Parse("b", 0) == 1);
        True("регистр и пробелы не важны", Themes.Parse(" C ", 0) == 2);
        True("чужое значение — как было", Themes.Parse("x", 1) == 1 && Themes.Parse("", 2) == 2
                                          && Themes.Parse(null, 0) == 0 && Themes.Parse("ab", 1) == 1);
    }
    private static void UiEngineLayout() {
        Head("Интерфейс: схема двигателей");
        Spot[] b = EngineLayout.Booster(), s = EngineLayout.Ship();
        True("у ускорителя 33 кружка", b.Length == 33);
        True("у корабля 6 кружков", s.Length == 6);
        foreach ((string name, Spot[] set) in new[] { ("ускоритель", b), ("корабль", s) }) {
            double gap = double.MaxValue, rx = 0, ry = 0;
            bool hits = true;
            for (int i = 0; i < set.Length; i++) {
                rx = Math.Max(rx, Math.Abs(set[i].X) + set[i].R);
                ry = Math.Max(ry, Math.Abs(set[i].Y) + set[i].R);
                hits &= EngineLayout.Hit(set, set[i].X, set[i].Y) == i;
                for (int j = i + 1; j < set.Length; j++) {
                    double dx = set[i].X - set[j].X, dy = set[i].Y - set[j].Y;
                    gap = Math.Min(gap, Math.Sqrt(dx * dx + dy * dy) - set[i].R - set[j].R);
                }
            }
            True($"{name}: кружки не касаются", gap >= 4, $"зазор {N(gap)} px");
            True($"{name}: схема влезает в 470 × 412", rx <= 235 && ry <= 206, $"{N(rx)} × {N(ry)} px от центра");
            True($"{name}: щелчок по кружку выбирает его", hits);
        }
        True("щелчок мимо — ничего", EngineLayout.Hit(b, 0, 0) == -1 && EngineLayout.Hit(b, 300, 300) == -1);
        True("первый двигатель — сверху по центру", Math.Abs(b[0].X) < 1e-9 && b[0].Y < 0);
        double R(Spot p) => Math.Sqrt(p.X * p.X + p.Y * p.Y);
        True("номера как в Vehicle.Eng: 3 центральных, 10 средних, 20 внешних",
             Math.Abs(R(b[2]) - 40) < 1e-9 && Math.Abs(R(b[3]) - 108) < 1e-9
             && Math.Abs(R(b[12]) - 108) < 1e-9 && Math.Abs(R(b[13]) - 180) < 1e-9);
    }
    private static void UiParams() {
        Head("Интерфейс: параметры и шкалы зон");
        ParamRow tb = ParamDefs.Row("_tb");
        var z = ParamDefs.Bands(tb, 314);
        Near("подшипники: жёлтая с 550 K из 700", z.W0, 550.0 / 700, 1e-9);
        Near("подшипники: красная с 650 K", z.C0, 650.0 / 700, 1e-9);
        True("подшипники: жёлтая упирается в красную, красная до конца", z.W1 == z.C0 && z.C1 == 1);
        Near("подшипники: отметка 314 K", z.Mark, 314.0 / 700, 1e-9);
        var p = ParamDefs.Bands(ParamDefs.Row("_pf"), 350);
        True("наддув горючего: красная от нуля до 200 кПа", p.C0 == 0 && Math.Abs(p.C1 - 0.4) < 1e-9,
             $"{N(p.C0)}…{N(p.C1)}");
        True("наддув горючего: жёлтая 200–280 кПа", Math.Abs(p.W0 - 0.4) < 1e-9 && Math.Abs(p.W1 - 0.56) < 1e-9,
             $"{N(p.W0)}…{N(p.W1)}");
        Near("наддув горючего: отметка 350 кПа", p.Mark, 0.7, 1e-9);
        True("отметка не вылезает за шкалу", ParamDefs.Bands(tb, 9000).Mark == 1 && ParamDefs.Bands(tb, -5).Mark == 0);
        string miss = "";
        foreach (ParamGroup g in ParamDefs.Groups)
            foreach (ParamSection s in g.Sections)
                foreach (ParamRow r in s.Rows)
                    if (!double.IsNaN(r.Crit) && !(r.Max > 0)) miss += r.Key + " ";
        True("у каждой строки с зонами есть шкала", miss.Length == 0, miss);
        True("наддув правится у всей ступени", ParamDefs.Row("pTankF").Stage && ParamDefs.Row("pTankOx").Stage);
        True("обороты — у одного двигателя", !ParamDefs.Row("rpmSet").Stage);
        True("неизвестный ключ — пусто", ParamDefs.Row("нет такого") == null);
        foreach (ParamGroup g in ParamDefs.Groups) {
            int k = ParamDefs.Split(g);
            True($"«{g.Name}»: обе колонки не пустые", k > 0 && k < g.Sections.Count,
                 $"вторая с раздела {k} из {g.Sections.Count}");
        }
    }
    private static void UiEngine() {
        Head("Интерфейс: показания двигателя");
        var sim = new SimState { Mission = "orbital", AnomOn = false };
        Physics.Sim.Reset(sim, 12345);
        Engine e = sim.Veh[0].Eng[0];
        True("до зажигания двигатель стоит", EngineState.Of(e) == EngLook.Off);
        while (sim.T < 20) Physics.Sim.Tick(sim, Const.DT);
        True("на T+20 двигатель работает", EngineState.Of(e) == EngLook.On, $"давление в камере {N(e.Pc)} МПа");
        PumpRow[] t = PumpTable.Of(e);
        True("в таблице пять строк", t.Length == 5);
        Same("обороты горючего — насос горючего", t[0].Fuel, NumFmt.F(e.Pf.Rpm, 0));
        Same("обороты окислителя — насос окислителя", t[0].Ox, NumFmt.F(e.Po.Rpm, 0));
        True("насосы крутятся по-разному (пункт 47)", t[0].Fuel != t[0].Ox, $"{t[0].Fuel} и {t[0].Ox}");
        Same("расход горючего", t[2].Fuel, NumFmt.F(e.Pf.Q * Pump.RHO_F, 1));
        Same("номинал напора окислителя", t[1].Ox.Split(" / ")[^1], NumFmt.F(Pump.NomO.DP / 1e6, 1));
        double cav = e.Pf.Cav;
        e.Pf.Cav = 0.5;
        PumpRow low = PumpTable.Of(e)[4];
        True("кавитация горючего подсвечена, окислителя нет", low.FuelWarn && !low.OxWarn);
        e.Pf.Cav = cav;
        double tb = e.Pf.T;
        e.Pf.T = 600;
        True("600 K в подшипниках — жёлтый", EngineState.Of(e) == EngLook.Warn);
        e.Pf.T = 680;
        True("680 K — красный", EngineState.Of(e) == EngLook.Crit);
        e.Pf.T = tb;
        e.Failed = true;
        True("отказ виден сразу", EngineState.Of(e) == EngLook.Failed);
    }
}
