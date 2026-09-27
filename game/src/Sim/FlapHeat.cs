using System;
namespace Starship.Physics;
public sealed class FlapState {
    public double Ang, Burn, TEdge = 290, THinge = 290, HingeDmg, MaxEdge, MaxHinge, GapK = Const.LEE_SHADE, EdgeK = 1;
    public bool Jammed;
    public int BurnStep;
}
public static class FlapHeat {
    private const double Sb = 5.67e-8;
    private static readonly double[] Steps = { 0.10, 0.25, 0.50, 1.0 };
    public static readonly string[] Names = { "переднего закрылка A", "переднего закрылка B", "заднего закрылка A", "заднего закрылка B" };
    public static double EdgeFlux(Vehicle v, int i) {
        FlapState f = v.Flaps[i];
        double lam = Math.Abs(Math.PI / 2 - Math.Abs(v.Alpha));
        double side = v.Alpha >= 0 ? 1 : Const.LEE_SHADE;
        return v.Heat * 1000 * Math.Sqrt(4.5 / Const.FLAP_RE) * Math.Pow(Math.Cos(lam), 1.5)
               * Math.Max(0, Math.Cos(f.Ang)) * Const.FLAP_SHOCK * side * f.EdgeK;
    }
    public static double HingeFlux(Vehicle v, int i)
        => v.Heat * 1000 * Math.Pow(Math.Abs(Math.Sin(v.Alpha)), 1.5) * (v.Alpha >= 0 ? v.Flaps[i].GapK : Const.LEE_SHADE);
    public static void Step(SimState sim, Vehicle v, double dt) {
        for (int i = 0; i < v.Flaps.Length; i++) {
            FlapState f = v.Flaps[i];
            f.TEdge += (EdgeFlux(v, i) - Const.TILE_EPS * Sb * Math.Pow(f.TEdge, 4) - 26 * (f.TEdge - v.TSkin)) / Const.TILE_CAP * dt;
            f.THinge += (HingeFlux(v, i) - Const.SKIN_EPS * Sb * Math.Pow(f.THinge, 4)) / Const.SKIN_CAP * dt;
            f.TEdge = Const.Clamp(f.TEdge, 90, 2600);
            f.THinge = Const.Clamp(f.THinge, 90, 2600);
            f.MaxEdge = Math.Max(f.MaxEdge, f.TEdge);
            f.MaxHinge = Math.Max(f.MaxHinge, f.THinge);
            if (v.Crashed || !v.Alive) continue;
            Edge(sim, v, i, f, dt);
            Hinge(sim, v, i, f, dt);
        }
    }
    private static void Edge(SimState sim, Vehicle v, int i, FlapState f, double dt) {
        double over = f.TEdge - Const.TILE_LIMIT;
        if (over <= 0 || f.Burn >= 1) return;
        sim.Once($"edge{v.Tag}{i}", () => sim.LogMsg($"{v.Tag}: перегрев кромки {Names[i]} — {f.TEdge:F0} К", 2));
        f.Burn = Math.Min(1, f.Burn + Const.BURN_EDGE * over * over * dt);
        while (f.BurnStep < Steps.Length && f.Burn >= Steps[f.BurnStep]) {
            sim.LogMsg($"{v.Tag}: прогар {Names[i]} — {Steps[f.BurnStep] * 100:F0} % площади", f.BurnStep >= 2 ? 3 : 2);
            f.BurnStep++;
        }
    }
    private static void Hinge(SimState sim, Vehicle v, int i, FlapState f, double dt) {
        double over = f.THinge - Const.SKIN_LIMIT;
        if (over <= 0 || f.Jammed) return;
        f.HingeDmg += Const.BURN_HINGE * over * over * dt;
        if (f.HingeDmg < 1) return;
        f.Jammed = true;
        sim.LogMsg($"{v.Tag}: заклинивание привода {Names[i]} на {f.Ang * Const.R2D:F0}°", 3);
    }
}
