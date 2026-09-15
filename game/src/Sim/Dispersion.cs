using System;
namespace Starship.Physics;
public sealed class Dispersion {
    public double RhoK = 1, GustK;
    public readonly double[] DryK = { 1, 1 }, PropK = { 1, 1 };
    public readonly double[][] EngK = { new double[40], new double[8] };
    public static Dispersion Roll(uint seed) {
        var rng = new Rng();
        rng.Seed(unchecked(seed * 747796405u + 2891336453u));
        var d = new Dispersion { RhoK = rng.About(0.03), GustK = 0.6 + rng.Next() * 0.6 };
        for (int i = 0; i < 2; i++) {
            d.DryK[i] = rng.About(0.006);
            d.PropK[i] = rng.About(0.004);
            for (int j = 0; j < d.EngK[i].Length; j++) d.EngK[i][j] = rng.About(0.015);
        }
        return d;
    }
    public void Apply(SimState sim) {
        for (int i = 0; i < sim.Veh.Count && i < 2; i++) {
            Vehicle v = sim.Veh[i];
            v.Dry *= DryK[i];
            v.Prop *= PropK[i];
            v.PropMax = v.Prop;
            for (int j = 0; j < v.Eng.Count; j++) v.Eng[j].P.Cf *= EngK[i][j % EngK[i].Length];
        }
        sim.RhoK = RhoK;
        sim.Wind.Gusts(GustK, sim.Seed);
    }
}
