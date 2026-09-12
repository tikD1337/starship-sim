using System;
using Starship.Physics;
namespace Starship.Game.Ui;
public enum EngLook { Off, On, Warn, Crit, Failed }
public static class EngineState {
    private static readonly string[] Watch = { "_tb", "_vb", "_wf", "_wo" };
    public static EngLook Of(Engine e) {
        if (e.Failed) return EngLook.Failed;
        if (!e.On || e.Pc <= 1) return EngLook.Off;
        EngLook worst = EngLook.On;
        foreach (string k in Watch) {
            ParamRow r = ParamDefs.Row(k);
            string z = ParamDefs.ZoneOf(r, r.FromEngine(e));
            if (z == "crit") return EngLook.Crit;
            if (z == "warn") worst = EngLook.Warn;
        }
        return worst;
    }
    public static int Worst(Vehicle v) {
        int best = 0;
        double top = -1;
        for (int i = 0; i < v.Eng.Count; i++) {
            Engine e = v.Eng[i];
            double f = e.Failed ? 10 : 0;
            foreach (string k in Watch) {
                ParamRow r = ParamDefs.Row(k);
                f = Math.Max(f, r.FromEngine(e) / r.Crit);
            }
            if (f > top) { top = f; best = i; }
        }
        return best;
    }
}
