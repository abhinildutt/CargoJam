using System;
using CargoJam.Core;

namespace CargoJam.LevelGen
{
    /// <summary>
    /// The knobs for one level: board size, colors, slots, cargo and the thoughtful-bot fail rate to aim
    /// for. The generator searches truck-order hostility (<see cref="BoardMaker"/>'s noise) to hit the
    /// target. Design v5: every cargo kind debuts by level 15 and from then on levels mix two to four
    /// kinds; difficulty is judged by <see cref="SmartBot"/>, a player who thinks before tapping.
    /// </summary>
    public sealed class LevelPlan
    {
        public int Id;
        /// <summary>tutorial, debut, relief, easy, medium, harder or HARD.</summary>
        public string Kind;
        public bool Hard;
        public int Width;
        public int Height;
        /// <summary>Parcels the board aims for: (cells - holes - bollards - gates) rounded down to whole trucks.</summary>
        public int Parcels;
        /// <summary>Empty cells before bollards take some of them over.</summary>
        public int Holes;
        public int Colors;
        public int Slots;
        public int Mystery;
        public int Locks;
        /// <summary>Free-standing bollards, mirrored left-right (gate walls come on top).</summary>
        public int Bollards;
        public int Frost;
        /// <summary>The range a frozen crate's thaw count is drawn from (moves until it can leave).</summary>
        public int FrostMinMoves = 2;
        public int FrostMaxMoves = 4;
        public int Express;
        /// <summary>Spare moves an express parcel gets beyond its dig position.</summary>
        public int ExpressSlack = 5;
        public int Straps;
        public int Gates;
        /// <summary>Cap on a gate's count (3 at the debut, 6 after).</summary>
        public int GateMaxCount = 6;
        /// <summary>The smallest gate count a candidate may have.</summary>
        public int GateMinCount = 3;
        /// <summary>Twist: one frozen crate covers a mystery parcel.</summary>
        public bool FrostOnMystery;
        /// <summary>Twist: one frozen crate is a key.</summary>
        public bool FrostOnKey;
        /// <summary>Twist: at least one frozen crate sits in the front row.</summary>
        public bool FrostInRow0;
        /// <summary>The cargo this level introduces, or null.</summary>
        public CargoKind? Debut;
        /// <summary>The <see cref="SmartBot"/> fail rate to aim for.</summary>
        public double TargetFail;

        public const int LevelCount = 100;
        public const int TruckCapacity = 3;
        public const int FirstSizedLevel = 60;

        /// <summary>
        /// The level each cargo kind first appears on, indexed by <see cref="CargoKind"/>: bollard 4,
        /// frost 5, mystery 7, express 12, lock 9, strap 13, gate 15. None is an arrival level (11, 21) or
        /// a booster unlock (6, 11, 16), and none is a storm finale (10, 20).
        /// </summary>
        public static readonly int[] DebutLevel = { 4, 5, 7, 12, 9, 13, 15 };

        /// <summary>The cargo debuting on level <paramref name="id"/>, or null.</summary>
        public static CargoKind? DebutAt(int id)
        {
            for (int k = 0; k < DebutLevel.Length; k++)
                if (DebutLevel[k] == id) return (CargoKind)k;
            return null;
        }

        /// <summary>Bollards may stand in the front row from this level on.</summary>
        public bool BollardsInRow0 => Id >= 25;

        /// <summary>
        /// The thoughtful-bot fail rates a level must land in. <c>generate</c> and <c>grade</c> fail when a
        /// level is outside it.
        /// </summary>
        public (double Lo, double Hi) SpecBand => Kind switch
        {
            "tutorial" => (0, 0.05),
            "debut" => (0.02, 0.22),
            "relief" => (0.04, 0.24),
            "easy" => (0.10, 0.36),
            "medium" => (0.20, 0.46),
            "harder" => (0.30, 0.56),
            "HARD" => (0.44, 0.74),
            _ => (0, 1),
        };

