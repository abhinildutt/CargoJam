# levelgen

Generates and grades the 100 Cargo Jam levels in `CargoJam/Assets/Resources/Levels/`. It compiles the game's real `CargoJam.Core` sources, so the generator always plays by the shipped rules. Output is deterministic for a given seed.

Run it from the repo root with the .NET SDK bundled with Unity (build output goes to `tools/.out/levelgen/`):

```sh
DOTNET=/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet

$DOTNET run --project tools/levelgen -c Release                    # regenerate level_001..100.json and print the table
$DOTNET run --project tools/levelgen -c Release -- --dry-run        # same, but write nothing
$DOTNET run --project tools/levelgen -c Release -- --levels 25-30   # only some levels
$DOTNET run --project tools/levelgen -c Release -- grade            # grade the files that are there now
$DOTNET run --project tools/levelgen -c Release -- explore --levels 40   # bot fail rate vs. noise, for tuning
$DOTNET run --project tools/levelgen -c Release -- play --level 7 --moves "a0 c0"   # play a level as text, one call at a time
```

Options: `--seed N` (default 20261026) and `--out DIR`. A full run takes about 20 s on a 10-core Mac. New level files need a Unity `.meta` (copy `level_001.json.meta` with a fresh guid).

## How it works

- **Plan** (`LevelPlan.cs`): the knobs for each level, such as board size, colors, waiting slots, the cargo it uses and the target fail rate (design v5):
  - The difficulty is a sawtooth: within each block of five levels it goes relief or easy, easy, medium, harder, then HARD (every 5th level from 10, plus L14; every storm finale is HARD). Targets are thoughtful-bot fail rates: debut 0.10, relief 0.12, easy 0.18→0.28, medium 0.28→0.38, harder 0.36→0.49, HARD 0.50→0.64, rising from L4 to L100 (bands in `SpecBand`).
  - Every level past the tutorials has 3 waiting slots, and colors share the trucks evenly. A level must be **tight**: perfect play can't win it with one slot fewer (medium, harder, HARD), or with two fewer (debut, relief, easy). The table's "Slots needed" column is that count (`SlotsNeeded`); the search penalises candidates with spare slots.
  - The opening isn't free: at most one parcel (two on a debut) can go straight into the first truck. Past its debut a padlock's key starts buried.
  - `generate` and `grade` print a `FAIL` line and exit 1 when a level lands outside its band, gets the thoughtful bot stuck in more than 3% of games, loses more than half its fails to express misses, or hits the solver's node limit.
  - Every cargo kind debuts by L15 (bollard L4, frost L5, mystery L7, lock L9, express L12, strap L13, gate L15). A debut uses only its new cargo; every other level from L6 mixes two to four kinds (hand-picked to L21, then a rotation of pairs and triples).
  - From L60 board sizes are fixed per kind: easy 6×7, medium and harder 7×8, HARD 7×9.
- **Board** (`BoardMaker.cs`):
  - It lays out gate walls (a row of bollards with the gate in its gap; each gate gets its own wall and must wall something off on its own), mirrored bollards and holes, then picks padlocks, mystery parcels and frozen crates. Frozen crates thaw after a number of moves (`layers` in the JSON, 2-6 by level kind), so three in four go in the front two rows, where the ice is what holds them; at most a third of the front row is frozen.
  - It picks a random legal dig order: each parcel is free once the ones before it are gone, a padlocked parcel comes after its key, a frozen crate once its thaw count of parcels has gone (one per move), and a gate is a virtual cell that "opens" once enough parcels are gone.
  - It colors the parcels along that order. `spread` deals the trucks round-robin (truck k takes dig positions k, k+T, k+2T: one near the front, one mid-yard, one deep), so every truck sends the player digging and the yard never just opens up; at 0 each truck's three are dug together. `noise` then moves parcels further from their truck. The search tunes both.
  - Then it places what depends on color and order: each gate's color and count (from the trucks that fill before it opens, never the first truck's color), express parcels (at an early dig position, preferably one whose truck is two or more trucks away, so shipping it means parking it), straps (two adjacent parcels of one color, preferably for a truck past the first two) and thaw counts (a frozen crate for one of the first trucks thaws a few moves after its truck could want it).
  - Every candidate must validate and start with a legal move.
- **Grading** (`SmartBot.cs`): the thoughtful bot sees what a player sees (the yard with covered colors hidden, the dock, the loading truck and the next two), scores every legal tap one move ahead (two when no free match exists or the dock is nearly full) and picks with a little human error (a softmax). Its fail rate is the difficulty measure. `CasualBot.cs` (a random tapper that takes visible matches 60% of the time) is still printed as "Bot fail" for contrast; the "Decisions" column is the share of moves with no free match.
  - The generator searches noise across many candidate boards until the fail rate is close to the plan's target and inside its band.
  - It rejects candidates where the bot gets stuck (`FailReason.Stuck`) or where express misses are more than half the fails.
  - It keeps only boards that `Solver` proves solvable without boosters. When no candidate works, it retries with one fewer cargo item.
- **Tutorial levels** 1-3 are hand-made in `Program.cs`. **Debut levels** are generated like the rest, but a candidate must also teach its cargo (`Teaches`): the new cargo sits where the player meets it in the first few moves, for example a frozen crate with an open path that thaws in 2-4 moves.
