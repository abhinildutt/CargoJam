using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CargoJam.Core;

namespace CargoJam.LevelGen
{
    /// <summary>
    /// Generates and grades Cargo Jam levels with the game's own rules. See README.md.
    /// <code>
    /// levelgen [generate] [--seed N] [--levels 2-100] [--out DIR] [--dry-run]
    /// levelgen grade [--out DIR]
    /// levelgen explore --levels 30
    /// </code>
    /// </summary>
    static class Program
    {
        const int DefaultSeed = 20261026;
        const int SearchRuns = 60;
        const int ConfirmRuns = 300;
        const int GradeRuns = 1000;
        const int GradeSeed = 777;
        const int SmartGradeRuns = 300;
        const int GreedyRuns = 200;
        /// <summary>How far the search may push the round-robin truck deal (see BoardMaker.Make).</summary>
        const double MaxSpread = 0.8;
        /// <summary>On express levels, express misses may be at most this share of the bot's fails.</summary>
        const double MaxExpressShare = 0.5;

        sealed class Options
        {
            public string Command = "generate";
            public int Seed = DefaultSeed;
            public int First = 1;
            public int Last = LevelPlan.LevelCount;
            public string Out;
            public bool DryRun;
            public int Level;
            public string Moves;
            public string File;
        }

        sealed class Result
        {
            public LevelPlan Plan;
            public LevelData Data;
            public LevelDefinition Level;
            public BotStats Stats;
            public SmartStats Smart;
            public int SolverNodes;
            public bool HitNodeLimit;
            /// <summary>The fewest waiting slots perfect play needs (searched down from the level's own).</summary>
            public int SlotsNeeded;
            public double GreedyFail;
            public double Noise;
            public double Spread;
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
                case "play": return Play(o);
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
            ok &= CheckGrades(results);
            Console.WriteLine($"{(o.DryRun ? "dry run, nothing written" : "wrote " + o.Out)} in {watch.Elapsed.TotalSeconds:0.0}s");
            return ok ? 0 : 1;
        }

        static Result MakeLevel(int id, int seed)
        {
            if (id == 1) return Finish(Level1(), null, 0);
            var plan = LevelPlan.For(id);
            if (id == 2) return Finish(Level2(), plan, 0);
            if (id == 3) return Finish(Level3(), plan, 0);

            while (true)
            {
                var best = Search(plan, seed);
                if (best != null) return Finish(best.Data, plan, best.Noise);
                if (!plan.DropOneCargoItem())
                {
                    Console.Error.WriteLine($"level {id}: no solvable candidate found");
                    return null;
                }
                Console.Error.WriteLine($"level {id}: no candidate, retrying with one fewer cargo item");
            }
        }

        /// <summary>Searches noise over many candidate boards for the one whose bot fail rate is closest to the plan's.</summary>
        static Result Search(LevelPlan plan, int seed)
        {
            var rng = new Random(seed * 7919 + plan.Id);
            double lo = 0, hi = plan.Parcels * 0.8;
            double spreadLo = 0, spreadHi = MaxSpread;
            const double tolerance = 0.03;
            bool express = plan.Express > 0;

            Result best = null;
            double bestError = double.MaxValue;
            for (int round = 0; round < 8 && bestError > tolerance; round++)
            {
                var pool = new List<(LevelData data, LevelDefinition level, double noise, double spread, SmartStats stats)>();
                for (int attempt = 0; attempt < 48; attempt++)
                {
                    double noise = lo + (hi - lo) * rng.NextDouble();
                    double spread = spreadLo + (spreadHi - spreadLo) * rng.NextDouble();
                    var data = BoardMaker.Make(plan, new Random(rng.Next()), noise, spread);
                    if (data == null) continue;
                    var level = LevelDefinition.Parse(data);
                    if (plan.Debut != null && !Teaches(data, plan.Debut.Value)) continue;
                    var stats = SmartBot.Grade(level, SearchRuns, rng.Next());
                    if (stats.StuckRate > 0.05) continue;
                    if (express && stats.Fails >= 10 && stats.ExpressShare > 0.65) continue;
                    pool.Add((data, level, noise, spread, stats));
                }
                pool.Sort((a, b) => Error(a.stats, plan).CompareTo(Error(b.stats, plan)));

                for (int i = 0; i < pool.Count && i < 10; i++)
                {
                    var c = pool[i];
                    // Confirm with the grading seed, so the table shows the numbers the choice was made on.
                    var stats = SmartBot.Grade(c.level, ConfirmRuns, GradeSeed + plan.Id);
                    double error = Error(stats, plan);
                    if (error >= bestError) continue;
                    // A level that perfect play wins with slots to spare never asks the player to count them.
                    if (plan.SpareSlots is int spare && SlotsNeeded(c.data) < c.data.holdingSlots - spare) error += 0.5;
                    // A level a reactive player wins doesn't ask for a plan.
                    if (GreedyBot.FailRate(c.level, GreedyRuns, GradeSeed + plan.Id) < plan.GreedyFloor) error += 0.5;
                    if (stats.StuckRate > 0.03 || error >= bestError) continue;
                    if (express && stats.ExpressShare > MaxExpressShare) continue;
                    var solved = Solver.Solve(c.level);
                    if (!solved.Solvable) continue;
                    bestError = error;
                    best = new Result { Plan = plan, Data = c.data, Level = c.level, Noise = c.noise, Spread = c.spread, SolverNodes = solved.NodesVisited };
                }

                // Narrow the noise range around the best so far for the next round.
                if (best != null)
                {
                    double span = (hi - lo) * 0.35;
                    lo = Math.Max(0, best.Noise - span);
                    hi = best.Noise + span;
                    double spreadSpan = (spreadHi - spreadLo) * 0.35;
                    spreadLo = Math.Max(0, best.Spread - spreadSpan);
                    spreadHi = Math.Min(MaxSpread, best.Spread + spreadSpan);
                }
            }
            return best;
        }

