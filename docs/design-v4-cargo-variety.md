# Cargo Jam — Design v4: cargo variety, a 100-level curve and livelier music

*Drafted 31 Oct 2026 · builds on design v3 (premium journey) · branch `feature/v4-cargo-variety`*

## 1. Why

Playtesting found the game "samey after 5–6 levels". The content agrees with that verdict:

- Nothing new happens between L4 and L15. Mystery debuts at L15 and locks at L25, both on HARD levels with no teaching.
- Levels only grow in size and color count.
- The music is six calm loops, one per destination.

**Genre research** (Royal Match, Candy Crush, Screw Jam, Pixel Flow, Bus Jam, Color Block Jam, Match Factory):

- Successful games add a new element about every 8 to 15 levels. Royal Match has added 11 blockers by L91, and Pixel Flow 9 by L100.
- Each element is taught on an easy level, then practised, twisted and combined, on a sawtooth difficulty curve.
- Move countdowns are preferred over real-time clocks. Candy Crush removed timed levels in 2018, and Bus Jam's timers draw "buy more time" complaints.
- Music fatigue is fought with layers, variation and situational energy, not more tracks.

**User decisions:**

- Timers are **tap countdowns only**.
- Add **all four** mechanic families.
- Music stays a **calm base with livelier swaps**.
- **Regenerate levels 1–100.**

**Goals.** Players meet a new element about every 5 levels early on. Each one is taught with a "New cargo!" card and a hand pointer, and HARD levels combine them. The music grooves, drives and tenses up as the situation changes.

**Non-goals.**

- Real-time timers.
- Conveyors, double-size crates and a second bay. These break the immutable `Parcel.Cell`, one-parcel-per-cell and single-truck assumptions.
- New boosters.
- Monetized continues for express fails.

**Constraints that still apply.**

- Readable in 3 s.
- Fair fails.
- Never punish a mis-tap.
- Input is never blocked by animation.
- 60 fps on iPhone 11 or a low-end Android.
- ≤150 draw calls.
- No per-frame GC.
- Colors stay distinct in grayscale.
- `Scripts/Core` stays engine-free.

## 2. Level data format

### Cell characters in `rows`

| Char | Meaning |
|---|---|
| `.` | empty |
| `RBGYPO` | parcel |
| `rbgypo` | mystery parcel |
| `#` | **bollard** |
| `+` | **customs gate** (detailed in `gates`) |

### New arrays

These follow the `LockData` pattern. A missing array means none: it reads as null under System.Text.Json and as empty under JsonUtility.

```json
"frost":   [ { "x": 1, "y": 0, "layers": 3 } ],
"express": [ { "x": 4, "y": 1, "taps": 9 } ],
"straps":  [ { "x": 0, "y": 2, "x2": 1, "y2": 2 } ],
"gates":   [ { "x": 3, "y": 1, "color": "R", "count": 3 } ]
```

The backing classes are `FrostData{x,y,layers}`, `ExpressData{x,y,taps}`, `StrapData{x,y,x2,y2}` and `GateData{x,y,string color,count}`. `color` is a string so that JsonUtility can read it. A frost entry's `layers` is the number of moves until the crate thaws; the field kept its name from the first frost rule (layers of ice cracked by neighbours) so older files still read.

### Validation (`LevelDefinition.Validate`)

**Count invariant.** For each color, the number of parcels equals trucks × capacity. Only parcel characters are counted; `#` and `+` hold none. A strapped pair counts as 2. A level has at least one parcel.

**Gates**
- Every `+` has exactly one `gates` entry, and every entry points at a `+`.
- The color is valid and has at least one truck.
- 1 ≤ count ≤ that color's parcel count.
- At most 3 gates per level.

**Frost**
- Only on a parcel cell, with one entry per cell.
- 1 ≤ layers ≤ 9 (`MaxFrostMoves`: moves to thaw, one figure on the ice). Any cell qualifies, front row included.

