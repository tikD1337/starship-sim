using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Starship.Physics;
namespace Starship.Tests;
internal static partial class Program {
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static int _ok, _fail;
    private static void Head(string s) {
        Console.WriteLine();
        Console.WriteLine("— " + s);
    }
    private static string N(double v) =>
        Math.Abs(v) >= 1e-3 && Math.Abs(v) < 1e6 ? v.ToString("G6", Inv) : v.ToString("G4", Inv);
    private static void True(string name, bool cond, string detail = "") {
        if (cond) _ok++; else _fail++;
        Console.WriteLine($"{(cond ? "  ok  " : "ПРОВАЛ")} {name}" + (detail.Length > 0 ? "   " + detail : ""));
    }
    private static void Near(string name, double got, double want, double rel, string unit = "") {
        bool ok = Math.Abs(got - want) <= Math.Abs(want) * rel + 1e-12;
        True(name, ok, $"{N(got)} против {N(want)}{unit}");
    }
    private static void Same(string name, string got, string want) =>
        True(name, got == want, $"«{got}» против «{want}»");
    private static void Group(string name, string detail, params (string What, bool Ok)[] parts) {
        string bad = string.Join("; ", parts.Where(p => !p.Ok).Select(p => p.What));
        True(name, bad.Length == 0, bad.Length == 0 ? detail : "не так: " + bad + (detail.Length > 0 ? " — " + detail : ""));
    }
    private static void Table(string name, params (string At, double Got, double Want, double Rel)[] rows) {
        bool ok = true;
        double worst = -1;
        string at = "";
        foreach ((string a, double got, double want, double rel) in rows) {
            double err = Math.Abs(got - want), tol = Math.Abs(want) * rel + 1e-12;
            ok &= err <= tol;
            if (err / tol > worst) { worst = err / tol; at = $"{a}: {N(got)} против {N(want)}"; }
        }
        True(name, ok, $"{rows.Length} точек, {(ok ? "ближе всех к допуску" : "мимо")} — {at}");
    }
    private static int Main() {
        Console.WriteLine("SimTests: физика, полёты и интерфейс без Godot.");
        var checks = new List<Action>();
        var runs = new List<Run>();
        FlightPlan(runs, checks);
        System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal;
        var par = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
        Task flights = Task.Run(() => Parallel.ForEach(runs, par, r => r.Go()));
        Atmosphere_();
        Drag();
        AeroForces();
        EngineThrust();
        ChamberCooling();
        PumpCavitation();
        Settling();
        Ullage();
        Slosh();
        LightHover();
        OpenLoop();
        RcsGas();
        HotStaging();
        Fins();
        Flaps();
        CenterOfMass();
        MomentOfInertia();
        OrbitElements();
        HeatShield();
        WindProfile();
        Dispersion_();
        ConditionEvents();
        MassBalance();
        EngineOut();
        BadNumbers();
        UiNumbers();
        UiThemes();
        UiEngineLayout();
        UiParams();
        UiEngine();
        UiAir();
        UiDirector();
        UiFallbackMarks();
        UiTabs();
        UiArmGeom();
        UiReplay();
        flights.Wait();
        foreach (Action c in checks) c();
        Console.WriteLine();
        Console.WriteLine($"итог: пройдено {_ok}, провалов {_fail}");
        return _fail == 0 ? 0 : 1;
    }
}