        /// <summary>
        /// The fewest waiting slots the level can be won with (perfect play, no boosters), from its own
        /// count down to 1. A search that hits its node limit counts as winnable, so "tight" is only
        /// claimed when it is proven.
        /// </summary>
        static int SlotsNeeded(LevelData data)
        {
            int need = data.holdingSlots;
            for (int slots = data.holdingSlots - 1; slots >= 1; slots--)
            {
                var copy = LevelJson.Parse(LevelJson.Write(data));
                copy.holdingSlots = slots;
                var solved = Solver.Solve(LevelDefinition.Parse(copy), 60_000);
                if (!solved.Solvable && !solved.HitNodeLimit) break;
                need = slots;
            }
            return need;
        }

        /// <summary>Distance from the target fail rate, plus a large penalty outside the plan's band.</summary>
        static double Error(SmartStats stats, LevelPlan plan)
        {
            var (lo, hi) = plan.Band;
            double error = Math.Abs(stats.FailRate - plan.TargetFail);
            return stats.FailRate < lo || stats.FailRate > hi ? error + 1 : error;
        }

        static Result Finish(LevelData data, LevelPlan plan, double noise)
        {
            var level = LevelDefinition.Parse(data);
            if (!new GameSession(level).HasLegalMove())
                throw new InvalidOperationException($"level {data.id} starts with no legal move");
            var solved = Solver.Solve(level);
            if (!solved.Solvable) throw new InvalidOperationException($"level {data.id} is not solvable (hit limit {solved.HitNodeLimit})");
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
                HitNodeLimit = solved.HitNodeLimit,
                Stats = CasualBot.Grade(level, GradeRuns, GradeSeed + data.id),
                Smart = SmartBot.Grade(level, SmartGradeRuns, GradeSeed + data.id),
                SlotsNeeded = SlotsNeeded(data),
                GreedyFail = GreedyBot.FailRate(level, GreedyRuns, GradeSeed + data.id),
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
            holdingSlots = 4,
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
                holdingSlots = 4,
                truckCapacity = LevelPlan.TruckCapacity,
                trucks = "RBRB",
                rows = new[] { "BBBB", "RRRR", "BBRR" },
            };
            if (new GameSession(LevelDefinition.Parse(data)).HasFreeMatch())
                throw new InvalidOperationException("level 3 must start with no free match");
            return data;
        }

        // ---- debut levels: generated like the rest, but they must teach their cargo -------------------

