# Range, roles and weapon variety

Research notes for the range/role scoring pass. **Implemented in 0.8.0**; see
the end of this document for what shipped and where it differs from the first
proposal. The goal was to stop every ranged pawn from converging on the chain
shotgun and to make range, skill, genes and character preference visible in
Armory's advice.

## 1. What RimWorld actually does

### Hit chance

RimWorld separates the shooter's aim from the weapon's accuracy:

```text
hit chance = shooter aim factor x weapon accuracy at distance x cover x weather ...
```

The shooter factor is exponential in distance. From `Verse.ShotReport`:

```csharp
float f = pawn.GetStatValue(StatDefOf.ShootingAccuracyPawn);
float num = Mathf.Pow(f, distance);
// then multiply by an interpolated ShootingAccuracyFactor_*
```

The range-specific factors are `ShootingAccuracyFactor_Touch`, `Short`,
`Medium` and `Long`. They are 1.0 by default, but genes and mods change them.
The interpolation anchors are:

| Band | Anchor distance |
|---|---:|
| Touch | 3 tiles |
| Short | 12 tiles |
| Medium | 25 tiles |
| Long | 40 tiles |

Weapon accuracy uses the same anchors. `VerbProperties.GetHitChanceFactor`
interpolates the weapon's `AccuracyTouch/Short/Medium/Long` and clamps the
result to 1%..100%. It also includes quality.

This means range is penalised twice: the pawn's per-tile aim decays
exponentially, and the weapon's accuracy curve changes with distance.

### Skill matters much more at long range

At 30 tiles, with normal accuracy factors:

| Weapon | Shooting 5 | Shooting 10 | Shooting 20 |
|---|---:|---:|---:|
| Assault rifle | 1.2 DPS | 2.7 DPS | 5.0 DPS |
| Sniper rifle | 0.8 DPS | 1.8 DPS | 3.3 DPS |

A high-skill shooter gets roughly four times the long-range output of a
low-skill shooter. The wiki calls close-range weapons "skill friendly" for
exactly this reason: they lose less of their DPS to distance.

### Genes can be decisive

The Biotech `Nearsighted` gene sets:

```xml
<ShootingAccuracyFactor_Medium>0.5</ShootingAccuracyFactor_Medium>
<ShootingAccuracyFactor_Long>0.25</ShootingAccuracyFactor_Long>
```

A nearsighted pawn is not just "a bit worse" at range; their medium-range aim
is halved and long-range aim quartered. The current save contains 19
`Nearsighted` gene entries. That is a large part of why close-range weapons are
actually correct for much of this colony. Armory should know that explicitly
instead of arriving at it by accident.

### Effective DPS at range

At Shooting 10 (`ShootingAccuracyPawn` 0.97), normal quality, no cover, no
weather, no armor:

| Weapon | Range | Raw DPS | 10 tiles | 20 tiles | 30 tiles | 40 tiles | Power/10 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Chain shotgun | 12.9 | 18.7 | 8.6 | - | - | - | 11.1 |
| Pump shotgun | 15.9 | 8.4 | 5.3 | - | - | - | 6.9 |
| Heavy SMG | 22.9 | 12.3 | 6.3 | 3.1 | - | - | 4.9 |
| Assault rifle | 30.9 | 10.9 | 5.4 | 4.0 | 2.7 | - | 7.7 |
| Charge rifle | 27.9 | 14.1 | 6.5 | 4.5 | - | - | 9.9 |
| LMG | 25.9 | 18.1 | 6.2 | 3.9 | - | - | 6.9 |
| Bolt-action | 36.9 | 5.6 | 3.2 | 2.6 | 2.0 | - | 5.9 |
| Sniper rifle | 44.9 | 5.0 | 2.4 | 2.2 | 1.8 | 1.3 | 5.9 |
| Minigun | 30.9 | 41.7 | 7.3 | 5.7 | 3.8 | - | 10.3 |

`-` means the weapon cannot fire at that distance. "Power" is Armory's current
value at 1/10 scale (the mod multiplies by 10 for display), including the
armor-penetration multiplier but not pawn accuracy. The table's DPS values
include shooter accuracy and weapon accuracy, but not cover, weather or armor.

The table shows the real trade-off:

- below about 13 tiles the chain shotgun is the strongest normal weapon;
- from about 13 to 25 tiles the charge rifle, assault rifle, SMG and LMG
  compete;
- past 31 tiles the assault rifle and most rifles are out of range; the
  bolt-action and sniper are the remaining common answers;
- the sniper is not a high-DPS weapon, it is a *reach* weapon.

## 2. Why the current model picks shotguns

