# Cargo Jam — Design v3: premium look, journey meta, living scenery

*Orchestrator decisions for the v3 push, 31 Oct 2026. This document is the source of truth for the parallel work streams. The code contracts in §8 are already stubbed on `feature/v3-premium-journey`. This document replaces the visual-language section of design v2 (§4); the rest of design v2 still applies unless this document overrides it.*

## 1. Why

The game plays well, but it looks like a kids' game:
- candy buttons with gloss strips and thick bottom edges
- white text with heavy outlines
- an overshoot bounce on every animation
- a home screen with about nine competing elements
- a bare beige ground plane during levels

We want a **premium casual** feel (Royal Match / Lily's Garden tier: warm and rich, but refined) for a broad 25–45 audience, and a reason to come back tomorrow.

**What we learned from the market:**
- The top games use a one-button home: Level N, with everything else on the edges.
- Gameplay pieces stay saturated while the UI and backgrounds calm down. Restraint reads as premium.
- Themed worlds plus time-of-day variety keep later levels fresh.
- A well-timed win sequence is the single biggest perceived-quality win.
- Visible streak tiers are one of the strongest retention levers.
- Fail screens should be honest: keep the board visible, and use no fake urgency or guilt.

**Anti-goals** (failure even if everything technically works):
- parcels or symbols that are harder to read than today
- UI that blocks input during animations
- lower fps on an iPhone 11-class device
- dark patterns: fake timers, guilt copy, hidden odds, or offers that cover the board

## 2. Journey (replaces the 20-level routes)

**Destinations.** There are six destinations of 10 levels each, which loop after level 60:

| Stop | Levels (1-based) | Destination | Feel |
|---|---|---|---|
| 0 | 1–10 | **Harbor Town** | Quay, stacked containers, cranes, water, gulls |
| 1 | 11–20 | **Farmlands** | Fields, barns, fences, round hay bales, trees |
| 2 | 21–30 | **Canyon Highway** | Red mesas, cacti, a gas station, heat haze |
| 3 | 31–40 | **Pine Ridge** | Pines, rocks, a log cabin, distant peaks |
| 4 | 41–50 | **Metro City** | Storefronts, streetlights, crosswalks, lit windows |
| 5 | 51–60 | **Frost Port** | Snow, ice, a lighthouse, warm windows, falling snow |

**Moods within each stop**, by the level's position in the stop (`step` 0–9):

| Step | Mood |
|---|---|
| 0–4 | `Day` |
| 5–8 | `Dusk` (golden hour; lit windows and streetlights turn on) |
| 9 | `Storm`. This is also the destination's HARD finale (levels 10, 20, …). |

The HARD flags in the level files are unchanged: levels 15, 25, … are still HARD, just in their normal mood.

**Variation.** Every level re-seeds the prop layout from its level index, so no two boards look identical. Seed = `levelIndex * 7919`.

**Arrival.** The first level of a new stop (11, 21, …) plays the *arrival reveal*: a camera pan over the new scenery, a destination name card, and a postcard stamp. A postcard is earned when the player **reaches** a destination, which includes Harbor Town at the first launch.

## 3. Screens

**Home**

- **Removed from Home:** the logo (moves to a first-launch splash), the chest card, the route bar, the streak chip in the top row, and the daily button in the top row.
- **Background:** a full-screen live 3D diorama of the current destination at the current mood.
- **Top-right:** a quiet coin pill.
- **Top-left:** a quiet settings button.
- **Left edge** (vertically centered): a slim column with at most two 112px glass icon buttons, Daily and Chest. **Each one shows only while it is claimable**, with a small gold dot.
- **Bottom third:**
  - Destination name in display type ("Pine Ridge"), with the mood as a caption ("Golden hour").
  - Journey strip: up to 10 dots for the stop's levels, a truck marker on the current one, and the caption "3 deliveries to Metro City". Tapping it opens the Journey screen.
  - **Level N** button: full-width minus gutters, 168px tall. When a streak tier is active it carries a small flame and tier chip ("×1.5").
- **Daily reward:** no longer pops up on its own. The Daily icon's dot is the only prompt.

**Journey screen**

- Full-screen, with a vertical scrolling road drawn with Painter2D.
- **Stops:** each stop is a postcard tile. Reached stops show their name, an illustration made of icon shapes, and the stamp. Locked stops show a silhouette and "Level 31".
- **Truck marker:** sits between stops in proportion to progress, and animates forward when the screen opens after a win.
- **Closing:** a back button.

**In-level HUD**

- **Top:** pause (glass circle), "Level 12" in a glass chip with a HARD tag when hard, and the coin pill.
- **Bottom:** three booster buttons as 156px glass discs with ink icons. Each disc shows a count badge, a price chip, or a lock chip.
- **Scenery shows through:** the HUD is a glass treatment, not opaque bars.

**Win**

1. The last truck departs, with a 0.3 s slow-down to 0.4× (FX side).
2. A bottom-anchored card rises: "Delivered", a coin count-up, and 8–15 coins flying along curves to the counter. Each landing plays a tick haptic and a rising pitch, and the counter squashes.
3. The streak-tier chip and the journey strip advance.
4. **Next** appears at about 2.2 s.
5. A tap anywhere skips to the end state.

**Fail**

- A **bottom sheet** covers no more than the lower 45% of the screen, so the stuck board stays visible above it.
- Content: "Out of space", "2 parcels to go", **Continue +1 slot · 300**, and **Try again**.
- If a streak tier would drop, one neutral line: "Streak will drop to ×1.5". No guilt copy.

**Popups (Pause/Settings, Daily, Chest, Buy booster, Unlock)**

- Off-white sheet or card, title in title type, no ribbons, a quiet ✕.

**Copy rules**

- Sentence case.
- No exclamation marks on routine labels. One is allowed on a genuine celebration, such as "Big chest!".

## 4. Visual language (tokens)

All UI sizes are in panel units at the 1080×1920 reference (PanelSettings: ScaleWithScreenSize, match 0.5).

**Colors**

| Token | Value | Use |
|---|---|---|
| `--ink` | `#1E2A3B` | Primary text, icons on light |
| `--ink-2` | `#4A5869` | Secondary text |
| `--ink-3` | `#8A94A1` | Captions, disabled |
| `--surface` | `#FBF7F0` | Cards, sheets |
| `--surface-2` | `#F1EADF` | Insets, rows, tracks |
| `--stroke` | `rgba(30,42,59,0.10)` | Hairlines |
| `--glass` | `rgba(18,26,38,0.46)` | HUD chips over 3D |
| `--glass-stroke` | `rgba(255,255,255,0.14)` | Glass hairline |
| `--on-glass` | `#FFFFFF` | Text on glass |
| `--primary` / `--primary-press` | `#2FA866` / `#258A53` | The one CTA per screen |
| `--secondary` / `--secondary-press` | `#2E3E56` / `#24324A` | Secondary actions |
| `--gold` / `--gold-deep` | `#E9B44C` / `#C8912E` | **Rewards only**: coins, chest, streak |
| `--danger` | `#D9534F` | Destructive, HARD tag (rare) |
| `--scrim` | `rgba(12,18,28,0.55)` | Behind modals |

**Type scale**

- **Font:** Nunito, using **Black** (900) for display, title and button text and **SemiBold** (600) for everything else.
- **Sizes:**

| Token | Size |
|---|---|
| `--fs-display` | 96 |
| `--fs-title` | 64 |
| `--fs-heading` | 48 |
| `--fs-body` | 38 |
| `--fs-label` | 32 |
| `--fs-caption` | 28 |
| `--fs-micro` | 24 |

- **Text effects:** no outlines. Text on glass or 3D gets a soft `text-shadow: 0 2px 6px rgba(0,0,0,0.35)`. Text on surfaces gets none.

**Space and shape**

- Spacing: 8 / 16 / 24 / 32 / 48 / 64.
- Radii: 20 (chips), 32 (buttons, cards), 56 (sheets), and pills fully rounded.
- Elevation: there is no `box-shadow`, so fake it with a blurred-look underlay, a sibling element offset 6–10px with `rgba(12,18,28,0.18)` and the same radius. Never use a hard bottom-border "edge".

**Buttons**

- **Look:** flat fill with a 1px lighter inner top hairline, about 12% white.
- **Press:** scale to 0.97 and switch to the `-press` color, over 120 ms.
- **Release:** OutQuint back to 1 over 180 ms, with no overshoot.
- **Removed:** the gloss strip.

**Motion**

| What | Duration | Easing |
|---|---|---|
| Press | 120 ms | |
| Small transitions | 220 ms | OutQuint |
| Panels and sheets | 320 ms | OutQuint |
| Hero / reward | 480 ms | OutBack(1.2) |

- **Overshoot is for reward moments only:** coins, chest, stamp, tier-up.
- **One idle animation per screen at most:** a slow 2.4 s "breathe" of 2% on the main CTA.

**Icons**

- They stay vector-drawn (`IconPainter`), using the shared palette (`UiPalette`).
- Single-color icons are ink or white with no drop shadow.
- Multi-color icons (coin, chest, flame, gift, star) use the gold tokens.

**3D world**

- Parcels and trucks keep their saturated colors and white symbols, which is the readability contract.
- Everything else (ground, tiles, props) stays at least 25% less saturated than the parcel palette near the yard.
- Detail and saturation can rise with distance from the board.

## 5. Environment (scenery)

**Placement bands**, relative to `Layout.ContentBounds` (`b`) in world space:

| Band | Region | What goes there |
|---|---|---|
| Back band | `z ∈ [b.max.z + 0.6, b.max.z + 9]` | Buildings, mesas, trees, cranes. Largest props go furthest back. |
| Truck lane | `z ≈ 2.4–4.1` | **Keep clear**: trucks enter at x−11, queue left of x 1.25 and exit at x 10.25 |
| Side gutters | `|x| > b.max.x + 0.4` | Low props only (fences, crates, bushes, lamps) |
| Foreground | `z < b.min.z − 0.8` | **Left empty.** The booster bar (levels) and the Home bottom block sit over this band, and props there read as clutter behind the UI (dropped during integration). |

**Assets**

- Kenney CC0 kits live under `Assets/Resources/Env/<Kit>/` and are listed in `Assets/Resources/Env/LICENSES.md`.
- **Draw calls:** combine static props per environment build (`Mesh.CombineMeshes` per material), and turn on instancing on the environment materials. Budget: **≤150 draw calls** on a 7×9 board in the densest biome.

**Props to avoid:** anything box-shaped and parcel-sized (e.g. the Car Kit `box`) reads as an oversized game parcel.

**Falling snow** (Frost Port) comes from the FX layer's ambient life, which keeps it off the board; the environment doesn't add its own.

**Persistence**

- The environment lives **outside** the level root, like `ParticlePool`. It fully rebuilds only when the stop or mood changes.
- Between levels in the same stop and mood, only the seeded variation runs: props are re-placed and the combined meshes rebuilt.

**Lighting per biome and mood**

- Sun color, intensity and rotation; trilight ambient (sky, equator, ground); fog color and density; and the camera clear color. The camera clear color is only visible on the menu.
- Dusk lowers the sun and warms it. Storm is dark blue-grey, with rain particles from the FX stream and a darker fog.

**Post-processing**

- Turn on the code-made camera's post-processing.
- One global Volume per mood: ACES tonemapping, mild bloom (threshold 1.0, intensity ≤0.35), slight color adjustments, vignette ≤0.22.

**Trucks and parcels**

- **Trucks must look like real trucks** (user feedback on the mockup). Use the Kenney Car Kit **`Env/Cars/truck-flat`** model:
  - It has a `body` and four `wheel-*` children, uses the `colormap` texture, measures 1.5 × 1.3 × 2.75, and drives along +z.
  - Rotate it to drive along +x and scale it to the lane.
  - Tint the cab and bed to the truck's parcel color with a per-truck material (a colormap-aware tint, or a separate solid-color material on the body). Wheels and glass stay neutral.
  - Parcels sit visibly on the flatbed's 3 slots, so keep the bed-slot logic and positions.
  - The wheels spin while driving, and the body keeps the dip, punch and squash animations through the existing "Body" child pattern.
  - `truck` (box) and `delivery` are available for scenery traffic.
- Parcels get a beveled box mesh with a tape stripe. Keep the white symbol and size. The symbols (■ ● ▲ ◆ ★ ♥, one per color) exist so colorblind players and grayscale ad clips can tell colors apart, so they stay.

## 6. Engagement

**Streak tiers** (replace the ×1 / ×1.5 / ×2 multiplier steps):

| Tier | Streak | Coin multiplier | Perk |
|---|---|---|---|
| 0 | 0–2 | ×1 | — |
| 1 | 3–5 | ×1.5 | — |
| 2 | 6–9 | ×2 | — |
| 3 | 10+ | ×2.5 | One free **Undo** charge each level (not banked) |

- **Gentle loss:** giving up, or restarting after making a move, sets the streak to the **floor of the tier below**. For example, 7 drops to 3, 12 drops to 6, and 4 drops to 0.
- Continue still keeps the streak.

**Postcards**

- One postcard per destination, earned on reaching it.
- They are shown on the Journey screen. There is no album screen in this push.

**Unchanged:** the chest every 5 wins, the 7-day daily calendar and booster unlocks (levels 6 / 11 / 16) stay as in design v2.

**Deferred** (not in this push): the "Express Run" streak event, local notifications, and a postcard album.

## 7. Work streams and who owns what

Each stream works in its own git worktree, branched from `feature/v3-premium-journey`.

| Stream | Owns (may create and edit) | Must not edit |
|---|---|---|
| **A · UI system + screens** | `Assets/Scripts/Game/UI/**`, `Assets/Resources/UI/**` | Core, Meta, Presentation, Feel, GameController |
| **B · Biomes + environment** | `Presentation/Biome*.cs`, `Presentation/Environment/**`, `Presentation/Scenery.cs`, `Presentation/MenuDiorama.cs`, `Presentation/MaterialBank.cs`, `Presentation/TruckView.cs`, `Presentation/ParcelView.cs`, `Presentation/Symbols.cs`, `Assets/Resources/Env/**`, `Assets/Resources/Materials/**` | UI, Meta, Feel, Fx, GameController |
| **C · Journey + streak meta** | `Assets/Scripts/Meta/**`, `Assets/Tests/EditMode/Meta/**`, `Assets/Scripts/Game/Meta/**` | Everything else |
| **D · Juice + audio** | `Assets/Scripts/Game/Feel/**`, `Assets/Scripts/Game/Presentation/Fx/**` | Core, Meta, UI, environment, GameController |
| **Orchestrator** | `GameController.cs`, `Presentation/Contracts.cs`, the rest of `Presentation/**`, scenes, settings, docs, integration | n/a |

**Rules for every stream:**
- Keep the contracts in §8 source-compatible. If one has to change, say so in the report instead of editing someone else's file.
- Verify with `python3 -I tools/check/check.py` (compiles with editor and iOS defines, then runs the Core and Meta tests). Never open Unity.
- Commit to the stream's own branch.
- End with a report covering what was built, what couldn't be checked outside the editor, and any contract friction.

## 8. Contracts (stubbed on the branch)

- **`CargoJam.Meta.Journey`** (complete, engine-free):
  - Members: `LevelsPerStop`, `StopCount`, `StopFor(levelIndex)`, `StepInStop`, `MoodFor`, `IsArrival`, `LevelsToNextStop`, `NameOf(stop)`, `MoodName(mood)`.
  - `Mood { Day, Dusk, Storm }`.
- **`CargoJam.Meta.ProfileData` v2:**
  - New fields: `bool[] postcards`, `int lastArrivalShown` (the *leg*, `Journey.LegFor`, whose reveal has played, −1 = none, so loops replay reveals).
  - Streak tiers are derived from `winStreak`.
- **`CargoJam.Meta.Progression`** (stream C implements):
  - `StreakTier`, `StreakMultiplier`, `StreakTierFor(int streak)`, `StreakFloorBelow(int streak)`, `TierPerkUndo`. The tier helpers are already implemented.
  - `HasPostcard(int stop)` and `ShouldPlayArrival(int levelIndex)` are implemented.
  - `MarkPostcard(int stop)` and `MarkArrivalShown(int levelIndex)` throw until stream C implements them.
- **`GameController`** (orchestrator, already wired):
  - Creates a persistent `WorldEnvironment`, and calls `ShowLevel` after `Scenery.Build` and `ShowMenu` in `ShowMenuScene`.
  - Calls `fx.EnvironmentChanged(info)` after `LevelBuilt` and adds the returned seconds to the input delay.
  - Calls `fx.WinSequence` just before `fx.Win`.
  - Exposes `EnvironmentShown`, `MenuLayout` (an `IMenuLayout`), and `ShouldRevealArrival` (a `Predicate<int>` the UI sets to `Progression.ShouldPlayArrival`; the UI marks the reveal shown on `LevelStarted`).
  - The camera has post-processing on.
  - `LevelStartInfo.Route` is replaced by `Stop` and `Mood`.
- **`CargoJam.Game.Presentation.Biome`:**
  - `BiomeDefinition` (Id, Name, palette, prop set id, lighting per mood) and `LightingPreset`.
  - `Biomes.For(int stop)`.
- **`CargoJam.Game.Presentation.WorldEnvironment`** (MonoBehaviour, stream B implements):
  - `Init(Light sun, Camera camera, MaterialBank materials)`
  - `ShowLevel(int levelIndex, Layout layout)` → `EnvironmentInfo`
  - `ShowMenu(int levelIndex)` → `EnvironmentInfo`, which builds the menu diorama in the same biome.
  - `RevealPoints` gives camera waypoints for the arrival pan.
  - `Clear()`.
- **`EnvironmentInfo`** (struct): `Stop`, `Mood`, `Biome`, `Changed` (stop or mood differs from the last call), `Arrival`.
- **`ILevelFx`** gains `EnvironmentChanged(EnvironmentInfo info)` and `WinSequence(float at, Vector3 lastTruck)`. Stream D implements both. The controller calls `EnvironmentChanged` right after `LevelBuilt`.
- **`IMenuLayout`** (UI → controller): `Rect MenuViewport { get; }`, the screen-space normalized rect the diorama should frame. `GameFlow` implements it.
- **`UiPalette`** (`Scripts/Game/UI/Core/UiPalette.cs`): the §4 tokens as `Color` constants for C#-drawn things (icons, fly-coins, FX callouts). Stream D may read it.

## 9. Verification

- `python3 -I tools/check/check.py` must pass, including new Journey and streak-tier tests.
- **Orchestrator editor pass** (Play mode, `Main.unity`, `PortraitPreview` at 1170×2532):
  - Home only shows what §3 lists.
  - Levels 1→12 show the dusk change at 6, the storm finale at 10, and the arrival reveal plus postcard at 11.
  - The win sequence runs and can be skipped.
  - The fail sheet keeps the board visible.
  - The streak tier rises and drops gently.
- **Performance:** ≤150 draw calls and no per-frame GC allocations during play on the largest board in Metro City at dusk.
- **Readability:** the parcel colors and symbols stay distinct in every biome and mood, including in a grayscale capture.
