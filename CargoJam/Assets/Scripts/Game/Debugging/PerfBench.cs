#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CargoJam.Core;
using CargoJam.Game.UI;
using Unity.Profiling;
using UnityEngine;

namespace CargoJam.Game.Debugging
{
    /// <summary>
    /// Performance benchmark for development builds, started with <c>-perfbench &lt;report path&gt;</c>
    /// (optionally <c>-perfbench-level &lt;1-based level&gt;</c>). It renders at iPhone resolution through
    /// <see cref="PortraitPreview"/>, samples the home screen, then plays the level by itself (a legal
    /// move every <see cref="MoveInterval"/>s) and writes frame, GPU, draw-call and memory figures,
    /// then quits. Frame timings need Frame Timing Stats on in Player settings.
    /// </summary>
    public sealed class PerfBench : MonoBehaviour
    {
        const float Warmup = 3f;
        const float HomeSeconds = 10f;
        const float LevelSeconds = 25f;
        const float MoveInterval = 0.7f;

        string reportPath;
        int levelNumber = 38;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfAsked()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-perfbench");
            if (at < 0 || at + 1 >= args.Length) return;
            // Keep running while another app has focus, so the run doesn't stall unattended.
            Application.runInBackground = true;
            var bench = new GameObject("PerfBench").AddComponent<PerfBench>();
            bench.reportPath = args[at + 1];
            int level = Array.IndexOf(args, "-perfbench-level");
            if (level >= 0 && level + 1 < args.Length && int.TryParse(args[level + 1], out int n)) bench.levelNumber = n;
            DontDestroyOnLoad(bench.gameObject);
        }

        IEnumerator Start()
        {
            var report = new StringBuilder();
            report.AppendLine($"device: {SystemInfo.deviceModel} | gpu: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})");
            report.AppendLine($"quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]} | pipeline: {(QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline.name : "default")}");
            report.AppendLine($"render size: {PortraitPreview.Width}x{PortraitPreview.Height} | target fps: {Application.targetFrameRate}");

            yield return new WaitForSeconds(Warmup);
            PortraitPreview.Begin();
            yield return Sample("home", HomeSeconds, null, report);

            var flow = FindAnyObjectByType<GameFlow>();
            var controller = FindAnyObjectByType<GameController>();
            flow.JumpToLevel(levelNumber - 1);
            yield return new WaitForSeconds(Warmup);

            var moves = new List<Parcel>();
            float nextMove = 0f;
            yield return Sample($"level {levelNumber} autoplay", LevelSeconds, () =>
            {
                if (Time.unscaledTime < nextMove || controller.Session == null) return;
                nextMove = Time.unscaledTime + MoveInterval;
                controller.Session.GetMoves(moves);
                var move = moves.FirstOrDefault(p => controller.Session.CanTake(p));
                if (move != null) controller.Tap(move.Cell);
            }, report);

            report.AppendLine();
            report.AppendLine("== largest textures (MB, size, format, mips, msaa)");
            foreach (var t in Resources.FindObjectsOfTypeAll<Texture>()
                         .OrderByDescending(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t)).Take(25))
            {
                var rt = t as RenderTexture;
                report.AppendLine($"{UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t) / 1048576f:0.0}  {t.name}  {t.width}x{t.height}  {t.graphicsFormat}  mips {t.mipmapCount}  msaa {(rt != null ? rt.antiAliasing : 1)}");
            }

            PortraitPreview.End();
            var dir = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(reportPath, report.ToString());
            Application.Quit();
        }

        static readonly (ProfilerCategory category, string name, string label, double scale)[] Counters =
        {
            (ProfilerCategory.Render, "Draw Calls Count", "draw calls", 1),
            (ProfilerCategory.Render, "SetPass Calls Count", "setpass calls", 1),
            (ProfilerCategory.Render, "Triangles Count", "triangles", 1),
            (ProfilerCategory.Memory, "GC Allocated In Frame", "GC alloc/frame (bytes)", 1),
            (ProfilerCategory.Memory, "Total Used Memory", "total used (MB)", 1.0 / (1 << 20)),
            (ProfilerCategory.Memory, "Gfx Used Memory", "graphics used (MB)", 1.0 / (1 << 20)),
            (ProfilerCategory.Memory, "Texture Memory", "textures (MB)", 1.0 / (1 << 20)),
            (ProfilerCategory.Memory, "Mesh Memory", "meshes (MB)", 1.0 / (1 << 20)),
            (ProfilerCategory.Memory, "GC Reserved Memory", "managed heap (MB)", 1.0 / (1 << 20)),
        };

        static IEnumerator Sample(string phase, float seconds, Action eachFrame, StringBuilder report)
        {
            var recorders = Counters.Select(c => ProfilerRecorder.StartNew(c.category, c.name)).ToArray();
            var values = Counters.Select(_ => new List<double>()).ToArray();
            var cpu = new List<double>();
            var main = new List<double>();
            var render = new List<double>();
            var gpu = new List<double>();
            var timings = new FrameTiming[1];
            float end = Time.unscaledTime + seconds;
            int frames = 0;
            while (Time.unscaledTime < end)
            {
                eachFrame?.Invoke();
                yield return null;
                frames++;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                {
                    cpu.Add(timings[0].cpuFrameTime);
                    main.Add(timings[0].cpuMainThreadFrameTime);
                    render.Add(timings[0].cpuRenderThreadFrameTime);
                    if (timings[0].gpuFrameTime > 0) gpu.Add(timings[0].gpuFrameTime);
                }
                for (int i = 0; i < recorders.Length; i++)
                    if (recorders[i].Valid) values[i].Add(recorders[i].LastValue * Counters[i].scale);
            }

            report.AppendLine();
            report.AppendLine($"== {phase}: {frames} frames in {seconds:0}s ({frames / seconds:0.0} fps)");
            report.AppendLine(Line("cpu frame (ms)", cpu));
            report.AppendLine(Line("cpu main thread (ms)", main));
            report.AppendLine(Line("cpu render thread (ms)", render));
            report.AppendLine(Line("gpu frame (ms)", gpu));
            for (int i = 0; i < recorders.Length; i++)
            {
                report.AppendLine(Line(Counters[i].label, values[i]));
                recorders[i].Dispose();
            }
        }

        static string Line(string label, List<double> v)
        {
            if (v.Count == 0) return $"{label}: n/a";
            var sorted = v.OrderBy(x => x).ToList();
            double p95 = sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.95))];
            return $"{label}: avg {v.Average():0.##}  p95 {p95:0.##}  max {sorted[^1]:0.##}";
        }
    }
}
#endif
