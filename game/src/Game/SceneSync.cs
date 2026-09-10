using System;
using Godot;
using Starship.Physics;
namespace Starship.Game;
public readonly struct Origin {
    public readonly double X, Y;
    private Origin(double x, double y) { X = x; Y = y; }
    public static Origin OfFocus(SimState sim) {
        Vehicle v = sim.FocusVeh();
        return new Origin(sim.Downrange(v), Math.Max(v.Alt, 0));
    }
    public Vector3 Place(double dr, double alt) =>
        new((float)(dr - X), (float)(Math.Max(alt, 0) - Y), 0f);
}
public readonly struct ShipPose {
    public readonly Vector3 Mid;
    public readonly Basis Body;
    public ShipPose(Vector3 mid, Basis body) { Mid = mid; Body = body; }
}
public sealed class SceneSync {
    private readonly StackView _stack;
    private readonly TowerView _tower;
    private readonly PadView _pad;
    private readonly LaunchSmoke _smoke;
    private readonly PlasmaView _plasma;
    private readonly SatsView _sats;
    private readonly BayView _bay;
    private readonly EngineSound _soundB, _soundS;
    private readonly Splash _splash;
    public SceneSync(StackView stack, TowerView tower, PadView pad, LaunchSmoke smoke,
                     PlasmaView plasma, SatsView sats, BayView bay,
                     EngineSound soundB, EngineSound soundS, Splash splash) {
        _stack = stack; _tower = tower; _pad = pad; _smoke = smoke; _plasma = plasma;
        _sats = sats; _bay = bay; _soundB = soundB; _soundS = soundS; _splash = splash;
    }
    public Origin Frame(SimState sim, double dt, bool tileGlow) {
        Origin org = Origin.OfFocus(sim);
        ShipPose pose = Bodies(sim, org, dt);
        Plumes(sim, tileGlow);
        Plasma(sim, pose);
        Ground(sim, org);
        Sea(sim, org);
        Sound(sim, dt);
        return org;
    }
    private void Sea(SimState sim, Origin org) {
        foreach (Vehicle v in sim.Veh)
            if (v.Landed && Math.Abs(sim.Downrange(v)) > 60e3) _splash.Fire(sim.Downrange(v));
        _splash.Update(org);
    }
    private ShipPose Bodies(SimState sim, Origin org, double dt) {
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        _stack.SetStacked(s.Attached);
        _stack.BoosterRoot.Position = org.Place(sim.Downrange(b), b.Alt);
        _stack.BoosterRoot.Rotation = new Vector3(0, 0, (float)-b.Th);
        _stack.BoosterRoot.Visible = !b.Attached && !b.Hauled;
        if (!s.Attached) {
            _stack.ShipRoot.Position = org.Place(sim.Downrange(s), s.Alt);
            _stack.ShipRoot.Rotation = new Vector3(0, 0, (float)-s.Th);
        }
        _stack.SetFins((float)b.FinDep, (float)b.Fin);
        (float tf, float ta) = FlapTarget(s);
        _stack.StepFlaps(dt, tf, ta);
        return new ShipPose(_stack.ShipRoot.GlobalPosition + _stack.ShipRoot.GlobalBasis.Y * 24f,
                            _stack.ShipRoot.Basis);
    }
    private static (float Fwd, float Aft) FlapTarget(Vehicle s) {
        float trim = 18f * (float)s.Flap;
        switch (s.Mode) {
        case "entryS":
            return (40f - trim, 55f + trim);
        case "coastD":
            return (24f - trim, 34f + trim);
        case "flipS":
        case "landS":
            return (14f, 20f);
        default:
            return (0f, 0f);
        }
    }
    private void Plumes(SimState sim, bool tileGlow) {
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        _stack.BoosterPlume.Update(Frac(b), b.Pa, b.NRun, 1.30);
        _stack.ShipPlume.Update(Frac(s), s.Pa, s.NRun, 1.30, s.Attached ? 7f : 0f);
        if (tileGlow) _stack.SetTileGlow(s.TTile);
    }
    private void Plasma(SimState sim, ShipPose pose) {
        Vehicle s = sim.Veh[1];
        var vs = new Vector3((float)s.VHor, (float)s.VVert, 0f);
        Vector3 flow = vs.LengthSquared() > 1f ? -vs.Normalized() : Vector3.Down;
        Vector3 flowLoc = pose.Body.Inverse() * flow;
        _plasma.Update(pose.Mid, flow, flowLoc, s.Heat, sim.T,
                       s.Alive && !s.Landed && !s.Attached);
    }
    private void Ground(SimState sim, Origin org) {
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        _sats.Update(sim, s, org.X, org.Y);
        _bay.Update(sim, s);
        _pad.Update(org.X, org.Y);
        _tower.SetOrigin(org.X, org.Y);
        _tower.Set(sim.Arms, sim.ArmDrop, sim.ArmY);
        _smoke.SetOrigin(org.X, org.Y);
        _smoke.Update(b.Alt, Frac(b), b.NRun, b.Pa);
    }
    private void Sound(SimState sim, double dt) {
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        _soundB.Update(Frac(b), b.NRun, b.Rho, b.Q, dt);
        _soundS.Update(Frac(s), s.NRun, s.Rho, s.Q, dt);
    }
    public static double Frac(Vehicle v)
        => v.F / Math.Max(1.0, Math.Max(1, v.NRun) * Spec.RaptorSL.Fv);
}
