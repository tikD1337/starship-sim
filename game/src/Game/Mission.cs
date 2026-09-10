using System;
using System.Collections.Generic;
using Godot;
using Starship.Physics;
namespace Starship.Game;
public enum Aim { Wait, Done, Fail }
public sealed class Goal {
    public string Name;
    public int Base, MaxBonus, Bonus, Who = -1;
    public Aim State = Aim.Wait;
    public string Note = "";
    public Func<Mission, SimState, Goal, Aim> Rule;
    public int Score => State == Aim.Done ? Base + Bonus : 0;
    public int Max => Base + MaxBonus;
}
public sealed class Mission {
    public const string RecPath = "user://records.json";
    public string Key { get; private set; } = "orbital";
    public string Title { get; private set; } = "";
    public readonly List<Goal> Goals = new();
    public bool Over { get; private set; }
    public int Score { get; private set; }
    public int Best { get; private set; }
    public bool NewBest { get; private set; }
    public string Grade { get; private set; } = "";
    public double BoostMiss = double.NaN, ShipMiss = double.NaN;
    public double BoostTouch = double.NaN, ShipTouch = double.NaN;
    private readonly double[] _wasVv = { 0, 0 };
    private static Dictionary<string, Rec> _recs;
    public static readonly string[] Keys = { "orbital", "trans", "high" };
    public static string NameOf(string key) => key switch {
        "trans" => "трансатмосферный полёт, возврат обеих ступеней",
        "high" => "20 т на орбиту 520 × 500 км",
        _ => "67 т на орбиту 220 × 200 км",
    };
    public int Total {
        get { int s = 0; foreach (Goal g in Goals) s += g.Score; return s; }
    }
    public int MaxTotal {
        get { int s = 0; foreach (Goal g in Goals) s += g.Max; return s; }
    }
    public string RecKey { get; private set; } = "orbital";
    public static string RecKeyOf(string key, bool anom, string script)
        => !anom ? key : key + "/" + (script ?? "anom");
    public static Mission Start(string key, bool anom = false, string script = null) {
        var m = new Mission { Key = key, Title = NameOf(key), RecKey = RecKeyOf(key, anom, script) };
        m.Best = BestOf(m.RecKey);
        m.Build();
        return m;
    }
    private void Build() {
        Goals.Clear();
        Add("Пройти участок максимального напора", 100, 0, -1, (m, s, g) => {
            if (s.Veh[0].Crashed || s.Veh[1].Crashed) return Aim.Fail;
            return s.Events.ContainsKey("maxq") ? Aim.Done : Aim.Wait;
        });
        Add("Горячее разделение ступеней", 100, 0, -1, (m, s, g) => {
            if (s.Veh[0].Crashed || s.Veh[1].Crashed) return Aim.Fail;
            return s.Veh[1].Attached ? Aim.Wait : Aim.Done;
        });
        Add("Поймать ускоритель башней", 400, 200, 0, (m, s, g) => Catch(m, s.Veh[0], s, true));
        if (Key == "trans") {
            Add("Провести корабль через вход в атмосферу", 200, 0, -1, (m, s, g) => {
                Vehicle v = s.Veh[1];
                if (v.Crashed) return Aim.Fail;
                if (v.MaxHeat < 40) return Aim.Wait;
                return v.Alt < 20000 && v.Alive ? Aim.Done : Aim.Wait;
            });
            Add("Привести корабль на воду", 300, 0, -1, (m, s, g) => {
                Vehicle v = s.Veh[1];
                if (v.Crashed) return Aim.Fail;
                return v.Landed ? Aim.Done : Aim.Wait;
            });
        }
        else {
            Add("Вывести груз на целевую орбиту", 300, 0, -1, (m, s, g) => {
                Vehicle v = s.Veh[1];
                if (v.Crashed) return Aim.Fail;
                if (v.F > 1e3) return Aim.Wait;
                Orbit o = Guidance.Orb(v);
                bool ok = o.Peri > s.TargetPeri - 30e3 && o.Apo > s.TargetApo - 30e3
                          && o.Apo < s.TargetApo + 120e3;
                if (!ok) return Aim.Wait;
                g.Note = $"{o.Apo / 1000:F0} × {o.Peri / 1000:F0} км";
                return Aim.Done;
            });
            Add("Поймать корабль башней", 500, 200, 1, (m, s, g) => Catch(m, s.Veh[1], s, false));
        }
    }
    private void Add(string name, int bas, int bonus, int who, Func<Mission, SimState, Goal, Aim> rule)
        => Goals.Add(new Goal { Name = name, Base = bas, MaxBonus = bonus, Who = who, Rule = rule });
    private static Aim Catch(Mission m, Vehicle v, SimState s, bool booster) {
        if (v.Crashed) return Aim.Fail;
        if (v.Caught) {
            double miss = Math.Abs(s.Downrange(v));
            double vv = m._wasVv[booster ? 0 : 1];
            if (booster) { m.BoostMiss = miss; m.BoostTouch = vv; }
            else { m.ShipMiss = miss; m.ShipTouch = vv; }
            return Aim.Done;
        }
        return v.Landed ? Aim.Fail : Aim.Wait;
    }
    public void Track(SimState sim) {
        if (Over) return;
        foreach (Goal g in Goals) {
            if (g.State != Aim.Wait) continue;
            Aim st = g.Rule(this, sim, g);
            if (st == Aim.Wait) continue;
            g.State = st;
            if (st != Aim.Done) continue;
            if (g.MaxBonus > 0) {
                double miss = g.Who == 0 ? BoostMiss : ShipMiss;
                double vv = g.Who == 0 ? BoostTouch : ShipTouch;
                g.Bonus = (int)Math.Round(g.MaxBonus * Math.Clamp(1 - miss / 8.0, 0, 1));
                g.Note = $"промах {miss:F1} м, касание {Math.Abs(vv):F1} м/с, бонус {g.Bonus}";
            }
            else if (g.Note.Length == 0) {
                g.Note = $"T+{(int)(sim.T / 60):00}:{sim.T % 60:00.0}";
            }
        }
        Vehicle b = sim.Veh[0], s2 = sim.Veh[1];
        if (!b.Landed && !b.Crashed) _wasVv[0] = b.VVert;
        if (!s2.Landed && !s2.Crashed) _wasVv[1] = s2.VVert;
        bool done = (b.Landed || b.Crashed) && (s2.Landed || s2.Crashed);
        if (!done) return;
        Over = true;
        Score = Total;
        double frac = MaxTotal > 0 ? (double)Score / MaxTotal : 0;
        Grade = b.Crashed && s2.Crashed ? "F"
              : frac >= 0.95 ? "S" : frac >= 0.80 ? "A" : frac >= 0.60 ? "B"
              : frac >= 0.35 ? "C" : "D";
        NewBest = Score > Best;
        if (NewBest) { Best = Score; Remember(RecKey, Score, Grade); }
    }
    public readonly struct Rec {
        public readonly int Score;
        public readonly string Grade, When;
        public Rec(int score, string grade, string when) {
            Score = score; Grade = grade; When = when;
        }
    }
    private static void Load() {
        if (_recs != null) return;
        _recs = new Dictionary<string, Rec>();
        if (!FileAccess.FileExists(RecPath)) return;
        using FileAccess f = FileAccess.Open(RecPath, FileAccess.ModeFlags.Read);
        if (f == null) return;
        var json = Json.ParseString(f.GetAsText());
        if (json.VariantType != Variant.Type.Dictionary) return;
        foreach (var kv in (Godot.Collections.Dictionary)json) {
            if (kv.Value.VariantType == Variant.Type.Dictionary) {
                var d = (Godot.Collections.Dictionary)kv.Value;
                _recs[kv.Key.AsString()] = new Rec((int)d["s"].AsDouble(),
                                                   d["g"].AsString(), d["t"].AsString());
            }
            else _recs[kv.Key.AsString()] = new Rec((int)kv.Value.AsDouble(), "", "");
        }
    }
    public static int BestOf(string key) {
        Load();
        return _recs.TryGetValue(key, out Rec v) ? v.Score : 0;
    }
    public static Rec RecordOf(string key) {
        Load();
        return _recs.TryGetValue(key, out Rec v) ? v : default;
    }
    private static void Remember(string key, int score, string grade) {
        Load();
        var now = Time.GetDatetimeDictFromSystem();
        _recs[key] = new Rec(score, grade,
            $"{(int)now["day"]:00}.{(int)now["month"]:00}.{(int)now["year"]}");
        var all = new Godot.Collections.Dictionary();
        foreach (var kv in _recs)
            all[kv.Key] = new Godot.Collections.Dictionary {
                { "s", kv.Value.Score }, { "g", kv.Value.Grade }, { "t", kv.Value.When },
            };
        using FileAccess f = FileAccess.Open(RecPath, FileAccess.ModeFlags.Write);
        f?.StoreString(Json.Stringify(all));
    }
}
