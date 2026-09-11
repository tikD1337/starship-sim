using System;
using Godot;
using Starship.Game.Ui;
using Starship.Physics;
namespace Starship.Game;
public sealed class Controls {
    private readonly Node _host;
    private readonly SimState _sim;
    private readonly CamRig _rig;
    private readonly ConsoleFull _ui;
    public double Speed = 1.0;
    private const double MaxStepPerFrame = 0.60;
    public bool Paused;
    public Action Restart, NextMission, ToggleAnom, ToggleSmoke, ToggleFlat, ShowRecords;
    public Action ShowTape, StepOne;
    public bool TapeOn;
    public Action<int> StepMark;
    private bool _drag;
    private double _accum;
    public Replay Rec, Play;
    public Action<string> Saved;
    public Action Replay;
    public Controls(Node host, SimState sim, CamRig rig, ConsoleFull ui) {
        _host = host; _sim = sim; _rig = rig; _ui = ui;
    }
    public void StepPhysics(double delta) {
        if (Paused) return;
        _accum += delta * Speed;
        double budget = Math.Min(MaxStepPerFrame, delta * Speed * 1.5);
        while (_accum > Const.DT && budget > 0) {
            Play?.Apply(_sim, this);
            Physics.Sim.Tick(_sim, Const.DT);
            _accum -= Const.DT;
            budget -= Const.DT;
        }
        if (_accum > Const.DT) _accum = Const.DT;
    }
    public void ReadAxes(double delta) {
        if (_rig.Cur == CamRig.Kind.Free) {
            var ax = new Vector3(
                (Input.IsKeyPressed(Key.D) ? 1 : 0) - (Input.IsKeyPressed(Key.A) ? 1 : 0),
                (Input.IsKeyPressed(Key.E) ? 1 : 0) - (Input.IsKeyPressed(Key.Q) ? 1 : 0),
                (Input.IsKeyPressed(Key.S) ? 1 : 0) - (Input.IsKeyPressed(Key.W) ? 1 : 0));
            _rig.FreeMove(ax, delta, Input.IsKeyPressed(Key.Shift));
        }
        double pitch = (Input.IsKeyPressed(Key.Right) ? 1 : 0) - (Input.IsKeyPressed(Key.Left) ? 1 : 0);
        double thr = (Input.IsKeyPressed(Key.Up) ? 1 : 0) - (Input.IsKeyPressed(Key.Down) ? 1 : 0);
        double bank = (Input.IsKeyPressed(Key.Period) ? 1 : 0) - (Input.IsKeyPressed(Key.Comma) ? 1 : 0);
        if (Play != null) return;
        _sim.ManPitchAxis = pitch;
        _sim.ManThrAxis = thr;
        _sim.ManBankAxis = bank;
        Rec?.Put(_sim.T, "pitch", pitch);
        Rec?.Put(_sim.T, "thr", thr);
        Rec?.Put(_sim.T, "bank", bank);
        if ((pitch != 0 || thr != 0 || bank != 0) && _sim.Mode != "man") {
            _sim.Mode = "man";
            _sim.ManThr = _sim.FocusVeh().Throttle;
            Rec?.Put(_sim.T, "man", 1);
            _sim.LogMsg("Ручное управление: тяга и тангаж со стрелок, крен на «,» и «.»", 1);
        }
    }
    public void Event(InputEvent e) {
        bool free = _rig.Cur == CamRig.Kind.Free;
        if (e is InputEventMouseButton mb) {
            if (mb.ButtonIndex == MouseButton.Left) _drag = mb.Pressed;
            if (mb.ButtonIndex == MouseButton.WheelUp) _rig.Zoom(0.9f);
            if (mb.ButtonIndex == MouseButton.WheelDown) _rig.Zoom(1.11f);
            return;
        }
        if (e is InputEventMouseMotion mm) {
            if (_drag) _rig.Turn(mm.Relative.X * 0.006f, mm.Relative.Y * 0.005f);
            return;
        }
        if (e is not InputEventKey k || !k.Pressed || k.Echo) return;
        switch (k.Keycode) {
            case Key.Tab: _ui.ToggleFull(); break;
            case Key.F1: _ui.ToggleKeys(); break;
            case Key.F2: _ui.SetTab(0); break;
            case Key.F3: _ui.SetTab(1); break;
            case Key.F4: _ui.SetTab(2); break;
            case Key.F5: _ui.SetTab(3); break;
            case Key.F6: NextMission?.Invoke(); break;
            case Key.F7: ToggleAnom?.Invoke(); break;
            case Key.F8: ShowRecords?.Invoke(); break;
            case Key.F11: ShowTape?.Invoke(); break;
            case Key.Comma: if (TapeOn) StepMark?.Invoke(-1); break;
            case Key.Period: if (TapeOn) StepMark?.Invoke(1); break;
            case Key.Slash: if (TapeOn) { Paused = true; StepOne?.Invoke(); } break;
            case Key.Space: Paused = !Paused; break;
            case Key.Bracketright: Speed = Math.Min(64, Speed * 2); break;
            case Key.Bracketleft: Speed = Math.Max(0.125, Speed / 2); break;
            case Key.R:
                if (Restart != null) Restart();
                else Physics.Sim.Reset(_sim, _sim.Seed);
                break;
            case Key.Escape: _host.GetTree().Quit(); break;
            case Key.B: {
                bool open = _sim.Veh[1].BayS is { Want: < 0.5 };
                Rec?.Put(_sim.T, "bay", open ? 1 : 0);
                Physics.Sim.BaySet(_sim, _sim.Veh[1], open);
                break;
            }
            case Key.N: Rec?.Put(_sim.T, "sat", 1); Physics.Sim.DeploySat(_sim, _sim.Veh[1], 1); break;
            case Key.M: Rec?.Put(_sim.T, "sat", 10); Physics.Sim.DeploySat(_sim, _sim.Veh[1], 10); break;
            case Key.Key1: _rig.Cur = CamRig.Kind.Orbit; break;
            case Key.Key2: _rig.EnterMount(_sim.FocusVeh() == _sim.Veh[1] && !_sim.Veh[1].Attached); break;
            case Key.Key3: _rig.EnterFree(); break;
            case Key.G: ToggleSmoke?.Invoke(); break;
            case Key.L: ToggleFlat?.Invoke(); break;
            case Key.Pageup: _rig.Speed *= 2f; _rig.FastSpeed *= 2f; break;
            case Key.Pagedown: _rig.Speed /= 2f; _rig.FastSpeed /= 2f; break;
            case Key.Home: _rig.Speed = 48f; _rig.FastSpeed = 340f; break;
            case Key.T: ToggleRcs(); break;
            case Key.F9: Saved?.Invoke(Rec?.Save()); break;
            case Key.F10: Replay?.Invoke(); break;
            case Key.V: SwitchFocus(); break;
            case Key.Key0: BackToAuto(); break;
            case Key.A: if (!free) BackToAuto(); break;
            case Key.Enter:
            case Key.KpEnter: Stage(); break;
            case Key.S: if (!free) Stage(); break;
        }
    }
    private void Stage() {
        Rec?.Put(_sim.T, "sep", 1);
        Physics.Sim.ManualStage(_sim);
    }
    private void ToggleRcs() {
        Vehicle fv = _sim.FocusVeh();
        fv.Rcs = !fv.Rcs;
        Rec?.Put(_sim.T, "rcs", fv.Rcs ? 1 : 0);
        _sim.LogMsg(fv.Tag + (fv.Rcs ? ": ДМТ включён" : ": ДМТ отключён"), fv.Rcs ? 1 : 2);
    }
    private void SwitchFocus() {
        if (_sim.Veh[1].Attached) return;
        _sim.Focus = _sim.Focus == "ship" ? "stack" : "ship";
        Rec?.Put(_sim.T, "focus", _sim.Focus == "ship" ? 1 : 0);
    }
    private void BackToAuto() {
        if (_sim.Mode == "auto") return;
        Rec?.Put(_sim.T, "man", 0);
        _sim.Mode = "auto";
        _sim.ManEng = null;
        _sim.LogMsg("Управление возвращено штатному наведению", 1);
    }
}