**Express**
- Only on a parcel cell, with 1 ≤ taps ≤ 99.
- Not on a mystery, padlocked, key, frozen or strapped parcel.

**Straps**
- Both cells are parcels, orthogonally adjacent, and the same color.
- A parcel is in at most one strap.
- Neither parcel is a mystery, padlocked, key, frozen or express parcel.
- truckCapacity ≥ 2.

**Locks.** The existing checks already reject `#` and `+`, because their color is None.

### New API

- `IsBollard(x,y)`, `IsGateCell(x,y)`
- `FrostAt(x,y)` (0 = none), `ExpressAt(x,y)` (0 = none)
- `IReadOnlyList<ParcelStrap> Straps`, `IReadOnlyList<ParcelGate> Gates`
- `bool Uses(CargoKind)`, `int CargoMask`

The new Core enum is `CargoKind { Bollard, Frost, Mystery, Express, Lock, Strap, Gate }`, in debut order. Its int values are persisted in the profile.

## 3. Rules

### Board

- `Board` gets an obstacle mask: `IsObstacle(Cell)`, `internal SetObstacle(Cell,bool)` and `internal CopyObstaclesFrom(Board)`.
- `FindExitPath` and `GetFreeParcels` treat obstacles as walls. An obstacle in row 0 does not seed the free search.
- `Get` returns null on an obstacle, and `Count` counts parcels only.
- A parcel in row 0 is always free.

### Session API

The solver, bot, controller and views all use the same API:

- `IsRestrained(p)`: padlocked or frozen.
- `FrostLeft(p)` (moves until it thaws, 0 when not frozen), `ExpressLeft(p)`, `StrapPartner(p)` (null when not strapped now), `GateLeft(gateIndex)`, `IsGateOpen(gateIndex)`.
- `IsUnitFree(p)`: p, or its strap partner, has an exit path.
- `GetMoves(List<Parcel>)`: one representative per unit that is free and not restrained. For a pair this is the free member, listed once.
- `CanTake(p, extraSlots = 0)`:
  - Single parcel: the truck accepts it, or the dock has a free slot.
  - Pair: `min(truckRoom if color matches else 0, 2) + freeSlots + extraSlots ≥ 2`.
- `HasLegalMove()`: some unit in `GetMoves` passes `CanTake`.
- `FailReason FailReason`.
- `HasFreeMatch` is kept.

### FailReason

`enum FailReason { None, OutOfSpace, Stuck, ExpressMissed }`. `CheckForEnd` checks in this order:

1. Board and dock both empty → Won.
2. A pending express miss → ExpressMissed.
3. No legal move:
   - **OutOfSpace** if `CanTake(p, extraSlots: 1)` holds for some unit.
   - Otherwise **Stuck**.

Continue (Extra Slot) is only offered after OutOfSpace. `AddHoldingSlot` refuses to resume play after any other reason.

**Behaviour change.** A full dock whose only free parcels are padlocked or frozen is now Stuck, so no Continue is offered.

### Event order per move (Tap or Crane)

```
[Crane only: ParcelRevealed, ParcelUnlocked, FrostCracked(left 0, lifted), StrapCut]
[pair only:  StrapMoved(lead, trail)]
per departing parcel (lead, then trail):
  ParcelLoaded | ParcelHeld
  ParcelUnlocked*      (if loaded and a key)
  GateProgress*, GateOpened*   (if loaded and the gate's color)
  TruckDeparted, TruckArrived, ParcelLoadedFromDock (+ its ParcelUnlocked*, GateProgress*/GateOpened*) ...
ExpressTicked*         (once per MOVE, for express parcels still in the yard)
FrostCracked*          (once per MOVE, for crates still frozen, in parcel id order; left 0 = thawed)
ParcelRevealed*        (RevealFree; also sees cells opened by gates)
LevelWon | ExpressMissed, LevelFailed(ExpressMissed) | LevelFailed(OutOfSpace|Stuck)
```

