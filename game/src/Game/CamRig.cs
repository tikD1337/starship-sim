using System;
using System.Collections.Generic;
using Godot;
namespace Starship.Game;
public sealed class CamRig {
    public enum Kind { Orbit, Mount, Free, Pad }
    public enum Carrier { Body, FlapFwd, FlapAft }
    public readonly record struct Spot(string Name, bool Ship, Carrier On, Vector3 Pos);
    private sealed class Mount {
        public string Name;
        public bool Ship;
        public Carrier On;
        public float Azim, Radius, Y;
        public Vector3 Target;
        public bool HasTarget;
        public Vector3 Dir;
        public float Fov = 75f;
        public float Roll;
    }
    private static readonly List<Mount> Mounts = new() {
        new Mount { Name = "Передний закрылок", Ship = true, On = Carrier.FlapFwd,
                    Azim = 113.0f, Radius = 7.46f, Y = 41.30f,
                    HasTarget = true, Target = new Vector3(0f, 8f, -7.5f), Fov = 64f, Roll = -90f },
        new Mount { Name = "Петля переднего закрылка", Ship = true, Azim = 78f, Radius = 4.52f, Y = 40.2f,
                    HasTarget = true, Target = new Vector3(-1.94f, 45.0f, -5.04f), Fov = 70f },
        new Mount { Name = "Петля заднего закрылка", Ship = true, Azim = 102f, Radius = 4.50f, Y = 14.6f,
                    HasTarget = true, Target = new Vector3(-1.35f, -10f, -6.36f), Fov = 72f, Roll = 90f },
        new Mount { Name = "Решётчатые рули", Ship = false, Azim = 42f, Radius = 4.42f, Y = 49.5f,
                    HasTarget = true, Target = new Vector3(0f, 61.2f, -4.9f), Fov = 62f },
        new Mount { Name = "Сверху на двигатели", Ship = false, Azim = 112f, Radius = 4.65f, Y = 63.6f,
                    HasTarget = true, Target = new Vector3(-9.33f, -12f, -23.09f), Fov = 70f, Roll = 90f },
        new Mount { Name = "Наплыв", Ship = false, Azim = 126f, Radius = 5.0f, Y = 27.45f,
                    HasTarget = true, Target = new Vector3(-3.06f, 70f, -4.21f), Fov = 66f },
        new Mount { Name = "Факел", Ship = false, Azim = 0f, Radius = 4.50f, Y = 7.0f,
                    Dir = new Vector3(0.174f, -0.985f, 0f), Fov = 84f },
    };
    public Node3D FlapFwd, FlapAft;
    public Kind Cur = Kind.Orbit;
    public int MountIdx;
    private sealed class PadCam {
        public string Name;
        public Vector3 Off, Aim;
        public float Fov;
        public bool Zoom;
        public float Range = 9000;
        public float AimAt = 1f;
    }
    private static readonly List<PadCam> Pads = new() {
        new PadCam { Name = "Стол", Off = new Vector3(-120, 6, 300), Fov = 24, Zoom = true },
        new PadCam { Name = "Башня сверху", Off = new Vector3(-58, 168, 42), Aim = new Vector3(0, -42, 0), Fov = 52, Range = 3000 },
        new PadCam { Name = "Панорама", Off = new Vector3(-900, 45, 1150), Fov = 32, Zoom = true, Range = 20000 },
        new PadCam { Name = "Ловильные руки", Off = new Vector3(-58, 118, 95), Aim = new Vector3(0, -6, 0), Fov = 40, Zoom = true, Range = 1500 },
        new PadCam { Name = "Под столом", Off = new Vector3(-3.5f, 3.5f, 6.5f), Fov = 62, Range = 400, AimAt = 0.04f },
    };
    public int PadIdx;
    public static int Total => Mounts.Count + Pads.Count;
    public static bool IsPad(int idx) => idx >= Mounts.Count;
    private static PadCam PadAt(int idx) {
        int n = idx - Mounts.Count;
        return Pads[((n % Pads.Count) + Pads.Count) % Pads.Count];
    }
    public static Vector3 PadOff(int idx) => PadAt(idx).Off;
    public static float RangeOf(int idx) => PadAt(idx).Range;
    public void EnterCam(int idx) {
        if (idx >= Mounts.Count) {
            PadIdx = idx - Mounts.Count;
            Cur = Kind.Pad;
            return;
        }
        MountIdx = idx;
        Cur = Kind.Mount;
    }
    public float Yaw = 0.9f, Pitch = 0.14f, Dist = 300f;
    private Vector3 _freePos;
    private float _freeYaw, _freePitch;
    private bool _freeReady;
    private readonly Camera3D _cam;
    public CamRig(Camera3D cam) => _cam = cam;
    private Mount Cursor => Mounts[((MountIdx % Mounts.Count) + Mounts.Count) % Mounts.Count];
    public static IEnumerable<Spot> Spots {
        get { foreach (Mount m in Mounts) yield return new Spot(m.Name, m.Ship, m.On, PosOf(m)); }
    }
    private static Vector3 PosOf(Mount m) {
        float a = Mathf.DegToRad(m.Azim);
        return new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a)) * m.Radius + new Vector3(0, m.Y, 0);
    }
    public static int Count => Mounts.Count;
    private static Mount At(int idx) => Mounts[((idx % Mounts.Count) + Mounts.Count) % Mounts.Count];
    public static Vector3 DirOf(int idx) {
        Mount m = At(idx);
        return (m.HasTarget ? (m.Target - PosOf(m)) : m.Dir).Normalized();
    }
    public static bool ShipCam(int idx) => At(idx).Ship;
    public static float FovOf(int idx) => idx >= Mounts.Count ? PadAt(idx).Fov : At(idx).Fov;
    public static string NameOf(int idx) => idx >= Mounts.Count ? PadAt(idx).Name : At(idx).Name;
    public string Name => Cur switch {
        Kind.Orbit => "Орбита",
        Kind.Free => "Свободная",
        Kind.Pad => PadAt(Mounts.Count + PadIdx).Name,
        _ => Cursor.Name,
    };
    public void NextMount(bool shipFocused) {
        for (int k = 0; k < Mounts.Count; k++) {
            MountIdx = (MountIdx + 1) % Mounts.Count;
            if (Mounts[MountIdx].Ship == shipFocused) return;
        }
    }
    public void EnterMount(bool shipFocused) {
        if (Cur == Kind.Mount) NextMount(shipFocused);
        else if (Cursor.Ship != shipFocused) NextMount(shipFocused);
        Cur = Kind.Mount;
    }
    public void EnterFree() {
        if (!_freeReady) {
            _freePos = _cam.Position;
            Vector3 f = -_cam.Basis.Z;
            _freeYaw = Mathf.Atan2(-f.X, -f.Z);
            _freePitch = Mathf.Asin(Mathf.Clamp(f.Y, -1f, 1f));
            _freeReady = true;
        }
        Cur = Kind.Free;
    }
    public void Turn(float dYaw, float dPitch) {
        if (Cur == Kind.Free) {
            _freeYaw -= dYaw;
            _freePitch = Mathf.Clamp(_freePitch - dPitch, -1.45f, 1.45f);
            return;
        }
        Yaw -= dYaw;
        Pitch = Mathf.Clamp(Pitch + dPitch, -1.4f, 1.4f);
    }
    public void Zoom(float k) {
        if (Cur == Kind.Mount) return;
        Dist = Mathf.Clamp(Dist * k, 12f, 6000f);
    }
    public float Speed = 48f, FastSpeed = 340f;
    public void FreeMove(Vector3 axes, double dt, bool fast) {
        if (Cur != Kind.Free || axes.LengthSquared() < 1e-6f) return;
        float sp = (fast ? FastSpeed : Speed) * (float)dt;
        _freePos += Basis.FromEuler(new Vector3(_freePitch, _freeYaw, 0)) * axes.Normalized() * sp;
    }
    public void Update(Node3D body, float halfLen, bool shipFocused, Vector3 padPos) {
        if (Cur == Kind.Free) {
            _cam.Fov = 60f;
            _cam.Position = _freePos;
            _cam.Basis = Basis.FromEuler(new Vector3(_freePitch, _freeYaw, 0));
            return;
        }
        _freeReady = false;
        if (Cur == Kind.Mount) {
            if (Cursor.Ship != shipFocused) NextMount(shipFocused);
            Mount m = Cursor;
            float a = Mathf.DegToRad(m.Azim);
            var radial = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a));
            Vector3 pos = PosOf(m);
            Vector3 dir = m.HasTarget ? (m.Target - pos).Normalized() : m.Dir.Normalized();
            Vector3 up = Mathf.Abs(dir.Dot(Vector3.Up)) > 0.86f ? radial : Vector3.Up;
            Basis look = Basis.LookingAt(dir, up);
            if (m.Roll != 0f) look = new Basis(dir, Mathf.DegToRad(m.Roll)) * look;
            Node3D carrier = m.On switch {
                Carrier.FlapFwd => FlapFwd ?? body,
                Carrier.FlapAft => FlapAft ?? body,
                _ => body,
            };
            Vector3 origin = carrier == body ? Vector3.Zero : carrier.Position;
            _cam.Fov = m.Fov;
            _cam.GlobalTransform = carrier.GlobalTransform * new Transform3D(look, pos - origin);
            return;
        }
        if (Cur == Kind.Pad) {
            PadCam p = PadAt(Mounts.Count + PadIdx);
            Vector3 pos = padPos + p.Off;
            Vector3 aim = body.Position + body.Basis.Y * (halfLen * p.AimAt) + p.Aim;
            float dist = pos.DistanceTo(aim);
            _cam.Fov = p.Zoom
                ? Mathf.Clamp(Mathf.RadToDeg(2f * Mathf.Atan(halfLen * 2.4f / Mathf.Max(1f, dist))), 5f, p.Fov)
                : p.Fov;
            _cam.Position = pos;
            _cam.LookAt(aim, Vector3.Up);
            return;
        }
        _cam.Fov = 42f;
        Vector3 target = body.Position + body.Basis.Y * halfLen;
        var d = new Vector3(
            Mathf.Cos(Pitch) * Mathf.Cos(Yaw),
            Mathf.Sin(Pitch),
            Mathf.Cos(Pitch) * Mathf.Sin(Yaw));
        _cam.Position = target + d * Dist;
        _cam.LookAt(target, Vector3.Up);
    }
}
