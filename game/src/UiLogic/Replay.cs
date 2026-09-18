using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Starship.Game.Ui;
using Starship.Physics;
namespace Starship.Game;
public sealed class Replay {
    public string Mission = "orbital";
    public uint Seed = 12345u;
    public bool Anom, Disp;
    public string Script;
    public readonly List<(double T, string K, double A)> Ev = new();
    private readonly Dictionary<string, double> _last = new();
    private int _at;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public void Head(string mission, uint seed, bool anom, string script, bool disp) {
        Mission = mission; Seed = seed; Anom = anom; Script = script; Disp = disp;
        Ev.Clear();
        _last.Clear();
        _at = 0;
    }
    private static bool Level(string k) =>
        k is "pitch" or "thr" or "bank" or "man" or "focus" or "bay" || k.StartsWith("p:", StringComparison.Ordinal);
    public void Put(double t, string k, double a) {
        if (Level(k)) {
            if (_last.TryGetValue(k, out double was) && Math.Abs(was - a) < 1e-9) return;
            _last[k] = a;
        }
        Ev.Add((t, k, a));
    }
    public string FileName(DateTime now) =>
        string.Format(Inv, "{0}-{1:yyyyMMdd-HHmmss}-{2}.txt", Mission, now, Ev.Count);
    public static string ParamKey(string key, int veh, int eng) => string.Format(Inv, "p:{0}:{1}:{2}", key, veh, eng);
    public Replay Copy() {
        var r = new Replay();
        r.From(this);
        return r;
    }
    public void From(Replay src) {
        Mission = src.Mission; Seed = src.Seed; Anom = src.Anom; Disp = src.Disp; Script = src.Script;
        Ev.Clear();
        Ev.AddRange(src.Ev);
        _last.Clear();
        foreach (var kv in src._last) _last[kv.Key] = kv.Value;
        _at = 0;
    }
    public void Keep(double t) {
        Ev.RemoveAll(e => e.T > t);
        _last.Clear();
        foreach ((double _, string k, double a) in Ev) if (Level(k)) _last[k] = a;
        _at = Math.Min(_at, Ev.Count);
    }
    public string Text() {
        var sb = new StringBuilder();
        sb.Append("mission ").Append(Mission).Append('\n');
        sb.Append("seed ").Append(Seed.ToString(Inv)).Append('\n');
        sb.Append("anom ").Append(Anom ? "1" : "0").Append('\n');
        sb.Append("disp ").Append(Disp ? "1" : "0").Append('\n');
        sb.Append("script ").Append(Script ?? "-").Append('\n');
        foreach ((double t, string k, double a) in Ev)
            sb.Append(string.Format(Inv, "{0:F2} {1} {2:R}", t, k, a)).Append('\n');
        return sb.ToString();
    }
    public static Replay FromText(IEnumerable<string> lines, string[] missions) {
        ReplayText.Data d = ReplayText.Parse(lines, missions);
        var r = new Replay { Mission = d.Mission, Seed = d.Seed, Anom = d.Anom, Script = d.Script, Disp = d.Disp };
        r.Ev.AddRange(d.Ev);
        return r;
    }
    public static string Newest(IEnumerable<(string Name, ulong Time)> files) {
        string best = null;
        ulong bestT = 0;
        foreach ((string name, ulong time) in files) {
            if (!name.EndsWith(".txt", StringComparison.Ordinal)) continue;
            if (best == null || time > bestT || time == bestT && string.CompareOrdinal(name, best) > 0) {
                best = name;
                bestT = time;
            }
        }
        return best;
    }
    private static void SetParam(SimState sim, string k, double a) {
        string[] p = k.Split(':');
        if (p.Length != 4 || !int.TryParse(p[2], NumberStyles.Integer, Inv, out int vi)
            || !int.TryParse(p[3], NumberStyles.Integer, Inv, out int ei) || vi < 0 || vi >= sim.Veh.Count) return;
        Vehicle v = sim.Veh[vi];
        ParamDefs.Apply(ParamDefs.Row(p[1]), v, ei >= 0 && ei < v.Eng.Count ? v.Eng[ei] : null, a);
    }
    public void Rewind() => _at = 0;
    public bool Done => _at >= Ev.Count;
    public void Apply(SimState sim) {
        while (_at < Ev.Count && Ev[_at].T <= sim.T) {
            (double _, string k, double a) = Ev[_at++];
            Vehicle s = sim.Veh[1];
            switch (k) {
                case "pitch": sim.ManPitchAxis = a; break;
                case "thr": sim.ManThrAxis = a; break;
                case "bank": sim.ManBankAxis = a; break;
                case "man": Physics.Sim.SetManual(sim, a > 0); break;
                case "rcs": sim.FocusVeh().Rcs = a > 0; break;
                case "sep": Physics.Sim.ManualStage(sim); break;
                case "bay": Physics.Sim.BaySet(sim, s, a > 0.5); break;
                case "sat": Physics.Sim.DeploySat(sim, s, (int)a); break;
                case "focus": sim.Focus = a > 0 ? "ship" : "stack"; break;
                default: if (k.StartsWith("p:", StringComparison.Ordinal)) SetParam(sim, k, a); break;
            }
        }
    }
}
