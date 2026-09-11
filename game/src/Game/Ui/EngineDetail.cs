using System;
using System.Collections.Generic;
using Godot;
using PEngine = Starship.Physics.Engine;
namespace Starship.Game.Ui;
public sealed class EngineDetail {
    private Label _id, _where;
    private readonly Label[] _big = new Label[4];
    private readonly List<(Label Fuel, Label FuelU, Label Ox, Label OxU)> _cells = new();
    private readonly List<(ParamRow Row, ZoneGauge Gauge, Label Val)> _zones = new();
    public static EngineDetail Build(Control parent) {
        var d = new EngineDetail();
        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 0);
        parent.AddChild(col);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 14);
        col.AddChild(head);
        d._id = Look.Caption(head, "", 32, 300, () => Look.Ink);
        d._where = Look.Caption(head, "", 14, 400, () => Look.Lab);
        d._id.VerticalAlignment = VerticalAlignment.Bottom;
        d._where.VerticalAlignment = VerticalAlignment.Bottom;
        d._where.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        d._where.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        Gap(col, 14);
        var big = new GridContainer { Columns = 4 };
        big.AddThemeConstantOverride("h_separation", 10);
        col.AddChild(big);
        string[] names = { "Камера", "Тяга", "Режим", "Компоненты" };
        string[] units = { "МПа", "кН", "%", "O₂ / CH₄" };
        for (int i = 0; i < 4; i++) {
            var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            Look.Bind(card, x => x.AddThemeStyleboxOverride("panel", Look.Box(Look.Field, 10, 10, 8)));
            big.AddChild(card);
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 0);
            card.AddChild(v);
            Look.Caption(v, names[i], 13, 400, () => Look.Lab);
            d._big[i] = Look.Caption(v, "—", 24, 300, () => Look.Ink);
            Look.Caption(v, units[i], 12, 400, () => Look.Lab);
        }
        Gap(col, 16);
        var t = new GridContainer { Columns = 3 };
        t.AddThemeConstantOverride("h_separation", 10);
        t.AddThemeConstantOverride("v_separation", 10);
        col.AddChild(t);
        Look.Caption(t, "", 13, 400, null);
        Look.Caption(t, "Горючее", 13, 400, () => Look.Lab).HorizontalAlignment = HorizontalAlignment.Right;
        Look.Caption(t, "Окислитель", 13, 400, () => Look.Lab).HorizontalAlignment = HorizontalAlignment.Right;
        foreach (string lab in PumpTable.Labels) {
            Look.Caption(t, lab, 14, 400, () => Look.Lab).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            (Label a, Label au) = Cell(t);
            (Label b, Label bu) = Cell(t);
            d._cells.Add((a, au, b, bu));
        }
        Gap(col, 18);
        var z = new VBoxContainer();
        z.AddThemeConstantOverride("separation", 13);
        col.AddChild(z);
        d.Zone(z, "Подшипники", "_tb");
        d.Zone(z, "Вибрация", "_vb");
        d.Zone(z, "Износ крыльчаток", "_wf");
        return d;
    }
    private static void Gap(Control parent, float h) =>
        parent.AddChild(new Control { CustomMinimumSize = new Vector2(0, h), MouseFilter = Control.MouseFilterEnum.Ignore });
    private static (Label Val, Label Unit) Cell(Control grid) {
        var h = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        h.AddThemeConstantOverride("separation", 4);
        grid.AddChild(h);
        Label v = Look.Caption(h, "—", 15, 500, null);
        Label u = Look.Caption(h, "", 12, 400, () => Look.Lab);
        v.VerticalAlignment = VerticalAlignment.Bottom;
        u.VerticalAlignment = VerticalAlignment.Bottom;
        return (v, u);
    }
    private void Zone(Control parent, string name, string key) {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 12);
        parent.AddChild(h);
        Look.Caption(h, name, 14, 400, () => Look.Lab).CustomMinimumSize = new Vector2(150, 0);
        ParamRow r = ParamDefs.Row(key);
        ZoneGauge g = ZoneGauge.Make(h, r);
        Label v = Look.Caption(h, "—", 15, 500, null);
        v.CustomMinimumSize = new Vector2(88, 0);
        v.HorizontalAlignment = HorizontalAlignment.Right;
        _zones.Add((r, g, v));
    }
    public void Update(PEngine e) {
        Look.Set(_id, e.Name);
        Look.Set(_where, e.IsVac ? "вакуумный Raptor" : e.Ring + ", Raptor на уровне моря");
        Look.Set(_big[0], NumFmt.F(e.Pc, 1));
        Look.Set(_big[1], NumFmt.F(e.F / 1000, 0));
        Look.Set(_big[2], NumFmt.F(e.Spool * 100, 0));
        Look.Set(_big[3], NumFmt.F(ParamDefs.Row("_mr").FromEngine(e), 2));
        PumpRow[] rows = PumpTable.Of(e);
        for (int i = 0; i < rows.Length && i < _cells.Count; i++) {
            (Label a, Label au, Label b, Label bu) = _cells[i];
            Look.Set(a, rows[i].Fuel);
            Look.Set(b, rows[i].Ox);
            Look.Set(au, rows[i].Unit);
            Look.Set(bu, rows[i].Unit);
            Look.Tint(a, rows[i].FuelWarn ? Look.Warn : Look.Ink);
            Look.Tint(b, rows[i].OxWarn ? Look.Warn : Look.Ink);
        }
        foreach ((ParamRow r, ZoneGauge g, Label v) in _zones) {
            double x = r.FromEngine(e);
            string txt;
            if (r.Key == "_wf") {
                double wo = ParamDefs.Row("_wo").FromEngine(e);
                txt = $"{NumFmt.F(x * 100, 0)} / {NumFmt.F(wo * 100, 0)} %";
                x = Math.Max(x, wo);
            }
            else txt = NumFmt.F(x, r.Digits) + " " + r.Unit;
            g.Set(x);
            Look.Set(v, txt);
            string zone = ParamDefs.ZoneOf(r, x);
            Look.Tint(v, zone == "crit" ? Look.Crit : zone == "warn" ? Look.Warn : Look.Ink);
        }
    }
}
