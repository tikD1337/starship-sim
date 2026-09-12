using System;
using Godot;
using Starship.Game.Ui;
using Starship.Physics;
namespace Starship.Game;
public partial class Main : Node {
    private static readonly string[] Flags = {
        "--free", "--screen", "--theme", "--eng", "--noshadow", "--noglow", "--shot", "--t", "--dbg", "--camcheck",
        "--dist", "--deploy", "--pitch", "--yaw", "--cam", "--focus", "--tab",
        "--mission", "--anom", "--seed", "--nosmoke", "--flat", "--debugcam", "--perf", "--spin", "--cold", "--shotat", "--vsync", "--replay", "--recs", "--tape", "--script", "--pgrp", "--pset",
    };
    private SimState _sim;
    private StackView _stack;
    private TowerView _tower;
    private PadView _pad;
    private LaunchSmoke _smoke;
    private PlasmaView _plasma;
    private SatsView _sats;
    private BayView _bay;
    private Splash _splash;
    private EngineSound _soundB, _soundS;
    private Screens _scr;
    private EngineerView _eng;
    private OnAirView _air;
    private Node3D _world;
    private WorldRig _rigWorld;
    private SceneSync _scene;
    private Controls _ctl;
    private Shot _shot;
    private Perf _perf;
    private CamRig _rig;
    private Mission _mission;
    private MissionView _missionView;
    private string _missionKey = "orbital";
    private bool _anomOn;
    private uint _seed = 12345u;
    private string _script;
    private readonly Replay _rec = new();
    private FlightTape _tape;
    private bool _saved;
    private bool _tileGlow = true, _flat, _freeArg;
    private ModelLibrary _lib;
    private int _frame;
    public override void _Ready() {
        Log.Sink = (m, lv) => GD.Print(m);
        _sim = new SimState { Mission = "orbital", AnomOn = false };
        Physics.Sim.Reset(_sim, 12345u);
        Look.Load();
        _scr = Screens.Build(this);
        _eng = EngineerView.Build(_scr);
        _air = OnAirView.Build(_scr);
        _world = new Node3D { Name = "World" };
        _scr.World.AddChild(_world);
        _rigWorld = WorldRig.Build(_world);
        ModelLibrary lib = _lib = ModelLibrary.Load("res://assets/starship.glb",
                                                   "res://assets/starship_proc.glb");
        _stack = StackView.Build(lib, _world);
        _stack.SetStacked(true);
        _stack.SetFins(0f, 0f);
        _stack.SetFlaps(0f, 0f);
        _pad = PadView.Build(lib, _world);
        _tower = TowerView.Build(lib, _world);
        _smoke = LaunchSmoke.Build(_world);
        _plasma = PlasmaView.Build(_world, _stack.ShipRoot);
        _sats = SatsView.Build(lib, _world);
        _bay = BayView.Build(lib, _stack.ShipRoot);
        _splash = Splash.Build(_world);
        _soundB = EngineSound.Attach(_stack.BoosterRoot);
        _soundS = EngineSound.Attach(_stack.ShipRoot);
        _scene = new SceneSync(_stack, _tower, _pad, _smoke, _plasma, _sats, _bay, _soundB, _soundS, _splash);
        _rig = new CamRig(_rigWorld.Cam) { FlapFwd = _stack.FlapFwd, FlapAft = _stack.FlapAft };
        _ctl = new Controls(this, _sim, _rig, _scr, _eng);
        _eng.Focus = ship => _ctl.SetFocus(ship);
        _shot = new Shot(this);
        _perf = new Perf(this);
        _missionView = MissionView.Build(_eng.MissionCard, _eng.MissionCaption, _scr,
            key => StartFlight(key, _anomOn, _seed),
            () => StartFlight(_missionKey, !_anomOn, _seed),
            key => { _script = key; StartFlight(_missionKey, key != null || _anomOn, _seed); });
        _tape = FlightTape.Build(_scr);
        _ctl.Rec = _rec;
        _ctl.Replay = () => PlayBack(Starship.Game.Replay.Newest());
        _ctl.Saved = path => _sim.LogMsg(path != null
            ? "Прогон записан: " + path : "Записать прогон не удалось", 2);
        _ctl.Restart = () => StartFlight(_missionKey, _anomOn, _seed);
        _ctl.NextMission = () => StartFlight(NextKey(_missionKey), _anomOn, _seed);
        _ctl.ToggleAnom = () => StartFlight(_missionKey, !_anomOn, _seed);
        _ctl.ShowRecords = () => _missionView.ToggleRecords(_missionKey);
        _ctl.ShowTape = () => {
            _tape.ToggleSummary(_sim, _mission);
            if (_tape.SummaryShown) _missionView.HideFinal(); else _missionView.ShowFinalAgain();
        };
        _ctl.StepMark = dir => { double? t = FlightTape.Step(_sim, dir); if (t != null) SeekTo(t.Value); };
        _ctl.StepOne = () => { _ctl.Play?.Apply(_sim, _ctl); Physics.Sim.Tick(_sim, Const.DT); };
        _ctl.ToggleSmoke = () => _smoke.Off = !_smoke.Off;
        _ctl.ToggleFlat = () => { _flat = !_flat; if (_flat) _rigWorld.FlatLight(); else _rigWorld.NormalLight(); };
        foreach (string name in _scr.MouseGrabs()) GD.Print("UI_MOUSE_GRAB " + name);
        Apply(CliArgs.Parse(OS.GetCmdlineUserArgs()));
    }
    private static string NextKey(string key) {
        for (int i = 0; i < Mission.Keys.Length; i++)
            if (Mission.Keys[i] == key) return Mission.Keys[(i + 1) % Mission.Keys.Length];
        return Mission.Keys[0];
    }
    private void StartFlight(string key, bool anom, uint seed) {
        _missionKey = key;
        _anomOn = anom;
        _seed = seed;
        _sim.Mission = key;
        _sim.AnomOn = anom;
        _sim.AnomScript = _script == null ? null
            : new System.Collections.Generic.HashSet<string> { _script };
        _missionView.Script = _script;
        _rec.Head(key, seed, anom, _script);
        _saved = false;
        (_sim.TargetApo, _sim.TargetPeri) = key switch {
            "high" => (520e3, 500e3),
            "trans" => (180e3, 160e3),
            _ => (220e3, 200e3),
        };
        Physics.Sim.Reset(_sim, seed);
        _mission = Mission.Start(key, anom, _script);
        _missionView.Reset(_mission);
        _eng.ClearTele();
        _splash.Reset();
        _ctl.Paused = false;
        _sim.LogMsg($"Задание: {Mission.NameOf(key)}" + (anom ? " · отказы включены" : ""), 1);
    }
    private void Apply(CliArgs a) {
        foreach (string bad in a.Unknown(Flags)) GD.Print("ARG_UNKNOWN " + bad);
        _script = a.Str("--script", _script);
        string key = a.Str("--mission", _missionKey);
        if (System.Array.IndexOf(Mission.Keys, key) < 0) key = _missionKey;
        StartFlight(key, a.Has("--anom"), (uint)a.Int("--seed", (int)_seed));
        if (a.Has("--camcheck")) { GetTree().Quit(CamCheck.Run(_lib) == 0 ? 0 : 1); return; }
        if (a.Has("--theme")) Look.SetTheme(Themes.Parse(a.Str("--theme"), Look.Current), false);
        if (a.Has("--noshadow")) _rigWorld.NoShadow();
        if (a.Has("--noglow")) _tileGlow = false;
        if (a.Has("--nosmoke")) _smoke.Off = true;
        if (a.Has("--flat")) _rigWorld.FlatLight();
        if (a.Has("--recs")) _missionView.ToggleRecords(_missionKey);
        if (a.Has("--debugcam")) { _rig.Cur = CamRig.Kind.Free; _scr.Show(2); }
        if (a.Has("--dbg")) _rigWorld.SetDbg(a.Int("--dbg", 0));
        if (a.Has("--tab")) {
            int tab = a.Int("--tab", 0);
            _eng.SetPlot(tab == 2 ? 1 : tab == 3 ? 2 : 0);
            if (tab > 0) _scr.Show(3);
        }
        if (a.Has("--pgrp")) _eng.SelectGroup(a.Int("--pgrp", 0));
        if (a.Has("--pset"))
            foreach (string one in a.Str("--pset").Split(';')) {
                string[] kv = one.Split('=');
                if (kv.Length == 2) _eng.SetParam(_sim.FocusVeh(), kv[0], kv[1]);
            }
        _rig.Yaw = a.Flt("--yaw", 0.9f);
        _rig.Pitch = a.Flt("--pitch", 0.14f);
        _rig.Dist = a.Flt("--dist", 300f);
        if (a.Has("--cam")) {
            _rig.MountIdx = a.Int("--cam", 0);
            _rig.Cur = CamRig.Kind.Mount;
            _scr.Show(2);
        }
        if (a.Has("--free")) { _freeArg = true; _scr.Show(2); }
        if (a.Num("--t", out double toT)) FastForward(toT);
        if (a.Has("--tape")) {
            _tape.Visible = true;
            _tape.ToggleSummary(_sim, _mission);
            _missionView.HideFinal();
        }
        if (a.Has("--focus")) _sim.Focus = a.Str("--focus") == "b" ? "stack" : "ship";
        if (a.Has("--eng")) _eng.SelectEngine(_sim.FocusVeh(), a.Int("--eng", 0));
        if (a.Has("--deploy")) {
            Vehicle sh = _sim.Veh[1];
            if (sh.BayS != null) { sh.BayS.Want = 1; sh.BayS.Open = 1; }
            Physics.Sim.DeploySat(_sim, sh, a.Int("--deploy", 1));
        }
        if (a.Has("--screen")) _scr.Show(a.Int("--screen", 1));
        if (a.Has("--shot")) _shot.Arm(a.Str("--shot"), a.Int("--shotat", 200));
        _perf.Cold = a.Has("--cold");
        _perf.KeepVsync = a.Has("--vsync");
        if (a.Has("--perf")) _perf.Arm(a.Int("--perf", 600));
        if (a.Has("--replay")) PlayBack(a.Str("--replay"));
        _perf.Spin = a.Has("--spin");
    }
    private void PlayBack(string path) {
        Replay r = path == null ? null : Replay.Load(path);
        if (r == null) { _sim.LogMsg("Записи прогона не нашлось", 2); return; }
        _script = r.Script;
        StartFlight(r.Mission, r.Anom, r.Seed);
        r.Rewind();
        _ctl.Rec = null;
        _ctl.Play = r;
        _sim.LogMsg("Повтор записанного полёта: " + path, 1);
    }
    private void FastForward(double toT) {
        int guard = 0;
        while (_sim.T < toT && guard++ < 3_000_000) {
            _ctl.Play?.Apply(_sim, _ctl);
            Physics.Sim.Tick(_sim, Const.DT);
            if (guard % 50 != 0) continue;
            _eng.PushTele(_sim);
            _mission.Track(_sim);
        }
    }
    private void SeekTo(double toT) {
        Replay play = _ctl.Play;
        double span = Math.Max(_sim.T, toT);
        if (toT < _sim.T) {
            StartFlight(_missionKey, _anomOn, _seed);
            if (play != null) { play.Rewind(); _ctl.Play = play; }
        }
        FastForward(toT);
        _tape.Widen(span);
        _ctl.Paused = true;
    }
    public override void _Process(double delta) {
        _frame++;
        if (_perf.Spin) _rig.Yaw += (float)delta * 0.5f;
        _perf.Mark();
        _ctl.ReadAxes(delta);
        _ctl.StepPhysics(delta);
        _perf.Add(Perf.Part.Physics);
        Origin org = _scene.Frame(_sim, delta, _tileGlow);
        UpdateCamera();
        _rigWorld.Update(_sim, org);
        _shot.Track(_frame, _stack.ShipRoot.GlobalPosition - _rigWorld.Cam.GlobalPosition);
        _perf.Add(Perf.Part.Scene);
        _eng.Update(_sim, _ctl.Speed, _ctl.Paused);
        _air.Update(_sim);
        _perf.Add(Perf.Part.Ui);
        _mission.Track(_sim);
        _missionView.Update(_mission, _anomOn);
        _tape.Visible = _ctl.Play != null || _mission.Over;
        _ctl.TapeOn = _tape.Visible;
        _tape.Update(_sim);
        if (_mission.Over && !_saved && _ctl.Play == null) {
            _saved = true;
            string path = _rec.Save();
            if (path != null) _sim.LogMsg("Прогон записан: " + path + " — F10 повторить", 1);
        }
        _perf.Add(Perf.Part.Mission);
        _shot.Maybe(_frame, _sim, _rigWorld.Cam, _stack, _bay, _tower);
        _perf.Step(delta);
    }
    private void UpdateCamera() {
        Vehicle s = _sim.Veh[1];
        Vehicle v = _sim.FocusVeh();
        float top = s.Attached ? 123f : (v.Kind == Kind.Booster ? 71f : 52f);
        bool onShip = v == s && !s.Attached;
        Node3D body = onShip ? _stack.ShipRoot : _stack.BoosterRoot;
        _rig.Update(body, top * 0.5f, onShip);
        if (_freeArg && _frame > 2) { _freeArg = false; _rig.EnterFree(); }
    }
    public override void _UnhandledInput(InputEvent e) => _ctl.Event(e);
}