`WeaponPower.Compute` uses a fixed accuracy blend:

```csharp
accuracy = 0.2 * AccuracyShort + 0.3 * AccuracyMedium + 0.5 * AccuracyLong;
```

This has three problems:

1. **It samples accuracy the weapon cannot use.** A chain shotgun has a
   `AccuracyLong` value but a 12.9-tile range. The long-range term still counts
   for 50% of its score.
2. **It ignores whether the weapon can reach the sampled distance.** There is
   no range gate.
3. **It does not use the pawn's range-specific accuracy factors.** `PawnFit`
   uses `ShootingAccuracyPawn` once, plus aiming speed, but never the
   `ShootingAccuracyFactor_*` curve. A nearsighted pawn and a normal pawn get
   almost the same weapon ranking.

The result is predictable: the chain shotgun has the highest raw DPS and a
decent blended accuracy, so it wins the scalar comparison for nearly every
pawn, even when the fight is at 20+ tiles and the shotgun cannot fire.

## 3. How the reference mods handle it

### Simple Sidearms

Simple Sidearms already solves "which carried weapon at this distance". During
a ranged warmup, before the shot fires, it:

1. lists every carried ranged weapon that can reach the target;
2. computes `raw DPS x GetHitChanceFactor(weapon, targetDistance)` for each;
3. switches if another weapon beats the current one by an anti-oscillation
   margin.

Defaults: auto-switch on, can switch until 75% of the warmup has elapsed,
speed bias 1.1. It does not model armor (there is a `TODO: handle DPS vs.
armor?` in the source).

Consequences for Armory:

- SS will use a sniper at long range and a shotgun at close range **if the pawn
  carries both**.
- Armory currently gives a ranged pawn one ranged hand weapon and one
  opposite-type (melee) backup. It never builds a ranged range ladder.
- The hand choice therefore matters a lot: it is the only ranged weapon the
  pawn is guaranteed to have for most fights.

### Equipment Manager

Equipment Manager's default loadouts are the role model the user remembered:

| Loadout | Weapon rule | Assignment bias |
|---|---|---|
| Assault | Highest DPSA, range weight 0.2 | default ranged |
| Sniper | Range 2.0, damage 1.5, long DPS 1.0, stopping power 0.5 | high Shooting skill and accuracy |
| Support | High rate of fire, burst count, short DPS | negative Shooting weight |
| Slasher / Crusher | melee rules | Brawler |
| Pacifist | no weapon | violent incapability |

The important idea is not the exact weights. It is that **role assignment is
based on the pawn**, so the colony naturally mixes weapon classes. The mod also
uses a separate "low warmup" rule for ranged sidearms.

### What this suggests for Armory

Armory does not need a full loadout editor. It needs:

1. a range-aware weapon score;
2. a pawn-specific preferred range;
3. a light variety layer so equal pawns do not become clones.

## 4. Design goals

- A shotgun should still be the best choice for a close-range pawn.
- A rifle should win for a mid-range pawn.
- A bolt-action or sniper should be viable for a high-skill, long-range pawn.
- A nearsighted pawn should not be advised to carry a sniper.
- The reason should be visible in the UI, not hidden in a single number.
- Modded weapons should work through their own stats, not a hardcoded table.
- Melee scoring should stay as it is.

## 5. Options

### A. Range-aware effective DPS profile

Replace the fixed accuracy blend with a weighted sample of effective DPS over
distance:

```text
rawDps      = damage x burst / (warmup + (burst-1) x gap/60 + cooldown)
weaponHit(d)= verb.GetHitChanceFactor(weapon, d)
pawnHit(d)  = ShotReport.HitFactorFromShooter(pawn, d)
dps(d)      = rawDps x weaponHit(d) x pawnHit(d) x armor x situational
score       = sum(weight(d) x dps(d))
```

Advantages:

- uses the game's own accuracy interpolation and quality;
- handles Nearsighted, Careful shooter, Trigger-happy, drugs, bionics and mods
  automatically;
- a weapon that cannot reach a distance contributes zero there;
- no hardcoded weapon table.

The only design choice is the distance weighting.

### B. Pawn preferred range from effective reach

Instead of a fixed distribution for everyone, derive a preferred distance from
the pawn's actual aim curve.

Proposed heuristic:

1. Compute `aim(d) = ShotReport.HitFactorFromShooter(pawn, d)` for 3..45.
2. Let `reach` be the farthest distance where `aim(d)` is still at least half
   of `aim(10)`.
3. `preferred = clamp(reach - 8, 8, 34)`.

Why subtract 8? It keeps the pawn inside its effective range instead of
fighting at the edge where every shot is a coin flip. This is a tunable margin,
not a game rule.

