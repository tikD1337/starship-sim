using System;
using System.Collections.Generic;
using System.Globalization;
namespace Starship.Game;
public sealed class CliArgs {
    private readonly Dictionary<string, string> _val = new();
    public static CliArgs Parse(string[] argv) {
        var a = new CliArgs();
        for (int i = 0; i < argv.Length; i++) {
            string k = argv[i];
            if (k == null || !k.StartsWith("--")) continue;
            string v = i + 1 < argv.Length && !argv[i + 1].StartsWith("--") ? argv[i + 1] : null;
            a._val[k] = v;
        }
        return a;
    }
    public bool Has(string key) => _val.ContainsKey(key);
    public string Str(string key, string def = null)
        => _val.TryGetValue(key, out string v) && v != null ? v : def;
    public bool Num(string key, out double x) {
        x = 0;
        string s = Str(key);
        return s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out x);
    }
    public double Dbl(string key, double def) => Num(key, out double x) ? x : def;
    public float Flt(string key, float def) => Num(key, out double x) ? (float)x : def;
    public int Int(string key, int def) => Num(key, out double x) ? (int)Math.Round(x) : def;
    public List<string> Unknown(IEnumerable<string> known) {
        var set = new HashSet<string>(known);
        var bad = new List<string>();
        foreach (string k in _val.Keys) if (!set.Contains(k)) bad.Add(k);
        return bad;
    }
}