        /// <summary>The band the search aims inside: <see cref="SpecBand"/> less a margin for sampling noise.</summary>
        public (double Lo, double Hi) Band
        {
            get
            {
                var (lo, hi) = SpecBand;
                return (lo == 0 ? 0 : lo + 0.02, hi - 0.02);
            }
        }

        public static LevelPlan For(int id)
        {
            if (id < 2 || id > LevelCount) throw new ArgumentOutOfRangeException(nameof(id), "levels run from 2 to 100; level 1 is hand-made");

            var plan = new LevelPlan { Id = id, Slots = 4, Debut = DebutAt(id) };
            int pos = (id - 1) % 5; // 0..4 within each block of five
            // 0 at the start, 1 by level 60: the early levels ease in, the later ones ramp up to the targets.
            double t = Math.Clamp((id - 4) / 56.0, 0, 1);
            double late = Math.Clamp((id - 60) / 40.0, 0, 1);

            if (id <= 3) plan.Kind = "tutorial";
            else if (plan.Debut != null) plan.Kind = "debut";
            else if (id == 14 || (pos == 4 && id >= 10)) plan.Kind = "HARD";
            else if (pos == 0 && id > 10) plan.Kind = "relief";
            else plan.Kind = pos switch { 0 or 1 => "easy", 2 => "medium", _ => "harder" };
            plan.Hard = plan.Kind == "HARD";

            plan.TargetFail = plan.Kind switch
            {
                "tutorial" => 0,
                "debut" => 0.10,
                "relief" => 0.12,
                "easy" => 0.18 + 0.06 * t + 0.04 * late,
                "medium" => 0.28 + 0.06 * t + 0.04 * late,
                "harder" => 0.36 + 0.08 * t + 0.05 * late,
                _ => 0.50 + 0.08 * t + 0.06 * late,
            };

            plan.Colors = id <= 3 ? 2 : id <= 19 ? 4 : id <= 39 ? 5 : 6;
            if (plan.Hard) plan.Colors = Math.Min(6, plan.Colors + 1);
            if (plan.Kind == "debut") plan.Colors = 4;
            // Few waiting slots are what make a level ask for a plan: four, three on the hardest and on the
            // small early boards and debuts (with four, a careful player can't lose a 5-wide yard however
            // it is dealt).
            if (plan.Hard || plan.Kind == "debut" || (plan.Kind == "harder" && id >= 20) || id <= 9) plan.Slots = 3;
            // Relief yards are a row shorter, so below 7 columns they need the tighter dock too.
            if (plan.Kind == "relief" && id < 30) plan.Slots = 3;

            SetCargo(plan);
            SetSize(plan, id);
            return plan;
        }

