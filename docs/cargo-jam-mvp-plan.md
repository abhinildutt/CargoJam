# Cargo Jam — MVP Plan

*Game #1 · hybrid-casual sort-and-load puzzle · iOS + Android · drafted 25 Oct 2026*

Picked from the research brief's top pick ("Cargo Jam: a sort-and-load hybrid puzzle"). This plan covers the MVP only: a build where the core loop is smooth and fun and we can measure retention. Level design, monetization and live ops come after the MVP.

---

## 1. What the MVP must prove

| Question | How we answer it | Pass bar |
|---|---|---|
| Is tapping parcels fun with no progression around it? | Greybox "toy test" with 5 to 8 people who aren't on the team | Players keep going without being asked; they smile at a truck leaving |
| Do players understand it without reading? | Watch first-time players on level 1 | They get it within 3 taps and need no text |
| Does a fail feel like their own fault? | Ask "why did you lose?" after fails | They name a move ("I shouldn't have taken that red") instead of "bad luck" |
| Is it smooth on cheap phones? | Profile on a low-end Android and an older iPhone | Steady 60 fps, tap feedback in under 50 ms |
| Will people come back? | Soft-launch analytics later | Brief's targets: D1 35 to 40%, D7 12%+ |

**Out of scope for the MVP:** IAP, ads (stub the hooks only), battle pass, events, coins, a level editor, final level design, and more than about 20 levels.

---

## 2. Design pillars

1. **Readable in 3 seconds.** A stranger watching a 3-second clip knows what to do. This also decides whether the ads work.
2. **Every tap feels good.** Instant response, juicy motion, haptics. The toy should be fun before it becomes a game.
3. **Fair fails, near misses.** You lose because of a choice you can see in hindsight, ideally by one slot.
4. **Short levels.** 60 to 120 seconds each, so "one more level" is an easy yes.

---

## 3. Core gameplay

### Screen layout (portrait)

```
┌───────────────────────────────┐
│  Level 7                ⚙  ↻  │  HUD
│                               │
│  [next] [next]  ▶ [ TRUCK ▢▢▢ ] │  truck queue + loading bay
│                               │
│   [ ] [ ] [ ] [ ] [ ]         │  holding dock (5 slots)
│ ════════ dock edge ══════════ │
│   ■  ●  ▲  ■  ●  ▲  ■         │
│   ●  ■  ■  ▲  ●  ■  ▲         │  parcel yard (grid, up to ~7×9)
│   ▲  ●  ■  ●  ▲  ■  ●         │
└───────────────────────────────┘
```

### Rules

- **Yard.** A grid of colored parcels. The top edge of the yard is the dock.
- **Free parcels.** A parcel can be tapped only if there is a path of empty cells (up, down, left, right) from it to the dock edge. Free parcels sit slightly raised and brighter. Blocked ones are a bit dimmer.
- **Loading bay.** One truck is loading at a time, and the next 2 trucks are visible in the queue so players can plan. Each truck has a color and holds 3 parcels.
- **Tapping a free parcel:**
  - If it matches the loading truck and the truck has room, it slides along its path and loads.
  - Otherwise it goes to the first empty holding slot.
