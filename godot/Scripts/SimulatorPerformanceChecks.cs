using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

/// <summary>Benchmark randat cu aceeași grafică, fără limita monitorului.</summary>
public static class SimulatorPerformanceChecks
{
    public static async Task Run(Simulation game)
    {
        try
        {
            if (DisplayServer.GetName()=="headless") throw new Exception("--fps-test requires a rendered window");
            // Diagnostic override for comparison; does not change project settings.
            if (Array.IndexOf(OS.GetCmdlineUserArgs(),"--fps-120")>=0) Engine.PhysicsTicksPerSecond=120;
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps=0;
            game.Testing=true; game.Practice=true;
            foreach (string scenario in new[] {"rest", "drive", "bots"})
            {
                game.Reset(); foreach (var robot in game.Robots) robot.Bot=scenario=="bots";
                var warmup=Stopwatch.StartNew();
                while (warmup.Elapsed.TotalSeconds<1)
                    await game.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                double elapsedBefore=game.Elapsed;
                long allocatedBefore=GC.GetTotalAllocatedBytes(false);
                var frames=new List<double>(); double physicsMs=0;
                var watch=Stopwatch.StartNew(); double previous=0;
                while (watch.Elapsed.TotalSeconds<4)
                {
                    if (scenario=="drive") game.Player.Command=((int)watch.Elapsed.TotalSeconds%2==0 ? Vector3.Right : Vector3.Left);
                    await game.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    double now=watch.Elapsed.TotalSeconds;
                    frames.Add((now-previous)*1000); previous=now;
                    physicsMs+=Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess)*1000;
                }
                watch.Stop(); frames.Sort();
                double percentile(double p) => frames[Math.Min(frames.Count-1,(int)(p*frames.Count))];
                GD.Print($"FPS {scenario}: {frames.Count/watch.Elapsed.TotalSeconds:0.0}; frame p95/p99 {percentile(.95):0.00}/{percentile(.99):0.00} ms; physics monitor mean {physicsMs/frames.Count:0.00} ms; simulated/wall {(game.Elapsed-elapsedBefore)/watch.Elapsed.TotalSeconds:0.000}; allocations {(GC.GetTotalAllocatedBytes(false)-allocatedBefore)/watch.Elapsed.TotalSeconds/1048576:0.00} MiB/s; ticks {Engine.PhysicsTicksPerSecond}");
            }
            GD.Print("FPS BENCHMARK COMPLETE"); game.GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("FPS BENCHMARK FAILURE: "+ex); game.GetTree().Quit(1); }
    }
}