        /// <summary>Which cargo each level uses: debuts alone, then mixes of two to four kinds.</summary>
        static void SetCargo(LevelPlan p)
        {
            int id = p.Id;
            bool relief = p.Kind == "relief";
            int mystery = Math.Min(5, 2 + id / 25 + (p.Hard ? 1 : 0));
            int locks = Math.Min(3, 1 + id / 40 + (p.Hard && id >= 30 ? 1 : 0));
            int frost = p.Hard ? 4 : relief ? 2 : 3;
            int straps = p.Hard ? 3 : relief ? 1 : 2;
            int bollards = id >= 30 ? 4 : 2;
            // Thaw counts: short on relief levels, longer on the harder ones of each block.
            (p.FrostMinMoves, p.FrostMaxMoves) = p.Kind switch
            {
                "relief" or "debut" => (2, 4),
                "easy" => (2, 5),
                "medium" => (3, 6),
                _ => (3, 7),
            };
            p.GateMaxCount = 6;
            p.ExpressSlack = p.Hard ? (id >= 50 ? 2 : 3) : relief ? 7 : 4;

            CargoKind[] kinds;
            if (p.Debut != null)
            {
                kinds = new[] { p.Debut.Value };
                p.GateMaxCount = 3;
                p.ExpressSlack = 7;
                straps = 2;
                frost = 2;
            }
            else
            {
                kinds = id switch
                {
                    2 or 3 => Array.Empty<CargoKind>(),
                    6 => new[] { CargoKind.Bollard, CargoKind.Frost },
                    8 => new[] { CargoKind.Mystery, CargoKind.Frost },
                    10 => new[] { CargoKind.Lock, CargoKind.Mystery, CargoKind.Bollard },
                    11 => new[] { CargoKind.Frost, CargoKind.Lock },
                    14 => new[] { CargoKind.Express, CargoKind.Strap, CargoKind.Lock },
                    16 => new[] { CargoKind.Gate, CargoKind.Mystery },
                    17 => new[] { CargoKind.Gate, CargoKind.Strap },
                    18 => new[] { CargoKind.Gate, CargoKind.Express, CargoKind.Frost },
                    19 => new[] { CargoKind.Lock, CargoKind.Strap, CargoKind.Mystery },
                    20 => new[] { CargoKind.Gate, CargoKind.Frost, CargoKind.Lock, CargoKind.Express },
                    // The rotation's frost + express pair only gets hard through express misses here.
                    21 => new[] { CargoKind.Frost, CargoKind.Mystery },
                    _ => Rotation(p),
                };
            }

            foreach (var k in kinds)
            {
                switch (k)
                {
                    case CargoKind.Bollard: p.Bollards = bollards; break;
                    case CargoKind.Frost: p.Frost = frost; break;
                    case CargoKind.Mystery: p.Mystery = p.Debut != null ? 2 : mystery; break;
                    case CargoKind.Express: p.Express = p.Hard && id >= 50 ? 2 : 1; break;
                    case CargoKind.Lock: p.Locks = p.Debut != null ? 1 : locks; break;
                    case CargoKind.Strap: p.Straps = straps; break;
                    case CargoKind.Gate: p.Gates = p.Hard && id >= 50 ? 2 : 1; break;
                }
            }
            // A few twists once the kinds are familiar.
            if (id >= 30 && p.Frost > 0 && p.Mystery > 0 && id % 3 == 0) p.FrostOnMystery = true;
            if (id >= 35 && p.Frost > 0 && p.Locks > 0 && id % 4 == 0) p.FrostOnKey = true;
            if (id >= 25 && p.Frost > 0 && id % 5 == 2) p.FrostInRow0 = true;
        }

        /// <summary>From level 21: relief and easy levels mix two kinds, medium two or three, harder three, HARD three or four.</summary>
        static CargoKind[] Rotation(LevelPlan p)
        {
            int id = p.Id;
            switch (p.Kind)
            {
                case "relief":
                case "easy":
                    return Pairs[(id * 3) % Pairs.Length];
                case "medium":
                    return id % 2 == 0 ? Triples[(id + 1) % Triples.Length] : Pairs[(id * 5) % Pairs.Length];
                case "harder":
                    return Triples[(id + 2) % Triples.Length];
                default:
                {
                    var three = Triples[(id / 5) % Triples.Length];
                    return id >= 30 && (id / 5) % 2 == 0 ? With(three, Extra[(id / 10) % Extra.Length]) : three;
                }
            }
        }

        static readonly CargoKind[] Singles =
        {
            CargoKind.Frost, CargoKind.Mystery, CargoKind.Strap, CargoKind.Lock, CargoKind.Bollard, CargoKind.Gate, CargoKind.Express,
        };

        static readonly CargoKind[][] Pairs =
        {
            new[] { CargoKind.Frost, CargoKind.Mystery },
            new[] { CargoKind.Lock, CargoKind.Strap },
            new[] { CargoKind.Express, CargoKind.Bollard },
            new[] { CargoKind.Gate, CargoKind.Frost },
            new[] { CargoKind.Mystery, CargoKind.Strap },
            new[] { CargoKind.Lock, CargoKind.Express },
            new[] { CargoKind.Gate, CargoKind.Mystery },
            new[] { CargoKind.Frost, CargoKind.Strap },
            new[] { CargoKind.Express, CargoKind.Mystery },
            new[] { CargoKind.Lock, CargoKind.Bollard },
            new[] { CargoKind.Gate, CargoKind.Strap },
            new[] { CargoKind.Frost, CargoKind.Express },
            new[] { CargoKind.Gate, CargoKind.Lock },
        };

