using Godot;
using Starship.Physics;
namespace Starship.Game;
public sealed class Shot {
    private readonly Node _host;
    private int _at = -1;
    private string _path;
    private Vector3 _relPrev;
    private float _jitMax;
    public Shot(Node host) { _host = host; }
    public void Arm(string path, int frame) {
        _path = path;
        _at = frame;
    }
    public void Track(int frame, Vector3 rel) {
        if (frame > 3) {
            float d = (rel - _relPrev).Length();
            if (d > _jitMax) _jitMax = d;
        }
        _relPrev = rel;
    }
    public void Maybe(int frame, SimState sim, Camera3D cam, StackView stack, BayView bay,
                      TowerView tower) {
        if (_at <= 0 || frame < _at) return;
        _at = -1;
        Image img = _host.GetViewport().GetTexture().GetImage();
        img.SavePng(_path);
        Vehicle fv = sim.FocusVeh();
        Vehicle s = sim.Veh[1];
        GD.Print("SHOT_OK " + _path);
        GD.Print($"SHOT_INFO focus={fv.Name} alt={fv.Alt:F0} camY={cam.Position.Y:F0} " +
                 $"sunY={SkyEarth.SunScene(fv.Up, fv.East).Y:F3} dr={sim.Downrange(fv) / 1000:F0}km " +
                 $"q={fv.Q:F0} heat={fv.Heat:F1} tile={fv.TTile:F0} bank={fv.Bank * Const.R2D:F1} mode={fv.Mode} " +
                 $"flaps={stack.FlapAngles.Fwd:F1}/{stack.FlapAngles.Aft:F1}");
        GD.Print($"SHOT_STATE t={sim.T:F2} bMode={sim.Veh[0].Mode} bAlt={sim.Veh[0].Alt:F1} " +
                 $"bDR={sim.Downrange(sim.Veh[0]):F1} sMode={s.Mode} sAlt={s.Alt:F1} " +
                 $"sDR={sim.Downrange(s):F1} sProp={s.Prop / 1000:F1}t");
        GD.Print($"SHOT_TOWER {tower.Probe()} boosterX={stack.BoosterRoot.Position.X:F1} boosterY={stack.BoosterRoot.Position.Y:F1}");
        GD.Print($"SHOT_JITTER maxRelStep={_jitMax:F4} m  shipX={stack.ShipRoot.Position.X:F1} camX={cam.Position.X:F1}");
        Vector3 dn = bay.DoorNormal;
        GD.Print($"SHOT_BAY th={s.Th:F3} open={s.BayS?.Open:F2} packs={bay.Shown} " +
                 $"doorN=({dn.X:F3},{dn.Y:F3},{dn.Z:F3}) " +
                 $"yaw={Mathf.Atan2(dn.Z, dn.X):F3} pitch={Mathf.Asin(Mathf.Clamp(dn.Y, -1f, 1f)):F3}");
        _host.GetTree().Quit();
    }
}
