using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CargoJam.Core;

namespace CargoJam.LevelGen
{
    /// <summary>
    /// Generates and grades Cargo Jam levels with the game's own rules. See README.md.
    /// <code>
    /// levelgen [generate] [--seed N] [--levels 2-60] [--out DIR] [--dry-run]
    /// levelgen grade [--out DIR]
    /// levelgen explore --levels 30
    /// </code>
    /// </summary>
    static class Program
    {
        const int DefaultSeed = 20261026;
        const int SearchRuns = 120;
        const int ConfirmRuns = 1000;
        const int GradeRuns = 1000;
        const int GradeSeed = 777;

        sealed class Options
        {
            public string Command = "generate";
            public int Seed = DefaultSeed;
            public int First = 1;
            public int Last = LevelPlan.LevelCount;
            public string Out;
            public bool DryRun;
        }

        sealed class Result
        {
            public LevelPlan Plan;
            public LevelData Data;
            public LevelDefinition Level;
            public BotStats Stats;
            public int SolverNodes;
            public double Noise;
        }

        static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            var o = ParseArgs(args);
            if (o == null) return 2;
            o.Out ??= Path.Combine(FindRepo(), "CargoJam", "Assets", "Resources", "Levels");

            switch (o.Command)
            {
                case "grade": return Grade(o);
                case "explore": return Explore(o);
                default: return Generate(o);
            }
        }

        // ---- generate ---------------------------------------------------------------------------

        static int Generate(Options o)
        {
            var watch = Stopwatch.StartNew();
            int count = o.Last - o.First + 1;
            var results = new Result[count];
            Parallel.For(0, count, i => results[i] = MakeLevel(o.First + i, o.Seed));

            bool ok = true;
            foreach (var r in results)
            {
                if (r == null) { ok = false; continue; }
                if (o.DryRun) continue;
                Directory.CreateDirectory(o.Out);
                File.WriteAllText(Path.Combine(o.Out, $"level_{r.Plan.Id:000}.json"), LevelJson.Write(r.Data));
            }
            PrintTable(results);
            Console.WriteLine($"{(o.DryRun ? "dry run, nothing written" : "wrote " + o.Out)} in {watch.Elapsed.TotalSeconds:0.0}s");
            return ok ? 0 : 1;
        }

        static Result MakeLevel(int id, int seed)
        {
            if (id == 1) return Finish(Level1(), null, 0);
            var plan = LevelPlan.For(id);
            if (id == 2) return Finish(Level2(), plan, 0);
            if (id == 3) return Finish(Level3(), plan, 0);

            var rng = new Random(seed * 7919 + id);
            double lo = 0, hi = plan.Parcels * 0.8;
            const double tolerance = 0.015;

            Result best = null;
            double bestError = double.MaxValue;
            for (int round = 0; round < 8 && bestError > tolerance; round++)
            {
                var pool = new List<(LevelData data, LevelDefinition level, double noise, BotStats stats)>();
                for (int attempt = 0; attempt < 48; attempt++)
                {
                    double noise = lo + (hi - lo) * rng.NextDouble();
                    var data = BoardMaker.Make(plan, new Random(rng.Next()), noise);
                    if (data == null) continue;
                    var level = LevelDefinition.Parse(data);
                    var stats = CasualBot.Grade(level, SearchRuns, rng.Next());
                    if (stats.StuckRate > 0.02) continue;
                    pool.Add((data, level, noise, stats));
                }
                pool.Sort((a, b) => Error(a.stats, plan).CompareTo(Error(b.stats, plan)));

                for (int i = 0; i < pool.Count && i < 6; i++)
                {
                    var c = pool[i];
                    var stats = CasualBot.Grade(c.level, ConfirmRuns, rng.Next());
                    double error = Error(stats, plan);
                    if (stats.StuckRate > 0.01 || error >= bestError) continue;
                    var solved = Solver.Solve(c.level);
                    if (!solved.Solvable) continue;
                    bestError = error;
                    best = new Result { Plan = plan, Data = c.data, Level = c.level, Noise = c.noise, SolverNodes = solved.NodesVisited };
                }

                // Narrow the noise range around the best so far for the next round.
                if (best != null)
                {
                    double span = (hi - lo) * 0.35;
                    lo = Math.Max(0, best.Noise - span);
                    hi = best.Noise + span;
                }
            }

            if (best == null)
            {
                Console.Error.WriteLine($"level {id}: no solvable candidate found");
                return null;
            }
            return Finish(best.Data, plan, best.Noise);
        }