- **Truck full.** It drives off, the next truck pulls in, and any matching parcels in the holding dock jump in on their own. These chains are the big payoff moment.
- **Win.** Every parcel is loaded.
- **Full dock.** While all slots are taken, only parcels that match the loading truck can be tapped. Other free parcels just shake.
- **Fail.** The holding dock is full and no tappable parcel matches the loading truck. Later, this is where the fail offer ("+1 slot") goes. *(Built this way in the week-1 greybox. It's more forgiving than failing the instant the dock fills, so check it in the toy test.)*
- **Tapping a blocked parcel.** It does a small shake with a soft haptic and no penalty. Never punish a mis-tap.

**Level invariant:** for every color, the number of parcels equals truck capacity × the number of trucks of that color. The level loader checks this so a broken level never ships.

### MVP blockers (two, to prove the system can grow)

1. **Mystery crate.** Its color is hidden under a tarp until it becomes free, which adds controlled uncertainty.
2. **Locked crate + key.** A padlocked crate can't move until its key parcel has been loaded. It teaches planning ahead.

For after the MVP: conveyors, timed trucks and double-size crates (all mentioned in the brief), plus ice and a second loading bay.

### Difficulty knobs (for tuning, not level design)

Number of colors (3 to 6) · holding slots (4 to 6) · how unfriendly the truck order is · yard density · how many queued trucks are visible · how many blockers are used.

### Session flow (MVP)

App start → straight into the current level (no main-menu wall) → win screen (stars or confetti, "Next") or fail screen ("Retry") → next level. Settings: sound, music, haptics. Progress is saved after every level.

**Tutorial:** levels 1 to 3 teach by design, not text. Level 1 has only free parcels and one color. Level 2 introduces blocking. Level 3 introduces the holding dock. A hand pointer shows on level 1 only.

---

## 4. Game feel spec ("smooth and fun")

This is the most important section for the MVP. Treat it as a checklist.

**Input**
- The game state updates the moment you tap. Animations play on top and **never block input**: you can tap the next parcel while the previous one is still moving.
- Tap targets are at least 44 pt, with a forgiving hit radius (snap to the nearest free parcel within roughly half a cell).

**Motion** (all tweens use ease-out curves)
- Tap: parcel squashes (scale 0.9) and pops up over about 80 ms.
- Travel: parcel slides along the actual path, about 0.15 s per cell, capped at about 0.45 s total.
- Load: parcel arcs into the truck bed, the truck dips and bounces, and a slot indicator fills.
- Truck departs: a 0.1 s anticipation squash, then it drives off with a dust puff. The next truck pulls in with a bounce.
- Auto-load chain: parcels jump in from the holding dock in a staggered sequence (about 60 ms apart).
- Win: confetti burst, the camera eases in slightly, a short fanfare.

**Haptics (iOS Core Haptics / Android vibration through a single wrapper)**
- Light tap: parcel picked · Medium: truck full · Success pattern: level won · Soft: blocked tap · Warning: holding dock at 4/5.

**Sound** (about 15 SFX + 1 music loop)
- Tap, slide, load thunk, truck horn on depart, engine on arrive, blocked "bonk", holding-slot place, danger tick, win fanfare, fail sting, UI clicks.
- The load sound **rises in pitch** across a chain (do-re-mi), which makes combos feel great.

**Tension and clarity**
- The holding dock glows amber at 4/5 slots and pulses red at 5/5.
- A fail always shows *why*: the camera nudges toward the stuck dock and the matching parcel that was buried.
- Restart takes under 1 second. No loading screens between levels.

**Performance budget**
- 60 fps on a low-end Android (Galaxy A-series class) and iPhone 11. Optional 120 fps on ProMotion.
- Cold start to playable in under 5 s. Pool every parcel and VFX object, and allocate nothing per frame during play.

---

## 5. Tech plan

### Engine: Unity 6 LTS (URP)

- The brief says to soft-launch on **Android first** and then turn on iOS, so we need one codebase for both platforms.
- The SDKs we'll need later (AppLovin MAX, AdMob, GameAnalytics, and publisher SDKs from Voodoo, SayGames and Homa) are Unity-first. Publisher CPI tests also expect Unity builds.
- If iOS-only had been the goal, SpriteKit would be the leaner choice. The brief's plan rules that out.

**Look:** low-poly 3D with a fixed, near-top-down camera. That's the genre standard, it reads well in ads, and it's cheap to produce.

### Architecture

```
CargoJam/Assets/
  Scripts/
    Core/            CargoJam.Core assembly: pure C#, no UnityEngine
      Board (grid + BFS exit paths), Parcel, Truck, HoldingDock
      GameSession.Tap(cell) -> events, in order
      LevelData / LevelDefinition (parsing + color-count check)
      Solver (DFS over the real rules)
    Game/            CargoJam.Game assembly
      GameController (input, tap → cell with near-miss forgiveness, event → animation)
      Presentation/  Layout, ParcelView, TruckView, DockView, Scenery,
                     Tweener (time-scheduled tweens), Timing (all durations), CameraRig, Hud
      Meta/          LevelLibrary, Progress
      (later) Feel/ haptics + audio, Analytics/, Ads/ stub
  Resources/Levels/  level_NNN.json
  Tests/EditMode/    CargoJam.Core.Tests
```

- **Logic and view are fully separate.** `GameRules` is deterministic, and every tap returns events (`ParcelMoved(path)`, `ParcelHeld`, `ParcelLoaded`, `TruckDeparted`, `TruckArrived`, `AutoLoaded`, `Won`, `Failed`). This gives us unit tests, instant restart, undo later, replays for ad capture, and a **solver** later to check that levels can be solved. The solver matters a lot once level design starts.
- **Level format** (JSON, one letter per color, `.` for empty; row 0 touches the dock):
  ```json
  { "id": 5, "holdingSlots": 5, "truckCapacity": 3,
    "trucks": "RRBBGRBG",
    "rows": ["GBBBG",
             "RGRRB",
             "R.GBR"] }
  ```
  Blockers (mystery, locked) will add their own markers when they're built in week 3.
- **Libraries:** DOTween (tweens), Nice Vibrations (haptics, now open source), TextMeshPro, GameAnalytics (free).
- **Rendering:** one shared parcel material, with color set through `MaterialPropertyBlock` and GPU instancing so a full board is a handful of draw calls.

### Accounts and tooling

- Apple Developer Program ($99/yr) and Google Play Console ($25 one-time). Sign up for the Apple **Small Business Program** right away (15% cut).
- Xcode on the Mac for iOS builds. Distribute through TestFlight and the Play internal testing track.
- Git with Git LFS for art and audio, plus a Unity `.gitignore`.

---

## 6. Artifacts needed

### Design docs
| Artifact | Purpose | When |
|---|---|---|
| This MVP plan | Scope, rules, feel, tech | Now |
| GDD-lite (rules spec) | Exact rules and edge cases (ties, chain order, fail check timing) | Week 1 |
| Game-feel checklist | Section 4, ticked off in builds | Weeks 1 to 3 |
| Analytics event plan | See below | Week 4 |
| Playtest script and notes template | Same questions every session | Week 2 |

### Art (placeholder first, then one style pass)
- **Style guide / moodboard:** 1 page. Color palette, lighting, "toy-like" materials.
- **Parcels:** 6 colors, **each with its own symbol** (■ ● ▲ ◆ ★ ♥) so the game works for colorblind players and reads in grayscale ads. Plus mystery tarp and padlock variants.
- **Trucks:** 1 base model with a color swap. Cargo bed shows 3 slots.
- **Environment:** dock, yard floor, holding-dock pads, simple backdrop.
- **VFX:** dust puff, load sparkle, confetti, danger glow.
- **UI:** HUD (level number, settings, restart), win screen, fail screen, settings panel, tutorial hand.
- **App icon** (draft) and splash.

### Audio
- About 15 SFX (listed in section 4) and 1 upbeat music loop (60 to 90 s, loopable). Royalty-free or commissioned. Keep the licenses.

### Content
- **About 20 tuning levels.** These aren't the real level design. They exist to check difficulty knobs, blockers and the tutorial.

### Analytics events (GameAnalytics)
`session_start` · `level_start {id}` · `level_complete {id, time, taps, max_hold_used}` · `level_fail {id, time, parcels_left}` · `level_restart {id}` · `tutorial_step {n}` · `settings_change`

These give us per-level fail rates, drop-off funnels and D1/D7 from day one of the soft launch.

### Marketing (for the CPI test)
- A 15 s gameplay capture of the greybox or art build. The replay system makes this easy. Publishers like Voodoo, SayGames and Homa can CPI-test it.

---

## 7. Milestones (assumes 1 to 2 people)

| Week | Milestone | Done when |
|---|---|---|
| 1 | **Greybox core loop** | Cubes and flat colors. Tap, pathing, holding dock, truck queue, chains, win/fail. Core unit tests pass. Builds on a real phone. |
| 2 | **Feel pass v1 + toy test** | Section 4 motion, haptics and placeholder SFX in. Non-blocking input. 5 to 8 outside testers. |
| — | **🚦 Fun gate** | Testers want to keep playing. If not, iterate on the core for one more week before going further. |
| 3 | **Level plumbing** | JSON loading, validator, progression and save, instant restart, 2 blockers, tutorial levels 1 to 3. |
| 4 | **Tuning set + analytics** | About 20 tuning levels, analytics events live, settings screen. |
| 5–6 | **Art and audio pass** | Style guide applied, final-ish models, UI, VFX, music. Profile on low-end devices and fix perf. |
| 7 | **Playtest and polish** | TestFlight and Play internal track. 10+ outside testers. Fix the top 10 issues. Record the 15 s ad video. |
| 8 | **MVP locked** | Meets the pass bars in section 1. Ready for a CPI test and a small Android soft launch. |

**After the MVP:** level design (100 levels plus a solver), the fail-offer and coins economy, rewarded and interstitial ads, remove-ads IAP, light battle pass, then events.

---

## 8. Risks

- **The genre is crowded** (the brief notes 215 "hole" games in 7 months). The MVP has to show one signature moment that reads in a clip, probably the truck-full chain. Decide on a stronger twist only after the toy test, not before.
- **Greybox fun ≠ final fun.** Do the feel pass before judging the game. Cubes with great motion are a fair test. Cubes with no motion aren't.
- **Scope creep into meta systems.** Coins, shops and passes stay out until the retention numbers justify them.

## 9. Open decisions

1. Team size and roles. This changes the timeline above.
2. Art direction: make it ourselves with asset packs, or commission a stylized look?
3. Whether to approach a publisher for a CPI test at week 2 (greybox video) or week 7 (art build).