A crate whose count reaches 0 in a move is free from the next move on: the end check that follows already counts it as a legal move.

With two or more gates, a load's gate events interleave per gate: `GateProgress(0)`, `GateOpened(0)`, then `GateProgress(1)`, `GateOpened(1)`. The controller keeps per-gate timings to match.

`FinishMove(events, tick)` runs TickExpress, then TickFrost, then RevealFree, then CheckForEnd. Undo and AddHoldingSlot call it with `tick: false`.

### New events

- `FrostCracked(Parcel, int left, bool lifted)`: one move went by on a frozen crate (`left` moves to go, 0 = thawed), or with `lifted` the Crane thawed it outright.
- `ExpressTicked(Parcel, int left)`, `ExpressMissed(Parcel)`
- `StrapMoved(Parcel lead, Parcel trail)`, `StrapCut(Parcel lifted, Parcel partner)`, `StrapRestored(Parcel a, Parcel b)`
- `GateProgress(int gate, int left, Parcel loaded)`, `GateOpened(int gate)`

`LevelFailed` gains `FailReason Reason`.

### Bollard

A permanent obstacle. It cannot be tapped or targeted by the Crane.

### Customs gate

- A closed obstacle.
- Every load into a truck of the gate's color counts it down, including loads from the dock. Holds don't count.
- At 0 the gate opens, becomes empty and emits `GateOpened`.
- It never closes again, Undo included.

### Frozen crate

The mirror of an express parcel: express must leave within N moves, a frozen crate can't leave until N moves have gone by.

- **The count.** The crate shows N (1–9), the moves until it thaws.
- **Taps and paths.** While N > 0 it stays in place, even with a free path (in the front row, say), and blocks paths for others. A tap on it is `ParcelBlocked`: nothing changes and nothing ticks.
- **What counts down.** Exactly the moves that tick express parcels: each successful move that takes something out of the yard (a load, hold or Crane lift) counts every frozen crate down by one, once per move. A strapped pair's move counts once. Rejected or blocked taps, dock chains, gate openings, Undo and Extra Slot don't count.
- **Thawing.** At 0 it emits `FrostCracked(left 0)` and is an ordinary parcel from the next move on.
- **Crane.** Thaws the crate fully first (`FrostCracked(left 0, lifted)`).
- **Undo.** Never re-freezes it and never refunds the count.
- **Frozen mystery.** A frozen mystery parcel is still revealed once it has a path, so the player sees its color under the ice.

### Express parcel

- **What ticks.** Each successful move that takes something out of the yard (a load, hold or Crane lift) ticks every express parcel still in the yard, once per move.
- **What doesn't tick.** Rejected or blocked taps, dock chains, gate openings, Undo and Extra Slot.
- **Its own move.** It does not tick on the move that ships it. Once out of the yard its count freezes and it is safe.
- **Missing.** The shown number is the moves left, counting the one that ships it. When a tick reaches 0 while it is still in the yard, the level fails with ExpressMissed. There is no Continue.
- **Undo.** Undo never refunds ticks. Like thaw counts, reveals and gate progress, it is not a snapshot. An express parcel returned by Undo keeps its frozen count and resumes ticking.

### Strapped pair

**Tapping.** Tap either member while the pair has an exit path. The free member leads, so a tap on the blocked member still works. The trail parcel's path is its own cell plus the lead's path.

**Routing.** Both parcels leave the board and are routed lead-first by the single-parcel rules:

- **Matching truck with room ≥ 2:** both load. If the room was exactly 2, the truck departs after the trail.
- **Matching truck with room 1:** the lead loads. The truck departs, the next arrives and chain-loads from the dock. Then the trail goes to that truck if it accepts, else to the dock.
- **No match:** both go to the dock (2 slots).

**Why routing works this way.** Every parcel follows the rule "go to the dock only if the current truck rejects it", so the game never freezes waiting for a parcel already in the dock.

