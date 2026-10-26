using System;

namespace CargoJam.LevelGen
{
    /// <summary>
    /// The knobs for one level: board size, colors, blockers and the casual-bot fail rate to aim for.
    /// The generator searches truck-order hostility (<see cref="BoardMaker"/>'s noise) to hit the target.
    /// </summary>
    public sealed class LevelPlan
    {
        public int Id;
        /// <summary>tutorial, relief, easy, medium, harder or HARD.</summary>
        public string Kind;
        public bool Hard;
        public int Width;
        public int Height;
        public int Parcels;
        public int Colors;
        public int Slots;
        public int Mystery;
        public int Locks;
        public double TargetFail;

        public const int LevelCount = 60;
        public const int TruckCapacity = 3;

        public static LevelPlan For(int id)
        {
            if (id < 2 || id > LevelCount) throw new ArgumentOutOfRangeException(nameof(id), "levels run from 2 to 60; level 1 is hand-made");

            var plan = new LevelPlan { Id = id, Slots = 5 };
            int pos = (id - 1) % 5; // 0..4 within each block of five
            double t = Math.Clamp((id - 4) / 56.0, 0, 1);
            double medium = 0.07 + 0.33 * t;

            if (id <= 3)
            {
                plan.Kind = "tutorial";
                plan.TargetFail = 0;
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
                plan.TargetFail = plan.Hard ? 0.55 + 0.20 * (id - 10) / 50.0 : 0.02;
            }
            else
            {
                switch (pos)
                {
                    case 0 when id > 10:
                        // Straight after a HARD level: a breather.
                        plan.Kind = "relief";
                        plan.TargetFail = Math.Max(0.02, medium - 0.12);
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

            SetSize(plan, id);

            plan.Colors = id <= 3 ? 2 : id <= 9 ? 3 : id <= 19 ? 4 : id <= 39 ? 5 : 6;
            if (plan.Hard) plan.Colors = Math.Min(6, plan.Colors + 1);
            if (plan.Kind == "relief" && id > 5) plan.Colors = Math.Max(3, plan.Colors - 1);
            // HARD levels lose a slot early on; later they rely on more colors and a meaner truck order.
            // Small early boards barely trouble a 5-slot dock, so their "harder" levels lose one too.
            if (plan.Hard && id <= 30) plan.Slots = 4;
            if (plan.Kind == "harder" && id <= 14) plan.Slots = 4;

            if (id >= 15) plan.Mystery = Math.Min(5, 2 + (id - 15) / 12 + (plan.Hard ? 1 : 0));
            if (id >= 25) plan.Locks = Math.Min(3, 1 + (id - 25) / 12);

            return plan;
        }

        static void SetSize(LevelPlan plan, int id)
        {
            int w, h;
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

            plan.Width = w;
            plan.Height = h;

            // A few holes give the yard some air; tutorials are full boards.
            int cells = w * h;
            int holes = id <= 3 ? 0 : Math.Max(1, (int)Math.Round(cells * (plan.Kind == "relief" ? 0.12 : 0.06)));
            plan.Parcels = (cells - holes) / TruckCapacity * TruckCapacity;
        }
    }
}