Examples:

| Pawn | Effective reach | Preferred | Likely role |
|---|---:|---:|---|
| Nearsighted, Shooting 7 | ~19 | ~11 | CQB: chain shotgun, heavy SMG |
| Normal, Shooting 7 | ~24 | ~16 | Assault: charge rifle, SMG |
| Normal, Shooting 10 | ~33 | ~25 | Line: assault rifle, charge rifle |
| Normal, Shooting 15 | ~40 | ~32 | Marksman: sniper, bolt-action || Nearsighted, Shooting 15 | ~23 | ~15 | Assault, not marksman |

This is the cleanest way to make skill and genes matter without hardcoding
"skill > 12 means sniper".

### C. Trait and passion offsets

Small offsets on top of B, for character flavour:

| Source | Offset |
|---|---:|
| Careful shooter | +2 to +3 tiles |
| Trigger-happy | -2 to -3 tiles |
| Shooting passion (minor / major) | +1 / +2 tiles |
| Shooting specialist role | +2 tiles |
| Brawler | melee, existing logic |

Most of the effect is already in the stats. The offsets are there to make the
role readable ("careful shooter: prefers 29 tiles") and to avoid treating a
passionate shooter like a clone.

### D. Variety layer

Two independent mechanisms:

**Personal affinity.** A deterministic hash of pawn ID and weapon def gives a
small multiplier, for example +/-5%. Show it in the ledger as "personal
preference x1.04". This breaks ties and makes two equal pawns choose different
but comparable weapons.

**Duplicate penalty.** When building the colony plan, apply a soft penalty for
each other pawn already assigned the same def:

```text
adjusted = score / (1 + variety x 0.15 x plannedCount[def])
```

At `variety = 0.5`, the second copy of a weapon is about 7% less attractive,
the third about 13% less. This is enough to turn "everyone gets a chain
shotgun" into a mix of chain shotguns, heavy SMGs and charge rifles without
forcing a bad weapon.

The duplicate penalty needs a deterministic pawn order and a two-pass Demand
build. It is more complex than personal affinity, but it is the mechanism that
actually prevents clone armies.

### E. Support weapons

A pure DPS profile makes the minigun and LMG look excellent for almost every
role. The wiki is explicit that they are support/crowd-control weapons: the
minigun user is a "sitting duck", bursts waste damage when the target dies, and
the LMG has lower single-target DPS than rifles.

Simple first pass:

- tag weapons with `burstShotCount >= 6` as support weapons;
- apply a hand-slot multiplier of about 0.7-0.8 for non-support pawns;
- show "support weapon: x0.75 in hand" in the ledger;
- keep them fully available for manual use and for a future explicit Support
  role.

Without this, the range model simply replaces "everyone gets a shotgun" with
"everyone gets a minigun".

### F. Simple Sidearms range ladder

A larger change: let a ranged pawn carry a second ranged weapon as a backup,
chosen to cover the band the hand weapon does not. SS would then switch
automatically at the target distance.

Advantages:

- uses SS's strongest feature;
- makes a sniper + shotgun or rifle + SMG loadout meaningful;
- range becomes a property of the loadout, not just the hand weapon.

Costs:

- changes the current "hand + opposite type backup" model;
- needs UI for two ranged slots and SS slot-limit handling;
- interacts with the existing duplicate-backup rule and mass limits.

Worth doing after the scoring model is stable, not in the same change.

## 6. Recommended first iteration

I would do **A + B + E**, with C as small offsets and D as a light layer.

### Scoring

- Keep `Power` as the pawn-independent base strength for the Weapons tab.
- Add a `CombatScore` used for pawn candidates and Demand:
  - sample `dps(d)` at 3..45 tiles (1- or 2-tile steps);
  - weights from a normal distribution centred on the pawn's preferred
    distance, sigma about 6;
  - multiply by the existing armor/situational terms and a support penalty.
- Use `ShotReport.HitFactorFromShooter` and `VerbProperties.GetHitChanceFactor`
  directly; do not reimplement the curves.
- Keep melee scoring unchanged.

### Preferred distance

- `reach` = last distance where aim is at least half the 10-tile aim.
- `preferred = clamp(reach - 8 + settings.engagementOffset, 8, 34)`.
- `engagementOffset` is a colony-wide setting:
  - -5 closer / killbox,
  - 0 balanced (default),
  - +5 open field.
- Label the result: CQB (<=12), Assault (13-22), Line (23-30), Marksman (31+).

### Variety

