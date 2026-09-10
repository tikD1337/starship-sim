using System;
using Godot;
namespace Starship.Game;
public sealed class EngineSound {
    private const int Rate = 22050, Seconds = 4;
    private AudioStreamPlayer3D _roar, _wind;
    private static AudioStreamWav Noise(double lowpass, double rumbleHz, double rumbleDepth, int seed) {
        int n = Rate * Seconds;
        var data = new byte[n * 2];
        var rng = new Random(seed);
        double brown = 0, lp = 0;
        for (int i = 0; i < n; i++) {
            double white = rng.NextDouble() * 2.0 - 1.0;
            brown = (brown + 0.02 * white) * 0.998;
            lp += lowpass * (white - lp);
            double t = i / (double)Rate;
            double env = 1.0 - rumbleDepth + rumbleDepth * (0.5 + 0.5 * Math.Sin(Math.Tau * rumbleHz * t));
            double v = (brown * 6.0 + lp * 0.9) * env;
            double fade = Math.Min(1.0, Math.Min(i, n - 1 - i) / (Rate * 0.05));
            v *= fade;
            short s = (short)Math.Clamp(v * 12000.0, short.MinValue, short.MaxValue);
            data[i * 2] = (byte)(s & 0xff);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return new AudioStreamWav {
            Data = data, Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false,
            LoopMode = AudioStreamWav.LoopModeEnum.Forward, LoopBegin = 0, LoopEnd = n - 1,
        };
    }
    public static EngineSound Attach(Node3D target) {
        var s = new EngineSound();
        s._roar = new AudioStreamPlayer3D {
            Stream = Noise(0.06, 27.0, 0.35, 12345), UnitSize = 260f, MaxDistance = 6000f,
            VolumeDb = -80f, Autoplay = false,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseSquareDistance,
            DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.PhysicsStep,
        };
        target.AddChild(s._roar);
        s._roar.Position = new Vector3(0, 2f, 0);
        s._roar.Play();
        s._wind = new AudioStreamPlayer3D {
            Stream = Noise(0.35, 5.0, 0.15, 999), UnitSize = 120f, MaxDistance = 3000f, VolumeDb = -80f,
            Autoplay = false,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseSquareDistance,
        };
        target.AddChild(s._wind);
        s._wind.Position = new Vector3(0, 30f, 0);
        s._wind.Play();
        return s;
    }
    public void Update(double thrustFrac, int running, double rho, double q, double delta) {
        double air = Math.Clamp(rho / 1.225, 0.0, 1.0);
        double carry = Math.Pow(air, 0.45);
        double lvl = Math.Clamp(thrustFrac, 0, 1) * Math.Min(1.0, running / 6.0) * carry;
        _roar.VolumeDb = lvl > 0.002 ? (float)(Mathf.LinearToDb((float)lvl) - 2.0) : -80f;
        _roar.PitchScale = (float)(0.72 + 0.32 * Math.Clamp(thrustFrac, 0, 1));
        double w = Math.Clamp(q / 30000.0, 0, 1) * carry;
        _wind.VolumeDb = w > 0.002 ? (float)(Mathf.LinearToDb((float)w) - 6.0) : -80f;
        _wind.PitchScale = (float)(0.85 + 0.5 * w);
    }
}
