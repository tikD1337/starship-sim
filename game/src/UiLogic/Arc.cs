using System;
using Starship.Physics;
namespace Starship.Game.Ui;
public sealed class Arc {
    private static readonly string[] Orbital = {
        "старт", "max Q", "разделение", "тормозной импульс", "SECO", "захват ускорителя",
        "орбита", "сход с орбиты", "вход", "захват корабля",
    };
    private static readonly string[] Trans = {
        "старт", "max Q", "разделение", "тормозной импульс", "SECO", "захват ускорителя",
        "вход", "приводнение",
    };
    private static readonly double[] NomOrbital = { 0, 72, 136, 141, 438, 451, 548, 3570, 4819, 6280 };
    private static readonly double[] NomHigh = { 0, 72, 135, 139, 451, 452, 932, 3587, 5447, 6675 };
    private static readonly double[] NomTrans = { 0, 72, 135, 139, 426, 448, 1097, 2233 };
    public const int Window = 6;
    private readonly double[] _nom;
    private readonly bool _trans;
    public string[] Names { get; }
    public bool[] Passed { get; }
    public double[] At { get; }
    public int First { get; private set; }
    public int Shown => Math.Min(Window, Names.Length);
    public Arc(string mission) {
        _trans = mission == "trans";
        Names = _trans ? Trans : Orbital;
        _nom = mission switch { "trans" => NomTrans, "high" => NomHigh, _ => NomOrbital };
        Passed = new bool[Names.Length];
        At = new double[Names.Length];
        for (int i = 0; i < At.Length; i++) At[i] = double.NaN;
    }
    private static int Stage(Vehicle s) {
        if (s.Caught || s.Landed || s.Crashed) return 7;
        return s.Mode switch {
            "flipS" or "landS" => 6,
            "entryS" => 5,
            "deorbit" or "coastD" => 4,
            "orbit" => 3,
            "circ" => 2,
            "coastS" => 1,
            _ => 0,
        };
    }
    public void Track(SimState sim) {
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        int st = s.Attached ? 0 : Stage(s);
        bool boosterOver = b.Caught || b.Landed || b.Crashed;
        bool[] now = _trans
            ? new[] {
                b.Launched || sim.T >= 0, sim.Events.ContainsKey("maxq"), !s.Attached,
                b.Mode == "boostback" || b.Mode == "coastB" || b.Mode == "landB" || boosterOver,
                st >= 1, boosterOver, st >= 5, st >= 7,
            }
            : new[] {
                b.Launched || sim.T >= 0, sim.Events.ContainsKey("maxq"), !s.Attached,
                b.Mode == "boostback" || b.Mode == "coastB" || b.Mode == "landB" || boosterOver,
                st >= 1, boosterOver, st >= 3, st >= 4, st >= 5, st >= 7,
            };
        for (int i = 0; i < Passed.Length; i++)
            if (now[i] && !Passed[i]) {
                Passed[i] = true;
                At[i] = sim.T;
            }
        First = Math.Clamp(Last() - 2, 0, Names.Length - Shown);
    }
    private int Last() {
        int last = -1;
        for (int i = 0; i < Passed.Length; i++)
            if (Passed[i]) last = i;
        return last;
    }
    public double Frac(double t) {
        int last = Last();
        if (last < 0) return 0;
        double pos = last;
        if (last < Passed.Length - 1) {
            double span = Math.Max(1, _nom[last + 1] - _nom[last]);
            pos += Math.Clamp((t - At[last]) / span, 0, 0.95);
        }
        return Math.Clamp((pos - First) / (Shown - 1), 0, 1);
    }
}
