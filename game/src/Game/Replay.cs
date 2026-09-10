using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Starship.Physics;
namespace Starship.Game;
public sealed class Replay {
    public string Mission = "orbital";
    public uint Seed = 12345u;
    public bool Anom;
    public string Script;
    public readonly List<(double T, string K, double A)> Ev = new();
    private int _at;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public const string Dir = "user://replays";
    public void Head(string mission, uint seed, bool anom, string script) {
        Mission = mission; Seed = seed; Anom = anom; Script = script;
        Ev.Clear();
        _at = 0;
    }
    public void Put(double t, string k, double a) {
        if (Ev.Count > 0 && Ev[^1].K == k && Math.Abs(Ev[^1].A - a) < 1e-9) return;
        Ev.Add((t, k, a));
    }
    public string Save() {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        string path = string.Format(Inv, "{0}/{1}-{2:0000}.txt", Dir, Mission, Ev.Count);
        using FileAccess f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (f == null) return null;
        f.StoreLine("mission " + Mission);
        f.StoreLine("seed " + Seed.ToString(Inv));
        f.StoreLine("anom " + (Anom ? "1" : "0"));
        f.StoreLine("script " + (Script ?? "-"));
        foreach ((double t, string k, double a) in Ev)
            f.StoreLine(string.Format(Inv, "{0:F2} {1} {2:R}", t, k, a));
        return path;
    }
    public static Replay Load(string path) {
        using FileAccess f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return null;
        var r = new Replay();
        while (!f.EofReached()) {
            string[] p = f.GetLine().Split(' ');
            if (p.Length < 2) continue;
            switch (p[0]) {
                case "mission": r.Mission = p[1]; break;
                case "seed": r.Seed = uint.Parse(p[1], Inv); break;
                case "anom": r.Anom = p[1] == "1"; break;
                case "script": r.Script = p[1] == "-" ? null : p[1]; break;
                default:
                    if (p.Length >= 3 && double.TryParse(p[0], NumberStyles.Float, Inv, out double t)
                        && double.TryParse(p[2], NumberStyles.Float, Inv, out double a))
                        r.Ev.Add((t, p[1], a));
                    break;
            }
        }
        return r;
    }
    public static string Newest() {
        using DirAccess d = DirAccess.Open(Dir);
        if (d == null) return null;
        string best = null;
        foreach (string name in d.GetFiles())
            if (name.EndsWith(".txt") && (best == null || string.CompareOrdinal(name, best) > 0))
                best = name;
        return best == null ? null : Dir + "/" + best;
    }
    public void Rewind() => _at = 0;
    public bool Done => _at >= Ev.Count;
    public void Apply(SimState sim, Controls ctl) {
        while (_at < Ev.Count && Ev[_at].T <= sim.T) {
            (double _, string k, double a) = Ev[_at++];
            Vehicle s = sim.Veh[1];
            switch (k) {
                case "pitch": sim.ManPitchAxis = a; break;
                case "thr": sim.ManThrAxis = a; break;
                case "bank": sim.ManBankAxis = a; break;
                case "man": sim.Mode = a > 0 ? "man" : "auto"; break;
                case "rcs": sim.FocusVeh().Rcs = a > 0; break;
                case "sep": Physics.Sim.ManualStage(sim); break;
                case "bay": Physics.Sim.BaySet(sim, s, a > 0.5); break;
                case "sat": Physics.Sim.DeploySat(sim, s, (int)a); break;
                case "focus": sim.Focus = a > 0 ? "ship" : "stack"; break;
            }
        }
    }
}
