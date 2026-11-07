# Cargo Jam — Monetization, retention and growth plan

*Drafted 1 Nov 2026 · builds on the MVP plan and designs v2–v4 · nothing here is built yet*

## 1. Where the game stands

**Already built and worth money later:**

- A tight core loop.
- 100 solver-checked levels on a sawtooth curve. HARD levels fail 55–72% of the time.
- Seven cargo types that debut about every 5 levels.
- Three boosters (Undo, Crane, Extra Slot) and a coin economy.
- A fail-screen Continue (+1 slot) that already costs coins.
- Streak tiers with gentle loss, a 7-day daily calendar, chests, a six-destination journey and postcards.
- Analytics events defined in `Game/Meta/Analytics.cs`. They are local-only today, but a backend can subscribe to `Analytics.Logged`.

**Missing before any money comes in:**

| Gap | Why it blocks revenue |
|---|---|
| No real analytics backend | Without D1/D7, fail funnels and ARPDAU we can't tune anything, and a publisher won't look at the game. |
| No ads SDK or IAP | No revenue at all. |
| No remote config | Every price, ad frequency or difficulty change would need an App Store release. |
| Only 100 levels, looping from L61 | An engaged player (20–40 min a day, about 2 min a level) reaches L100 in about 1–2 weeks and then sees repeats. That hurts D14+ retention, which is where most revenue comes from. |
| No push notifications or ratings prompt | Two cheap retention and organic-growth levers we haven't pulled. |
| No privacy plumbing (ATT, EU consent, privacy policy) | Required by Apple and ad networks; without ATT consent ads pay much less. |

## 2. How money actually gets made

```
Revenue/day = DAU × ARPDAU
DAU         = installs/day × average days a player stays   (retention)
Profit      = LTV per install − cost per install (CPI)
```

Each lever only works if the one before it is in place, so the order matters:

1. **Measure.** Analytics first. Everything else is guessing without it.
2. **Retain.** Retention multiplies everything. A game with D7 at 6% can't be saved by monetization or by paid users.
3. **Monetize.** Ads plus IAP, tuned in a soft launch.
4. **Scale traffic.** Buy users only once LTV is above CPI. Before that, every dollar of ads loses money.

**Model: hybrid monetization** (rewarded ads + light interstitials + IAP), the standard for hybrid-casual puzzle games like Bus Jam, Screw Jam and Color Block Jam. Expect ads to bring in most of the revenue at launch, with IAP's share growing as live ops (events, passes) arrive.