        /// <summary>
        /// True when a debut candidate makes its new cargo matter in the opening: the player meets it in
        /// the first few moves and has to act on its rule, not just see it (playtest round 7: frost,
        /// covers, locks and express that were merely visible taught nothing).
        /// </summary>
        static bool Teaches(LevelData data, CargoKind kind)
        {
            var level = LevelDefinition.Parse(data);
            var session = new GameSession(level);
            Parcel At(int x, int y) => session.Board.Get(new Cell(x, y));
            bool Free(Parcel p) => p != null && session.Board.IsFree(p.Cell);
            bool Near(Cell c) => c.Y <= 1;
            // The index of the first truck of a color (trucks.Count when none comes).
            int FirstTruck(int color)
            {
                for (int i = 0; i < level.Trucks.Count; i++)
                    if (level.Trucks[i] == color) return i;
                return level.Trucks.Count;
            }

            switch (kind)
            {
                case CargoKind.Bollard:
                    // A bollard near the front with a parcel the first or second truck wants right behind it: the
                    // wall is met at once and the player picks which side to dig around.
                    for (int y = 0; y + 1 < level.Height && y <= 1; y++)
                    for (int x = 0; x < level.Width; x++)
                    {
                        if (!level.IsBollard(x, y)) continue;
                        int color = level.ColorAt(x, y + 1);
                        if (color != ParcelColors.None && FirstTruck(color) <= 1) return true;
                    }
                    return false;
                case CargoKind.Frost:
                    // A front-row crate in the first truck's color, iced for 3-4 moves: the player has to find
                    // useful moves while it thaws.
                    return data.frost.Any(f => f.y == 0 && f.layers >= 3 && f.layers <= 4
                                               && level.ColorAt(f.x, f.y) == level.Trucks[0]);
                case CargoKind.Mystery:
                {
                    // One cover right behind the front row, and covers over more than one color, so counting
                    // colors can't say what is under them.
                    var covered = new HashSet<int>();
                    bool early = false;
                    for (int y = 0; y < level.Height; y++)
                    for (int x = 0; x < level.Width; x++)
                    {
                        if (!level.IsMystery(x, y)) continue;
                        covered.Add(level.ColorAt(x, y));
                        if (y == 1) early = true;
                    }
                    return early && covered.Count >= 2;
                }
                case CargoKind.Express:
                {
                    // Two or three parcels deep, a short count, and a color no truck in sight wants: the player
                    // has to choose between finishing the truck and digging to it, and spend a slot on it.
                    var e = data.express.Single();
                    int color = level.ColorAt(e.x, e.y);
                    return e.y >= 2 && e.y <= 3 && !Free(At(e.x, e.y)) && e.taps <= 6 && FirstTruck(color) >= 2;
                }
                case CargoKind.Lock:
                {
                    // The padlock sits at the front right in front of a parcel the first or second truck wants,
                    // and the key's truck isn't the first or second: getting past the lock takes a plan
                    // (parking the key doesn't open it).
                    var l = level.Locks.Single();
                    var behind = l.Locked.Y + 1 < level.Height ? level.ColorAt(l.Locked.X, l.Locked.Y + 1) : ParcelColors.None;
                    return Near(l.Locked) && Near(l.Key) && FirstTruck(level.ColorAt(l.Key.X, l.Key.Y)) >= 2
                           && behind != ParcelColors.None && FirstTruck(behind) <= 1;
                }
                case CargoKind.Strap:
                    return level.Straps.Any(st => Near(st.A) && Near(st.B));
                default:
                {
                    // The gate's color comes fourth or later, so opening it is something to work towards.
                    var g = level.Gates.Single();
                    return g.Count == LevelPlan.TruckCapacity && FirstTruck(g.Color) >= 3;
                }
            }
        }

