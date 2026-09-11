using Starship.Game.Ui;
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
}