        static double Error(BotStats stats, LevelPlan plan) => Math.Abs(stats.FailRate - plan.TargetFail);

        static Result Finish(LevelData data, LevelPlan plan, double noise)
        {
            var level = LevelDefinition.Parse(data);
            var solved = Solver.Solve(level);
            if (!solved.Solvable) throw new InvalidOperationException($"level {data.id} is not solvable");
            plan ??= new LevelPlan
            {
                Id = data.id, Kind = "tutorial", Width = level.Width, Height = level.Height,
                Colors = 1, Slots = data.holdingSlots, TargetFail = 0,
            };
            return new Result
            {
                Plan = plan,
                Data = data,
                Level = level,
                Noise = noise,
                SolverNodes = solved.NodesVisited,
                Stats = CasualBot.Grade(level, GradeRuns, GradeSeed + data.id),
            };
        }

        /// <summary>One color, and every parcel free from the start: the hand pointer level.</summary>
        static LevelData Level1() => new LevelData
        {
            id = 1,
            hard = false,
            holdingSlots = 5,
            truckCapacity = LevelPlan.TruckCapacity,
            trucks = "RR",
            rows = new[] { "R.R", "R.R", "R.R" },
        };

        /// <summary>
        /// The blocking lesson: the front-row reds fill the first truck and open the blue row behind them;
        /// each truck's color is reachable in turn, so the dock is never needed.
        /// </summary>
        static LevelData Level2() => new LevelData
        {
            id = 2,
            hard = false,
            holdingSlots = 5,
            truckCapacity = LevelPlan.TruckCapacity,
            trucks = "RBRB",
            rows = new[] { "RRRB", "BBBR", "RRBB" },
        };

        /// <summary>
        /// The dock lesson: the first truck wants red, and every red sits behind the blue front row,
        /// so the first move has to park a blue.
        /// </summary>
        static LevelData Level3()
        {
            var data = new LevelData
            {
                id = 3,
                hard = false,
                holdingSlots = 5,
                truckCapacity = LevelPlan.TruckCapacity,
                trucks = "RBRB",
                rows = new[] { "BBBB", "RRRR", "BBRR" },
            };
            if (new GameSession(LevelDefinition.Parse(data)).HasFreeMatch())
                throw new InvalidOperationException("level 3 must start with no free match");
            return data;
        }

        // ---- grade ------------------------------------------------------------------------------

