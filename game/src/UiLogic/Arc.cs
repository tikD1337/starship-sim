using System;
using Starship.Physics;
namespace Starship.Game.Ui;
public sealed class Arc {
    private static readonly string[] Orbital = {
        "старт", "max Q", "разделение", "тормозной импульс", "SECO", "захват",
    };
    private static readonly string[] Trans = {
        "старт", "max Q", "разделение", "тормозной импульс", "вход", "приводнение",
    };
    private static readonly double[] NomOrbital = { 0, 72, 136, 150, 560, 440 };
    private static readonly double[] NomTrans = { 0, 72, 136, 150, 1500, 1800 };
    private readonly double[] _nom;
    private readonly bool _trans;
    public string[] Names { get; }
    public bool[] Passed { get; }
    public double[] At { get; }
    public Arc(string mission) {
        _trans = mission == "trans";
        Names = _trans ? Trans : Orbital;
        _nom = _trans ? NomTrans : NomOrbital;
        Passed = new bool[Names.Length];
        At = new double[Names.Length];
        for (int i = 0; i < At.Length; i++) At[i] = double.NaN;
    }
    public void Track(SimState sim) {
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        bool entry = s.Mode == "entryS" || s.Mode == "flipS" || s.Mode == "landS" || s.Landed || s.Crashed;
        bool[] now = {
            b.Launched || sim.T >= 0,
            sim.Events.ContainsKey("maxq"),
            !s.Attached,
            b.Mode == "boostback" || b.Mode == "coastB" || b.Mode == "landB" || b.Caught || b.Landed || b.Crashed,
            _trans ? entry
                   : s.Mode == "coastS" || s.Mode == "circ" || s.Mode == "orbit" || s.Mode == "deorbit" || entry,
            _trans ? s.Landed || s.Crashed : b.Caught || b.Landed || b.Crashed,
        };
        for (int i = 0; i < Passed.Length; i++)
            if (now[i] && !Passed[i]) {
                Passed[i] = true;
                At[i] = sim.T;
            }
    }
    public double Frac(double t) {
        int last = -1;
        for (int i = 0; i < Passed.Length; i++)
            if (Passed[i]) last = i;
        if (last < 0) return 0;
        if (last == Passed.Length - 1) return 1;
        double span = Math.Max(1, _nom[last + 1] - _nom[last]);
        double p = Math.Clamp((t - At[last]) / span, 0, 0.95);
        return (last + p) / (Passed.Length - 1);
    }
}