**Rejecting.** If `CanTake` fails, the tap is `DockFullRejected` and nothing changes.

**Lifetime.** A strap lasts while the pair is in the yard. `StrapMoved` ends it as the pair leaves, because the two members may end up in different places (a truck, the dock). `StrapRestored` brings it back when Undo returns both. On screen the band slips off as the pair leaves (`StrapView.Release`) and grows back on Restore.

**Undo.** If the last held parcel p was `heldAsPair` and its partner is the next entry in holdOrder and still in the dock, Undo returns both (`ParcelReturned` ×2, then `StrapRestored`). Otherwise p returns alone and the strap stays cut.

**Crane.** It cuts the strap (`StrapCut`) and lifts only the tapped parcel.

### Solver and CasualBot

**Solver**
- Uses `GetMoves` filtered by `CanTake`.
- Move order: express parcels with ≤2 left, then matches, then the rest.
- Node limit 200k.

**CasualBot**
- Legal moves come from `GetMoves` + `CanTake`.
- If an express parcel with ≤3 left is takeable, the bot takes it with probability 0.75.
- `BotStats` gets `ExpressFails`. "Stuck" now means `FailReason == Stuck`.

### Clone and StateKey

**Clone** copies the obstacles, frost, expressLeft, strapped, heldAsPair, gateLeft and FailReason.

**StateKey:**
- On the board, a bollard or closed gate is written as `#` and an open gate as `.`.
- Then `|F` frost digits, `|E` express left per id (`-` once out), `|G` gate counts, and `|S` strap bits.

## 4. Content curve (levels 1–100)

Levels 1–3 stay hand-made. The sawtooth per block of 5 stays: easy, easy, medium, harder, then HARD or relief.

### Debut levels

Each debut is on an easy, non-HARD level. None falls on an arrival level (11, 21, …) or a booster-unlock level (6, 11, 16). Debut levels have 3 colors and 5 slots, use only the debuting cargo (plus bollards), are hand-made, and target about a 0.03 fail rate.

| Cargo | Bollard | Frost | Mystery | Express | Lock | Strap | Gate |
|---|---|---|---|---|---|---|---|
| Debut | L5 | L7 | L12 | L17 | L22 | L27 | L42 |

### Ramp

| Levels | Content |
|---|---|
| 4–10 | L6 bollards; L8 frost ×2 (thaw 2–3); L9 frost (thaw 2–4) + bollards; L10 HARD frost ×3 (thaw 3–5) + bollards |
| 11–20 | L13 mystery + frost; L15 HARD mystery + frost; L18 express (slack 6); L19 express + mystery; L20 express (slack 4) + frost |
| 21–30 | L23 lock + bollards; L24 lock + express; L25 HARD lock + mystery + frost; L28–29 straps (L29 + lock); L30 straps + express |
| 31–40 (twists) | L33 frost on a mystery; L35 HARD frost in row 0 for sure + straps; L36 express ×2; L38 frost on a key; L40 express ×2 with tight slack + locks |
| 42–50 | Gates; L44 gate + frost; L45 HARD gate (count 6) + mystery + straps; L46–50 gates in two colors; L50 gates + locks + express |
| 51–60 | Two cargo types per level, three on HARD |
| 61–100 | A deterministic rotation of 2–3-type combos; HARD gets 3–4, relief gets 1 |

### Fail targets and sizes

- Fail-rate targets: medium caps at 0.40; HARD runs 0.55–0.72; debut and relief levels stay ≤0.05.
- Across L61–100 the targets ramp gently, inside those caps: easy 0.30→0.36, medium 0.34→0.385, harder 0.41→0.47, HARD 0.64→0.70. Relief stays at 0.03.
- Board sizes from L60: easy 6×7, medium and harder 7×8, HARD 7×9.
- Parcels = (cells − holes − bollards − gates) / 3 × 3.

### Generator placement

