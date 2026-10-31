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
```

Options: `--seed N` (default 20261026) and `--out DIR`. A full run takes about 20 s on a 10-core Mac. New level files need a Unity `.meta` (copy `level_001.json.meta` with a fresh guid).

## How it works

- **Plan** (`LevelPlan.cs`): the knobs for each level, such as board size, colors, holding slots, the cargo it uses and the target fail rate. The content curve is design v4 §4:
  - The difficulty is a sawtooth: within each block of five levels it goes relief or easy, easy, medium, harder, then HARD (every 5th level from 10, so every storm finale is HARD). Fail targets: debut and relief about 0.03 (at most 0.05), medium up to 0.36 (cap 0.40), HARD 0.58 rising to 0.68 (band 0.55–0.72).
  - Each cargo kind debuts on a hand-made level (bollard L5, frost L7, mystery L12, express L17, lock L22, strap L27, gate L42), is practised and twisted on the levels after it (the ramp table), and from L51 levels mix two or three kinds from a fixed rotation.
  - From L60 board sizes are fixed per kind: easy 6×7, medium and harder 7×8, HARD 7×9.
- **Board** (`BoardMaker.cs`):
  - It lays out gate walls (a row of bollards with the gate in its gap), mirrored bollards and holes, then picks padlocks, mystery parcels and frozen crates.
  - It picks a random legal dig order: each parcel is free once the ones before it are gone, a padlocked parcel comes after its key, a frozen crate after `layers` of its neighbours, and a gate is a virtual cell that "opens" once enough parcels are gone.
  - It colors the parcels in groups of three along that order. With `noise` 0 the truck order follows the dig order exactly. More noise moves parcels further from their truck, which fills the dock.
  - Then it places what depends on color and order: each gate's color and count (from the trucks that fill before it opens), express parcels (at an early dig position, with spare moves on top) and straps (two adjacent parcels of one color).
  - Every candidate must validate and start with a legal move.
- **Grading** (`CasualBot.cs`): the casual bot takes an express parcel with 3 or fewer moves left 75% of the time, else a visible match 60% of the time, else a random legal move (`GetMoves` + `CanTake`). Its fail rate is the difficulty measure.
  - The generator searches noise across many candidate boards until the fail rate is close to the plan's target and inside its band.
  - It rejects candidates where the bot gets stuck (`FailReason.Stuck`) or where express misses are more than half the fails.
  - It keeps only boards that `Solver` proves solvable without boosters. When no candidate works, it retries with one fewer cargo item.
- **Debut and tutorial levels** are hand-made in `Program.cs`. Each one checks its teaching intent when it is built; for example, the lock debut's key must be free and fit the first truck.
