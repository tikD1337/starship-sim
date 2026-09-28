using System;
namespace Starship.Game.Ui;
public static class Warp {
    public const double Min = 0.125, Fine = 64, Coast = 1024;
    public static double Up(double speed, bool coast) => Math.Min(coast ? Coast : Fine, speed * 2);
    public static double Down(double speed) => Math.Max(Min, speed / 2);
    public static double Limit(double speed, bool coast) => coast ? speed : Math.Min(Fine, speed);
}
