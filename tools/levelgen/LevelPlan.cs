using System;
using CargoJam.Core;

namespace CargoJam.LevelGen
{
    /// <summary>
    /// The knobs for one level: board size, colors, cargo and the casual-bot fail rate to aim for.
    /// The generator searches truck-order hostility (<see cref="BoardMaker"/>'s noise) to hit the target.
    /// The content curve (which cargo each level uses) is design v4 §4.
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
        public int FrostMaxLayers = 1;
        public int Express;
        /// <summary>Spare moves an express parcel gets beyond its dig position.</summary>
        public int ExpressSlack = 5;
        public int Straps;
        public int Gates;
        /// <summary>Cap on a gate's count (3 at the debut, 6 after).</summary>
        public int GateMaxCount = 6;
        /// <summary>The smallest gate count a candidate may have (L45 asks for a count-6 gate).</summary>
        public int GateMinCount = 3;
        /// <summary>Twist: one frozen crate covers a mystery parcel.</summary>
        public bool FrostOnMystery;
        /// <summary>Twist: one frozen crate is a key.</summary>
        public bool FrostOnKey;
        /// <summary>Twist: at least one frozen crate sits in the front row.</summary>
        public bool FrostInRow0;
        /// <summary>The cargo this level introduces (hand-made levels), or null.</summary>
        public CargoKind? Debut;
        public double TargetFail;

        public const int LevelCount = 100;
        public const int TruckCapacity = 3;
        public const int FirstSizedLevel = 60;

        /// <summary>The level each cargo kind first appears on, indexed by <see cref="CargoKind"/>.</summary>
        public static readonly int[] DebutLevel = { 5, 7, 12, 17, 22, 27, 42 };

        /// <summary>The cargo debuting on level <paramref name="id"/>, or null.</summary>
        public static CargoKind? DebutAt(int id)
        {
            for (int k = 0; k < DebutLevel.Length; k++)
                if (DebutLevel[k] == id) return (CargoKind)k;
            return null;
        }

        /// <summary>Bollards may stand in the front row from this level on.</summary>
        public bool BollardsInRow0 => Id >= 35;

        /// <summary>
        /// The fail rates a level must land in (design v4): debut and relief levels at most 0.05, medium at
        /// most 0.40, HARD 0.55-0.72. <c>generate</c> and <c>grade</c> fail when a level is outside it.
        /// </summary>
        public (double Lo, double Hi) SpecBand => Kind switch
        {
            "HARD" => (0.55, 0.72),
            "debut" or "relief" or "tutorial" => (0, 0.05),
            "medium" => (0, 0.40),
            _ => (0, 1),
        };

        /// <summary>
        /// The band the search aims inside: <see cref="SpecBand"/> with a small margin for sampling noise.
        /// The late ramp (L61-100) runs closer to the caps, so its margin is smaller.
        /// </summary>
        public (double Lo, double Hi) Band => Kind switch
        {
            "HARD" => (0.565, Id > LateRampStart ? 0.715 : 0.71),
            "debut" or "relief" or "tutorial" => (0, 0.045),
            "medium" => (0, Id > LateRampStart ? 0.395 : 0.385),
            _ => (0, 1),
        };

        /// <summary>Levels after this one ramp their fail targets up again towards the caps.</summary>
        const int LateRampStart = 60;

