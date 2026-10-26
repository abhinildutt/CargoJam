# levelgen

Generates and grades the 60 Cargo Jam levels in `CargoJam/Assets/Resources/Levels/`. It compiles the game's real `CargoJam.Core` sources, so the generator always plays by the shipped rules. Output is deterministic for a given seed.

Run it from the repo root with the .NET SDK bundled with Unity (build output goes to `tools/.out/levelgen/`):

```sh
DOTNET=/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet

$DOTNET run --project tools/levelgen -c Release                    # regenerate level_001..060.json and print the table
$DOTNET run --project tools/levelgen -c Release -- --dry-run        # same, but write nothing
$DOTNET run --project tools/levelgen -c Release -- --levels 25-30   # only some levels
$DOTNET run --project tools/levelgen -c Release -- grade            # grade the files that are there now
$DOTNET run --project tools/levelgen -c Release -- explore --levels 40   # bot fail rate vs. noise, for tuning
```

Options: `--seed N` (default 20261026) and `--out DIR`.

## How it works

- **Plan** (`LevelPlan.cs`): the knobs for each level, such as board size, colors, holding slots, mystery and lock counts, and the target fail rate. The difficulty is a sawtooth: within each block of five levels it goes relief or easy, easy, medium, harder, then HARD (every 5th level from 10). Levels 1–3 are hand-made tutorials.
- **Board** (`BoardMaker.cs`): it first picks a random legal dig order, where each parcel is free once the ones before it are gone and a padlocked parcel comes only after its key. Then it colors the parcels in groups of three along that order. With `noise` 0 the truck order follows the dig order exactly. More noise moves parcels further from their truck, which fills the dock.
- **Grading** (`CasualBot.cs`): the casual bot takes a visible match 60% of the time; otherwise it makes a random legal tap. Its fail rate is the difficulty measure. The generator searches noise across many candidate boards until the fail rate is close to the plan's target. It rejects candidates where the bot gets stuck on locks, and it keeps only boards that `Solver` proves solvable without boosters.
