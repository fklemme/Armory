# Armory

**The right weapon, in the right hands.**

**Version 0.12.0** · RimWorld **1.6** · packageId **`kruecke.armory`**

A weapon inspector and automatic manager for RimWorld. It answers two
questions that existing managers leave opaque:

1. **What does each colonist want to carry, and why aren't they carrying it?**
2. **Who should get this new weapon, and what should I build or unforbid?**

Four tabs: **Colonists**, **Weapons**, **Upgrades**, **Log**.

---

## Quick facts

| Item | Value |
|---|---|
| Mod name | Armory |
| Author | Kruecke |
| packageId | `kruecke.armory` |
| Steam Workshop | [Armory on Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3842538224) |
| Source / issues | [github.com/fklemme/Armory](https://github.com/fklemme/Armory) |
| Game version | 1.6.4871 |
| Dependencies | None. Simple Sidearms is optional and read by reflection |
| Build target | `net472` |
| Workspace | `C:\Users\mail\Projects\Rimworld Mods\Armory` |
| Game install | `C:\Program Files (x86)\Steam\steamapps\common\RimWorld` |
| Junction | `...\RimWorld\Mods\Armory` → workspace |
| Current milestone | 0-6 complete; 7 (Simple Sidearms hand alignment) done in 0.12.0; range/role scoring in 0.8.0, persona guard in 0.8.1, encoding fix in 0.8.2, interface wording pass in 0.8.3, tab rename/width pass in 0.8.4, icon/tab-order pass in 0.8.5, simplified icon in 0.8.6, release text/defaults/banner pass in 0.9.0, colonist-bar order in 0.9.1, release staging and `Best vs current` in 0.9.2, typed hand/auto-align/passion in 0.10.0, list/UI polish in 0.10.1, backup safety/unusable-baseline fixes in 0.10.2, double-click map jumps in 0.10.3, comparison/material/role insights in 0.10.4, accuracy-column/table-layout polish in 0.10.5, percent accuracy/cost icons in 0.10.6, named craft bills in 0.10.7, pending-job duplicate guard in 0.10.8, market value columns in 0.10.9, value/layout polish in 0.10.10, blocked-reason spacing fix in 0.10.11, sidearm loadouts in 0.11.0, stale-sidearm cleanup and persistent diagnostics in 0.11.1, Simple Sidearms hand-preference alignment in 0.12.0 |

---

## What it does

### Colonists
For every free colonist, including pawns in pods/caskets:

The list follows the **colonist bar** at the top of the screen — the same order
as the portraits, which defaults to join order. Drag a portrait to reorder both
the bar and this list.

- a multi-line **Weapons** block with one line each for **Hand**, **Sidearm 1**
  and **Sidearm 2**;
- a **`Best vs current`** column comparing the best available hand candidate
  with the logical hand weapon; negative means the current weapon is still better.
  When the equipped weapon is the other hand type, the column says `switch type`
  instead of a meaningless percentage;
- a **hand slot** ranking of one type, `ranged` or `melee`, with the best
  available upgrade; the type is derived automatically and can be overridden per
  colonist;
- a **ranged role** (`CQB`, `Assault`, `Line`, `Marksman`) and preferred
  engagement distance, derived from the colonist's actual accuracy curve and genes,
  or fixed with a per-colonist override; melee pawns have no ranged role;
- with Simple Sidearms, **Sidearm 1** is the opposite-type slot and **Sidearm 2**
  is an optional ranged complement chosen to cover a real range gap, with SS's
  slot/mass/selection blockers on each candidate;
- SS's own default ranged / preferred melee preference is aligned with the
  Armory hand. The white default dots and the equipped-first gizmo icon then
  follow the same weapon Armory displays in the Hand slot;
- when Simple Sidearms draws a sidearm in combat, the hand line keeps the
  logical hand and marks it `(stowed)`, while the drawn sidearm is marked
  `(drawn)`; Armory pauses weapon changes until SS stows it. Outside combat an
  **unremembered** sidearm is treated as temporary: Armory restores the
  remembered hand and drops a Sidearm 2 that no longer covers a range gap. A
  remembered sidearm that still fills a gap is left to Simple Sidearms;
- double-click a row to close Armory, move the camera to that colonist and select
  them on the map;
- a full ranked list with a status per candidate and an expandable factor ledger;
- a separate `Cannot equip` list for biocoded/bonded/role-restricted weapons;
- an `Automation blocked:` or `Automation paused:` reason when the planner cannot
  act (manual weapon change, swap cooldown, drafted, pacifist, downed, bonded
  weapon, Simple Sidearms job, and so on).

Ranged and melee weapons are never compared in one ranking. Every pawn has a
hand type, so a ranged pawn only considers ranged hand weapons and a melee pawn
only considers melee hand weapons. If the equipped weapon is the other type,
Armory automatically switches to the best ready weapon of the chosen type; the
old weapon is dropped by the normal equip job. The type is chosen from the
player's per-pawn override, then a bonded weapon, a ranged-blocking shield belt,
Brawler, shooting/melee passion, and finally skill. When no signal prefers melee,
ranged is the default. Without Simple Sidearms only the Hand line is shown; the
pawn detail notes that sidearm slots require Simple Sidearms.

### Weapons
Every loose weapon on the map with:

- base market value in silver as the second column, followed by raw DPS, generic
  DPSA, armor penetration, separate accuracy columns for
  Close/Short/Medium/Long (shown as percentages), range and the best-ready colonist;
- sortable comparison headers for every stat, including each accuracy band and value;
- role filters for **All / Melee / CQB / Assault / Line / Marksman**, so the same
  list can answer "what is best for a line shooter?" without changing the pawns;
- a **Best by role** table in the detail pane, showing the best pawn and score for
  each role, plus the usual colonist-by-score view with deep checks (reachability,
  reservations) and separate Hand / Sidearm 1 / Sidearm 2 readiness;
- best use (`Hand`, `Sidearm 1`, `Sidearm 2`, their blocked variants, or
  `No colonist can use it`);
- double-click a row to close Armory, move the camera to that weapon and select it
  on the map.

The generic **DPSA** is a catalog comparison: raw DPS multiplied by the average
accuracy over the four standard range bands, with unreachable selected bands
counted as zero. It is deliberately not the recommendation. The colonist-fit
score uses the game's real hit chance at that pawn's preferred range, plus skill,
fit, support and variety. The formula is shown in the weapon detail and in the
Upgrades header.

Ranged power is scored at each pawn's preferred distance using RimWorld's real
accuracy curve. A shotgun and a sniper are not compared as if both could fire at
every range.

This tab lists **loose weapons on the map only**. A weapon that is equipped or
carried by a pawn is visible in that pawn's detail instead. Unbonded persona
weapons are listed, but Armory will not auto-equip them: bonding is permanent,
so the player chooses the pawn and equips the weapon manually.

`Hand (blocked)` means the weapon is the best hand fit for a colonist but cannot
currently be equipped (usually a Simple Sidearms hand-type block). A melee weapon
can be a `Sidearm 1` candidate for a ranged colonist even when it cannot be that
colonist's hand weapon; if Simple Sidearms' mass cap rejects it, it is
`Sidearm 1 (blocked)` with the reason. `Sidearm 2` is the optional ranged
complement; it appears only when it closes a real range gap.

### Upgrades
The upgrade menu: every obtainable weapon **variant** that would improve a
colonist, minus what the colony already owns. It answers "what could we make,
research, or buy next?" instead of "who would pick up the leftovers on the
floor?"

Stuffable weapons are split by material, so a steel knife and a plasteel knife
are separate rows with their own score, cost and owned count. This makes the
resource trade-off explicit: a plasteel knife may score higher but cost 75
plasteel per copy, while a steel one is cheaper. The `Craft` button queues the
bill for the material in that row, with the bill's ingredient filter restricted
to that stuff. Armory also names the bill `Armory: Uranium mace` (or a shorter
`Armory ...` form for long item names) so it is distinguishable from existing
“Make mace” bills; RimWorld's rename dialog caps custom bill names at 28
characters. To keep the list manageable, only materials with stock or owned
copies plus the highest-scoring material are shown; Show owned reveals owned
copies that are not upgrade candidates.

For every colonist and slot, Armory scores every weapon variant the colony could
obtain: craftable now, craftable after pending research, buyable from a trader,
or already owned. **Every** variant that beats the current weapon by the upgrade
threshold is listed. Rows are independent options, so one colonist can appear in
several of them. A gold star before the name marks the variant as the best
option in its section for at least one colonist; all weapon names are white.
Role filters (All / Melee / CQB / Assault / Line / Marksman) limit the list to
colonists of that role and re-score the rows for that role.

Research options must also beat the best currently obtainable option by the
threshold, so research never advertises a weapon that is worse than something
already buildable. Owned copies (equipped, carried, or loose) are subtracted
per material:

```
Needed = max(0, Wanted by - owned copies of that material that are not forbidden)
```

Columns:

- `Value`: base market value in silver per copy, before negotiator skill and
  buy/sell multipliers. Craft/Research/Buy rows assume normal quality; Owned rows
  use the actual best copy;
- `Power`: normal-quality base strength before range and pawn fit;
- `Range`: maximum firing range in tiles (`-` for melee);
- `Cost`: per-copy ingredients shown as item icons with `x` counts; a count is
  orange when current stockpiles cannot cover all needed copies. Hover for the
  full ingredient list, current stock and the total for the needed copies;
- `Score`: range-aware expected value for the selected role (or all roles),
  summed over the colonists who would take it; this is the main sort key;
- `Slot`: `Hand`, `Sidearm 1`, `Sidearm 2`, or `Both`, using the same
  green/blue/cyan colors as the Weapons tab; hover for the slot counts;
- `Wanted by`: colonists who would improve with this variant (may overlap other rows);
- `Owned`: copies of this material in the colony (`2 · 1 forbidden` when some are forbidden);
- `Needed`: copies still missing; `enough` (green) when the variant is the best option
  for someone but already covered;
- `Gain`: average improvement over the current weapon, assuming a newly made
  normal-quality copy of this material; `new slot` when the colonist has nothing in that slot and
  `switch type` when they hold the other hand type;
- `Source`: `craft · bench`, the research project, or `buy`. For craft rows the
  bench is the best **built** bench that can make it; if none is built, the best
  **unlocked** bench is shown instead. A craft row with missing copies and a
  built bench also gets a `Craft` button that queues one bill at that bench,
  restricted to the row's material. It does not equip the weapon.

**Show owned** adds an `Owned` section listing owned material variants for
comparison, using the best actual quality/material copy. Owned rows show no
crafting cost and appear regardless of the active role filter.

Sections follow the order of action: **Craft** (recipe unlocked now),
**Research** (a project unlocks it; `· buy` when a trader can also supply it),
**Buy** (no crafting route, trader only), then **Owned** when Show owned is on.
Within a section, rows are ordered by `Score` (the selected role's score when a
role filter is active), then total gain, best count and needed copies. This is
not pure weapon power: a weapon that suits more colonists can outrank a slightly
stronger one that helps only one. `Power` is the weapon alone; `Score` adds range,
pawn fit, role preference and a light squad-variety penalty.

Rows that are the best option for someone stay visible even when the requirement
is already covered; those show `enough` in green. A gold star before the name
marks the best option in its section for at least one colonist; all names are
white, so the star is the only distinction. Without that, an owned ideal (for
example the hatchet a colonist already carries) would disappear. Click a row to
see one detail line per contributing colonist, with their name, current weapon,
ranged role, individual `Score`, `Slot`, and `Gain` aligned under the same
columns. Variants with no gap are otherwise hidden.

### Hand type, range and ranged roles

Every colonist has a **hand type**: `ranged` or `melee`. Armory resolves it in
this order:

1. the player's per-pawn **Hand type** override (`Auto`, `Ranged`, `Melee`);
2. a bonded persona weapon currently in hand (its type wins, and the hand stays
   locked);
3. a worn shield belt that blocks ranged fire (`melee`);
4. the `Brawler` trait (`melee`);
5. the higher Shooting/Melee **passion** (`Major` beats `Minor`, `Minor` beats
   `None`);
6. skill: melee is chosen only when Melee is at least **3 levels higher** than
   Shooting; otherwise ranged is the default. Close skills therefore keep the
   ranged default rather than flipping to melee on a one-point difference.

Only weapons of the chosen hand type are ranked. If the colonist is holding the
other type, the planner automatically equips the best ready weapon of the chosen
type; if none is ready, the current weapon is kept and the detail shows a
mismatch warning. The per-pawn override is saved with the colony and survives
save/load. With Simple Sidearms active and respected, SS's own primary mode owns
the hand type instead; the override is ignored while SS is in control.

RimWorld's hit chance is a per-tile shooting-accuracy curve multiplied by the
weapon's accuracy at that distance. Armory samples that curve over a band around
each pawn's preferred distance, so a chain shotgun and a sniper rifle are never
compared as if both could fire at every range.

The preferred distance is automatic by default:

- the pawn's effective reach is the farthest distance where aim is still half
  of its 10-tile value;
- preferred distance is that reach minus 8 tiles, clamped to 8–34;
- low skill and the Biotech `Nearsighted` gene therefore produce a short-range
  role; high skill with normal long-range accuracy produces a marksman;
- `CQB` is 12 tiles or less, `Assault` 13–22, `Line` 23–30 and `Marksman` 31+.

The pawn detail has a `Change range` button for a per-pawn ranged-role override.
Overrides are saved with the colony (not the mod settings) and survive save/load.
`Auto` returns to the derived role. The separate `Change type` button controls
the ranged/melee hand type.

A small deterministic personal preference and a sequential duplicate penalty
keep equal pawns from becoming clones. Both are controlled by **Squad variety**
in settings; set it to 0% to disable them. The duplicate penalty applies to
upgrade rows and to the optional hand planner. LMGs and miniguns are treated as
support weapons and receive a 0.75 support penalty so their raw burst DPS does
not make every pawn want one.

`Owned` includes forbidden copies; `Needed` only counts usable ones, and its
tooltip shows how many unforbidding would cover. A research-locked def you
already own shows `more: <project>` when only extra copies need the research.
A pawn is never offered a second copy of an ordinary weapon def they already
carry as a sidearm; hand swaps of the same def are still allowed for quality
upgrades, and persona/unique weapons compare by instance. The same duplicate
check runs against the pawn's live inventory, equipped weapon and queued jobs
immediately before a pickup is ordered, not only against the cached snapshot.

Unlike the planner, the Upgrades tab ignores transient state: a drafted, asleep, or
full-bagged colonist still counts, because the plan is about what the colony
should own, not what a job can do this second. Only permanent gates (age,
violence incapability, manipulation) remove a colonist. Manual-use ranged
weapons (such as a Molotov) are not offered as sidearms; biocoding and bonds do
not apply to hypothetical new copies.

### Log
Session decision log for the planner: time, colonist, action, weapon, and the
reason the action was taken. Actions are `Equip hand`, `Equip Sidearm 1`,
`Equip Sidearm 2`, `Drop sidearm`, `Drop extra sidearm`, `Skip upgrade`,
`Drop failed`, `Restore sidearm`, and `Align SS hand`. Newest first, clearable.

The same decisions, plus full snapshots on map load and on demand, are written
to a persistent rolling log in `ArmoryLogs` next to the save data. **Log
snapshot** captures the current state for a bug report; **Open logs** opens the
folder. The newest five sessions are kept.

---

## Build

```
dotnet build Armory\Source\Armory\Armory.csproj
```

Output: `Armory\Assemblies\Armory.dll`.

NuGet references:

- `Krafs.Rimworld.Ref` `1.6.4871` — reference-only game assemblies;
- `Microsoft.NETFramework.ReferenceAssemblies` `1.0.3` — lets `dotnet build`
  target `net472` without Visual Studio.

No Harmony dependency yet. No XML patches beyond `MainButtonDef`.

---

## Install for testing

The workspace is junctioned into the game, so building is enough:

```
cmd /c mklink /J "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Armory" "C:\Users\mail\Projects\Rimworld Mods\Armory"
```

In RimWorld: enable **Armory** in the mod list, restart, open the **Armory** tab.

> Close RimWorld before rebuilding. Windows locks the loaded `Armory.dll`.

### Publishing to Steam Workshop

`tools/Publish-Armory.ps1 -Prepare` builds a clean release copy and swaps it
into `RimWorld/Mods/Armory`; upload from RimWorld's Mods screen in Dev Mode.
`-Restore` removes the release copy and recreates the development junction.
The full checklist, release-folder contents and Workshop page copy are in
[`docs/WORKSHOP.md`](docs/WORKSHOP.md). The current Workshop item is
[Armory on Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3842538224).

---

## Design principles

### Blockers are status, not exclusion

Map-generated weapons start forbidden. Hiding them would produce "nothing
available" on a fresh map. Instead every usable-in-principle weapon is ranked:

- `Can equip` — can be picked up now;
- `Forbidden`, `Outside allowed area`, `No path to weapon`, `Reserved by another
  job` — soft blockers the player can fix;
- biocoded/bonded/role restrictions — hard blockers, shown under `Cannot equip`.

The header shows both lines when they differ:

```
Best available:  bolt-action rifle    Score 64 (+12% vs current)
Highest-scoring hand weapon: plasteel knife    Score 70 (+40% vs current)  - Forbidden
```

### What counts as a weapon

`ThingRequestGroup.Weapon` is broader than it sounds: `ThingDef.IsWeapon` is
`category == Item && (verbs or tools) && !IsApparel`. Wood inherits
`ResourceVerbBase`, beer and meals have ingestible verbs.

Armory excludes:

- `IsStuff` resources (wood, steel, …);
- drugs (`IsDrug`), ingestibles (`IsIngestible`), medicine, apparel, shells.

### Scoring

`score = expected range-aware DPS × fit × (1 + AP) × support × variety × 10`

- **Expected DPS** uses the game's real hit chance at each distance around the
  pawn's preferred range, so skill, genes, traits, weapon accuracy and range all
  matter. A generic four-band `DPSA` is shown in the Weapons comparison for
  catalog purposes only.
- **Power** from vanilla StatDefs and VerbProperties: melee DPS/AP; ranged
  DPS × burst/cycle, sustained fire, blended accuracy, armour penetration.
  Modded and Combat Extended weapons use their own numbers.
- Explosive/incendiary/EMP/smoke weapons get a **0.35× situational penalty**.
- Burst weapons of 6+ shots get a **0.75× support penalty** in the per-pawn score.
- **Fit**: relevant skill, melee-vs-ranged mismatch, Brawler, ranged accuracy
  and aim speed, melee hit chance, ideoligion noble/despised disposition,
  optional global ranged/melee preference.
- Hard blockers never become score penalties.
- Factor ledgers are computed lazily only for rows shown in the UI.

### Unavailable colonists

`MapPawns.FreeColonists` is used, not `FreeColonistsSpawned`, so pod/casket
occupants appear. `DescribeUnavailable` walks `ThingOwner → ThingComp →
Building`, and if that fails it searches player buildings and their
`IThingHolder` comps for the pawn. Rows are grey with a reason such as
`In a biosculpter pod`. Their preferences are shown for reference, but they are
excluded from the upgrade plan, best-fit, and automation.

### Ages

Vanilla has **no weapon-specific age gate**. `EquipmentUtility.CanEquip`
applies developmental-stage filtering only to apparel; `Violent` work tags come
from backstory/traits/genes/kind, not age. Children can equip weapons.

Default minimum age is **3**, slider range **0–18**. A stored value of `13`
from settings created before 0.2.2 is migrated to `3` once.

### Automation

Automation is on by default. While enabled:

- only weapons that can be equipped now are used; **Armory never unforbids**;
- largest upgrades first, then next-best fallback if the top choice is claimed
  or unreachable;
- one claim per weapon instance (900 ticks), per-pawn swap cooldown;
- minimum upgrade gain gate;
- equip jobs are queued with `requestQueueing: true`, so work is not
  interrupted; a pawn with a current or queued Armory job is skipped, so a
  resting pawn cannot accumulate duplicate pickup jobs that all run later;
- manual weapon changes pause that pawn for a configurable period;
- skips pacifists, prisoners, slaves, downed/mental pawns, under-age pawns, and
  drafted pawns by default;
- for a bonded persona weapon, locks the hand only while the bonded weapon is
  equipped; if it is in the bag or on the ground, the hand can still be managed,
  with a warning that another weapon may trigger the persona mood penalty;
- if Auto Arm or Equipment Manager is loaded, automation stays paused unless
  `overrideCompetitor` is enabled.

Every hand and sidearm action, with its reason, goes to the **Log** tab.

### Simple Sidearms coexistence

Simple Sidearms is detected by packageId and read **entirely through
reflection**, so Armory still loads in a Core-only game and does not break when
Simple Sidearms changes. Load order does not matter: Armory resolves the SS
types lazily when it first needs them, so it works with Armory loaded before or
after Simple Sidearms.

SS keeps exactly one weapon in `equipment.Primary` — the **hand** — and extra
weapons in the pawn's inventory — the **bag**. Its per-pawn primary mode
(`Ranged`, `Melee`, `BySkill`) says which type SS keeps in hand; it is not a
separate inventory category, and **sidearm limits apply only to the bag**. That
is why a 2 kg Monosword can be a melee hand weapon but not a melee sidearm under
SS's Basic preset.

Armory therefore models three logical slots when Simple Sidearms is active and
respected:

- **Hand** = the colonist's Simple Sidearms hand type. Armory may upgrade it with
  a better weapon of the same type via the vanilla `Equip` job. The logical hand
  is the best carried ranged weapon at the pawn's preferred range, not whichever
  weapon SS is currently holding; SS can temporarily draw Sidearm 2 for a close
  target without confusing the plan.
- **Sidearm 1** = the opposite type. Ranged pawns get a melee sidearm; melee
  pawns get a ranged sidearm. It is picked from loose weapons with SS's slot,
  mass, selection, pacifist-tool, biocoding and role rules applied.
- **Sidearm 2** = an optional ranged complement. It is chosen by range-gap
  coverage, not raw DPS: Armory compares the Hand + Sidearm 1 profile across
  Close/Short/Medium/Long bands and only adds a weapon that closes a real weak
  band by the upgrade threshold. A sniper hand often pairs with a pistol or light
  SMG; an assault rifle often needs no second sidearm. If SS has no free slot,
  the second sidearm stays empty and the reason is shown.

Ranged and melee are never ranked against each other. Armory's own per-pawn
best carried hand is the plan. SS's QuickSwitch choice remains a conservative
baseline for hand upgrades, so a weapon SS would skip (manual use, dangerous when
SS skips dangerous weapons, or EMP) does not inflate an upgrade; if the
reflection call fails, Armory logs once and uses the current weapon.

When **Respect Simple Sidearms** is on (default):

- a forced weapon, forced unarmed, or prefer-unarmed state is an automation veto;
- with **Align Simple Sidearms hand preference** on (default), Armory sets SS's
  `DefaultRangedWeapon` / `PreferredMeleeWeapon` to the Armory hand and remembers
  that hand through SS's own API. The white default dots and the equipped-first
  gizmo icon then follow the Armory Hand slot. This also clears the temporary
  diagonal marker from the hand;
- Armory waits until SS has switched the pawn to its role type before adding a
  sidearm, because adding the other type while the wrong weapon is in hand invites
  SS to promote it;
- explicit hand blockers remain visible as `Requires a ranged hand` or
  `Requires a melee hand`;
- pawns currently running `EquipSecondary` / `ReequipSecondary` /
  `ReequipSecondaryCombat` are skipped;
- if SS draws a sidearm in combat, Armory keeps the logical hand visible as
  `(stowed)`, marks the drawn weapon `(drawn)`, and waits for SS to stow it
  before swapping weapons. Outside combat Armory aligns SS's hand preference and
  restores the hand, even when the sidearm in hand is remembered and useful. A
  remembered **no-gap Sidearm 2** is restored and removed. A draw no longer
  registers as a manual hand change;

Sidearm management is on by default; when enabled it:

- adds Sidearm 1 via Simple Sidearms' `EquipSecondary` job;
- adds Sidearm 2 only when it improves a weak range band, SS has room, and the
  weapon is legal;
- validates every candidate with `CanPickupSidearmInstance`, so SS slot, mass,
  selection, pacifist-tool, biocoding, and role rules all apply;
- reconciles the carried set instead of only adding: duplicates, unusable
  sidearms and weapons beyond the plan are dropped automatically;
- drops a Sidearm 2 whose range gap disappeared after a hand or Sidearm 1 change,
  after trying a useful replacement first;
- keeps one usable wrong-type fallback until a legal replacement is ready, so a
  cleanup never deliberately leaves the pawn weaker;
- replaces one slot at a time, drops the old weapon first, and restores it if SS
  rejects the replacement;
- uses the same claims, per-pawn cooldown, and manual-change protection as hand
  upgrades;
- never acts on a pawn with a forced/unarmed/prefer-unarmed SS state;
- never auto-adds or auto-drops named work tools, fire extinguishers, or
  non-weapon stat-boost items; real weapons with stat offsets are still weapons.

Armory does **not** edit Simple Sidearms' remembered-weapon list directly. It
uses SS's own APIs: `InformOfAddedPrimary` to remember the chosen hand,
`SetRangedWeaponTypeAsDefault` / `SetMeleeWeaponTypeAsPreferred` for the hand
preference, and `DropSidearm` with `intentionalDrop` and `unmemorise` for weapons
it deliberately removes. The last is the sanctioned way to prevent an immediate
retrieval loop. If SS still restores a weapon that is beyond the plan after a
downing, Armory drops the extra on its next pass and restores the planned
weapon. The old `Multiple backups` manual instruction is gone.

SS sorts the carried icons by equipped first, then market value. Armory does
**not** reorder the gizmo. Aligning the preference and restoring the hand makes
the hand the equipped, default weapon, so it naturally appears first and carries
the white dots; the remaining sidearms follow SS's value sort.

Disabling SS's automatic re-equip is no longer required for hand consistency.
It still gives Armory full control of sidearm restoration; if SS re-equips
instead, Armory may drop an extra afterwards.

Simple Sidearms reports two different capacity failures with terse text. Armory
expands them so they cannot be confused:

- `All sidearm slots are full` - the SS slot **count** limit is reached;
- `Can't carry (X kg, Y kg free)` - the weapon does not fit in the pawn's
  remaining inventory **weight**, even when a slot is free.

SS's mass limits still matter. The Basic preset caps a melee sidearm at 1.9 kg
and a ranged sidearm at 2.7 kg, with 3 total slots including the equipped
weapon. A 2 kg Monosword or Zeushammer is therefore not a valid melee sidearm
while a 0.85 kg gladius is. Heavy rifles often fail the single-sidearm cap, so
Sidearm 2 is often a pistol or light SMG. The Weapons tab shows the blocked
status with Simple Sidearms' reason, and the colonist detail lists the candidate
under Sidearm 1 or Sidearm 2 with that blocker.

Turning **Respect Simple Sidearms** off hands the hand type back to Armory's own
automatic assignment; Armory may then disagree with SS's primary mode while
sidearm management still runs. Leave it on unless you are deliberately taking
manual control.
SS's primary mode is **per pawn**. Changing SS's global default only affects
newly generated pawns; existing colonists keep their own mode. Use the Simple
Sidearms gizmo on a colonist to switch them to Melee or By skill. A melee-mode
pawn gets a ranged Sidearm 1, subject to SS's ranged-sidearm cap, and may get a
second ranged sidearm if it closes another range gap.

---

## Settings reference

All settings are under **Options → Mod settings → Armory**. The page scrolls.

| Setting | Default | Range | Meaning |
|---|---:|---:|---|
| Enable automatic equipping | on | — | Master switch for the planner |
| Run anyway while another manager is loaded | off | — | Bypasses the competitor guard |
| Upgrade check interval | 250 ticks | 120–600 | How often Armory checks for upgrades |
| Wait after a weapon swap | 2500 ticks (~42 s) | 600–10000 | Wait after a weapon swap, per colonist |
| Minimum colonist age | 3 | 0–18 | Vanilla has no weapon age gate |
| Equip drafted colonists | off | — | Queues jobs for drafted colonists |
| Respect manual weapon changes | on | — | Pause after a player/other-mod change |
| Pause after a manual weapon change | 15000 ticks (~250 s) | 2500–30000 | Length of that pause |
| Respect Simple Sidearms | on | - | SS primary mode owns the hand type; respect forced states, logical hand and sidearm jobs |
| Align Simple Sidearms hand preference | on | - | Set SS's default ranged / preferred melee weapon to the Armory hand and remember it through SS's API |
| Manage sidearms | on | - | Keep an opposite-type Sidearm 1 and an optional ranged Sidearm 2; reconcile extras (Simple Sidearms only) |
| Upgrade sidearms | on | - | Replace Sidearm 1 or Sidearm 2 when a better valid option exists |
| Minimum upgrade gain | 10% | 0–50% | Required improvement to recommend or equip a weapon |
| Ranged score weight | ×1.00 | 0.5–2.0 | Global scoring bias |
| Melee score weight | ×1.00 | 0.5–2.0 | Global scoring bias |
| Combat distance bias | 0 tiles | -5–+5 | Shift all preferred ranges closer (negative) or longer (positive) |
| Squad variety | 50% | 0–100% | Per-colonist weapon affinity and duplicate top-choice penalty; 0% disables both |
| Hand candidates shown per colonist | Auto | 1–50 | Auto = 15 without Simple Sidearms, 5 with it |
| Sidearm candidates shown per colonist | Auto | 1-50 | Auto = 5; Simple Sidearms only |
| Show blocked candidates by default | on | — | Include candidates that cannot be equipped now |
| Snapshot refresh interval | 120 ticks | 30–600 | How often the lists recalculate while open |
| Write a persistent diagnostic log | on | — | Rolling per-session log in `ArmoryLogs`; newest five kept |
| Also write the verbose snapshot to `Player.log` | off | — | One-time map-load snapshot in `Player.log` |

Internal: `settingsVersion` (currently 5) drives the age, typed-slot, list-limit
and range/variety migrations. The 0.9.0 automation defaults apply to new
settings only; no migration rewrites an existing `autoEquipEnabled` or
`manageSidearms` value. Per-pawn ranged-role and hand-type overrides are saved in
the colony, not in mod settings.

---

## Layout

```
Armory/
  About/About.xml
  About/Preview.png                mod-list banner
  About/ModIcon.png                512x512 transparent mod-list/loading icon
  Defs/MainButtonDefs.xml          MainButtonDef "Armory", order 25
  Textures/UI/Buttons/MainButtons/IconArmory.png  64x64 crossed-weapons tab glyph
  Textures/UI/Icons/ArmoryBestStar.png  Upgrades best-option star
  Assemblies/Armory.dll            build output
  Source/Armory/
    Armory.csproj
    ArmoryMod.cs                   Mod entry, settings, competitor detection
    ArmorySettings.cs              settings + scrollable options UI
    ArmoryDiagnostics.cs           persistent per-session diagnostic log
    ArmoryMapComponent.cs          snapshot cache + planner tick
    ArmorySnapshotBuilder.cs       colonist/weapon/upgrade analysis, pod lookup
    ArmoryPlanner.cs               automatic equip jobs, claims, cooldowns
    ArmoryGameComponent.cs         session decision log, ranged-role and hand-type overrides
    DebugActions_Armory.cs         log snapshot / unforbid / run planner
    WeaponFilter.cs                single definition of a real weapon
    Compatibility/SimpleSidearmsAdapter.cs  reflection-only SS state and rules
    Model/ArmoryModels.cs          snapshot/candidate/pawn/weapon models
    Scoring/WeaponPower.cs         pawn-independent power + situational penalty
    Scoring/PawnFit.cs             pawn fit + optional ledger
    Scoring/CombatScoring.cs       range profile, preferred distance, roles, variety
    Scoring/WeaponCatalog.cs       craftable/buyable defs + synthetic scoring probes
    Scoring/WeaponVariants.cs      per-material probes, costs and stock status
    Scoring/Eligibility.cs         soft/hard blockers
    UI/MainTabWindow_Armory.cs     Colonists/Weapons/Upgrades/Log tabs
    UI/TextUtil.cs                 rich-text safe measurement/truncation
  docs/RESEARCH.md                 decompiled-mod + API findings
  docs/RANGE_AND_ROLES.md          range/role research, design and decisions
  docs/HANDOFF.md                  exact state and next-step plan
  docs/TESTING.md                  quick + live-save testing procedure
  docs/WORKSHOP.md                 Steam Workshop copy + release checklist
  docs/screenshots/                Workshop gallery images
  tools/Quicktest-Armory.ps1       isolated quicktest harness
  tools/Setup-LiveTest.ps1         copies a real save into an isolated folder
  tools/LiveTestScaffold.cs.txt    temporary auto-load/screenshot scaffold
  tools/Generate-ArmoryIcon.ps1    regenerates the tab glyph and ModIcon
  tools/Generate-ArmoryBanner.ps1  regenerates About/Preview.png from the ModIcon
  tools/Publish-Armory.ps1         stages a clean Workshop copy and swaps the dev junction
```

---

## Diagnostics

- **Persistent diagnostic log** (on by default): one file per session in
  `ArmoryLogs`, next to the save data. It contains the armory version, RimWorld
  version, active Simple Sidearms state, settings summary, every automatic
  decision and full map-load snapshots. The newest five sessions are kept.
  Use **Open logs** in the Log tab or **Open Armory log folder** in settings.
- **Log snapshot** in the Log tab writes the current plan and loaded weapons to
  `Player.log` and to the persistent log. It is the best thing to attach to a
  bug report.
- Enable **Also write the verbose snapshot to Player.log** to get the same
  summary in `Player.log` on every map load.
- Dev mode actions:
  - **Armory → Log weapon snapshot**
  - **Armory → Unforbid loose weapons**
  - **Armory → Run planner now**
- RimWorld log:
  `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`
- Armory logs:
  `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\ArmoryLogs\`

### Isolated test harness

`tools/Quicktest-Armory.ps1` launches RimWorld with:

```
-quicktest -savedatafolder=C:\Users\mail\AppData\Local\Temp\opencode\armorytest
```

If the target folder has no `Config/ModsConfig.xml`, the script creates one with
Core + Armory. Pass `-IncludeSimpleSidearms` to create a config with Harmony and
Simple Sidearms. Your real mod list and settings are never touched. The script
waits for an `[Armory]` log line, prints Armory lines, and closes the game.

To disable automation in the isolated game and enable diagnostics, write
`Config/Mod_Armory_ArmoryMod.xml` in that folder:

```xml
<?xml version="1.0" encoding="utf-8"?>
<SettingsBlock>
  <ModSettings Class="Armory.ArmorySettings">
    <autoEquipEnabled>False</autoEquipEnabled>
    <manageSidearms>False</manageSidearms>
    <diagnosticLogging>True</diagnosticLogging>
    <debugLogging>True</debugLogging>
  </ModSettings>
</SettingsBlock>
```

Manual equivalent:

```powershell
& "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64.exe" `
  -quicktest -savedatafolder="C:\Users\mail\AppData\Local\Temp\opencode\armorytest"
```

### Live save

`tools/Setup-LiveTest.ps1` copies one real save and the whole `Config` folder into
an isolated save-data folder, then optionally builds and launches it with the
full mod list. A temporary copy-in scaffold
(`tools/LiveTestScaffold.cs.txt`) auto-loads the save, dumps the Armory snapshot,
opens the Upgrades tab, and captures `armory-live.png` using Unity's
`ScreenCapture` — no keystrokes, focus changes, or desktop capture. The real save
and config are never modified.

Full procedure, switches, pitfalls and cleanup:
[`docs/TESTING.md`](docs/TESTING.md).

---

## Known limitations

- **Sidearm slots are one opposite-type sidearm plus one optional ranged
  complement.** Armory does not add a second weapon of the same slot. It does not
  compute exact mass/weight budgets itself; SS's `CanPickupSidearmInstance` is
  checked per candidate. Remembered weapons the pawn no longer carries are not
  used as loose candidates.
- **Duplicate ordinary defs are not added, and existing extras are removed.**
  The planner and the Upgrades tab skip an ordinary sidearm def the pawn already
  carries in the hand or bag, so a gladius is never followed by a second gladius.
  Persona and unique weapons compare by instance. Duplicates, unusable sidearms,
  and weapons beyond the plan are dropped automatically with Simple Sidearms'
  intentional-drop API. One usable fallback is kept until a replacement is ready.
- **Drop-first upgrades have a brief gap.** At SS's slot cap Armory drops the old
  sidearm before queueing the new one, and restores the old weapon if SS still
  rejects the replacement. The dropped weapon is on the ground in the meantime.
  Armory pre-checks SS's mass/selection/pacifist rules first and suppresses
  retries for the same pawn+weapon for about 17 minutes after a refusal.
- **Simple Sidearms can still re-retrieve a dropped weapon after a downing.**
  Armory does not edit SS's remembered list directly. It drops an unwanted
  retrieval on a later pass and restores the planned sidearm. Disabling SS's
  automatic re-equip gives the cleanest result.
- **Container scanning** is limited to loose/spawned weapons plus ordinary
  shelves/stockpiles. Weapons inside transport pods or pawn inventory are not
  offered as hand candidates; pod occupants are still listed as unavailable
  pawns.
- **The Upgrades tab assumes normal quality for new copies.** Each material variant is
  scored as a normal-quality copy made from that stuff, so a high-skill crafter's
  excellent or masterwork output can beat the estimate. Owned copies are counted
  per material; their individual quality only affects each colonist's current
  baseline, so owning one awful copy also hides the need to craft a better copy of
  the same material. The optional Owned section uses the best actual copy's
  quality/material label for comparison.
- **The Upgrades tab is per def + material, not per item.** Every upgrade variant is
  listed, so wants overlap: a colonist can appear in several rows for the same
  slot. `Need` describes what one row alone would require, not a combined
  shopping list.
- **The Upgrades tab does not buy anything or queue bills automatically.** `buy` comes from the def's
  `tradeability`; it does not know whether a suitable trader is currently
  available. The **Craft** button queues one bill only when clicked, and only
  for a row whose missing copies can be made at a built bench. The **Buy**
  section is usually empty in vanilla because most weapon defs have recipes; it
  is for modded or quest weapons with no crafting route. Research-locked defs
  stay under Research even when a trader could also supply them.
- **Weapons carried by colonists in caravans** are not part of owned supply,
  because the snapshot only sees the current map.
- **Combat Extended** ammo state is not scored beyond its stat effects.
- **Other mods' forced weapons** are not read. Simple Sidearms forced states are;
  manual changes are detected heuristically and pause automation.
- **No auto-unforbid**, by design.
- **Dropped weapons** stay on the ground; no auto-haul yet.
- **No raid behaviour** yet (e.g., arm everyone when a raid starts).
- **The in-game decision list is session-only** and records hand/sidearm equips,
  drops, and aborted upgrades, not every skip. The persistent `ArmoryLogs` files
  keep the same decisions plus snapshots across sessions. Pawn automation
  pauses/blocks are shown in the Colonists tab and pawn detail.
- **English strings are hardcoded**; localization not started.
- **Steam Workshop publishing** is live: the first item is uploaded and awaiting
  Steam's automated content check; see [`docs/WORKSHOP.md`](docs/WORKSHOP.md).
- **RimWorld 1.7** (Waylith/.NET 10) not targeted; the structure is ready for a
  `LoadFolders.xml` split later.
- Minor alignment imperfections in the tab remain.

---

## Roadmap

1. **Milestone 3 — Simple Sidearms primary coexistence** — done in 0.3.0.
   - reflection adapter, no hard reference;
   - forced states and primary modes respected;
   - carried-primary baseline and SS job guard prevent most swap loops;
   - SS state and carried sidearms surfaced in pawn detail.
2. **Milestone 4 - Sidearm ranking and assignment** - done in 0.4.0;
   sidearm visibility in 0.5.0; replaced by the typed hand/backup model in 0.6.0.
   - one typed hand slot (Simple Sidearms mode when active, otherwise Armory's
     automatic type) and one opposite-type sidearm;
   - same-type-only upgrades, add-first or drop-first depending on SS slots;
   - mirrored for ranged and melee pawns;
   - SS slot/mass/selection checks on every candidate;
   - hand and sidearm shown separately, never in one cross-type ranking.
3. **Milestone 5 - Polish**
   - Upgrades as a build plan (0.7.0): ideal loadout vs owned, craftable/buyable
     defs, research-locked section, normal-quality estimates;
   - exotic container scanning;
   - auto-haul dropped weapons;
   - raid arming;
   - localization;
   - Workshop prep.
4. **Milestone 6 - Sidearm loadouts** - done in 0.11.0.
   - Hand, opposite-type Sidearm 1 and optional ranged Sidearm 2;
   - range-gap coverage for the second sidearm;
   - carried-set reconciliation, duplicate cleanup and drawn-sidearm handling;
   - multi-line Colonists rows and Sidearm 1/2 detail sections;
   - Sidearm 2 demand in Upgrades.
5. **Milestone 7 - Simple Sidearms hand alignment** - done in 0.12.0.
   - SS default ranged / preferred melee weapon follows the Armory hand;
   - chosen hand remembered through SS's own API;
   - white default dots and equipped-first gizmo order match the Hand slot;
   - remembered sidearms left in hand are restored once the preference is aligned.
6. **Later** - joint sidearm-pair optimization, per-pawn sidearm locks if
   requested, exotic container scanning, auto-haul and raid arming.

---

## Docs

- [`docs/RESEARCH.md`](docs/RESEARCH.md) — Auto Arm / Equipment Manager /
  Simple Sidearms internals, vanilla API notes, integration plan.
- [`docs/HANDOFF.md`](docs/HANDOFF.md) — current state, test evidence, exact
  next steps, open questions.
- [`docs/TESTING.md`](docs/TESTING.md) — quick harness and live-save testing,
  including the isolated copy + temporary scaffold procedure.
- [`docs/WORKSHOP.md`](docs/WORKSHOP.md) - clean release contents, upload
  checklist, private-test steps and Workshop page copy.
- [`docs/SIDEARMS.md`](docs/SIDEARMS.md) - sidearm-loadout design,
  Simple Sidearms boundary, hand-preference alignment and live-test notes.
