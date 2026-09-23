using System;
using Starship.Physics;
namespace Starship.Game.Ui;
public static class ArmGeom {
    public const double VehR = 4.73, RailR = 0.6, Inset = 1.2, HingeZ = 6.53, Reach = 23.0, Length = 34.4;
    public const double Ready = Const.ARM_GAP_READY, Park = Const.ARM_GAP_PARK;
    public static double Angle(double gap) {
        double c = HingeZ + gap, r = Math.Sqrt(Reach * Reach + HingeZ * HingeZ);
        return Math.Asin(Math.Clamp(c / r, -1, 1)) - Math.Atan2(HingeZ, Reach);
    }
    public static double RailDist(double angle) => Reach * Math.Sin(angle) + HingeZ * Math.Cos(angle) - Inset;
}
