using System;
using System.Collections.Generic;
using Godot;
namespace Starship.Game;
public sealed class CamRig {
    public enum Kind { Orbit, Mount, Free, Cockpit }
    private sealed class Mount {
        public string Name;
        public bool Ship;
        public float Azim, Radius, Y;
        public Vector3 Target;
        public bool HasTarget;
        public Vector3 Dir;
        public float Fov = 75f;
    }
    private static readonly List<Mount> Mounts = new() {
        new Mount { Name = "Нос вперёд", Ship = true, Azim = 0f, Radius = 3.4f, Y = 46.2f,
                    Dir = new Vector3(0.34f, 0.94f, 0f), Fov = 82f },
        new Mount { Name = "Нос назад", Ship = true, Azim = 0f, Radius = 3.6f, Y = 45.4f,
                    Dir = new Vector3(0.78f, -0.63f, 0f), Fov = 88f },
        new Mount { Name = "Хвост корабля", Ship = true, Azim = 90f, Radius = 5.1f, Y = 2.8f,
                    Dir = new Vector3(-0.42f, 0.91f, 0f), Fov = 78f },
        new Mount { Name = "Закрылок задний", Ship = true, Azim = 93f, Radius = 6.4f, Y = 12.0f,
                    HasTarget = true, Target = new Vector3(-0.3f, 5.8f, -6.2f), Fov = 68f },
        new Mount { Name = "Закрылок передний", Ship = true, Azim = 150f, Radius = 11.0f, Y = 47.6f,
                    HasTarget = true, Target = new Vector3(-1.22f, 47.6f, -2.61f), Fov = 55f },
        new Mount { Name = "Грузовой отсек", Ship = true, Azim = 212f, Radius = 9.5f, Y = 30.5f,
                    HasTarget = true, Target = new Vector3(-3.32f, 28.4f, 2.05f), Fov = 62f },
        new Mount { Name = "Решётчатые рули", Ship = false, Azim = 42f, Radius = 5.3f, Y = 49.5f,
                    HasTarget = true, Target = new Vector3(0f, 61.2f, -4.9f), Fov = 62f },
        new Mount { Name = "Факел", Ship = false, Azim = 0f, Radius = 5.6f, Y = 7.0f,
                    Dir = new Vector3(-0.36f, -0.93f, 0f), Fov = 84f },
        new Mount { Name = "Хвост ускорителя", Ship = false, Azim = 90f, Radius = 5.2f, Y = 4.2f,
                    Dir = new Vector3(-0.30f, 0.95f, 0f), Fov = 76f },
    };
    public Kind Cur = Kind.Orbit;
    public int MountIdx;
    public float Yaw = 0.9f, Pitch = 0.14f, Dist = 300f;
    private Vector3 _freePos;
    private float _freeYaw, _freePitch;
    private bool _freeReady;
    private float _cockYaw, _cockPitch;
    private float _cockFov = 78f;
    public float BankView;
    private static readonly Vector3 CockpitPos = new(0f, 44.6f, 3.95f);
    private static readonly Basis CockpitBasis =
        new(new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, -1, 0));
    private readonly Camera3D _cam;
    public CamRig(Camera3D cam) => _cam = cam;
    private Mount Cursor => Mounts[((MountIdx % Mounts.Count) + Mounts.Count) % Mounts.Count];
    public string Name => Cur switch {
        Kind.Orbit => "Орбита",
        Kind.Free => "Свободная",
        Kind.Cockpit => "Кабина",
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
        if (Cur == Kind.Cockpit) {
            _cockYaw = Mathf.Clamp(_cockYaw - dYaw, -1.25f, 1.25f);
            _cockPitch = Mathf.Clamp(_cockPitch - dPitch, -1.0f, 1.0f);
            return;
        }
        Yaw -= dYaw;
        Pitch = Mathf.Clamp(Pitch + dPitch, -1.4f, 1.4f);
    }
    public void Zoom(float k) {
        if (Cur == Kind.Mount) return;
        if (Cur == Kind.Cockpit) {
            _cockFov = Mathf.Clamp(_cockFov * k, 42f, 104f);
            return;
        }
        Dist = Mathf.Clamp(Dist * k, 12f, 6000f);
    }
    public float Speed = 48f, FastSpeed = 340f;
    public void FreeMove(Vector3 axes, double dt, bool fast) {
        if (Cur != Kind.Free || axes.LengthSquared() < 1e-6f) return;
        float sp = (fast ? FastSpeed : Speed) * (float)dt;
        _freePos += Basis.FromEuler(new Vector3(_freePitch, _freeYaw, 0)) * axes.Normalized() * sp;
    }
    public void Update(Node3D body, float halfLen, bool shipFocused) {
        if (Cur == Kind.Free) {
            _cam.Fov = 60f;
            _cam.Position = _freePos;
            _cam.Basis = Basis.FromEuler(new Vector3(_freePitch, _freeYaw, 0));
            return;
        }
        _freeReady = false;
        if (Cur == Kind.Cockpit) {
            Basis head = Basis.FromEuler(new Vector3(_cockPitch, _cockYaw, BankView));
            _cam.Fov = _cockFov;
            _cam.GlobalTransform = body.GlobalTransform *
                new Transform3D(CockpitBasis * head, CockpitPos);
            return;
        }
        if (Cur == Kind.Mount) {
            if (Cursor.Ship != shipFocused) NextMount(shipFocused);
            Mount m = Cursor;
            float a = Mathf.DegToRad(m.Azim);
            var radial = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a));
            Vector3 pos = radial * m.Radius + new Vector3(0, m.Y, 0);
            Vector3 dir = m.HasTarget ? (m.Target - pos).Normalized() : m.Dir.Normalized();
            Vector3 up = Mathf.Abs(dir.Dot(Vector3.Up)) > 0.86f ? radial : Vector3.Up;
            _cam.Fov = m.Fov;
            _cam.GlobalTransform = body.GlobalTransform * new Transform3D(Basis.LookingAt(dir, up), pos);
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
