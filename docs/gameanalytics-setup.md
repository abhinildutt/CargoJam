# GameAnalytics setup

The code side is done. `CargoJam/Packages/manifest.json` pulls `com.gameanalytics.sdk` 8.3.1 from OpenUPM, the registry GameAnalytics documents for Unity. `Assets/Scripts/Analytics/GameAnalyticsBridge.cs` forwards every game event and reads Remote Configs.

The bridge only compiles once the package is installed, so the game runs the same without it. What's left needs your GameAnalytics account.

## 1. Create the game (about 5 minutes)

1. Sign up at gameanalytics.com. It's free.
2. Create a studio and a game named **Cargo Jam**, platform **iOS**. Add Android later if you need it.
3. Copy the **game key** and **secret key** from the game's settings.

## 2. Add the keys in Unity

1. Open this branch's project in Unity. The Package Manager downloads the SDK the first time.
2. Choose **Window → GameAnalytics → Select Settings**.
3. Either log in there and pick the game, or click **Add platform → iOS** and paste both keys.
4. Under **Setup**, leave the build number on auto (it reads Player Settings → Version).

The keys are saved in `Assets/Resources/GameAnalytics/Settings.asset`. Every build contains them anyway, but **don't commit the secret key to a public repository**. If the GitHub repo is public, add that file to `.gitignore`.

You don't need to add anything to a scene. The bridge creates the GameAnalytics object itself and declares the `coins` currency and its item types in code.

## 3. Check that events arrive

The Editor doesn't send events (GameAnalytics prints "Unsupported platform"), so test on a device:

1. Build to an iPhone or the Simulator (see `docs/ios-simulator.md`).
2. Play 2–3 levels.
3. Open **Realtime** in the GameAnalytics dashboard. Events show up within a few minutes.

| Game event | GameAnalytics event |
|---|---|
| `level_start` / `level_complete` / `level_fail` | Progression `level_007` (Start / Complete / Fail), plus design `level_fail:<reason>` |
| `coins_source` / `coins_sink` | Resource, currency `coins`, item type = reason (for example `win`, `continue`, `booster` / `crane`) |
| everything else | Design event: `booster_used:crane`, `destination_reached:farm`, `streak_tier_changed:tier2`, `chest_opened:big`, `daily_claimed:day3`, `review_requested`, … |

Every event also carries its fields (level, hard, streak, coins, and so on) as custom fields.

## 4. Remote Configs (tune without an App Store release)

Under **Remote Configs** in the dashboard, add any of these keys. Leave a key out to keep the shipped default.

| Key | Default | What it changes |
|---|---|---|
| `economy_starting_coins` | 300 | Coins a new player starts with |
| `economy_win_coins` / `economy_hard_win_coins` | 40 / 80 | Base coins per win |
| `economy_chest_coins` / `economy_big_chest_coins` | 100 / 300 | Chest coins |
| `economy_price_undo` / `_crane` / `_extra_slot` | 120 / 200 / 240 | Booster prices |
| `economy_continue_cost` / `economy_continue_cost_2` | 300 / 600 | First and second Continue |
| `economy_streak_x_tier1` / `_tier2` / `_tier3` | 1.5 / 2 / 2.5 | Streak coin multipliers |
| `review_enabled` · `review_min_level` · `review_min_days_between` · `review_max_asks` | true · 12 · 120 · 3 | Rating prompt |
| `notify_enabled` · `notify_hour` · `notify_days` · `notify_min_level` | true · 19 · 3 · 5 | Reminder notifications |

How values reach players:
- Values apply as soon as GameAnalytics delivers them, and are cached for the next launch.
- Bad values fall back to the default or are clamped to a safe range.
- **A/B tests** in GameAnalytics use the same keys, so you can test, for example, `economy_continue_cost` at 250 vs 300.

## 5. Privacy (needed for the App Store anyway)

GameAnalytics collects a device identifier (IDFV) and gameplay events. It doesn't use the IDFA unless ad tracking is turned on, which only matters once ads arrive.

In App Store Connect → **App Privacy**, declare:
- **Identifiers → Device ID** and **Usage Data → Product Interaction**, both *not linked to identity* and *not used for tracking*.
- **Diagnostics → Crash/Performance data**, if you turn on the FPS or hardware tracking options in the GameAnalytics settings.

Also mention GameAnalytics in the privacy policy.
