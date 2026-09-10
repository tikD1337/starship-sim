using Godot;
namespace Starship.Game;
public sealed class Splash {
    private GpuParticles3D _spray, _mist;
    private Node3D _root;
    private bool _fired;
    public static Splash Build(Node parent) {
        var s = new Splash();
        s._root = new Node3D { Name = "Splash" };
        parent.AddChild(s._root);
        var quad = new QuadMesh { Size = new Vector2(1, 1) };
        Texture2D tex = LaunchSmoke.Puff();
        var sprayMat = new ParticleProcessMaterial {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
            EmissionRingRadius = 12f, EmissionRingInnerRadius = 3f, EmissionRingHeight = 2f,
            EmissionRingAxis = Vector3.Up,
            Direction = new Vector3(1, 1.5f, 0), Spread = 60f,
            Gravity = new Vector3(0, -9.8f, 0),
            InitialVelocityMin = 22f, InitialVelocityMax = 58f,
            Damping = new Vector2(1f, 3f),
            ScaleMin = 2.5f, ScaleMax = 7f,
            LifetimeRandomness = 0.5f, AngleMin = -180f, AngleMax = 180f,
        };
        sprayMat.ScaleCurve = new CurveTexture { Curve = Ramp(0.4f, 1.0f, 1.6f) };
        s._spray = new GpuParticles3D {
            Amount = 900, Lifetime = 4.0, OneShot = true, Explosiveness = 0.85f,
            Randomness = 0.4f, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth,
            ProcessMaterial = sprayMat, DrawPass1 = quad, Emitting = false, LocalCoords = false,
            MaterialOverride = Water(new Color(0.93f, 0.96f, 0.98f, 0.75f), tex),
            VisibilityAabb = new Aabb(new Vector3(-400, -100, -400), new Vector3(800, 600, 800)),
        };
        s._root.AddChild(s._spray);
        var mistMat = new ParticleProcessMaterial {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
            EmissionRingRadius = 18f, EmissionRingInnerRadius = 6f, EmissionRingHeight = 1f,
            EmissionRingAxis = Vector3.Up,
            Direction = new Vector3(1, 0.25f, 0), Spread = 85f,
            Gravity = new Vector3(0, 1.2f, 0),
            InitialVelocityMin = 10f, InitialVelocityMax = 30f,
            Damping = new Vector2(4f, 9f),
            ScaleMin = 8f, ScaleMax = 22f,
            LifetimeRandomness = 0.5f, AngleMin = -180f, AngleMax = 180f,
            AnimOffsetMin = 0f, AnimOffsetMax = 1f,
        };
        mistMat.ScaleCurve = new CurveTexture { Curve = Ramp(0.3f, 1.0f, 2.4f) };
        s._mist = new GpuParticles3D {
            Amount = 700, Lifetime = 7.0, OneShot = true, Explosiveness = 0.7f,
            Randomness = 0.45f, DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth,
            ProcessMaterial = mistMat, DrawPass1 = quad, Emitting = false, LocalCoords = false,
            MaterialOverride = Water(new Color(0.90f, 0.94f, 0.97f, 0.30f), tex),
            VisibilityAabb = new Aabb(new Vector3(-400, -100, -400), new Vector3(800, 600, 800)),
        };
        s._root.AddChild(s._mist);
        return s;
    }
    private static StandardMaterial3D Water(Color tint, Texture2D tex) {
        var m = new StandardMaterial3D {
            AlbedoTexture = tex, AlbedoColor = tint,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            BillboardKeepScale = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true, DisableReceiveShadows = true,
            Roughness = 1.0f, DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Lambert,
            ParticlesAnimHFrames = 2, ParticlesAnimVFrames = 2, ParticlesAnimLoop = false,
        };
        Texture2D nrm = ResourceLoader.Load<Texture2D>("res://assets/textures/smoke_normal.png");
        if (nrm != null) { m.NormalEnabled = true; m.NormalTexture = nrm; m.NormalScale = 0.5f; }
        return m;
    }
    private static Curve Ramp(float a, float b, float c) {
        var cu = new Curve { MinValue = 0f, MaxValue = 4f };
        cu.AddPoint(new Vector2(0f, a));
        cu.AddPoint(new Vector2(0.25f, b));
        cu.AddPoint(new Vector2(1f, c));
        return cu;
    }
    public void Reset() {
        _fired = false;
        _spray.Emitting = false;
        _mist.Emitting = false;
    }
    private double _dr;
    public void Fire(double dr) {
        if (_fired) return;
        _fired = true;
        _dr = dr;
        _spray.Restart();
        _mist.Restart();
        _spray.Emitting = true;
        _mist.Emitting = true;
    }
    public void Update(Origin org) {
        _root.Visible = _fired;
        if (_fired) _root.Position = org.Place(_dr, 0);
    }
}
