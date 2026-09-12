using System;
using System.Collections.Generic;
using Godot;
using Starship.Physics;
namespace Starship.Game.Ui;
public sealed class TankPanel {
    private sealed class Block {
        public Label Name, Info, LoxPct, Ch4Pct;
        public ProgressBar Lox, Ch4;
        public VBoxContainer Extra;
        public readonly List<(ParamRow Row, ZoneGauge Gauge, Label Val)> Zones = new();
        public KvRow Gas, Hdr;
    }
    private static readonly (string Name, string Key)[] Gauges = {
        ("Наддув горючего", "_pf"), ("Наддув окислителя", "_po"), ("Баллоны наддува", "_copv"),
    };
    private readonly Block[] _b = new Block[2];
    public static TankPanel Build(Control parent) {
        var t = new TankPanel();
        VBoxContainer body = Widgets.Section(parent, "Баки и наддув").Body;
        t._b[0] = MakeBlock(body);
        body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 9), MouseFilter = Control.MouseFilterEnum.Ignore });
        t._b[1] = MakeBlock(body);
        return t;
    }
    private static Block MakeBlock(VBoxContainer body) {
        var b = new Block();
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        body.AddChild(v);
        var hd = new HBoxContainer();
        v.AddChild(hd);
        b.Name = Look.Caption(hd, "", 16, 600, () => Look.Ink);
        b.Name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        b.Name.VerticalAlignment = VerticalAlignment.Bottom;
        b.Info = Look.Caption(hd, "", 14, 400, () => Look.Ink2);
        b.Info.VerticalAlignment = VerticalAlignment.Bottom;
        (b.Lox, b.LoxPct) = FillRow(v, "LOX");
        (b.Ch4, b.Ch4Pct) = FillRow(v, "CH4");
        b.Extra = new VBoxContainer();
        b.Extra.AddThemeConstantOverride("separation", 8);
        v.AddChild(b.Extra);
        foreach ((string name, string key) in Gauges) {
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 12);
            b.Extra.AddChild(h);
            Look.Caption(h, name, 14, 400, () => Look.Lab).CustomMinimumSize = new Vector2(140, 0);
            ParamRow r = ParamDefs.Row(key);
            ZoneGauge g = ZoneGauge.Make(h, r);
            Label val = Look.Caption(h, "—", 15, 500, null);
            val.CustomMinimumSize = new Vector2(80, 0);
            val.HorizontalAlignment = HorizontalAlignment.Right;
            b.Zones.Add((r, g, val));
        }
        b.Gas = KvRow.Make(b.Extra, "Газ в подушках");
        b.Hdr = KvRow.Make(b.Extra, "Головные баки");
        return b;
    }
    private static (ProgressBar Bar, Label Pct) FillRow(Control parent, string name) {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 10);
        parent.AddChild(h);
        Look.Caption(h, name, 13, 400, () => Look.Lab).CustomMinimumSize = new Vector2(40, 0);
        var bar = new ProgressBar {
            MinValue = 0, MaxValue = 1, Step = 0, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 6),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        Look.Bind(bar, x => {
            x.AddThemeStyleboxOverride("background", Look.Box(Look.Trace, 3, 0, 0));
            x.AddThemeStyleboxOverride("fill", Look.Box(Look.Ink, 3, 0, 0));
        });
        h.AddChild(bar);
        Label pct = Look.Caption(h, "", 14, 500, () => Look.Ink);
        pct.CustomMinimumSize = new Vector2(52, 0);
        pct.HorizontalAlignment = HorizontalAlignment.Right;
        return (bar, pct);
    }
    public void Update(SimState sim, int stage) {
        Fill(_b[0], sim.Veh[stage], true);
        Fill(_b[1], sim.Veh[1 - stage], false);
    }
    private static void Fill(Block b, Vehicle v, bool full) {
        Look.Set(b.Name, Widgets.StageName(v));
        string rest = full ? "запас Δv " + NumFmt.F(ParamDefs.Row("_dv").FromVehicle(v), 0) + " м/с"
                    : v.Fill >= 0.999 ? "баки полные" : "топливо " + NumFmt.F(v.Fill * 100, 0) + " %";
        Look.Set(b.Info, NumFmt.F(v.Prop / 1000, 1) + " т, " + rest);
        double fill = Math.Clamp(v.Fill, 0, 1);
        b.Lox.Value = fill;
        b.Ch4.Value = fill;
        string pct = NumFmt.F(fill * 100, 0) + " %";
        Look.Set(b.LoxPct, pct);
        Look.Set(b.Ch4Pct, pct);
        b.Extra.Visible = full;
        if (!full) return;
        foreach ((ParamRow r, ZoneGauge g, Label val) in b.Zones) {
            double x = r.FromVehicle(v);
            g.Set(x);
            Look.Set(val, NumFmt.F(x, 0) + " " + r.Unit);
            string z = ParamDefs.ZoneOf(r, x);
            Look.Tint(val, z == "crit" ? Look.Crit : z == "warn" ? Look.Warn : Look.Ink);
        }
        b.Gas.Set(NumFmt.F(ParamDefs.Row("_gas").FromVehicle(v), 0), "кг");
        b.Hdr.Set(v.Hdr <= 0 ? "нет" : v.HdrLeft <= 0 ? "пусты" : NumFmt.F(v.HdrLeft / 1000, 1),
                  v.Hdr > 0 && v.HdrLeft > 0 ? "т" : "");
    }
}