**Bollards**
- They replace some holes and are mirrored left–right.
- None in row 0 before L35.
- Candidates go through the DigOrder legality check.

**Gates**
- Laid out from a wall template: a row of bollards across row r (2 ≤ r ≤ h−3) with 1–2 gaps, and the gate in a gap.
- DigOrder treats the gate as a virtual cell dug when it opens.
- Color C is chosen from truck groups that finish before the gate in the order. Count = 3 × the number of such C trucks, capped at 3 for the debut and 6 after.
- The gate must wall something off.

**Frost**
- A crate only matters where it would otherwise be free soon, so three in four go in rows 0–1 (the front row, or right behind it); the rest go anywhere.
- Thaw counts by level kind: relief 2–3, easy 2–4, medium 2–5, harder and HARD 3–6 (L8–L10 as in the ramp table).
- At most a third of row 0 is frozen, so a level always opens with moves to make.
- In the dig order a crate becomes diggable once its thaw count of parcels is gone (one parcel per move).
- The debut (L7) is hand-made: a yellow crate in row 0 thaws in 3 moves, right beside the first truck's three free reds, and it is the second truck's color, so loading the reds thaws it just as its truck pulls in. A second crate in row 1 waits out 5.

**Express**
- Placed at dig position p, with 2 ≤ p ≤ min(12, n/3).
- taps = p + 1 + slack, never less than p + 2.
- Slack is 8 at the debut, 5 in practice and 2–3 on HARD levels.

**Straps**
- Two adjacent plain parcels of the same color, preferably in the same truck group.
- At the debut, the strap sits in rows 0–1 and is the first truck's color.

**Mystery and locks**
- They use the existing pickers, with the new cargo added to their exclusions.
- Twist flags lift the exclusion for frost on a mystery and frost on a key.

Content loops after L100 from `LoopStart = 60`.

## 5. Teaching

- **First sighting is tracked in the profile, not by level number,** so existing players meet the new cargo too.
  - `ProfileData` v3 adds `bool[] cargoSeen`, with `CargoKinds = 7`.
  - The v2→v3 migration marks Mystery as seen if levelIndex ≥ 14, and Lock as seen if levelIndex ≥ 24.
  - A test asserts `Enum.GetValues(CargoKind).Length == ProfileData.CargoKinds`.
- **`CargoIntroPopup`** shows "New cargo!", an icon disc, the cargo's name, one line, and a "Got it" button. Each icon draws the cargo as it looks on the board (a white post with a dark band, a grey tarp with a "?", the dark express display).
  - It is queued after the level intro (and after the arrival or HARD banner when present).
  - "Got it" marks the cargo seen. The next card follows, or else the hand pointer starts.
- **Card lines:**
  - Bollard: "Bollards never move. Plan around them."
  - Frost: "Frozen crates thaw after their number of moves. Make other moves first."
  - Mystery: "Covered parcels show their color once they're free."
  - Express: "Ship express parcels before their number runs out. Every move counts."
  - Lock: "Load the key parcel into a truck to open the padlock."
  - Strap: "Strapped parcels travel together and need two spaces."
  - Gate: "Gates open once enough parcels of their color are loaded."
- **`Tutorial.BeginCargo(kind)`** points the hand at `GameController.TryGetCargoHintCell(kind)` and hides it on the first move.

  | Cargo | Hand points at |
  |---|---|
  | Frost | another good move (the usual hint, never a frozen crate), so the player sees the count drop |
  | Express | the express parcel, if takeable |
  | Lock | the key |
  | Strap | the strap's free member |
  | Gate | a free parcel of the gate's color that the truck accepts |
  | Mystery | the parcel covering it |
  | Bollard | no hand |

- **FailPanel titles:** "Out of space", "No moves left" and "Express missed". The express subline is "An express parcel ran out of moves." Continue shows only for OutOfSpace.
- **Analytics:** `level_fail` gains a `reason` field.