**What not to do** (from design v3's anti-goals, and still true):

- No fake timers, guilt copy or hidden odds.
- No offers that cover the board.
- No ads mid-level.
- No banner ads. They cheapen the premium look, take screen space from a portrait board, and pay little.

## 3. Ads

**Mediation:** AppLovin MAX. It has the strongest demand for casual games and a built-in consent flow. The demand networks are AppLovin, Unity Ads, Google AdMob, Meta Audience Network, Mintegral, ironSource, Liftoff and others, all added as adapters. Unity LevelPlay is the alternative; pick one, not both.

### Rewarded video (the backbone: opt-in, players like it)

| Placement | Offer | Cap | Fits existing code |
|---|---|---|---|
| **Fail → Continue** | "Continue +1 slot · ▶ Watch" next to the 300-coin button. The first continue in a level can be free by ad; the second still costs 600 coins. | 1 per level | `ContinueWithExtraSlot`, `EconomyConfig.ContinueCost` |
| **Win → ×2 coins** | "Claim ×2 ▶" vs "Claim". It doubles the base reward after the streak multiplier. | none (it's the main volume driver) | Win panel count-up |
| **Booster at 0** | "Get 1 free ▶" in the purchase popup, alongside the coin price | 3 a day per booster | Booster purchase popup |
| **Streak save** | When giving up would drop a streak tier: "Keep your 7-win streak ▶" | 1 a day | `Progression.RecordGiveUp` |
| **Daily ×2** | Double today's calendar reward | 1 a day | `ClaimDaily` |

The rewarded continue only appears where the fail screen already offers one, so the fair-fail feel stays. HARD levels (fail rate 0.55–0.72) are where rewarded views cluster. That's healthy because it's voluntary, and it's why the HARD sawtooth matters for revenue.

### Interstitials (careful: the premium feel is a pillar)

- **Never** in the first session or before **level 15**.
- **Only** between levels, after a **win**, before the next level loads. Never after a fail, never mid-level, never over the board.
- At least **3 levels and 120 s** between interstitials, and a 90 s cooldown after any rewarded ad.
- All of these numbers live in remote config, and the soft launch A/B tests them (for example, every 3 vs every 4 levels).
- **Remove Ads** turns them off for good.

## 4. In-app purchases

**Library:** Unity IAP (StoreKit 2), or RevenueCat if subscriptions or cross-platform receipts come in later. Apple requires a **Restore Purchases** button for non-consumables (put it in Settings). Enroll in the **Small Business Program** so Apple takes 15% instead of 30%.

### Launch catalog

| Product | Type | Price (US) | Contents | When shown |
|---|---|---|---|---|
| **Remove Ads** | non-consumable | $4.99 | No interstitials, forever. Rewarded ads stay because they're opt-in. | Settings, the store, and once after the 3rd interstitial |
| **Starter Pack** | one-time | $1.99 | 1,500 coins + 2 of each booster + Remove Ads for 24 h | Once, after the L20 win, for 48 h. The timer must be real. |
| **No Ads Bundle** | non-consumable + consumable | $7.99 | Remove Ads + 2,500 coins + 3 of each booster | In the store |
| Coins S | consumable | $1.99 | 1,000 | store, and the "not enough coins" sheet |
| Coins M | consumable | $4.99 | 3,000 (+20% shown) | store |
| Coins L | consumable | $9.99 | 7,000 (+40%) | store |
| Coins XL | consumable | $19.99 | 16,000 (+60%) | store |

The prices are anchored to the current economy. A Continue costs 300, which is about 4–5 wins of coins, so the $1.99 pack is about 3 Continues. That's an easy first purchase for someone stuck on a HARD level.

### After the soft launch (live-ops IAP, the bigger IAP lever)

- **Piggy bank:** every win drops coins into a jar that you can only open by paying $2.99. It fits the existing chest and coin flow well and converts well in the genre.
- **Cargo Pass:** a monthly season with a free track and a $4.99 premium track, earned by winning levels. Postcards and destination cosmetics are natural rewards for it.
- **Event packs** tied to time-limited events (section 5).

**Chest odds:** the chest gives a *random* booster. Selling chests for real money would require showing the odds (App Store guideline 3.1.1), so for now chests stay earned-only.

### Economy check

The current faucets (40/80 coins a win, ×1–2.5 streak, a chest every 5 wins, the daily calendar) are fairly generous for a game without IAP. **Don't cut them before launch.** Instead, move every number in `EconomyConfig` behind remote config (with today's values as defaults) and tune them using soft-launch sink and source data. The goal: a non-payer can afford a Continue on most HARD levels, but not on every one.

## 5. Retention (the multiplier on everything)

**Benchmarks to aim for** (from the original brief): D1 35–40%, D7 12%+. Also D30 4–6% and 20+ minutes of play per day.

| Lever | What | Cost | Priority |
|---|---|---|---|
| **More levels** | Generate and solver-check up to **300 levels before global launch** (at least 200 for the soft launch), then release +20–40 each month. The generator makes this mostly compute plus QA. | Low–med | **P0** |
| **Local notifications** | "Your daily reward is ready", "Your 8-win streak is waiting", and "New levels!". At most 1 a day, asked for after the L5 win rather than at launch. | Low | P0 |
| **Ratings prompt** | `SKStoreReviewController` after a HARD-level win or a tier-2 streak, never after a fail. Apple caps it at 3 a year. | Tiny | P0 |
| **Express Run event** | The streak event deferred in v3: win N levels in a row within a weekend for a big chest. | Med | P1 (soft launch) |
| **Daily challenge** | One special board a day with its own reward, pulled from the generator | Med | P1 |
| **Postcard album** | Collection screen (deferred in v3). Collections drive completion. | Low | P2 |
| **Weekly leaderboard / team events** | Needs a backend (Game Center for the leaderboard is free) | High | P2, after product-market fit |

**Lives/energy:** recommended **not** at launch. Royal Match-style lives earn IAP but add friction that hybrid-casual audiences churn on, and this game makes money through ads. A/B test it later if IAP share stays low.

## 6. User traffic

### Organic (free, do it all before launch)

- **ASO.**
  - Title: "Cargo Jam: Truck Sort Puzzle".
  - Subtitle: built from keywords like *sort, truck, parcel, jam, puzzle, relaxing, brain*.
  - The 100-character keyword field, with no repeats from the title.
- **Store assets.**
  - 6 screenshots: the truck-full chain, a HARD level, the journey and the destinations.
  - A 15–30 s app preview made with the replay system.
  - Use **Product Page Optimization** to A/B test the icon. `docs/logo-options/` already holds candidates.
- **Apple featuring.** Submit a **featuring nomination** in App Store Connect 2–3 months ahead of launch. The premium look, no dark patterns and accessible symbols are what Apple's editors favor.
- **Short-form video.** Post satisfying chain clips to TikTok, Reels and Shorts. "Readable in 3 s" was designed for exactly this.

Be realistic: without featuring or paid installs, a new puzzle game typically gets tens to low hundreds of installs a day. Organic alone won't make meaningful revenue. It lowers the blended CPI once paid installs start.

### Paid user acquisition (only after the gates in section 8)

1. **CPI / creative test** ($300–800). Run 3–5 short video creatives on AppLovin, Unity Ads, Meta and TikTok to measure CPI, CTR and IPM. This tells us whether the game *sells* before we know whether it *earns*.
2. **Soft-launch UA** ($1–3k) to fill the cohorts for the retention and LTV reads.
3. **Scale** when D7 ROAS is above about 30–40% and the projected D90 LTV is above CPI × 1.2. Start with AppLovin AppDiscovery and Unity Ads, which have the cheapest casual audiences, then Meta and TikTok, then Apple Search Ads for brand and genre keywords.
4. **Creatives** come from the replay system: fail-bait ("can you load this?"), satisfying chains, and HARD level saves. Refresh them every 2–3 weeks, because creatives tire out fast.

An **MMP** (attribution) is needed only once money goes into paid UA. AppsFlyer, Adjust and Singular all have free or startup tiers. iOS attribution runs on SKAdNetwork and AdAttributionKit conversion values; map them to "won L5 / L10 / L20, watched an ad, bought".

### Self-publish vs a publisher

| | Self-publish | Publisher (Voodoo, SayGames, Homa, Supersonic, …) |
|---|---|---|
| UA money | yours | theirs |
| Revenue share | 100% (minus Apple's 15%) | typically 30–50% to you, after their UA costs |
| Know-how | learn by doing | they run the ads stack, creatives and UA |
| Risk | you can lose UA spend | low; they kill the game if CPI tests fail |

The plan already expected a publisher CPI test. **Suggested path:** send the publishers the current build plus a 15 s video now, *in parallel* with the soft-launch prep. Their CPI test is free market research, and the offer gives a baseline to compare self-publishing against.

## 7. Tech and compliance checklist

| Item | Choice |
|---|---|
| Analytics | GameAnalytics (free, gaming dashboards out of the box) or Firebase Analytics. Wire it to the existing `Analytics.Logged`. |
| Remote config + A/B | Firebase Remote Config or Unity Remote Config: economy, ad caps, interstitial gap, offer timing |
| Crash reporting | Firebase Crashlytics |
| Ads | AppLovin MAX + adapters, with impression-level revenue sent to analytics |
| IAP | Unity IAP (StoreKit 2), with receipt validation |
| ATT | Show a soft pre-prompt after the L5 win ("Keep ads relevant…"), then the system prompt. Never at launch. |
| EU/UK consent | MAX's Terms & Privacy flow, or Google UMP |
| Privacy | A privacy policy URL, App Privacy labels (ad networks collect a device ID), and an age rating of 4+ |
| Audience | **Not** "Made for Kids". The 25–45 target lets us run normal ad demand. |
| Account deletion / data | Data is local only today; mention that in the privacy policy |

**New analytics events** to add to `Analytics.cs`:

- `ad_offer_shown`, `ad_rewarded_started`, `ad_rewarded_completed` [placement]
- `ad_interstitial_shown` [level]
- `ad_revenue` [network, value]
- `iap_offer_shown`, `iap_purchased` [sku, price]
- `store_opened`
- `coins_source` and `coins_sink` [reason, amount]
- `notification_opened`

## 8. Roadmap and gates

| Phase | Weeks | Work | Gate to move on |
|---|---|---|---|
| **0 · Launch prep** | 1–3 | Analytics backend + new events, remote config, Crashlytics, local notifications, ratings prompt, levels to 200+, ATT/consent/privacy, store assets, featuring nomination, publisher outreach | Builds pass review on TestFlight; events show up in the dashboard |
| **1 · Soft launch, retention only** | 4–6 | Release in 2–3 test countries (for example Canada, Australia, and the Philippines or Brazil for cheap installs) with ads **off** but rewarded continues as coins only. $1–2k UA. Fix the worst-dropping levels from the fail funnels. | **D1 ≥ 35%, D7 ≥ 12%, 20+ min/day.** If not, iterate on onboarding and the L4–30 curve before anything else. |
| **2 · Soft launch, monetization** | 7–10 | Turn on MAX rewarded, interstitials from L15, the IAP catalog. A/B the interstitial gap, ×2 placement and starter-pack timing. | ARPDAU stable; retention dropped no more than about 1–2 points after ads; D7 ROAS read |
| **3 · Global launch** | 11+ | US, UK, Europe, and so on. Scale UA if ROAS is above target, otherwise lean on organic plus the publisher route. 300 levels. | CPI below the projected LTV |
| **4 · Live ops** | ongoing | Monthly level drops, Express Run, daily challenge, piggy bank, Cargo Pass, album | D30 and IAP share growing |

### What success looks like (ballpark, verify in the soft launch)

These are rough genre ranges, not promises:

- **ARPDAU:** US iOS hybrid-casual puzzle ≈ $0.10–0.25. Tier-2 countries ≈ $0.03–0.08.
- **Rewarded views:** 2–4 per DAU a day.
- **IAP conversion:** 1–3% of players.
- **Example:** 10k DAU at $0.15 ≈ $1,500 a day ≈ $45k a month in gross revenue, before UA costs and Apple's cut.

## 9. Open decisions

1. **Self-publish or publisher?** This changes who pays for UA and whether phases 1–2 happen in-house.
2. **UA budget.** How much can we risk on the soft launch? $2–5k is enough for a decision.
3. **Where to soft-launch.** The iOS App Store in test countries, or Android first, as the original brief said? The iOS build path is the one that's ready today.
4. **Interstitials at all?** This is a premium-feel game. A rewarded-only + IAP model earns less but is safer for retention. The soft-launch A/B can settle it with data.
