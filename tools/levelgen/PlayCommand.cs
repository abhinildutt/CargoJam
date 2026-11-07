using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CargoJam.Core;

namespace CargoJam.LevelGen
{
    /// <summary>
    /// <c>levelgen play --level N [--moves "a0 c1 ..."]</c>: replays the taps from the start of a level and
    /// prints what a player would see, as text, so people and agents can play a level one call at a
    /// time. Cells are a column letter and a row number; row 0 is the front row, next to the trucks.
    /// Only what the game shows is printed: hidden parcels are "?", and two trucks after the loading one.
    /// </summary>
    public static class PlayCommand
    {
        public static int Run(LevelData data, string moves)
        {
            var level = LevelDefinition.Parse(data);
            var session = new GameSession(level);
            var tokens = (moves ?? "").Split(new[] { ' ', ',', ';', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var log = new List<string>();

            for (int i = 0; i < tokens.Length; i++)
            {
                if (session.Status != GameStatus.Playing)
                {
                    log.Add($"move {i + 1} ({tokens[i]}): ignored, the level is over");
                    break;
                }
                if (!TryCell(tokens[i], level, out var cell))
                {
                    log.Add($"move {i + 1} ({tokens[i]}): not a cell on this board; stopped here");
                    break;
                }
                var before = session.Clone();
                var events = session.Tap(cell);
                string said = Describe(before, events);
                log.Add($"move {i + 1} ({tokens[i]}): {said}");
                if (events.Count == 0 || events[0] is ParcelBlocked || events[0] is DockFullRejected)
                {
                    log.Add("  (that tap did nothing; later moves were not applied)");
                    break;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"LEVEL {level.Id}{(level.Hard ? " (HARD)" : "")}: {level.Width}x{level.Height} yard, {level.HoldingSlots} waiting slots, trucks take {level.TruckCapacity}");
            int shown = Math.Max(0, log.Count - 6);
            if (shown > 0) sb.AppendLine($"({shown} earlier moves applied)");
            for (int i = shown; i < log.Count; i++) sb.AppendLine(log[i]);
            sb.AppendLine();
            AppendState(sb, session);
            Console.Write(sb.ToString());
            return 0;
        }

        static bool TryCell(string token, LevelDefinition level, out Cell cell)
        {
            cell = default;
            token = token.Trim().ToLowerInvariant();
            if (token.Length < 2 || token[0] < 'a' || token[0] > 'z') return false;
            if (!int.TryParse(token.Substring(1), out int y)) return false;
            int x = token[0] - 'a';
            cell = new Cell(x, y);
            return x < level.Width && y >= 0 && y < level.Height;
        }

        static string Name(Cell c) => $"{(char)('a' + c.X)}{c.Y}";

        static string Color(int color) => ParcelColors.ToCode(color).ToString();

        static string Describe(GameSession before, IReadOnlyList<GameEvent> events)
        {
            var parts = new List<string>();
            foreach (var e in events)
            {
                switch (e)
                {
                    case ParcelBlocked b: parts.Add($"{Name(b.Parcel.Cell)} is blocked or restrained, nothing happened"); break;
                    case DockFullRejected r: parts.Add($"no truck or slot can take {Name(r.Parcel.Cell)}, nothing happened"); break;
                    case ParcelLoaded l: parts.Add($"{Color(l.Parcel.Color)} loaded into the truck"); break;
                    case ParcelHeld h: parts.Add($"{Color(h.Parcel.Color)} parked in a waiting slot"); break;
                    case TruckDeparted d: parts.Add($"{Color(d.Truck.Color)} truck full and left"); break;
                    case TruckArrived a: parts.Add($"{Color(a.Truck.Color)} truck arrived"); break;
                    case ParcelLoadedFromDock f: parts.Add($"waiting {Color(f.Parcel.Color)} loaded from the slots"); break;
                    case ParcelRevealed v: parts.Add($"{Name(v.Parcel.Cell)} revealed as {Color(v.Parcel.Color)}"); break;
                    case ParcelUnlocked u: parts.Add($"{Name(u.Parcel.Cell)} unlocked"); break;
                    case GateOpened g: parts.Add($"gate {Name(before.Level.Gates[g.Gate].Cell)} opened"); break;
                    case LevelWon: parts.Add("LEVEL WON"); break;
                    case LevelFailed f: parts.Add($"LEVEL FAILED ({f.Reason})"); break;
                }
            }
            return string.Join("; ", parts);
        }

        static void AppendState(StringBuilder sb, GameSession s)
        {
            var level = s.Level;
            sb.AppendLine($"STATUS: {(s.Status == GameStatus.Failed ? "FAILED - " + s.FailReason : s.Status.ToString().ToUpperInvariant())}");

            // Trucks: the loading one and the next two, as the road shows them.
            var t = s.CurrentTruck;
            if (t != null)
            {
                var next = new List<string>();
                for (int i = 1; i <= SmartBot.VisibleTrucks && s.CurrentTruckIndex + i < s.Trucks.Count; i++)
                    next.Add(Color(s.Trucks[s.CurrentTruckIndex + i].Color));
                int unseen = s.Trucks.Count - s.CurrentTruckIndex - 1 - next.Count;
                sb.AppendLine($"TRUCK LOADING: {Color(t.Color)} ({t.Loaded}/{t.Capacity} loaded). Next: {(next.Count == 0 ? "none" : string.Join(", then ", next))}" +
                              (unseen > 0 ? $"; {unseen} more trucks after those (colors not shown yet)" : ""));
            }
            else sb.AppendLine("TRUCKS: all gone");

            var slots = new List<string>();
            for (int i = 0; i < s.Dock.Capacity; i++) slots.Add(s.Dock[i] == null ? "_" : Color(s.Dock[i].Color));
            sb.AppendLine($"WAITING SLOTS ({s.Dock.Count}/{s.Dock.Capacity} used): [{string.Join("] [", slots)}]  (a waiting parcel loads by itself when a truck of its color arrives)");

            // The yard, front row first.
            sb.AppendLine();
            sb.AppendLine("YARD (row 0 is the front; a parcel can leave when a path of empty cells leads from it to row 0, or it is in row 0):");
            var header = new StringBuilder("      ");
            for (int x = 0; x < level.Width; x++) header.Append($"{(char)('a' + x),-6}");
            sb.AppendLine(header.ToString().TrimEnd());
            var notes = new List<string>();
            for (int y = 0; y < level.Height; y++)
            {
                var row = new StringBuilder($"  {y,2}  ");
                for (int x = 0; x < level.Width; x++)
                {
                    var c = new Cell(x, y);
                    var p = s.Board.Get(c);
                    string token;
                    int gate = GateAt(level, c);
                    if (gate >= 0 && !s.IsGateOpen(gate)) token = $"G:{Color(level.Gates[gate].Color)}{s.GateLeft(gate)}";
                    else if (s.Board.IsObstacle(c)) token = "##";
                    else if (p == null) token = ".";
                    else token = Token(s, p, notes);
                    row.Append($"{token,-6}");
                }
                sb.AppendLine(row.ToString().TrimEnd());
            }
            sb.AppendLine();
            sb.AppendLine("KEY: letter = color (R red, B blue, G green, Y yellow, P purple, O orange); ? = covered, color shown once it can leave;");
            sb.AppendLine("     ~n = frozen, thaws after n more moves; !n = express, must be loaded or parked within n moves (counting the move that ships it) or the level is lost;");
            sb.AppendLine("     L = padlocked until its key parcel is loaded into a truck; K = a key; = strapped pair (leaves together, needs room for both);");
            sb.AppendLine("     ## = bollard (wall); G:Xn = closed gate (wall) that opens after n more X parcels are loaded into trucks.");
            foreach (var n in notes.Distinct()) sb.AppendLine("     " + n);

            if (s.Status != GameStatus.Playing) return;
            var moves = new List<Parcel>();
            s.GetMoves(moves);
            var parts = new List<string>();
            foreach (var p in moves)
            {
                if (!s.CanTake(p)) { parts.Add($"{Name(p.Cell)}(no room)"); continue; }
                bool loads = t != null && t.Accepts(p);
                var partner = s.StrapPartner(p);
                parts.Add($"{Name(p.Cell)}{(partner != null ? "+" + Name(partner.Cell) : "")}({(loads ? "loads" : "parks")})");
            }
            sb.AppendLine();
            sb.AppendLine("CAN TAP NOW: " + (parts.Count == 0 ? "nothing" : string.Join(" ", parts)));
        }

        static int GateAt(LevelDefinition level, Cell c)
        {
            for (int g = 0; g < level.Gates.Count; g++)
                if (level.Gates[g].Cell.X == c.X && level.Gates[g].Cell.Y == c.Y) return g;
            return -1;
        }

        static string Token(GameSession s, Parcel p, List<string> notes)
        {
            var sb = new StringBuilder(s.IsHidden(p) ? "?" : Color(p.Color));
            int frost = s.FrostLeft(p);
            if (frost > 0) sb.Append('~').Append(frost);
            int express = s.ExpressLeft(p);
            if (express > 0) sb.Append('!').Append(express);
            if (s.IsLocked(p))
            {
                sb.Append('L');
                foreach (var lk in s.Level.Locks)
                    if (lk.Locked.X == p.Cell.X && lk.Locked.Y == p.Cell.Y)
                        notes.Add($"{Name(p.Cell)} is padlocked; its key is {Name(lk.Key)}");
            }
            if (s.IsKey(p)) sb.Append('K');
            var partner = s.StrapPartner(p);
            if (partner != null)
            {
                sb.Append('=');
                var a = p.Cell.X * 100 + p.Cell.Y < partner.Cell.X * 100 + partner.Cell.Y ? p : partner;
                var b = a == p ? partner : p;
                notes.Add($"{Name(a.Cell)} and {Name(b.Cell)} are strapped together");
            }
            return sb.ToString();
        }
    }
}
