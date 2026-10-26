# Cargo Jam — Design v2: polish, rewards and retention

*Orchestrator decisions for the polish push, 26 Oct 2026. This doc is the source of truth for the parallel work streams; the code contracts in section 8 are already on the `feature/polish-v1` branch.*

## 1. What "good enough to play in my free time" means

The top hybrid-casual puzzlers (Royal Match, Bus Jam, Screw Jam, Block Jam) share a recipe. We copy the recipe, not the content:

1. **Every input pays out instantly.** Sound, motion and haptics fire on every tap, and loads in a row climb in pitch.
2. **Short sessions, with something carrying over.** A level takes 1–2 minutes, but coins, a streak and a chest bar carry from one level to the next.
3. **Tension, then relief.** Ordinary levels feel smooth. Every fifth level is a marked HARD level. The dock warns before you fail, and failing comes with a "so close" offer.
4. **A reason to come back tomorrow.** A daily reward calendar, the win streak, and a chest that's always partly filled.
5. **New things often.** A new booster unlocks every few levels early on, blockers arrive around levels 15 and 25, and each route of 20 levels has its own look.

We are still pre-monetization: no ads and no IAP. The economy is tuned so a player who never pays still feels rich enough to use boosters sometimes. Hooks for "x3 coins (watch ad)" can come later.

## 2. Core loop additions

### Boosters (three, all engine-free in `CargoJam.Core`)

| Booster | What it does | Rules |
|---|---|---|
| **Undo** | The most recently held parcel flies from the dock back to its yard cell. | Needs at least one held parcel. Its cell is always empty, because cells only ever get emptied. Repeatable while held parcels remain, in reverse order of holding. |
| **Crane** | The next tap can lift **any** parcel, blocked or not, straight up to the truck or dock. | Arms a mode; tapping the button again cancels it. The normal truck/dock rules apply after the lift, including the full-dock rule. |
| **Extra Slot** | Adds one holding slot for the rest of the level. | Up to 7 slots. It's also the fail "Continue" offer. |

### Blockers (introduced by level content, not by unlocks)

- **Mystery crate** (from level 15): the parcel's color is hidden under a tarp (grey, with a "?" symbol) until it becomes free. Then it flips and reveals itself (`ParcelRevealed`). Level format: a **lowercase** color letter.
- **Locked crate + key** (from level 25): a padlocked parcel can't be tapped, even when free, until its key parcel has been **loaded into a truck**. Then it unlocks (`ParcelUnlocked`). Level format: `locks: [{x, y, keyX, keyY}]`.

### HARD levels

Every 5th level from level 10 (10, 15, 20, …) has `"hard": true`. It shows a red **HARD** banner at the start and gives double coins. The generator makes these levels genuinely harder: more colors, more hostile truck order, and one fewer holding slot early on.

## 3. Economy (all numbers live in `CargoJam.Meta.EconomyConfig`)

| Thing | Value |
|---|---|
| Starting coins | 300 |
| Win, normal / HARD | 40 / 80 coins |
| Win streak multiplier | ×1 (streak 0–2), **×1.5** (3–5), **×2** (6+) |
| Undo / Crane / Extra Slot price | 120 / 200 / 240 coins |
| Continue after fail (+1 slot) | 300, then 600 for the second continue in the same level |
| Chest | Every 5 wins: 100 coins plus 1 random booster. Every 4th chest (20 wins) is a **big chest**: 300 coins plus one of each booster. |
| Booster unlocks | Undo at level 6, Crane at 11, Extra Slot at 16. Each unlock gifts 2 of that booster, with a popup that teaches it. |

**The win streak breaks** when the player gives up a failed level (Retry or Home from the fail screen) or restarts a level mid-play after making at least one move. Using Continue keeps the streak. This is the loss-aversion lever, and the fail screen spells out what you'd lose.

### Daily reward (7-day cycle)

