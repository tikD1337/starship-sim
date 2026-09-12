using Starship.Physics;
namespace Starship.Game.Ui;
public static class Rail {
    public sealed record Mark(string Name, bool Past, bool Next);
    private static readonly string[] Names = { "старт", "max Q", "разделение", "орбита" };
    public static Mark[] Of(SimState sim) {
        Vehicle b = sim.Veh[0], s = sim.Veh[1];
        Orbit o = Guidance.Orb(s);
        bool[] past = {
            b.Launched || sim.T >= 0,
            sim.Events.ContainsKey("maxq"),
            !s.Attached,
            s.Mode == "orbit" || o.Peri > 0,
        };
        int next = -1;
        for (int i = 0; i < past.Length; i++)
            if (!past[i]) { next = i; break; }
        var marks = new Mark[past.Length];
        for (int i = 0; i < past.Length; i++) marks[i] = new Mark(Names[i], past[i], i == next);
        return marks;
    }
}
