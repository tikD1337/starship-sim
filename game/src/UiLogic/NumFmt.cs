using System;
using System.Globalization;
using System.Text;
namespace Starship.Game.Ui;
public static class NumFmt {
    public const char Thin = ' ';
    public const char Minus = '−';
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static string F(double x, int digits) {
        if (double.IsNaN(x) || double.IsInfinity(x)) return "—";
        string s = Math.Abs(x).ToString("F" + digits, Inv);
        bool neg = x < 0 && s.Trim('0', '.').Length > 0;
        int dot = s.IndexOf('.');
        string whole = dot < 0 ? s : s[..dot];
        string frac = dot < 0 ? "" : "," + s[(dot + 1)..];
        if (whole.Length >= 4) {
            var sb = new StringBuilder();
            for (int i = 0; i < whole.Length; i++) {
                if (i > 0 && (whole.Length - i) % 3 == 0) sb.Append(Thin);
                sb.Append(whole[i]);
            }
            whole = sb.ToString();
        }
        return (neg ? Minus.ToString() : "") + whole + frac;
    }
    public static string Clock(double t) {
        int tenths = (int)Math.Round(Math.Abs(t) * 10);
        int s10 = tenths % 600;
        char sign = t < 0 && tenths > 0 ? Minus : '+';
        return $"T{sign}{tenths / 600:00}:{s10 / 10:00},{s10 % 10}";
    }
    public static string Dist(double m, out string unit) {
        double a = Math.Abs(m);
        if (a < 1000) { unit = "м"; return F(m, 0); }
        unit = "км";
        return F(m / 1000, a < 100e3 ? 1 : 0);
    }
    public static bool TryParse(string s, out double x) {
        x = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        string t = s.Replace(Thin, ' ').Replace('\u00A0', ' ').Replace(" ", "")
                    .Replace(Minus, '-').Replace(',', '.');
        return double.TryParse(t, NumberStyles.Float, Inv, out x);
    }
}