| Day | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| Reward | 50 coins | Undo ×1 | 100 coins | Crane ×1 | 150 coins | Extra Slot ×1 | **Chest**: 200 coins plus one of each booster |

You can claim once per local calendar day. Missing a day doesn't reset the cycle; it just waits, which is gentle. Separately, a **login streak** counts consecutive days and shows a small "Day N" badge. After day 7 the cycle wraps to day 1.

## 4. Screens and flow (UI Toolkit, built by the UI stream)

```
Launch ─► Home ──Play──► Level (intro) ──► Win panel ──Next──► (Chest popup?) ──► next Level
            ▲   ▲                    └──► Fail panel ──Continue──► back to Level
            │   └──────── Home ◄──── Pause menu / Fail "Give up"
            └── Daily reward popup (first open each day), Settings
```

- **Home:** the live board of the current level sits dimmed behind a scrim. On top: the route name and progress ("Route 1 · Harbor 7/20"), a big pulsing **Level N** button (with a HARD badge if it's a hard level), the coin counter, the streak flame, the chest progress bar, a daily reward button with a badge dot, and a settings gear.
- **In-level HUD:**
  - **Top:** pause button, "Level N" (plus a HARD badge), and the coin counter.
  - **Bottom:** the booster bar with three round buttons. Each shows its icon plus either a count badge, a price tag (if you own 0), or a lock with "Lv 6".
- **Win panel:** "Level Complete!", the coins earned counting up, coins flying into the counter, the streak multiplier flashing when it applies, the chest bar filling, and a big **Next** button. If the chest is ready, a chest popup opens next and you tap it to burst it open.
- **Fail panel:** "Out of space!" with "Only N parcels left!". A big **Continue +1 slot · 300** button (greyed out with "Not enough coins" if you can't afford it), and a smaller **Give up** that warns "You'll lose your 3-win streak" when there's a streak to lose.
- **Pause menu:** Sound / Music / Haptics toggles, Restart (with the streak warning if you've moved), Home.
- **Booster purchase:** tapping a booster you own 0 of opens "Buy Undo for 120?", or "Not enough coins".
- **Tutorial:**
  - Level 1: a hand pointer bounces over a free parcel until the first tap.
  - Level 2: a one-line tip, "Blocked parcels can't move. Clear a path!"
  - Level 3: a tip about the dock.
  - Booster unlock popups explain each booster.

### Visual language

Chunky and candy-like: rounded pill buttons with a darker bottom edge to fake depth, bold text with dark outlines, and a cream panel with a tan border.

| Token | Color |
|---|---|
| Primary (play/next) | `#3BC95B`, edge `#2A9644` |
| Secondary | `#4A90F0`, edge `#3570C4` |
| Coin | `#FFC83D` |
| Danger | `#F2554B` |
| Panel | `#FFF6E5`, border `#E8D3A8` |
| Text dark | `#3B2F2A` |
| Scrim | `rgba(20,16,30,0.55)` |

Icons (coin, flame, lock, gear, pause, undo arrow, crane hook, plus-slot, chest, hand) are drawn in code with UI Toolkit's vector API, so they stay crisp with no image files. Buttons press down (scale 0.94) and bounce back, panels pop in with an overshoot, and numbers roll when they change.

## 5. Juice v2 (the FX stream)

- **Level intro:** trucks roll in from the left, and parcels drop in row by row (back rows first, about 25 ms apart) with a squash and soft ticks. Input works after about 0.35 s.
- **Combo callouts:** at 3 / 5 / 8 / 12 loads in a row, floating text near the bay says "Nice!", "Great!", "Awesome!", "Unstoppable!", with a small burst and rising pitch.
- **Truck full:** a sparkle burst and a ✓ pop above the truck, then the horn and a dust puff as it pulls away.
- **Danger:** a red vignette pulses at the screen edges with one slot left, and pulses stronger and faster when the dock is full.
- **Dock landing:** the parcel squashes and the slot pad glows briefly.
- **Music:** a gentle generated loop at low volume, with its own toggle.
- **Variation:** pick and hold sounds vary in pitch by ±4%, so repetition doesn't grate.
- **Route themes:** every 20 levels the ground, tiles, road and background shift. Harbor (warm sand), Desert (orange), Snow (cool white-blue), Night (deep blue with warm light).
- **Mystery and lock visuals:** a grey tarp with "?" that flips on reveal, and a padlock with a key-colored band that springs open on unlock.

## 6. Levels

There will be 60 levels, generated by a C# tool that uses the real `CargoJam.Core` rules, so rules can never drift between the game and the generator. Each level is solver-checked. Difficulty is a sawtooth: easy, easy, medium, harder, then a HARD level or relief, rising over time.

- **Levels 1–3** are the tutorial.
- **Levels 4–14** grow the board.
- **From 15** mystery crates appear, and **from 25** locks.

Difficulty is measured by the fail rate of a "casual" bot that takes a visible match 60% of the time.

## 7. Work streams and who owns which files

| Stream | Owns (may create and edit) | Must not edit |
|---|---|---|
| **A · Core rules + levels** | `Assets/Scripts/Core/**`, `Assets/Tests/EditMode/*.cs` (Core tests), `tools/levelgen/**`, `Assets/Resources/Levels/**` | Game, Meta, UI |
| **B · Meta economy** | `Assets/Scripts/Meta/**`, `Assets/Tests/EditMode/Meta/**`, `Assets/Scripts/Game/Meta/**` | Core, UI, Presentation |
| **C · UI screens + flow** | `Assets/Scripts/Game/UI/**`, `Assets/Resources/UI/**` | Core, Meta internals, Presentation, Feel |
| **D · Juice FX + audio** | `Assets/Scripts/Game/Feel/**`, `Assets/Scripts/Game/Presentation/Fx/**`, `Assets/Scripts/Game/Presentation/Theme.cs` | Core, Meta, UI, GameController |
| **Orchestrator** | `GameController.cs`, the rest of `Presentation/**`, contracts, integration, scenes and assets, docs | n/a |

Every stream verifies with `python3 -I tools/check/check.py` (compile with editor and iOS defines, plus Core and Meta tests) and never opens Unity. The orchestrator integrates each branch, runs it in the real editor, and fixes what only shows up there.

## 8. Code contracts (already stubbed on the branch)

- `CargoJam.Core`: `GameSession.CanUndo / Undo()`, `CanAddHoldingSlot / AddHoldingSlot()`, `CanCrane / CraneLift(Cell)`, `IsLocked(Parcel)`, `IsHidden(Parcel)`. Events: `ParcelReturned`, `DockExpanded`, `ParcelRevealed`, `ParcelUnlocked`. `ParcelLoaded` and `ParcelHeld` gain `Lifted`. `LevelData` gains `hard` and `locks`, and lowercase letters mean mystery.
- `CargoJam.Meta` (engine-free): `BoosterType`, `EconomyConfig`, `ProfileData` (a JsonUtility-friendly data object), `PlayerProfile`, `Progression` (`RecordWin`, `RecordGiveUp`, `TrySpend`, `TryConsumeBooster`, `TryBuyBooster`, `ContinueCost`, `OpenChest`, `DailyStatus`, `ClaimDaily`, `UnlockFor`), `WinResult`, `ChestReward`, `DailyRewardInfo`, `IClock`.
- `CargoJam.Game`: `GameController` events (`LevelStarted`, `LevelWon`, `LevelFailed`, `ComboChanged`, `DockChanged`, `BoosterApplied`), methods (`StartLevel`, `Restart`, `CanUseBooster`, `UseBooster`, `CancelCrane`, `ContinueWithExtraSlot`, `TryGetHintCell`, `CellToScreen`, `InputEnabled`, `HudLayout`), the `IHudLayout` interface (what the UI tells the camera and input about screen space), and `ILevelFx` (what the FX stream implements).