- Start with personal affinity +/-5%, displayed in the ledger.
- Add the duplicate penalty behind a `variety` setting (default light) after
  the first pass is tested. If the colony is homogeneous, this is what creates
  the visible mix.

### Support weapons

- Apply the hand penalty for `burstShotCount >= 6`.
- Do not remove them from the candidate list; show the penalty.

### UI

- Add `Range` to the Weapons and Upgrades tables.
- Add `Role` / `Preferred` to the pawn list or detail.
- In the pawn detail, show the profile for the selected weapon, for example:

```text
Range 30.9    preferred ~25    role Line
DPS at 10 / 20 / 30: 5.4 / 4.0 / 2.7
```

- Add a ledger line: "range profile x0.82 (prefers 25)" so the score is
  explainable.

### Settings

Two new settings are enough:

```text
Combat distance: -5 closer ... 0 balanced ... +5 longer   (default 0)
Squad variety:   0% ... 100%                               (default 50%)
```

Everything else should be derived.

## 7. Expected effect on the current colony

The current save has many low-Shooting pawns and 19 Nearsighted gene entries.
A range-aware model will legitimately keep most of them on close-range weapons;
that is not a bug, it is the gene and skill profile. The difference is that it
will now be explicit and explainable.

What should change:

- chain shotgun stops being the default for every ranged pawn;
- low-skill / nearsighted pawns see chain shotguns, heavy SMGs and pump
  shotguns as close-range choices;
- normal mid-skill pawns see charge rifles and assault rifles;
- the few high-skill, non-nearsighted pawns see bolt-actions and snipers;
- miniguns and LMGs stop crowding out rifles unless the player deliberately
  wants a support weapon;
- equal pawns can still differ through personal affinity and the duplicate
  penalty.

## 8. Open questions

1. **Default combat distance.** Is the colony mostly a killbox (10-15),
   balanced (20), or open field (25-30)? I would default to 20 and expose the
   offset.
2. **Role assignment.** Automatic from effective reach, or also a manual
   per-pawn override? Automatic is the recommendation; a manual "prefer
   marksman / assault / CQB" toggle can come later.
3. **Variety strength.** Light personal affinity only, or also the duplicate
   penalty? The duplicate penalty is the one that actually prevents clones.
4. **Support weapons.** Penalize LMG/minigun in the hand by default, or leave
   them to raw DPS? I recommend the penalty.
5. **SS range ladder.** Keep one opposite-type backup for now, or make a second
   ranged backup part of the next milestone?

## 9. What shipped in 0.8.0

The first implementation follows the recommended path with a few concrete
choices:

- `CombatScoring` uses `ShotReport.HitFactorFromShooter` for the pawn curve and
  `VerbProperties.GetHitChanceFactor` for the weapon curve.
- Auto preferred distance is `reach - 8 + engagementOffset`, clamped 8–34.
- Role centers are CQB 10, Assault 17, Line 25 and Marksman 34. The marksman
  center is farther than the first proposal so a manual Marksman override
  actually prefers weapons that can reach past 31 tiles.
- Per-pawn overrides are stored in `ArmoryGameComponent` and saved with the
  colony; `Auto` removes the override.
- `Squad variety` defaults to 50% and controls both the ±5% personal affinity
  and the sequential duplicate top-choice penalty (Upgrades and the planner).
  0% disables both.
- Support weapons (`burstShotCount >= 6`) get a 0.75 support multiplier.
- Upgrades and Weapons show a `Range` column; Upgrades detail lines show the role
  on hover; the colonist detail shows a `Change role` menu.
- The one-hand + opposite-type backup model is unchanged. A second ranged
  backup for SS remains the next milestone (option F).

## 10. References

- RimWorld wiki, `Weapons#Accuracy`: accuracy anchors, interpolation and the
  per-tile shooting accuracy formula.
- RimWorld wiki, `Weapon Guide`: skill-friendly close-range weapons, assault
  rifle as generalist, chain shotgun as close-range specialist, sniper as
  skill-reliant reach weapon, LMG/minigun as support.
- `Verse.ShotReport.HitFactorFromShooter` and
  `Verse.VerbProperties.GetHitChanceFactor` in `Assembly-CSharp.dll` (1.6).
- Simple Sidearms source: `StatCalculator.RangedDPS`,
  `GettersFilters.findBestRangedWeapon`,
  `WeaponAssingment.trySwapToMoreAccurateRangedWeapon`.
- Equipment Manager source: `Loadout.DefaultLoadouts`,
  `RangedWeaponRule.DefaultRules`.
- Current save: `Northwestern Esora Humanlike Confederacy.rws`, 19
  `Nearsighted` entries; Shooting skills mostly 3-9 with one 15.
