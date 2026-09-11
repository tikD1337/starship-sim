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
}
