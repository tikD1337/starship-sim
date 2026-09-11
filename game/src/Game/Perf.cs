using System;
using Godot;
namespace Starship.Game;
public sealed class Perf {
    public enum Part { Physics, Scene, Ui, Mission, Total }
    private readonly Node _host;
    private double[] _ms;
    private readonly double[] _part = new double[5];
    private int _n, _warm, _i, _slow, _gc0;
    private ulong _mark;
    private readonly System.Collections.Generic.List<Rid> _rids = new();
    private double _gpu;
    public Perf(Node host) { _host = host; }
    public bool Spin, Cold, KeepVsync;
    public void Arm(int frames) {
        if (!KeepVsync) DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        _warm = Cold ? 0 : 90;
        _n = Math.Max(frames, 30);
        _ms = new double[_n];
        _i = 0;
        _gpu = 0;
        _rids.Clear();
        _rids.Add(_host.GetViewport().GetViewportRid());
        foreach (Node n in _host.GetTree().Root.FindChildren("*", "SubViewport", true, false))
            _rids.Add(((SubViewport)n).GetViewportRid());
        foreach (Rid r in _rids) RenderingServer.ViewportSetMeasureRenderTime(r, true);
    }
    public void Mark() => _mark = Time.GetTicksUsec();
    public void Add(Part p) {
        if (_ms == null || _warm > 0) return;
        _part[(int)p] += (Time.GetTicksUsec() - _mark) / 1000.0;
        _mark = Time.GetTicksUsec();
    }
    public void Step(double delta) {
        if (_ms == null) return;
        if (_warm > 0) {
            if (--_warm == 0) _gc0 = GC.CollectionCount(0);
            return;
        }
        double ms = delta * 1000;
        _ms[_i++] = ms;
        foreach (Rid r in _rids) _gpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(r);
        if (ms > 16.7) _slow++;
        if (_i < _n) return;
        Report();
        _ms = null;
        _host.GetTree().Quit();
    }
    private static double Mon(Performance.Monitor m) => Performance.GetMonitor(m);
    private void Report() {
        var sorted = (double[])_ms.Clone();
        Array.Sort(sorted);
        double sum = 0;
        foreach (double v in sorted) sum += v;
        double avg = sum / _n;
        if (Cold) {
            var head = new System.Text.StringBuilder("PERF_COLD первые кадры, мс:");
            for (int k = 0; k < Math.Min(24, _n); k++) head.Append(' ').Append(_ms[k].ToString("F1"));
            GD.Print(head.ToString());
        }
        GD.Print($"PERF кадров={_n} среднее={avg:F2}мс медиана={sorted[_n / 2]:F2}мс "
                 + $"1%худших={sorted[(int)(_n * 0.99)]:F2}мс наибольший={sorted[_n - 1]:F2}мс "
                 + $"кадров/с={1000 / avg:F0}");
        var q = new System.Text.StringBuilder("PERF_ЧЕТВЕРТИ мс:");
        for (int k = 0; k < 4; k++) {
            int a = _n * k / 4, b = _n * (k + 1) / 4;
            double t = 0;
            for (int j = a; j < b; j++) t += _ms[j];
            q.Append(' ').Append((t / (b - a)).ToString("F2"));
        }
        GD.Print(q.ToString());
        GD.Print($"PERF_LAG кадров дороже 16,7мс={_slow} ({100.0 * _slow / _n:F1}%) "
                 + $"сборок мусора={GC.CollectionCount(0) - _gc0}");
        double own = _part[0] + _part[1] + _part[2] + _part[3];
        GD.Print($"PERF_PART физика={_part[0] / _n:F2} сцена={_part[1] / _n:F2} "
                 + $"приборы={_part[2] / _n:F2} задание={_part[3] / _n:F2} "
                 + $"всего своего={own / _n:F2}мс на кадр");
        GD.Print($"PERF_GPUMS рисование={_gpu / _n:F2}мс на кадр по {_rids.Count} окнам");
        GD.Print($"PERF_GPU вызовов={Mon(Performance.Monitor.RenderTotalDrawCallsInFrame):F0} "
                 + $"объектов={Mon(Performance.Monitor.RenderTotalObjectsInFrame):F0} "
                 + $"примитивов={Mon(Performance.Monitor.RenderTotalPrimitivesInFrame) / 1000:F0}тыс "
                 + $"узлов={Mon(Performance.Monitor.ObjectNodeCount):F0}");
        GD.Print($"PERF_MEM видео={Mon(Performance.Monitor.RenderVideoMemUsed) / 1e6:F0}МБ "
                 + $"текстуры={Mon(Performance.Monitor.RenderTextureMemUsed) / 1e6:F0}МБ "
                 + $"своя={Mon(Performance.Monitor.MemoryStatic) / 1e6:F0}МБ");
    }
}
