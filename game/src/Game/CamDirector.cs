using System;
using System.Collections.Generic;
using Godot;
using Starship.Game.Ui;
using Starship.Physics;
namespace Starship.Game;
public sealed class CamDirector {
    private static readonly double[] Skin = { 0.45, 0.55, 0.5, 0.5, 0.55, 0.6, 0.35 };
    private readonly CamRig _rig;
    private readonly double[] _seen = new double[CamRig.Total + 1];
    private double _held, _clock, _bOver = double.NaN;
    private int _cur = int.MinValue;
    private string _mode = "";
    public bool Manual, Locked;
    public bool Log;
    public CamDirector(CamRig rig) => _rig = rig;
    public void Step(SimState sim, StackView stack, double dt, Vector3 pad) {
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        bool over = b.Caught || b.Landed || b.Crashed;
        if (!over || double.IsNaN(_bOver) || _bOver > sim.T) _bOver = over ? sim.T : double.NaN;
        bool ship = !s.Attached && CamPlan.Ship(b.Mode, s.Mode, over ? sim.T - _bOver : -1);
        Vehicle v = ship ? s : b;
        string mode = v.Mode + (ship ? " к" : " б");
        bool cut = mode != _mode;
        _mode = mode;
        if (cut) Manual = false;
        for (int i = 0; i < _seen.Length; i++) _seen[i] += dt;
        if (_cur != int.MinValue) _seen[Slot(_cur)] = 0;
        if (Manual || Locked) return;
        _held += dt;
        _clock += dt;
        if (!cut && _clock < 0.5) return;
        _clock = 0;
        double away = Math.Sqrt(sim.Downrange(v) * sim.Downrange(v) + v.Alt * v.Alt);
        var shots = new List<Director.Shot>();
        foreach (int c in CamPlan.For(v.Mode)) {
            if (c >= 0 && CamRig.IsPad(c)) {
                if (away > CamRig.RangeOf(c)) continue;
            }
            else if (c >= 0 && CamRig.ShipCam(c) != ship) continue;
            shots.Add(Look(sim, stack, v, ship, c, pad));
        }
        if (shots.Count == 0) return;
        int pick = Director.Pick(shots.ToArray(), _cur, _held, cut);
        if (pick == _cur) return;
        _cur = pick;
        _held = 0;
        Apply(sim, ship, pick);
        if (Log)
            GD.Print($"CUT {NumFmt.Clock(sim.T)} {(pick < 0 ? "облёт" : CamRig.NameOf(pick))}"
                     + $" · {Phases.Air(v.Mode)} · {(ship ? "корабль" : "ускоритель")}");
    }
    private static int Slot(int cam) => cam < 0 ? CamRig.Total : cam;
    private void Apply(SimState sim, bool ship, int cam) {
        if (!sim.Veh[1].Attached) sim.Focus = ship ? "ship" : "stack";
        if (cam < 0) {
            _rig.Cur = CamRig.Kind.Orbit;
            return;
        }
        _rig.EnterCam(cam);
    }
    private Director.Shot Look(SimState sim, StackView stack, Vehicle v, bool ship, int cam, Vector3 pad) {
        Node3D body = ship ? stack.ShipRoot : stack.BoosterRoot;
        Vector3 sun = SkyEarth.SunScene(v.Up, v.East).Normalized();
        if (cam >= 0 && CamRig.IsPad(cam)) {
            Vector3 pos = pad + CamRig.PadOff(cam);
            Vector3 toward = (body.GlobalPosition - pos).Normalized();
            double angle = Mathf.RadToDeg(toward.AngleTo(sun));
            double look = toward.Y < 0.3f ? 0.75 : 0.25;
            double fire = v.NRun > 0 ? 1 : 0;
            bool dark = sun.Y < -0.15f && fire < 0.05;
            return new Director.Shot(cam, look, fire, 0, angle, 0.1, dark, 0.8, _seen[Slot(cam)]);
        }
        Vector3 local = cam < 0 ? new Vector3(0, -0.2f, -1) : CamRig.DirOf(cam);
        Vector3 f = (body.GlobalTransform.Basis * local).Normalized();
        double skin = cam < 0 ? 0.12 : Skin[Math.Clamp(cam, 0, Skin.Length - 1)];
        double fov = cam < 0 ? 50 : CamRig.FovOf(cam);
        double sunAng = Mathf.RadToDeg(f.AngleTo(sun));
        double depress = Math.Acos(Const.RE / (Const.RE + Math.Max(0, v.Alt))) * Const.R2D;
        double down = -Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(f.Y, -1f, 1f)));
        double earth = Math.Clamp((down + depress + fov * 0.5) / fov, 0, 1);
        double at = Math.Clamp(-local.Y, 0, 1);
        double flame = v.NRun > 0 ? Math.Max(0.25, at) : 0;
        double plasma = Math.Clamp(v.Heat / 400, 0, 1) * (at > 0.2 ? 1 : 0.4);
        bool night = sun.Y < -0.15f && flame < 0.05 && plasma < 0.05;
        double lit = sun.Y > 0 ? 1 : 0.15;
        double other = 0;
        Vehicle o = ship ? sim.Veh[0] : sim.Veh[1];
        if (!sim.Veh[1].Attached
            && Math.Abs(v.Alt - o.Alt) + Math.Abs(sim.Downrange(v) - sim.Downrange(o)) < 4000) other = 0.6;
        if (v.Alt < 3000 && Math.Abs(sim.Downrange(v)) < 3000) other = Math.Max(other, 0.7);
        return new Director.Shot(cam, earth, flame, plasma, sunAng, skin * lit, night, other, _seen[Slot(cam)]);
    }
}
