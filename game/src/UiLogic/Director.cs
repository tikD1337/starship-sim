using System;
namespace Starship.Game.Ui;
public static class Director {
    public const double MinHold = 6, MaxHold = 15, Margin = 0.15;
    public readonly record struct Shot(int Cam, double Earth, double Flame, double Plasma, double Sun,
                                       double Skin, bool Night, double Other, double Since);
    public static double Score(Shot s) {
        double v = 0.35 * s.Earth + 0.30 * s.Flame + 0.40 * s.Plasma + 0.25 * s.Other;
        if (s.Sun < 20) v -= 0.5 * (1 - Math.Max(0, s.Sun) / 20);
        if (s.Skin > 0.75) v -= 0.4 * (s.Skin - 0.75) / 0.25;
        if (s.Night) v -= 0.6;
        return v + Math.Min(0.2, s.Since / 300);
    }
    public static int Pick(Shot[] shots, int cur, double held, bool cut) {
        if (shots == null || shots.Length == 0) return cur;
        int best = shots[0].Cam, second = int.MinValue;
        double bs = double.MinValue, ss = double.MinValue, curScore = double.MinValue;
        for (int i = 0; i < shots.Length; i++) {
            Shot s = shots[i];
            double v = Score(s) + 0.12 * (1 - (double)i / Math.Max(1, shots.Length - 1));
            if (s.Cam == cur) curScore = v;
            if (v > bs) {
                second = best;
                ss = bs;
                best = s.Cam;
                bs = v;
            }
            else if (v > ss) {
                second = s.Cam;
                ss = v;
            }
        }
        if (curScore == double.MinValue) return best;
        if (cut || held > MaxHold)
            return best != cur ? best : second != int.MinValue ? second : cur;
        if (held < MinHold) return cur;
        return best != cur && bs > curScore + Margin ? best : cur;
    }
}