## 6. Visuals

All visuals are grayscale-safe and built from cached static meshes. Cargo parts (ice, express labels, straps, gates) draw with the parcel skin, a 16×1 texture whose extra columns hold the ice, snow, express, strap and frost-tint colors, so they batch with the parcels. None of them casts a shadow.

- **ParcelView.Create** takes `in ParcelLook look`, with fields `Hidden, Locked, IsKey, Frost, Express`.
- **Frost:** a raised collar of clear ice around the top of the box, readable from the 58° camera, with frost down the upper sides, two crystals on the back corners and icicles along the front.
  - Inside the collar stands the thaw countdown, the size of an express label: a deep blue display on an ice rim with the moves left in white seven-segment figures, and the color's symbol on a paper chip in its corner. Dark and white read in grayscale, and the ice around it tells it from an express label.
  - A crate still under its tarp gets neutral ice (clear frost, a "?" on the chip), so its color doesn't show through.
  - Each color, count and variant is one cached mesh on the parcel skin (a deep-blue skin column), so it shares the parcel's draw call.
  - The parcel body uses the dim skin while frozen.
  - `Crack(tweener, at, left)` swaps to the new count with a light knock, as the move's express ticks land. At 0 the ice shrinks away with the shard burst.
- **Express:** a dark countdown display with an amber rim replaces the shipping label, with the moves left in amber seven-segment figures over 40% of the lid tall. The color's symbol sits on a paper chip in its corner. Dark and amber stand out on every parcel color, so an express parcel reads at a glance at any count.
  - Each color, count and ink is one cached mesh, drawn on the parcel skin, so it shares the parcel's draw call.
  - At ≤2 the display turns red with white figures and the parcel pulses.
  - API: `SetExpress(left, tweener, at)`.
- **StrapView:** a flat slate webbing band with pale stitched edges and a steel buckle over both lids, as one mesh. Its ends fold over the outer lid chamfers and never hang past the pair.
  - It is parented to the member in the later row.
  - API: `Snap(at)`, `Release(at)`, `Restore(at)`.
- **Bollards:** concrete posts in the Curb material with a dark band, merged into the Scenery yard mesh for +1 draw call. The floor tile under each one is skipped. Bollards are never yellow.
- **GateView:** a post and a boom arm carrying a road-sign plate in the gate's color with the count in big white figures (dark on yellow). The color's symbol sits on a label atop the post, away from the count, so a purple ✖ never reads as "× 3". Two renderers: the post, and the boom with its sign (one mesh per color and count).
  - `SetCount(left, at)` twitches the boom. `Open(at)` raises the boom 80°, sinks the gate (0.35 s) and deactivates it.
- **ILevelFx additions:**
  - `FrostCracked(ParcelView, int left, float at)`
  - `ExpressTicked(ParcelView, int left, float at)`
  - `StrapSnapped(StrapView, float at)`
  - `GateProgress(GateView, int left, float at)`
  - `GateOpened(GateView, float at)`
  - `TensionChanged(bool tense, float at)`
  - `Fail(float at, FailReason reason, IReadOnlyList<ParcelView> needed, TruckView waiting)`
- **SFX:** IceCrack, IceShatter, ExpressTick (rising pitch), StrapSnap, GateTick and GateOpen.
- **Haptics:** Tick when a crate thaws, Warning when an express count hits 1. (Each frost tick gets a soft IceCrack and a few chips; the thaw gets IceShatter.)

## 7. Music

### Selection (`MusicSelector`)

- style = `Journey.StopFor(i)`
- flavor = `(Journey.LegFor(i) / StopCount) % 2`. Flavor 1 plays a whole step up with alternate patterns, so L61+ sounds different.
- variant:
  - **Storm**: HARD on a Storm-mood level.
  - **Drive**: HARD otherwise.
  - **Groove**: Dusk.
  - **Calm**: everything else, and always on the menu.
