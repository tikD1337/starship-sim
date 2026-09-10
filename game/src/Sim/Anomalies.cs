using System;
using System.Collections.Generic;
namespace Starship.Physics;
public sealed class Rng {
    private uint _s = 1;
    public void Seed(uint sd) { _s = sd != 0 ? sd : 1u; }
    public double Next() {
        _s = unchecked(_s * 1664525u + 1013904223u);
        return _s / 4294967296.0;
    }
    public double About(double k) => 1 + (Next() * 2 - 1) * k;
}
public sealed class AnomalySpec {
    public string Key;
    public double P;
    public string Name;
    public Action<SimState, Anomalies> Apply;
    public string Stage;
    public Func<SimState, Anomalies, bool> When;
    public Action<SimState, Anomalies> Fire;
}
public sealed class Anomalies {
    public List<string> List = new();
    public HashSet<string> Flags = new(), Done = new();
    public int IgnIdx, OutIdx, PumpIdx;
    public double OutT, PumpK, CopvK, JamK, TileK;
    public bool JamShip;
    public double DryK = 1, PropK = 1, CfK = 1, RhoK = 1;
    public bool Has(string k) => Flags.Contains(k);
    public static readonly AnomalySpec[] Table = {
        new() {
            Key = "ignFail", P = 0.12, Name = "двигатель не вышел на режим",
            Stage = "ign",
            When = (sim, a) => sim.T >= 0,
            Fire = (sim, a) => Eng(sim.Veh[0], a.IgnIdx).Fail("не вышел на режим при запуске"),
        },
        new() {
            Key = "engOut", P = 0.15, Name = "аварийное выключение двигателя в полёте",
            Stage = "out",
            When = (sim, a) => sim.T >= a.OutT && sim.Veh[0].Mode == "ascent",
            Fire = (sim, a) => Eng(sim.Veh[0], a.OutIdx).Fail("нештатное выключение"),
        },
        new() {
            Key = "pumpLow", P = 0.18, Name = "просадка турбонасоса",
            Apply = (sim, a) => {
                Engine e = Eng(sim.Veh[0], a.PumpIdx);
                e.P.FHead *= a.PumpK;
                e.P.OHead *= a.PumpK;
                e.Anom = "просадка ТНА";
            },
        },
        new() {
            Key = "copvLeak", P = 0.10, Name = "утечка газа наддува",
            Apply = (sim, a) => sim.Veh[0].CopvK = a.CopvK,
            Stage = "copv",
            When = (sim, a) => sim.T >= 0,
            Fire = (sim, a) => sim.LogMsg("Б: падение расхода газа наддува — утечка в системе баллонов", 2),
        },
        new() {
            Key = "ctrlJam", P = 0.08, Name = "заедание управляющей плоскости",
            Apply = (sim, a) => Jammed(sim, a).CtrlK = a.JamK,
            Stage = "jam",
            When = (sim, a) => sim.T >= 0,
            Fire = (sim, a) => sim.LogMsg(Jammed(sim, a).Tag +
                ": заедание привода управляющей плоскости — власть снижена", 2),
        },
        new() {
            Key = "tileLoss", P = 0.10, Name = "потеря плиток теплозащиты",
            Apply = (sim, a) => sim.Veh[1].TileK = a.TileK,
            Stage = "tile",
            When = (sim, a) => sim.Veh[1].Heat > 60,
            Fire = (sim, a) => sim.LogMsg("К: потеря части плиток — местный нагрев выше расчётного", 2),
        },
    };
    private static Engine Eng(Vehicle v, int idx) => v.Eng[idx % v.Eng.Count];
    private static Vehicle Jammed(SimState sim, Anomalies a) => a.JamShip ? sim.Veh[1] : sim.Veh[0];
    public static Anomalies Roll(SimState sim, Rng rng) {
        var a = new Anomalies();
        if (!sim.AnomOn) return a;
        foreach (AnomalySpec s in Table) {
            bool hit = rng.Next() < s.P;
            if (sim.AnomScript != null) hit = sim.AnomScript.Contains(s.Key);
            if (hit) { a.Flags.Add(s.Key); a.List.Add(s.Name); }
        }
        a.IgnIdx = (int)Math.Floor(rng.Next() * 33);
        a.OutIdx = (int)Math.Floor(rng.Next() * 33);
        a.OutT = 25 + rng.Next() * 95;
        a.PumpIdx = (int)Math.Floor(rng.Next() * 33);
        a.PumpK = 0.80 + rng.Next() * 0.12;
        a.CopvK = 0.30 + rng.Next() * 0.35;
        a.JamShip = rng.Next() < 0.5;
        a.JamK = 0.35 + rng.Next() * 0.25;
        a.TileK = 1.25 + rng.Next() * 0.35;
        a.DryK = rng.About(0.006);
        a.PropK = rng.About(0.004);
        a.CfK = rng.About(0.005);
        a.RhoK = rng.About(0.03);
        return a;
    }
    public static void Apply(SimState sim) {
        Anomalies A = sim.Anom;
        if (A == null || !sim.AnomOn) return;
        foreach (Vehicle v in sim.Veh) {
            v.Prop *= A.PropK; v.PropMax = v.Prop;
            v.Dry *= A.DryK;
            foreach (Engine e in v.Eng) e.P.Cf *= A.CfK;
        }
        foreach (AnomalySpec s in Table)
            if (s.Apply != null && A.Has(s.Key)) s.Apply(sim, A);
    }
    public static void Tick(SimState sim) {
        Anomalies A = sim.Anom;
        if (A == null || !sim.AnomOn) return;
        foreach (AnomalySpec s in Table) {
            if (s.Fire == null || !A.Has(s.Key) || A.Done.Contains(s.Stage)) continue;
            if (!s.When(sim, A)) continue;
            A.Done.Add(s.Stage);
            s.Fire(sim, A);
        }
    }
}
