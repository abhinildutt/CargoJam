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
        const int SearchRuns = 120;
        const int ConfirmRuns = 1000;
        const int GradeRuns = 1000;
        const int GradeSeed = 777;
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
        }

        sealed class Result
        {
            public LevelPlan Plan;
            public LevelData Data;
            public LevelDefinition Level;
            public BotStats Stats;
            public int SolverNodes;
            public bool HitNodeLimit;
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
            var debut = DebutLevel(id);
            if (debut != null) return Finish(debut, plan, 0);

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
            const double tolerance = 0.015;
            bool express = plan.Express > 0;

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
                    if (express && stats.Fails >= 10 && stats.ExpressShare > 0.65) continue;
                    pool.Add((data, level, noise, stats));
                }
                pool.Sort((a, b) => Error(a.stats, plan).CompareTo(Error(b.stats, plan)));

                for (int i = 0; i < pool.Count && i < 6; i++)
                {
                    var c = pool[i];
                    // Confirm with the grading seed, so the table shows the numbers the choice was made on.
                    var stats = CasualBot.Grade(c.level, ConfirmRuns, GradeSeed + plan.Id);
                    double error = Error(stats, plan);
                    if (stats.StuckRate > 0.01 || error >= bestError) continue;
                    if (express && stats.ExpressShare > MaxExpressShare) continue;
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
            return best;
        }

        /// <summary>Distance from the target fail rate, plus a large penalty outside the plan's band.</summary>
        static double Error(BotStats stats, LevelPlan plan)
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

        // ---- debut levels (design v4 §4): 3 colors, 5 slots, only the new cargo plus bollards ---------

        /// <summary>The hand-made level that introduces a cargo kind on <paramref name="id"/>, or null.</summary>
        static LevelData DebutLevel(int id)
        {
            var kind = LevelPlan.DebutAt(id);
            if (kind == null) return null;
            var data = kind.Value switch
            {
                CargoKind.Bollard => Level5(),
                CargoKind.Frost => Level7(),
                CargoKind.Mystery => Level12(),
                CargoKind.Express => Level17(),
                CargoKind.Lock => Level22(),
                CargoKind.Strap => Level27(),
                _ => Level42(),
            };
            CheckDebut(data, kind.Value);
            return data;
        }

        /// <summary>
        /// Bollards: the front reds open the way, and the reds parked behind each bollard are dug out from
        /// the side once the middle column clears.
        /// </summary>
        static LevelData Level5() => new LevelData
        {
            id = 5,
            holdingSlots = 5,
            truckCapacity = LevelPlan.TruckCapacity,
            trucks = "RBGRBG",
            rows = new[]
            {
                "RRRBB",
                "G#B#G",
                "BRGRB",
                "GBRGG",
            },
        };

        /// <summary>
        /// Frost: two one-layer crates in row 1, each next to a front-row parcel. The first truck's reds
        /// sit beside the left crate, so the obvious first moves crack it.
        /// </summary>
        static LevelData Level7() => new LevelData
        {
            id = 7,
            holdingSlots = 5,
            truckCapacity = LevelPlan.TruckCapacity,
            trucks = "RYBYRBRB",
            rows = new[]
            {
                "RRYYB",
                "RYBBY",
                "RRYRY",
                "BBB.R",
                "RRBBB",
            },
            frost = new[]
            {
                new FrostData { x = 1, y = 1, layers = 1 },
                new FrostData { x = 4, y = 1, layers = 1 },
            },
        };

        /// <summary>
        /// Mystery: a covered parcel sits right behind the first truck's front-row blue, so the first
        /// match shows its color.
        /// </summary>
        static LevelData Level12() => new LevelData
        {
            id = 12,
            holdingSlots = 5,
            truckCapacity = LevelPlan.TruckCapacity,
            trucks = "BGOGBOBGO",
            rows = new[]
            {
                "BBBGGO",
                "gOGGGO",
                "BBOoOB",
                "BBGGgB",
                ".O.OO.",
            },
        };

        /// <summary>
        /// Express: the express parcel is in the front row and fits the first truck, with 8 spare moves;
        /// the hand points at it and it ships on the first tap.
        /// </summary>
        static LevelData Level17() => new LevelData
        {
            id = 17,
            holdingSlots = 5,
            truckCapacity = LevelPlan.TruckCapacity,
            trucks = "YRPYRPYRP",
            rows = new[]
            {
                "RYYYRR",
                "PPPYYY",
                "RRRPPP",
                "YYRRRY",
                ".PP.P.",
            },
            express = new[] { new ExpressData { x = 2, y = 0, taps = 9 } },
        };

        /// <summary>
        /// Lock: the key is in the front row and fits the first truck; its padlock is in row 1, freed by
        /// the second truck and opened by then.
        /// </summary>
        static LevelData Level22() => new LevelData
        {
            id = 22,
            holdingSlots = 5,
            truckCapacity = LevelPlan.TruckCapacity,
            trucks = "GBOGBOGBO",
            rows = new[]
            {
                "GGGBBB",
                "OGOOGG",
                "BBBOOO",
                "GBBBGG",
                "O.O..O",
            },
            locks = new[] { new LockData { x = 3, y = 1, keyX = 1, keyY = 0 } },
        };

        /// <summary>
        /// Strap: a red pair stands in rows 0-1 and the first truck is red with room for both, so tapping
        /// it loads the pair together. A second pair waits further back for practice.
        /// </summary>
        static LevelData Level27() => new LevelData
        {
            id = 27,
            holdingSlots = 5,
            truckCapacity = LevelPlan.TruckCapacity,
            trucks = "RYBRYBRYB",
            rows = new[]
            {
                "RRYYBB",
                "BRYRRR",
                "YYYBBB",
                "RRRYYY",
                "B.B.B.",
            },
            straps = new[]
            {
                new StrapData { x = 1, y = 0, x2 = 1, y2 = 1 },
                new StrapData { x = 0, y = 3, x2 = 1, y2 = 3 },
            },
        };

        /// <summary>
        /// Gate: a wall of bollards across row 3 with one gate, which opens after the first truck's three
        /// purples. All three are in the front row.
        /// </summary>
        static LevelData Level42() => new LevelData
        {
            id = 42,
            holdingSlots = 5,
            truckCapacity = LevelPlan.TruckCapacity,
            trucks = "PGOPGOPGOPG",
            rows = new[]
            {
                "PPPGGG",
                "OOOPPP",
                "GGGOOO",
                "##+###",
                "GPPPGO",
                "POGOPG",
                ".GPG..",
            },
            gates = new[] { new GateData { x = 2, y = 3, color = "P", count = 3 } },
        };

        /// <summary>
        /// Throws unless a debut level keeps the debut rules (3 colors, 5 slots, not HARD, only the new
        /// cargo plus bollards) and its first relevant interaction is the obvious, safe one.
        /// </summary>
        static void CheckDebut(LevelData data, CargoKind kind)
        {
            var level = LevelDefinition.Parse(data);
            var session = new GameSession(level);
            void Require(bool ok, string what)
            {
                if (!ok) throw new InvalidOperationException($"debut level {data.id} ({kind}): {what}");
            }

            var colors = new HashSet<int>();
            for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
                if (level.ColorAt(x, y) != ParcelColors.None) colors.Add(level.ColorAt(x, y));
            Require(colors.Count == 3, "must use 3 colors");
            Require(level.HoldingSlots == 5 && !level.Hard, "must have 5 slots and not be HARD");
            Require(level.Uses(kind), "must use its cargo");
            int allowed = (1 << (int)kind) | (1 << (int)CargoKind.Bollard);
            Require((level.CargoMask & ~allowed) == 0, "may use only its cargo plus bollards");
            Require(session.HasLegalMove(), "must start with a legal move");

            var moves = new List<Parcel>();
            session.GetMoves(moves);
            var truck = session.CurrentTruck;
            Parcel At(int x, int y) => session.Board.Get(new Cell(x, y));
            bool FirstTruckTakes(Parcel p) => p != null && moves.Contains(p) && session.CanTake(p) && truck.Accepts(p);

            switch (kind)
            {
                case CargoKind.Bollard:
                {
                    // Some parcel sits right behind a bollard and has to be dug out from the side.
                    bool behind = false;
                    for (int y = 0; y + 1 < level.Height; y++)
                    for (int x = 0; x < level.Width; x++)
                        if (level.IsBollard(x, y) && level.ColorAt(x, y + 1) != ParcelColors.None) behind = true;
                    Require(behind, "a parcel must sit behind a bollard");
                    break;
                }
                case CargoKind.Frost:
                {
                    // Every crate is in rows 0-1, one layer, next to a parcel the first truck takes now.
                    foreach (var f in data.frost)
                    {
                        Require(f.y <= 1 && f.layers == 1, "frost sits in rows 0-1 with one layer");
                    }
                    bool cracksFirst = data.frost.Any(f => Neighbours(f.x, f.y).Any(c => FirstTruckTakes(At(c.X, c.Y))));
                    Require(cracksFirst, "a frozen crate must be next to a parcel the first truck takes");
                    break;
                }
                case CargoKind.Mystery:
                {
                    // Tapping a first-truck parcel reveals a mystery parcel right behind it.
                    bool reveals = false;
                    foreach (var p in moves)
                    {
                        if (!FirstTruckTakes(p)) continue;
                        var next = session.Clone();
                        var hidden = new List<Parcel>();
                        foreach (var c in Neighbours(p.Cell.X, p.Cell.Y))
                        {
                            var q = next.Board.Get(c);
                            if (q != null && next.IsHidden(q)) hidden.Add(q);
                        }
                        next.Tap(p.Cell);
                        if (hidden.Any(q => !next.IsHidden(q))) reveals = true;
                    }
                    Require(reveals, "the first truck's match must reveal a mystery parcel");
                    break;
                }
                case CargoKind.Express:
                {
                    var e = data.express.Single();
                    var p = At(e.x, e.y);
                    Require(FirstTruckTakes(p), "the express parcel must be free and fit the first truck");
                    Require(e.taps >= 1 + 8, "the express parcel gets 8 spare moves");
                    break;
                }
                case CargoKind.Lock:
                {
                    var l = level.Locks.Single();
                    Require(l.Locked.Y <= 2, "the padlock must be in the front three rows");
                    Require(FirstTruckTakes(At(l.Key.X, l.Key.Y)), "the key must be free and fit the first truck");
                    break;
                }
                case CargoKind.Strap:
                {
                    var s = level.Straps[0];
                    var a = At(s.A.X, s.A.Y);
                    Require(s.A.Y <= 1 && s.B.Y <= 1, "the first strap must be in rows 0-1");
                    Require(a.Color == truck.Color, "the first strap must be the first truck's color");
                    var lead = moves.FirstOrDefault(m => m == a || m == session.StrapPartner(a));
                    Require(lead != null && session.CanTake(lead), "the first strap must be free and fit the first truck");
                    var next = session.Clone();
                    next.Tap(lead.Cell);
                    Require(next.Trucks[0].Loaded == 2 && next.Dock.Count == 0, "tapping the strap must load both parcels");
                    break;
                }
                case CargoKind.Gate:
                {
                    var g = level.Gates.Single();
                    Require(g.Count == LevelPlan.TruckCapacity && g.Color == truck.Color, "the gate opens on the first truck's three parcels");
                    // Taking the first truck's matches opens the gate within three moves.
                    var next = session.Clone();
                    for (int i = 0; i < LevelPlan.TruckCapacity; i++)
                    {
                        next.GetMoves(moves);
                        var pick = moves.FirstOrDefault(m => next.CurrentTruck.Accepts(m));
                        Require(pick != null, "the first truck's parcels must be free");
                        next.Tap(pick.Cell);
                    }
                    Require(next.IsGateOpen(0), "the gate must open after the first truck");
                    break;
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
            Console.WriteLine("| Level | Kind | Size | Colors | Trucks | Slots | Hard | Bollards | Frost | Mystery | Express | Locks | Straps | Gates | Target | Bot fail | Stuck | Express fails | Solver nodes |");
            Console.WriteLine("|---:|---|---|---:|---:|---:|---|---:|---|---:|---|---:|---:|---|---:|---:|---:|---|---:|");
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
                string frost = d.frost == null || d.frost.Length == 0 ? "" : $"{d.frost.Length} (max {d.frost.Max(f => f.layers)})";
                string express = d.express == null || d.express.Length == 0 ? "" : "taps " + string.Join(",", d.express.Select(e => e.taps));
                string gates = d.gates == null || d.gates.Length == 0 ? "" : string.Join(",", d.gates.Select(g => g.color + g.count));
                string nodes = r.HitNodeLimit ? $"{r.SolverNodes} LIMIT" : r.SolverNodes.ToString(CultureInfo.InvariantCulture);
                string expressFails = express.Length == 0 ? "" : $"{r.Stats.ExpressFails} ({r.Stats.ExpressShare:0.00})";
                Console.WriteLine($"| {d.id} | {r.Plan.Kind} | {l.Width}x{l.Height} | {colors.Count} | {l.Trucks.Count} | {l.HoldingSlots} | " +
                                  $"{(l.Hard ? "yes" : "")} | {Blank(bollards)} | {frost} | {Blank(mystery)} | {express} | {Blank(l.Locks.Count)} | " +
                                  $"{Blank(l.Straps.Count)} | {gates} | {r.Plan.TargetFail:0.00} | {r.Stats.FailRate:0.000} | " +
                                  $"{r.Stats.StuckRate:0.000} | {expressFails} | {nodes} |");
            }
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