        static readonly CargoKind[][] Triples =
        {
            new[] { CargoKind.Frost, CargoKind.Mystery, CargoKind.Lock },
            new[] { CargoKind.Gate, CargoKind.Strap, CargoKind.Express },
            new[] { CargoKind.Lock, CargoKind.Frost, CargoKind.Strap },
            new[] { CargoKind.Gate, CargoKind.Mystery, CargoKind.Bollard },
            new[] { CargoKind.Express, CargoKind.Strap, CargoKind.Mystery },
            new[] { CargoKind.Gate, CargoKind.Frost, CargoKind.Lock },
            new[] { CargoKind.Bollard, CargoKind.Frost, CargoKind.Express },
        };

        /// <summary>The fourth kind a HARD level adds to its triple (skipped when it already has it).</summary>
        static readonly CargoKind[] Extra = { CargoKind.Bollard, CargoKind.Mystery, CargoKind.Strap, CargoKind.Frost, CargoKind.Lock };

        static CargoKind[] With(CargoKind[] kinds, CargoKind extra)
        {
            if (Array.IndexOf(kinds, extra) >= 0) return kinds;
            var all = new CargoKind[kinds.Length + 1];
            kinds.CopyTo(all, 0);
            all[kinds.Length] = extra;
            return all;
        }

        /// <summary>
        /// Drops one cargo item for a retry when no candidate board could be found: the largest count goes
        /// down by one, but every kind the plan uses keeps at least one item. False when nothing can go.
        /// </summary>
        public bool DropOneCargoItem()
        {
            // Bollards come in mirrored pairs, so they count half.
            int max = Math.Max(Math.Max(Math.Max(Frost, Mystery), Math.Max(Straps, Locks)), Math.Max(Express, Bollards / 2));
            if (max <= 1) return false;
            if (Frost == max) Frost--;
            else if (Mystery == max) Mystery--;
            else if (Straps == max) Straps--;
            else if (Locks == max) Locks--;
            else if (Express == max) Express--;
            else Bollards -= 2;
            RecountParcels();
            return true;
        }

        static void SetSize(LevelPlan plan, int id)
        {
            int w, h;
            if (id >= FirstSizedLevel)
            {
                // Easy 6x7, medium and harder 7x8, HARD 7x9.
                if (plan.Hard) { w = 7; h = 9; }
                else if (plan.Kind == "medium" || plan.Kind == "harder") { w = 7; h = 8; }
                else { w = 6; h = 7; }
            }
            else
            {
                if (id <= 3) { w = 4; h = 3; }
                else if (id <= 5) { w = 5; h = 5; }
                else if (id <= 9) { w = 5; h = 6; }
                else if (id <= 19) { w = 6; h = 6; }
                else if (id <= 29) { w = 6; h = 7; }
                else if (id <= 39) { w = 7; h = 7; }
                else { w = 7; h = 8; }

                // Relief levels get a shorter yard; HARD levels a longer one where there's room.
                if (id > 5 && plan.Kind == "relief") h -= 1;
                if (plan.Hard && h < 9) h += 1;
            }
            // A gate needs a wall row with two rows behind it and two in front.
            if (plan.Gates > 0 && h < 7) h = 7;

            plan.Width = w;
            plan.Height = h;

            // A few holes give the yard some air; tutorials are full boards. Bollards take over holes first.
            int cells = w * h;
            plan.Holes = id <= 3 ? 0 : Math.Max(1, (int)Math.Round(cells * (plan.Kind == "relief" ? 0.10 : 0.05)));
            plan.RecountParcels();
        }

        /// <summary>
        /// Roughly (cells - holes - bollards - gate walls) / 3 * 3, where a gate wall's gap is left out. It only
        /// sets the noise search range; <see cref="BoardMaker"/> counts the real open cells.
        /// </summary>
        public void RecountParcels()
        {
            int cells = Width * Height;
            int holes = Math.Max(0, Holes - Bollards);
            int walls = Gates * (Width - 1);
            Parcels = (cells - holes - Bollards - walls) / TruckCapacity * TruckCapacity;
        }
    }
}
