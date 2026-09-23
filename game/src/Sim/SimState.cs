using System;
using System.Collections.Generic;
namespace Starship.Physics;
public sealed class LogEntry {
    public double T;
    public string M;
    public int Lv;
}
public sealed class SimState {
    public double T = -10, Dt = Const.DT, RhoK = 1, Payload = 60e3;
    public bool AnomOn = true, Disperse;
    public Dispersion Disp;
    public System.Collections.Generic.HashSet<string> AnomScript;
    public uint Seed;
    public List<Vehicle> Veh = new();
    public List<LogEntry> Log = new();
    public readonly List<LogEntry> Marks = new();
    public Dictionary<string, double> Events = new();
    public string Focus = "stack";
    public string Mode = "auto";
    public double ManThr = 1;
    public int? ManEng;
    public double ManPitchAxis, ManThrAxis, ManBankAxis;
    public string Mission = "orbital";
    public double TargetApo = 220e3, TargetPeri = 200e3, SecoPeri = 190e3, MecoFill = 0.19, MecoV = 1450;
    public Action OnSeparate;
    public Rng Rng = new(), NavRng = new();
    public Anomalies Anom;
    public Wind Wind = Wind.Calm();
    public double ArmDrop, ArmY = Const.ARM_PARK, ArmSag, ArmSagV, ArmHeldT = double.NaN, ArmShift, ArmShiftV, ArmGapL = Const.ARM_GAP_PARK, ArmGapR = Const.ARM_GAP_PARK;
    public static SimState Current = new();
    public void LogMsg(string txt, int lv = 1) {
        if (lv <= 1) Marks.Add(new LogEntry { T = T, M = txt, Lv = lv });
        Log.Insert(0, new LogEntry { T = T, M = txt, Lv = lv });
        if (Log.Count > 200) Log.RemoveAt(Log.Count - 1);
    }
    public bool Once(string key, Action fn = null) {
        if (!Events.ContainsKey(key)) { Events[key] = T; fn?.Invoke(); return true; }
        return false;
    }
    public static double PadAngle(double t) => -Const.W * Math.Max(t, 0);
    public static Vec2 PadPos(double t) {
        double a = PadAngle(t);
        return new Vec2(Const.RE * Math.Sin(a), Const.RE * Math.Cos(a));
    }
    public double Downrange(Vehicle v) => DownrangeAt(v, T);
    public static double DownrangeAt(Vehicle v, double t) => (PadAngle(t) - Math.Atan2(v.X, v.Y)) * Const.RE;
    public double NavDr(Vehicle v) => Downrange(v) + v.NavX - v.AimDr;
    public static double Surface(double dr) => Math.Abs(dr) < Const.MOUNT_R ? 0 : -Const.DECK_H;
    public static bool Water(double dr) => dr > Const.COAST_DR || dr < -Const.LAND_W;
    public Vehicle FocusVeh() => Focus == "ship" ? Veh[1] : Veh[0];
}
