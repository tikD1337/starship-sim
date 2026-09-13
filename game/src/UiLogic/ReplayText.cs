using System;
using System.Collections.Generic;
using System.Globalization;
namespace Starship.Game.Ui;
public static class ReplayText {
    public record struct Data(string Mission, uint Seed, bool Anom, string Script, List<(double T, string K, double A)> Ev);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static Data Parse(IEnumerable<string> lines, string[] missions) {
        var r = new Data(missions.Length > 0 ? missions[0] : "orbital", 12345u, false, null,
                         new List<(double, string, double)>());
        foreach (string line in lines) {
            string[] p = line.Trim().Split(' ');
            if (p.Length < 2) continue;
            switch (p[0]) {
                case "mission": if (Array.IndexOf(missions, p[1]) >= 0) r.Mission = p[1]; break;
                case "seed": if (uint.TryParse(p[1], NumberStyles.None, Inv, out uint seed)) r.Seed = seed; break;
                case "anom": r.Anom = p[1] == "1"; break;
                case "script": r.Script = p[1] == "-" ? null : p[1]; break;
                default:
                    if (p.Length >= 3 && double.TryParse(p[0], NumberStyles.Float, Inv, out double t)
                        && double.TryParse(p[2], NumberStyles.Float, Inv, out double a)
                        && double.IsFinite(t) && double.IsFinite(a))
                        r.Ev.Add((t, p[1], a));
                    break;
            }
        }
        return r;
    }
}
