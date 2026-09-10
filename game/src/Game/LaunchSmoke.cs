using Godot;
namespace Starship.Game;
public sealed class LaunchSmoke {
    private GpuParticles3D _dust, _column;
    private readonly GpuParticles3D[] _vent = new GpuParticles3D[2];
    private Node3D _root;
    private StandardMaterial3D _dustMat, _colMat, _ventMat;
    private const float Friction = 0.42f, Bounce = 0.06f;
    public static Texture2D Puff(int size = 160) {
        var loaded = ResourceLoader.Load<Texture2D>("res://assets/textures/smoke_puff.png");
        if (loaded != null) return loaded;
        var img = Image.CreateEmpty(size, size, true, Image.Format.Rgba8);
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = (x - c) / c, dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp((1f - r) * 1.55f, 0f, 1f);
                a = a * a * (3f - 2f * a);
                img.SetPixel(x, y, new Color(1, 1, 1, a * 0.97f));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }
    private static Texture2D Relief()
        => ResourceLoader.Load<Texture2D>("res://assets/textures/smoke_normal.png");
    private static StandardMaterial3D Smoke(Color tint, Texture2D tex) {
        Texture2D nrm = Relief();
        return new StandardMaterial3D {
            AlbedoTexture = tex, AlbedoColor = tint, NormalEnabled = nrm != null, NormalTexture = nrm,
            NormalScale = 0.5f, ParticlesAnimHFrames = 2, ParticlesAnimVFrames = 2,
            ParticlesAnimLoop = false, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, BillboardKeepScale = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, VertexColorUseAsAlbedo = true,
            DisableReceiveShadows = true, Roughness = 1.0f,
            DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Lambert,
        };
    }
    private static GradientTexture1D Fade() {
        var g = new Gradient();
        g.SetOffset(0, 0f);
        g.SetColor(0, new Color(1f, 1f, 1f, 0f));
        g.SetOffset(1, 1f);
        g.SetColor(1, new Color(0.82f, 0.83f, 0.86f, 0f));
        g.AddPoint(0.10f, new Color(1f, 1f, 1f, 1f));
        g.AddPoint(0.55f, new Color(0.93f, 0.94f, 0.96f, 0.72f));
        return new GradientTexture1D { Gradient = g };
    }
    private static void Collide(ParticleProcessMaterial m) {
        m.CollisionMode = ParticleProcessMaterial.CollisionModeEnum.Rigid;
        m.CollisionFriction = Friction;
        m.CollisionBounce = Bounce;
    }
    private static GpuParticlesCollisionBox3D Wall(Node parent, Vector3 pos, Vector3 size, string name) {
        var b = new GpuParticlesCollisionBox3D { Name = name, Size = size, Position = pos };
        parent.AddChild(b);
        return b;
    }
    public static LaunchSmoke Build(Node parent) {
        var s = new LaunchSmoke();
        s._root = new Node3D { Name = "Smoke" };
        parent.AddChild(s._root);
        Texture2D tex = Puff();
        var quad = new QuadMesh { Size = new Vector2(1, 1) };
        Wall(s._root, new Vector3(0, -PadView.DeckH - 30f, 0), new Vector3(1800, 60, 1800), "Ground");
        Wall(s._root, new Vector3(0, -PadView.DeckH * 0.5f, 0),
             new Vector3(46, PadView.DeckH, 46), "Deck");
        Wall(s._root, new Vector3(-TowerView.TowerX, 62f, 0), new Vector3(13, 148, 13), "Tower");
        var dustMat = new ParticleProcessMaterial {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring, EmissionRingRadius = 58f,
            EmissionRingInnerRadius = 34f, EmissionRingHeight = 4f, EmissionRingAxis = Vector3.Up,
            Direction = new Vector3(1, 0.10f, 0), Spread = 180f, Gravity = new Vector3(0, 1.15f, 0),
            InitialVelocityMin = 12f, InitialVelocityMax = 26f, Damping = new Vector2(5f, 10f),
            ScaleMin = 10f, ScaleMax = 26f, LifetimeRandomness = 0.55f, AngleMin = -180f, AngleMax = 180f,
            ColorRamp = Fade(), AnimOffsetMin = 0f, AnimOffsetMax = 1f,
        };
        dustMat.ScaleCurve = new CurveTexture { Curve = Ramp(0.25f, 1.0f, 2.3f) };
        Collide(dustMat);
        s._dust = new GpuParticles3D {
            Amount = 5600, Lifetime = 9.5, Explosiveness = 0f, Randomness = 0.45f,
            DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = dustMat,
            DrawPass1 = quad,
            MaterialOverride = s._dustMat = Smoke(new Color(0.96f, 0.94f, 0.91f, 0.55f), tex),
            Emitting = false, LocalCoords = false,
            VisibilityAabb = new Aabb(new Vector3(-900, -1200, -900), new Vector3(1800, 2400, 1800)),
        };
        s._root.AddChild(s._dust);
        s._dust.Position = new Vector3(0, -PadView.DeckH + 2f, 0);
        for (int k = 0; k < 2; k++) {
            float sx = k == 0 ? 1f : -1f;
            var vm = new ParticleProcessMaterial {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
                EmissionBoxExtents = new Vector3(9f, 4.5f, 7f), Direction = new Vector3(0f, 0.18f, sx),
                Spread = 21f, Gravity = new Vector3(0, 3.6f, 0), InitialVelocityMin = 58f,
                InitialVelocityMax = 124f, Damping = new Vector2(7f, 15f), ScaleMin = 7f, ScaleMax = 19f,
                LifetimeRandomness = 0.5f, AngleMin = -180f, AngleMax = 180f, ColorRamp = Fade(),
                AnimOffsetMin = 0f, AnimOffsetMax = 1f,
            };
            vm.ScaleCurve = new CurveTexture { Curve = Ramp(0.28f, 1.0f, 2.5f) };
            Collide(vm);
            var v = new GpuParticles3D {
                Amount = 3100, Lifetime = 7.0, Randomness = 0.45f,
                DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = vm,
                DrawPass1 = quad, Emitting = false, LocalCoords = false,
                VisibilityAabb = new Aabb(new Vector3(-900, -1200, -900),
                                          new Vector3(1800, 2400, 1800)),
            };
            v.MaterialOverride = k == 0
                ? s._ventMat = Smoke(new Color(0.97f, 0.95f, 0.92f, 0.58f), tex)
                : s._ventMat;
            s._root.AddChild(v);
            v.Position = new Vector3(0, -PadView.DeckH + 5.5f, sx * (PadView.MouthZ + 7f));
            s._vent[k] = v;
        }
        var colMat = new ParticleProcessMaterial {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = 22f,
            Direction = new Vector3(0, 1, 0), Spread = 28f, Gravity = new Vector3(0, 1.8f, 0),
            InitialVelocityMin = 9f, InitialVelocityMax = 22f, Damping = new Vector2(1.6f, 3.8f),
            ScaleMin = 10f, ScaleMax = 27f, LifetimeRandomness = 0.5f, AngleMin = -180f, AngleMax = 180f,
            ColorRamp = Fade(), AnimOffsetMin = 0f, AnimOffsetMax = 1f,
        };
        colMat.ScaleCurve = new CurveTexture { Curve = Ramp(0.3f, 1.0f, 2.2f) };
        Collide(colMat);
        s._column = new GpuParticles3D {
            Amount = 1100, Lifetime = 8.0, Randomness = 0.4f,
            DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth, ProcessMaterial = colMat,
            DrawPass1 = quad,
            MaterialOverride = s._colMat = Smoke(new Color(0.97f, 0.96f, 0.95f, 0.50f), tex),
            Emitting = false, LocalCoords = false,
            VisibilityAabb = new Aabb(new Vector3(-900, -1200, -900), new Vector3(1800, 2400, 1800)),
        };
        s._root.AddChild(s._column);
        s._column.Position = new Vector3(0, -PadView.DeckH + 2f, 0);
        return s;
    }
    private static Curve Ramp(float a, float b, float c) {
        var cu = new Curve { MinValue = 0f, MaxValue = 4f };
        cu.AddPoint(new Vector2(0f, a));
        cu.AddPoint(new Vector2(0.25f, b));
        cu.AddPoint(new Vector2(1f, c));
        return cu;
    }
    public void SetOrigin(double ox, double oy) {
        _root.Position = new Vector3(-(float)ox, -(float)oy, 0);
    }
    public bool Off;
    public void Update(double alt, double thrustFrac, int running, double ambientPa) {
        bool near = !Off && alt < 1400 && running > 0 && thrustFrac > 0.005;
        bool vent = near && alt < 240;
        _dust.Emitting = near;
        _column.Emitting = near;
        foreach (GpuParticles3D v in _vent) v.Emitting = vent;
        float k = (float)Mathf.Clamp(1.0 - alt / 1400.0, 0.0, 1.0);
        _dust.SpeedScale = 0.7f + 0.8f * k;
        _column.SpeedScale = 0.6f + 0.7f * k;
        float pow = Mathf.Sqrt(Mathf.Clamp(running / 33f, 0f, 1f));
        _dust.AmountRatio = 0.22f + 0.78f * pow;
        _column.AmountRatio = 0.18f + 0.82f * pow;
        _dustMat.AlbedoColor = new Color(0.96f, 0.94f, 0.91f, 0.32f + 0.48f * pow);
        _colMat.AlbedoColor = new Color(0.97f, 0.96f, 0.95f, 0.23f + 0.35f * pow);
        _ventMat.AlbedoColor = new Color(0.97f, 0.95f, 0.92f, 0.34f + 0.50f * pow);
        foreach (GpuParticles3D v in _vent) {
            v.SpeedScale = 0.85f + 0.6f * k;
            v.AmountRatio = 0.25f + 0.75f * pow;
        }
    }
}
