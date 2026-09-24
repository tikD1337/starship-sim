using Starship.Physics;
namespace Starship.Game;
public enum Aim { Wait, Done, Fail, Part }
public static class MissionRules {
    public static bool Fallback(Vehicle v) => v.SeekPad && v.Site == "sea";
    public static Aim Catch(Vehicle v) {
        if (v.Crashed) return Aim.Fail;
        if (v.Caught) return Aim.Done;
        if (!v.Landed) return Aim.Wait;
        return Fallback(v) ? Aim.Part : Aim.Fail;
    }
    public static string Where(Vehicle v) => Fallback(v) ? "запасная посадка в море" : "";
    public static int Score(Aim state, int bas, int bonus) =>
        state == Aim.Done ? bas + bonus : state == Aim.Part ? bas / 2 : 0;
    public static string Mark(Vehicle v, string who) =>
        v.Crashed ? "потеря " + who
        : v.Caught ? "захват " + who
        : v.Catch ? "захват " + who : "приводнение " + who;
}
