namespace Starship.Physics;
public sealed class EngineParams {
    public double RpmSet = 32394, AccLim = 0, Torque = 1, Mech = 1, Heat = 1, VibAdd = 0, FHead = 1,
        FEff = 1, OHead = 1, OEff = 1;
    public double? RpmDirect = null, TDirect = null;
    public double ValveF = 1, ValveOx = 1, PTankF = 350, PTankOx = 380, MinThr = 40, Cf = 1, AeK = 1,
        Tau = 0.28, IgnDelay = 0, Cool = 1;
}
public static class Log {
    public static System.Action<string, int> Sink;
    public static void Msg(string text, int level = 0) => Sink?.Invoke(text, level);
}