- Key = `((style*4 + variant)*2 + flavor)`.
- The next cue is prefetched using `LevelViews.NextHard`.

### Synth (`MusicSynth`)

- **Structure.** 16 bars as A + B halves. B uses PatternC/D, adds bass and drum fills in bars 8 and 16, and drops the arpeggio in bar 16.
- **Timing.** Events snap to an integer 16th-note grid, with swing on the odd 16ths. The loop is exactly 16 × samplesPerBar.
- **New voices:**
  - Kick: a sine sweep from 120 to 45 Hz, 0.25 s.
  - Snare: hash noise plus a 180 Hz tone, 0.18 s.
  - Hat: high-passed noise, 0.05 s.
  - Bass: sin + 0.4·sin2 + 0.15·sin3, an 8th-note long, on the chord root.
- **Variants.** `x` is an accent and `o` a ghost note. Bass numbers are semitones above the chord root per 16th; −1 is a rest.

| Variant | Kick | Snare | Hat | Bass | Other |
|---|---|---|---|---|---|
| Calm | — | — | B half only: `..o...o...o...o.` | — | — |
| Groove | `x.....x.x.......` | `....x.......x...` | `..x...x...x...x.` | `0,-1,-1,0,-1,-1,12,-1,0,-1,-1,7,-1,-1,12,-1` | — |
| Drive | four on the floor, fill `x...x...x.x.xxxx` | `....x.......x..o` | `xoxo…` | `0,-1,0,-1,12,-1,0,-1,0,-1,0,-1,12,-1,7,-1` | tempo ×1.08, pad ×0.8 |
| Storm | as Drive | as Drive | as Drive | as Drive | the dark harmony, no slowdown |

- **Per-style mix.** Each style sets Kick, Snare, Hat and Bass gains (around 0.5 / 0.25 / 0.12 / 0.22) and Swing. Farm uses 0.10 swing and Metro 0.15.
- **Loudness.** Normalization is RMS-targeted: `gain = min(targetRms/rms, 0.95/peak)`.
- **Render cost.**
  - The pad is rendered once and copy-added into the B half.
  - Pluck notes last `min(0.9, 4.6/decay)` s.
  - The existing per-frame budgets of 60k and 20k samples are kept.
  - Music renders at 16 kHz (`MusicSynth.Rate`), down from 22.05 kHz, to keep the cue cache near 7–9 MB. It trims the hats' air above 8 kHz.

### Tension layer

- A 2-bar clip at the same tempo and swing: hats on 16ths, a woodblock at tonic + 2 octaves, and a sub pulse.
- It plays on a third AudioSource, started with `PlayScheduled` alongside the main clip so the two stay beat-locked.
- `SetTension(bool)` fades it in over 0.5 s and out over 1.5 s.
- It is on while the dock is at capacity−1 or any express parcel in the yard has ≤2 left. Fail turns it off.

### Cache

- Up to 4 main clips and their tension clips are cached.
- Rendering is skipped while music is off.

## 8. Build streams

| Stream | Owns |
|---|---|
| Orchestrator | `GameController.cs`, `Contracts.cs`, `Timing.cs`, `Editor/**`, this doc |
| S1 core | `Scripts/Core/**`; EditMode tests except `Meta/` and `LevelContentTests.cs` |
| S2 levels | `tools/levelgen/**`, `Resources/Levels/**`, `LevelContentTests.cs` |
| S3 views | `ParcelView`, `GateView`, `StrapView`, `Scenery`, `MaterialBank`, `Symbols`, `Digits` |
| S4 feel | `Game/Feel/**`, `Presentation/Fx/**` |
| S5 UI/meta | `UI/**`, `Meta/**`, `Tests/EditMode/Meta/**`, `Game/Meta/Analytics.cs` |

**Merge order:** S1, then S2 (regenerated against the merged S1), then S3, S4 and S5, then the orchestrator integration.

**Check:** `python3 -I tools/check/check.py`.
