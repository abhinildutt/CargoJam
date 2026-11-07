# Design v5: harder, mixed levels tuned by playtests

The owner's brief (2026-11-01): the game felt too easy; new cargo arrived too late; colors came
grouped up, so no move needed thought or a retry; there were too many waiting slots. Levels should
mix mechanics, and AI playtesters should play them repeatedly and refine them until nothing more
changes.

## What changed

**Curve.** Every cargo kind debuts by level 10, one a level: bollard 4, frost 5, mystery 6, lock 7,
strap 8, express 9, gate 10 (simple to involved). A debut uses only its new cargo, and later debuts ask
a little more (thoughtful-bot target 0.12 rising to 0.24). Every level from 11 mixes two to four kinds,
each early mix pairing a kind with the ones met just before it (hand-picked to 21, then a rotation).
The sawtooth stays: relief, easy, easy, medium, harder, then HARD every fifth level from 15. Level
10's storm finale carries the gate debut instead of a HARD level. The Undo unlock (level 6) now lands
on the mystery debut; the unlock card shows after level 5's win, the cargo card inside level 6.

**A debut must make its cargo matter**, not just show it (playtest rounds 7-8 found frost, covers,
padlocks, express and back-corner bollards that never changed a decision): a bollard in the first two
rows with a parcel the first trucks want behind it;
a front-row crate in the first truck's color iced for 3-4 moves; three covers over more than one
color; a front padlock, right in front of a parcel the first trucks want, whose key's truck comes
third or later; an express two or three parcels deep with at most 6 moves and a color no truck in
sight wants; a gate whose color comes fourth or later.

*Earlier in v5 the debuts ran 4-15 (mystery 7, lock 9, express 12, strap 13, gate 15); the owner
asked for every mechanic by level 10.*

**Waiting slots.** 3 on every level after the tutorials, 2 on HARD levels from 40 (it was 4-5).

**How a level is judged** (`tools/levelgen`):

| Measure | What it is | Rule |
|---|---|---|
| Thoughtful-bot fail rate | `SmartBot` sees what a player sees and looks one or two moves ahead | in each kind's band |
| Slots needed | the fewest slots perfect play wins with | medium, harder and HARD need every slot; easier kinds may leave one spare |
| Opening | parcels the first truck can take at once | at most 1 (2 on a debut) |
| Reactive-bot fail rate | `GreedyBot` loads what fits, else parks whatever's truck comes soonest | floor per kind |

The original measure, a random tapper's fail rate, let careful players win every level. A
thoughtful bot won 100% of the old levels.

**Board making.**
- Trucks share colors evenly.
- `spread` deals each truck's parcels through the yard's depth, not as a clump.
- Pens split the back of the yard into lanes on most levels, so it can't hollow out.
- Frozen crates for early trucks outlast their truck.
- Express parcels sit within the first six digs, ideally in a color whose truck is far off.
- Gates never take the first truck's color.
- Keys start buried.
- 2-slot levels keep at most one covered crate.

## Playtests

In each round, 7 agents each played 5 levels through `levelgen play` (the text version of the game:
same rules, same visible information, two upcoming trucks). They played like careful humans, with
up to 3 attempts per level, and reported challenge (1 trivial to 5 unfair), where they were stuck,
trivial stretches and fairness. 35 levels per round, about 200 level-plays in all.

| Round | Change before it | Avg challenge | Retries/losses | Rated 4+ | Rated 1-2 |
|---|---|---|---|---|---|
| 1 | early debuts, mixes, 3-4 slots, thoughtful-bot targets | 2.34 | 1/35 | 1 | 19 |
| 2 | tightness, opening rule, sharper cargo, shorter yards | 2.83 | 5/35 | 6 | 12 |
| 3 | spread, later thaw, shorter express, higher targets | 2.69 | 3/35 | 3 | 14 |
| 4 | pens | 2.74 | 5/35 | 5 | 14 |
| 5 | 2-slot HARD from 40 | 3.00 | 4/35 | 10 | 10 |
| 6 | one covered crate on 2-slot levels, express within 6 digs | 2.91 | 5/35 | 7 | 11 |

Rounds 7-9 replayed only the new early curve (debuts 4-10, first mixes 11-15), 2 testers per level:

| Round | Change before it | Levels | Avg challenge | Retries | Finding |
|---|---|---|---|---|---|
| 7 | debuts moved to 4-10, new mixes 11-15 | 4-15 | 2.7 | 2/24 | pace of one new type a level is fine; frost, covers, padlock and express debuts never changed a decision; L12 a dip |
| 8 | debuts must make their cargo matter; L12 covers + straps | 4-12 | 2.6 | 0/18 | frost (5) and straps (8) teach well, L12 fixed; padlock (7), express (9), bollards (4) still passive |
| 9 | front padlock blocking a wanted parcel, deeper express, bollards in the first two rows | 4, 7, 9 | 2.7 | 0/6 | padlock and express now force a plan; bollards stay mild |

Rounds 5 and 6 match, so the loop stopped there. Testers agree that colors are no longer grouped,
that every level from about 8 has a real opening puzzle, and that 2-slot HARD levels (38, 40, 43,
55, 60, 85) are the best of the set.

## What's left: needs a game decision, not level tuning

1. **Hidden truck order.** The most repeated fairness note: a choice is punished by a truck that
   wasn't shown yet, 5-10 moves later (levels 20, 40, 43, 80, 83). Showing three upcoming trucks,
   or a remaining-trucks count per color, would turn those losses into planning.
   *Decided 2026-11-01: keep two.* The road keeps showing two trucks after the loading one, and
   `SmartBot` and `levelgen play` keep grading with that view. Not knowing further ahead stays part
   of the challenge.
2. **The back half still turns into cleanup** on many levels once the front is dug. Options: shorter
   levels, a second choke point late (gate or lock), or rules that keep pressure on, such as fewer
   slots late in a level.
3. **Covered crates** are either worked out by counting (each color is a multiple of 3) or, with 2
   slots, can feel like a coin flip. A per-color remaining count would make them fair puzzles.
4. **Strapped pairs** are always one color, so they are a free two-for-one. Mixed-color pairs (one
   to the truck, one to a slot) would make them a decision, but that changes the rules in
   `GameSession.CanTake`.
5. **Keys** usually match a truck that's coming anyway. Coloring keys against the truck order would
   make unlocking a detour.