        static IEnumerable<Cell> Neighbours(int x, int y)
        {
            yield return new Cell(x, y - 1);
            yield return new Cell(x - 1, y);
            yield return new Cell(x + 1, y);
            yield return new Cell(x, y + 1);
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
                if (!new GameSession(level).HasLegalMove())
                {
                    Console.Error.WriteLine($"{Path.GetFileName(files[i])}: no legal move at the start");
                    ok = false;
                }
                var solved = Solver.Solve(level);
                if (!solved.Solvable)
                {
                    Console.Error.WriteLine($"{Path.GetFileName(files[i])}: not solvable (nodes {solved.NodesVisited}, hit limit {solved.HitNodeLimit})");
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
                    HitNodeLimit = solved.HitNodeLimit,
                    Stats = CasualBot.Grade(level, GradeRuns, GradeSeed + data.id),
                    Smart = SmartBot.Grade(level, SmartGradeRuns, GradeSeed + data.id),
                    SlotsNeeded = SlotsNeeded(data),
                    GreedyFail = GreedyBot.FailRate(level, GreedyRuns, GradeSeed + data.id),
                };
            });
            PrintTable(results);
            ok &= CheckGrades(results);
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
                        sum += SmartBot.Grade(LevelDefinition.Parse(data), 60, rng.Next()).FailRate;
                        boards++;
                    }
                    Console.Write($" {noise,4:0}:{(boards == 0 ? double.NaN : sum / boards):0.00}");
                }
                Console.WriteLine();
            }
            return 0;
        }

        // ---- play: a level as text, one call at a time (see PlayCommand) ---------------------------

        static int Play(Options o)
        {
            string file = o.File ?? Path.Combine(o.Out, $"level_{o.Level:000}.json");
            if (!File.Exists(file))
            {
                Console.Error.WriteLine($"no level file {file}");
                return 2;
            }
            return PlayCommand.Run(LevelJson.Read(file), o.Moves);
        }

        // ---- output -----------------------------------------------------------------------------

        static void PrintTable(Result[] results)
        {
            Console.WriteLine("| Level | Kind | Size | Colors | Trucks | Slots | Hard | Bollards | Frost | Mystery | Express | Locks | Straps | Gates | Target | Bot fail | Stuck | Express fails | Smart fail | Decisions | Slots needed | Greedy fail | Solver nodes |");
            Console.WriteLine("|---:|---|---|---:|---:|---:|---|---:|---|---:|---|---:|---:|---|---:|---:|---:|---|---:|---:|---:|---:|---:|");
            foreach (var r in results)
            {
                if (r == null) continue;
                var l = r.Level;
                var d = r.Data;
                int mystery = 0, bollards = 0;
                var colors = new HashSet<int>();
                for (int y = 0; y < l.Height; y++)
                for (int x = 0; x < l.Width; x++)
                {
                    if (l.IsMystery(x, y)) mystery++;
                    if (l.IsBollard(x, y)) bollards++;
                    if (l.ColorAt(x, y) != ParcelColors.None) colors.Add(l.ColorAt(x, y));
                }
                string frost = d.frost == null || d.frost.Length == 0 ? "" : $"{d.frost.Length} (moves {string.Join(",", d.frost.Select(f => f.layers))})";
                string express = d.express == null || d.express.Length == 0 ? "" : "taps " + string.Join(",", d.express.Select(e => e.taps));
                string gates = d.gates == null || d.gates.Length == 0 ? "" : string.Join(",", d.gates.Select(g => g.color + g.count));
                string nodes = r.HitNodeLimit ? $"{r.SolverNodes} LIMIT" : r.SolverNodes.ToString(CultureInfo.InvariantCulture);
                string expressFails = express.Length == 0 ? "" : $"{r.Stats.ExpressFails} ({r.Stats.ExpressShare:0.00})";
                Console.WriteLine($"| {d.id} | {r.Plan.Kind} | {l.Width}x{l.Height} | {colors.Count} | {l.Trucks.Count} | {l.HoldingSlots} | " +
                                  $"{(l.Hard ? "yes" : "")} | {Blank(bollards)} | {frost} | {Blank(mystery)} | {express} | {Blank(l.Locks.Count)} | " +
                                  $"{Blank(l.Straps.Count)} | {gates} | {r.Plan.TargetFail:0.00} | {r.Stats.FailRate:0.000} | " +
                                  $"{r.Stats.StuckRate:0.000} | {expressFails} | {r.Smart.FailRate:0.000} | {r.Smart.DecisionShare:0.00} | {r.SlotsNeeded} | {r.GreedyFail:0.00} | {nodes} |");
            }
        }

        /// <summary>
        /// Prints a FAIL line for every level outside its design fail band, stuck more than 1% of games,
        /// missing too many express parcels, or past the solver's node limit. False when any did.
        /// </summary>
        static bool CheckGrades(Result[] results)
        {
            bool ok = true;
            foreach (var r in results)
            {
                if (r == null) continue;
                var problems = new List<string>();
                var (lo, hi) = r.Plan.SpecBand;
                double fail = r.Smart.FailRate;
                if (fail < lo || fail > hi) problems.Add($"smart fail {fail:0.000} outside {r.Plan.Kind} band {lo:0.00}-{hi:0.00}");
                if (r.Smart.StuckRate > 0.03) problems.Add($"smart stuck {r.Smart.StuckRate:0.000} > 0.03");
                // A handful of fails says nothing about their mix, so the express share needs a few to count.
                if (r.Smart.Fails >= 10 && r.Smart.ExpressShare > MaxExpressShare)
                    problems.Add($"express misses are {r.Smart.ExpressShare:0.00} of fails");
                if (r.HitNodeLimit) problems.Add("solver hit its node limit");
                if (problems.Count == 0) continue;
                ok = false;
                Console.WriteLine($"FAIL level {r.Data.id}: {string.Join("; ", problems)}");
            }
            if (ok) Console.WriteLine("all levels inside their fail bands");
            return ok;
        }

        static string Blank(int n) => n == 0 ? "" : n.ToString(CultureInfo.InvariantCulture);

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
                    case "play":
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
                    case "--level":
                        o.Level = int.Parse(Next(), CultureInfo.InvariantCulture);
                        break;
                    case "--moves":
                        o.Moves = Next();
                        break;
                    case "--file":
                        o.File = Path.GetFullPath(Next());
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