        public static LevelPlan For(int id)
        {
            if (id < 2 || id > LevelCount) throw new ArgumentOutOfRangeException(nameof(id), "levels run from 2 to 100; level 1 is hand-made");

            var plan = new LevelPlan { Id = id, Slots = 5, Debut = DebutAt(id) };
            int pos = (id - 1) % 5; // 0..4 within each block of five
            double t = Math.Clamp((id - 4) / 56.0, 0, 1);
            // Medium levels cap at 0.40 (design v4); the curve tops out at 0.36 to leave room for sampling noise.
            double medium = Math.Min(0.36, 0.07 + 0.33 * t);

            if (id <= 3)
            {
                plan.Kind = "tutorial";
                plan.TargetFail = 0;
            }
            else if (plan.Debut != null)
            {
                plan.Kind = "debut";
                plan.TargetFail = 0.03;
            }
            else if (id == 4)
            {
                plan.Kind = "harder";
                plan.TargetFail = 0.05;
            }
            else if (pos == 4)
            {
                plan.Hard = id >= 10;
                plan.Kind = plan.Hard ? "HARD" : "relief";
                // 0.58 at L10 rising to 0.68 at L100, well inside the 0.55-0.72 band.
                plan.TargetFail = plan.Hard ? 0.58 + 0.10 * (id - 10) / 90.0 : 0.02;
            }
            else
            {
                switch (pos)
                {
                    case 0 when id > 10:
                        // Straight after a HARD level: a breather.
                        plan.Kind = "relief";
                        plan.TargetFail = 0.03;
                        break;
                    case 0:
                        plan.Kind = "easy";
                        plan.TargetFail = Math.Max(0.02, medium - 0.06);
                        break;
                    case 1:
                        plan.Kind = "easy";
                        plan.TargetFail = Math.Max(0.03, medium - 0.04);
                        break;
                    case 2:
                        plan.Kind = "medium";
                        plan.TargetFail = medium;
                        break;
                    default:
                        plan.Kind = "harder";
                        plan.TargetFail = medium + 0.07;
                        break;
                }
            }

            if (id > LateRampStart) SetLateTarget(plan);

            plan.Colors = id <= 3 ? 2 : id <= 9 ? 3 : id <= 19 ? 4 : id <= 39 ? 5 : 6;
            if (plan.Hard) plan.Colors = Math.Min(6, plan.Colors + 1);
            if (plan.Kind == "relief" && id > 5) plan.Colors = Math.Max(3, plan.Colors - 1);
            if (plan.Kind == "debut") plan.Colors = 3;
            // HARD levels lose a slot early on; later they rely on more colors and a meaner truck order.
            // Small early boards barely trouble a 5-slot dock, so their "harder" levels lose one too.
            if (plan.Hard && id <= 30) plan.Slots = 4;
            if (plan.Kind == "harder" && id <= 14) plan.Slots = 4;

            SetCargo(plan);
            SetSize(plan, id);
            return plan;
        }

        /// <summary>
        /// Levels 61-100 have their full size and cargo, so without this the fail rate would sit on a plateau.
        /// A gentle ramp inside the caps keeps them getting harder: easy 0.30 to 0.36, medium 0.34 to 0.385
        /// (cap 0.40), harder 0.41 to 0.47 and HARD 0.64 to 0.70 (cap 0.72). Relief stays at 0.03.
        /// </summary>
        static void SetLateTarget(LevelPlan plan)
        {
            double r = (plan.Id - (LateRampStart + 1)) / (double)(LevelCount - LateRampStart - 1);
            switch (plan.Kind)
            {
                case "easy": plan.TargetFail = 0.30 + 0.06 * r; break;
                case "medium": plan.TargetFail = 0.34 + 0.045 * r; break;
                case "harder": plan.TargetFail = 0.41 + 0.06 * r; break;
                case "HARD": plan.TargetFail = 0.64 + 0.06 * r; break;
            }
        }

