using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Starship.Game.Ui;
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
        ReplayText.Data d = ReplayText.Parse(f.GetAsText().Split('\n'), Game.Mission.Keys);
        var r = new Replay { Mission = d.Mission, Seed = d.Seed, Anom = d.Anom, Script = d.Script };
        r.Ev.AddRange(d.Ev);
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
