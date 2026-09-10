using System;
using Godot;
using Starship.Physics;
namespace Starship.Game;
public sealed class WorldRig {
    public SkyEarth Sky;
    public Camera3D Cam;
    private DirectionalLight3D _sun;
    private MeshInstance3D _ground, _sea;
    private StandardMaterial3D _seaMat;
    private Godot.Environment _env;
    public static WorldRig Build(Node3D world) {
        var w = new WorldRig();
        var env = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Aces, TonemapExposure = 1.0f, SsrEnabled = true,
            SsaoEnabled = true, GlowEnabled = true, GlowIntensity = 0.45f, GlowBloom = 0.05f,
        };
        w.Sky = SkyEarth.Build();
        env.Sky = w.Sky.Sky;
        env.FogLightColor = new Color(0.60f, 0.69f, 0.82f);
        env.FogSunScatter = 0.12f;
        env.FogSkyAffect = 0f;
        env.FogAerialPerspective = 0.0f;
        w._env = env;
        world.AddChild(new WorldEnvironment { Environment = env });
        w._sun = new DirectionalLight3D {
            ShadowEnabled = true, LightEnergy = 3.2f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            DirectionalShadowMaxDistance = 700f, DirectionalShadowSplit1 = 0.06f,
            DirectionalShadowSplit2 = 0.16f, DirectionalShadowSplit3 = 0.42f,
            DirectionalShadowFadeStart = 0.9f, DirectionalShadowBlendSplits = true,
        };
        world.AddChild(w._sun);
        w._ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(60000, 60000) } };
        w._ground.MaterialOverride = new StandardMaterial3D {
            AlbedoColor = new Color(0.20f, 0.19f, 0.17f), Roughness = 0.95f, Metallic = 0f,
        };
        w._ground.Position = new Vector3(0, -PadView.DeckH - 0.05f, 0);
        world.AddChild(w._ground);
        w._sea = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(240000, 240000) } };
        var seaMat = new StandardMaterial3D {
            AlbedoColor = new Color(0.055f, 0.115f, 0.165f), Roughness = 0.12f, Metallic = 0.0f,
            Uv1Scale = new Vector3(1600, 1600, 1),
        };
        Texture2D wav = ResourceLoader.Load<Texture2D>("res://assets/textures/water_normal.png");
        if (wav != null) { seaMat.NormalEnabled = true; seaMat.NormalTexture = wav; seaMat.NormalScale = 0.9f; }
        w._seaMat = seaMat;
        w._sea.MaterialOverride = seaMat;
        w._sea.Position = new Vector3(0, -PadView.DeckH - 0.35f, 0);
        world.AddChild(w._sea);
        w.Cam = new Camera3D { Fov = 42f, Near = 0.15f, Far = 20000f };
        world.AddChild(w.Cam);
        return w;
    }
    public void NoShadow() => _sun.ShadowEnabled = false;
    public void FlatLight() {
        _sun.ShadowEnabled = false;
        _sun.LightEnergy = 1.6f;
        _env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        _env.AmbientLightColor = new Color(1f, 1f, 1f);
        _env.AmbientLightEnergy = 1.4f;
        _env.ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled;
        _env.SsrEnabled = false;
        _env.TonemapMode = Godot.Environment.ToneMapper.Linear;
    }
    public void NormalLight() {
        _sun.ShadowEnabled = true;
        _sun.LightEnergy = 1f;
        _env.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
        _env.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
        _env.TonemapMode = Godot.Environment.ToneMapper.Aces;
    }
    public void SetDbg(int n) => Sky.SetDbg(n);
    public void Update(SimState sim, Origin org) {
        Vehicle v = sim.FocusVeh();
        Vector3 sd = SkyEarth.SunScene(v.Up, v.East);
        double camAlt = org.Y + Cam.Position.Y;
        Sky.Update(camAlt, sim.Downrange(v) / Const.RE, sd, sim.T);
        Vector3 up = Math.Abs(sd.Y) > 0.985f ? Vector3.Right : Vector3.Up;
        _sun.Basis = Basis.LookingAt(-sd, up);
        _sun.LightEnergy = 3.2f * Mathf.Clamp((sd.Y + 0.12f) / 0.18f, 0f, 1f);
        _sun.DirectionalShadowMaxDistance =
            Mathf.Clamp(Cam.Position.Length() * 2.6f + 160f, 400f, 9000f);
        _ground.Position = new Vector3(-(float)org.X, -PadView.DeckH - 0.05f - (float)org.Y, 0);
        _ground.Visible = camAlt < 25000.0;
        _sea.Position = new Vector3(0, -PadView.DeckH - 0.35f - (float)org.Y, 0);
        _sea.Visible = camAlt < 25000.0;
        float drift = (float)(sim.T * 0.004);
        _seaMat.Uv1Offset = new Vector3(drift, drift * 0.6f, 0);
        double dens = Math.Exp(-Math.Max(camAlt, 0.0) / 8500.0);
        _env.FogEnabled = dens > 0.03;
        _env.FogDensity = (float)(6.0e-5 * dens);
        _env.FogLightEnergy = Mathf.Clamp((sd.Y + 0.10f) / 0.30f, 0.05f, 1f);
    }
}