        /// <summary>The ramp table of design v4 §4.</summary>
        static void SetCargo(LevelPlan p)
        {
            int id = p.Id;
            bool relief = p.Kind == "relief";
            int mystery = Math.Min(5, 2 + (id - 12) / 15 + (p.Hard ? 1 : 0));
            int locks = Math.Min(3, 1 + (id - 22) / 25 + (p.Hard && id >= 40 ? 1 : 0));
            int frost = p.Hard ? 4 : relief ? 2 : 3;
            int straps = p.Hard ? 3 : relief ? 1 : 2;
            int bollards = id >= 30 ? 4 : 2;
            p.FrostMaxLayers = id < 10 ? 1 : id < 30 ? 2 : p.Hard && id >= 50 ? 3 : 2;
            p.GateMaxCount = 6;
            p.ExpressSlack = p.Hard ? (id >= 60 ? 2 : 3) : relief ? 8 : 5;

            switch (id)
            {
                case 2: case 3: case 4: return;
                // 5 and 7 are debuts.
                case 6: p.Bollards = 2; return;
                case 8: p.Frost = 2; p.FrostMaxLayers = 1; return;
                case 9: p.Frost = 2; p.FrostMaxLayers = 1; p.Bollards = 2; return;
                case 10: p.Frost = 3; p.FrostMaxLayers = 2; p.Bollards = 2; return;
                case 11: p.Bollards = 2; return;
                // 12 debut.
                case 13: p.Mystery = 2; p.Frost = 2; return;
                case 14: p.Frost = 3; p.Bollards = 2; return;
                case 15: p.Mystery = 3; p.Frost = 3; return;
                case 16: p.Mystery = 2; return;
                // 17 debut.
                case 18: p.Express = 1; p.ExpressSlack = 6; return;
                case 19: p.Express = 1; p.ExpressSlack = 5; p.Mystery = 2; return;
                case 20: p.Express = 1; p.ExpressSlack = 4; p.Frost = 3; return;
                case 21: p.Frost = 2; return;
                // 22 debut.
                case 23: p.Locks = 1; p.Bollards = 2; return;
                case 24: p.Locks = 1; p.Express = 1; return;
                case 25: p.Locks = 1; p.Mystery = 3; p.Frost = 3; return;
                case 26: p.Mystery = 2; return;
                // 27 debut.
                case 28: p.Straps = 2; return;
                case 29: p.Straps = 2; p.Locks = 1; return;
                case 30: p.Straps = 2; p.Express = 1; return;
                case 31: p.Express = 1; return;
                case 32: p.Locks = 1; p.Bollards = 2; return;
                case 33: p.Mystery = 3; p.Frost = 3; p.FrostOnMystery = true; return;
                case 34: p.Straps = 2; p.Frost = 3; return;
                case 35: p.Frost = 3; p.FrostInRow0 = true; p.Straps = 2; return;
                case 36: p.Express = 2; p.ExpressSlack = 8; return;
                case 37: p.Locks = 1; p.Mystery = 2; return;
                case 38: p.Locks = 2; p.Frost = 3; p.FrostOnKey = true; return;
                case 39: p.Straps = 2; p.Mystery = 3; return;
                case 40: p.Express = 2; p.ExpressSlack = 3; p.Locks = 2; return;
                case 41: p.Straps = 1; return;
                // 42 debut.
                case 43: p.Gates = 1; return;
                case 44: p.Gates = 1; p.Frost = 3; return;
                case 45: p.Gates = 1; p.GateMinCount = 6; p.Mystery = 3; p.Straps = 2; return;
                case 46: case 47: case 48: case 49: p.Gates = 2; return;
                case 50: p.Gates = 2; p.Locks = 2; p.Express = 1; return;
            }

            // 51-60: two cargo types per level, three on HARD. 61-100: a rotation of 2-3-type combos,
            // HARD gets 3-4, relief gets 1.
            CargoKind[] kinds;
            if (id <= 60)
            {
                kinds = p.Hard ? Triples[(id / 5) % Triples.Length] : Pairs[id % Pairs.Length];
            }
            else if (relief)
            {
                kinds = new[] { Singles[(id / 5) % Singles.Length] };
            }
            else if (p.Hard)
            {
                var three = Triples[(id / 5) % Triples.Length];
                kinds = (id / 5) % 2 == 0 ? With(three, Extra[(id / 10) % Extra.Length]) : three;
            }
            else
            {
                kinds = p.Kind == "harder" && id % 3 == 0 ? Triples[(id + 2) % Triples.Length] : Pairs[(id * 3) % Pairs.Length];
            }

            foreach (var k in kinds)
            {
                switch (k)
                {
                    case CargoKind.Bollard: p.Bollards = bollards; break;
                    case CargoKind.Frost: p.Frost = frost; break;
                    case CargoKind.Mystery: p.Mystery = mystery; break;
                    case CargoKind.Express: p.Express = p.Hard && id >= 70 ? 2 : 1; break;
                    case CargoKind.Lock: p.Locks = locks; break;
                    case CargoKind.Strap: p.Straps = straps; break;
                    case CargoKind.Gate: p.Gates = p.Hard && id >= 70 ? 2 : 1; break;
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
                // Design v4: easy 6x7, medium and harder 7x8, HARD 7x9.
                if (plan.Hard) { w = 7; h = 9; }
                else if (plan.Kind == "medium" || plan.Kind == "harder") { w = 7; h = 8; }
                else { w = 6; h = 7; }
            }
            else
            {
                if (id <= 3) { w = 4; h = 3; }
                else if (id <= 5) { w = 4; h = 4; }
                else if (id <= 9) { w = 5; h = 5; }
                else if (id <= 14) { w = 5; h = 6; }
                else if (id <= 19) { w = 6; h = 6; }
                else if (id <= 29) { w = 6; h = 7; }
                else if (id <= 39) { w = 7; h = 7; }
                else if (id <= 49) { w = 7; h = 8; }
                else { w = 7; h = 9; }

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
            plan.Holes = id <= 3 ? 0 : Math.Max(1, (int)Math.Round(cells * (plan.Kind == "relief" ? 0.12 : 0.06)));
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
