using System;
namespace Starship.Physics;
public sealed class PumpNom {
    public double Rpm, DP, Rho, Eta, PIn, PVap, Npsh;
}
public sealed class Pump {
    public const double MR = 3.6;
    public const double RHO_F = 423, RHO_OX = 1141;
    public const double PVAP_F = 101e3, PVAP_OX = 105e3;
    public const double T_CRYO = 111, PC_NOM = 30.0;
    public static readonly PumpNom NomF = new()
    { Rpm = 32394, DP = 65e6, Rho = RHO_F, Eta = 0.750, PIn = 350e3, PVap = PVAP_F, Npsh = 38 };
    public static readonly PumpNom NomO = new()
    { Rpm = 22000, DP = 55e6, Rho = RHO_OX, Eta = 0.765, PIn = 380e3, PVap = PVAP_OX, Npsh = 19 };
    public bool IsFuel;
    public Engine Eng;
    public PumpNom N;
    public double Rpm, T = T_CRYO, Vib, Wear, Cav = 1;
    public double Head, Q, P, DP, Eta, NpshR;
    public double MdotNom, QNom, HeadNom, TqNom, J;
    public Pump(bool isFuel, Engine eng, double share) {
        IsFuel = isFuel;
        Eng = eng;
        N = isFuel ? NomF : NomO;
        Eta = N.Eta;
        MdotNom = eng.MdotNom * share;
        QNom = MdotNom / N.Rho;
        HeadNom = N.DP / (N.Rho * Const.G0);
        TqNom = N.DP * QNom / N.Eta / (N.Rpm * Math.PI / 30);
        J = isFuel ? 2.6 : 3.4;
    }
    public double PIn {
        get {
            Vehicle v = Eng.Veh;
            if (v == null || v.Tanks == null) return N.PIn;
            if (v.OnHeader) return 400e3;
            Tank t = IsFuel ? v.Tanks.F : v.Tanks.O;
            return Math.Max(3e3, t.P);
        }
    }
    public void Update(double dt, double thr, bool running) {
        EngineParams p = Eng.P;
        double hk = IsFuel ? p.FHead : p.OHead;
        double ek = IsFuel ? p.FEff : p.OEff;
        double set = IsFuel ? p.RpmSet : p.RpmSet * N.Rpm / NomF.Rpm;
        double rpmSet = running ? set * (0.45 + 0.55 * thr) : 0;
        double rel = Rpm / N.Rpm;
        double npshA = (PIn - N.PVap) / (N.Rho * Const.G0);
        NpshR = N.Npsh * rel * rel * 1.02;
        Cav = NpshR > 0.1 ? Const.Clamp(npshA / NpshR, 0, 1) : 1;
        double valve = Const.Clamp(IsFuel ? p.ValveF : p.ValveOx, 0, 1.2);
        Head = HeadNom * rel * rel * hk * valve * (1 - 0.35 * Wear) * Math.Pow(Cav, 1.5);
        Q = QNom * rel * (0.8 + 0.2 * thr);
        Eta = Const.Clamp(N.Eta * ek * (1 - 0.25 * Wear) * (0.55 + 0.45 * Cav), 0.12, 0.95);
        DP = N.Rho * Const.G0 * Head;
        P = DP * Q / Eta;
        double w = Rpm * Math.PI / 30, wSet = rpmSet * Math.PI / 30;
        double tqP = w > 40 ? P / w : 0;
        double tqL = 0.035 * TqNom * rel * rel * p.Mech;
        double tqT = 0;
        if (running && rpmSet > 10)
            tqT = Const.Clamp((wSet - w) / Math.Max(wSet, 1) * 3.2 * TqNom + tqP + tqL,
                              0, 1.75 * TqNom * p.Torque);
        double acc = (tqT - tqP - tqL) / J;
        if (p.AccLim > 0) acc = Const.Clamp(acc, -p.AccLim, p.AccLim);
        Rpm += acc * dt * 30 / Math.PI;
        if (Rpm < 0) Rpm = 0;
        if (p.RpmDirect.HasValue) Rpm = p.RpmDirect.Value;
        double fric = 25e3 * rel * rel * p.Mech;
        T += (fric - 120 * p.Heat * (T - T_CRYO)) / 2000 * dt;
        if (p.TDirect.HasValue) T = p.TDirect.Value;
        Vib = (0.15 + 4.2 * Wear) * rel * rel + (1 - Cav) * 9 + p.VibAdd;
        if (Vib > 2.5) Wear = Math.Min(1, Wear + (Vib - 2.5) * 2.2e-4 * dt);
        if (T > 700) Eng.Fail("перегрев подшипников " + T.ToString("F0") + " K");
        if (Vib > 12) Eng.Fail("разрушение крыльчатки, вибрация " + Vib.ToString("F1") + " g");
    }
}