        static int Grade(Options o)
        {
            var files = Directory.GetFiles(o.Out, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
            var results = new Result[files.Length];
            bool ok = true;
            Parallel.For(0, files.Length, i =>
            {
                var data = LevelJson.Read(files[i]);
                var errors = LevelDefinition.Validate(data, out var level);
                if (errors.Count > 0)
                {
                    Console.Error.WriteLine($"{Path.GetFileName(files[i])}: {string.Join("; ", errors)}");
                    ok = false;
                    return;
                }
                var solved = Solver.Solve(level);
                if (!solved.Solvable)
                {
                    Console.Error.WriteLine($"{Path.GetFileName(files[i])}: not solvable (nodes {solved.NodesVisited})");
                    ok = false;
                }
                LevelPlan plan = data.id >= 2 && data.id <= LevelPlan.LevelCount ? LevelPlan.For(data.id) : null;
                plan ??= new LevelPlan { Id = data.id, Kind = "tutorial" };
                results[i] = new Result
                {
                    Plan = plan,
                    Data = data,
                    Level = level,
                    SolverNodes = solved.NodesVisited,
                    Stats = CasualBot.Grade(level, GradeRuns, GradeSeed + data.id),
                };
            });
            PrintTable(results);
            return ok ? 0 : 1;
        }

        // ---- explore: how the bot fail rate responds to noise, for tuning plans ------------------

        static int Explore(Options o)
        {
            for (int id = Math.Max(2, o.First); id <= o.Last; id++)
            {
                var plan = LevelPlan.For(id);
                var rng = new Random(o.Seed + id);
                Console.Write($"L{id:00} {plan.Kind,-8} {plan.Width}x{plan.Height} c{plan.Colors} s{plan.Slots} target {plan.TargetFail:0.00} |");
                for (int step = 0; step <= 8; step++)
                {
                    double noise = plan.Parcels * 0.8 * step / 8;
                    double sum = 0;
                    int boards = 0;
                    for (int b = 0; b < 6; b++)
                    {
                        var data = BoardMaker.Make(plan, new Random(rng.Next()), noise);
                        if (data == null) continue;
                        sum += CasualBot.Grade(LevelDefinition.Parse(data), 100, rng.Next()).FailRate;
                        boards++;
                    }
                    Console.Write($" {noise,4:0}:{(boards == 0 ? double.NaN : sum / boards):0.00}");
                }
                Console.WriteLine();
            }
            return 0;
        }

        // ---- output -----------------------------------------------------------------------------

        static void PrintTable(Result[] results)
        {
            Console.WriteLine("| Level | Kind | Size | Colors | Trucks | Slots | Hard | Mystery | Locks | Target | Bot fail | Solver nodes |");
            Console.WriteLine("|---:|---|---|---:|---:|---:|---|---:|---:|---:|---:|---:|");
            foreach (var r in results)
            {
                if (r == null) continue;
                var l = r.Level;
                int mystery = 0;
                var colors = new HashSet<int>();
                for (int y = 0; y < l.Height; y++)
                for (int x = 0; x < l.Width; x++)
                {
                    if (l.IsMystery(x, y)) mystery++;
                    if (l.ColorAt(x, y) != ParcelColors.None) colors.Add(l.ColorAt(x, y));
                }
                string stuck = r.Stats.Stuck > 0 ? $" ({r.Stats.StuckRate:0.000} stuck)" : "";
                Console.WriteLine($"| {r.Data.id} | {r.Plan.Kind} | {l.Width}x{l.Height} | {colors.Count} | {l.Trucks.Count} | {l.HoldingSlots} | " +
                                  $"{(l.Hard ? "yes" : "")} | {mystery} | {l.Locks.Count} | {r.Plan.TargetFail:0.00} | {r.Stats.FailRate:0.000}{stuck} | {r.SolverNodes} |");
            }
        }

        static Options ParseArgs(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{a} needs a value");
                switch (a)
                {
                    case "generate":
                    case "grade":
                    case "explore":
                        o.Command = a;
                        break;
                    case "--seed":
                        o.Seed = int.Parse(Next(), CultureInfo.InvariantCulture);
                        break;
                    case "--levels":
                    {
                        var parts = Next().Split('-');
                        o.First = int.Parse(parts[0], CultureInfo.InvariantCulture);
                        o.Last = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : o.First;
                        break;
                    }
                    case "--out":
                        o.Out = Path.GetFullPath(Next());
                        break;
                    case "--dry-run":
                        o.DryRun = true;
                        break;
                    default:
                        Console.Error.WriteLine($"unknown argument {a}");
                        return null;
                }
            }
            return o;
        }

        /// <summary>The repo root: the nearest folder above the working directory that holds CargoJam/Assets.</summary>
        static string FindRepo()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                    if (Directory.Exists(Path.Combine(dir.FullName, "CargoJam", "Assets"))) return dir.FullName;
            }
            throw new DirectoryNotFoundException("run levelgen from inside the repo, or pass --out");
        }
    }
}
