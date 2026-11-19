# Armory

**Version 0.8.3** · RimWorld **1.6** · packageId **`kruecke.armory`**

A weapon inspector and opt-in auto-equip manager for RimWorld. It answers two
questions that existing managers leave opaque:

1. **What does each colonist want to carry, and why aren't they carrying it?**
2. **Who should get this new weapon, and what should I build or unforbid?**

Four tabs: **Pawns**, **Weapons**, **Demand**, **Log**.

---

## Quick facts

| Item | Value |
|---|---|
| Mod name | Armory |
| Author | Kruecke |
| packageId | `kruecke.armory` |
| Game version | 1.6.4871 |
| Dependencies | None. Simple Sidearms is optional and read by reflection |
| Build target | `net472` |
| Workspace | `C:\Users\mail\Projects\Rimworld Mods\Armory` |
| Game install | `C:\Program Files (x86)\Steam\steamapps\common\RimWorld` |
| Junction | `...\RimWorld\Mods\Armory` → workspace |
| Current milestone | 0–4 complete; 5 (polish) underway; range/role scoring in 0.8.0, persona guard in 0.8.1, encoding fix in 0.8.2, interface wording pass in 0.8.3 |

---

## What it does

### Pawns
For every free colonist, including pawns in pods/caskets:

- current hand weapon and its score;
- a **hand slot** ranking of the colonist's Simple Sidearms type
  (`ranged` or `melee`), with the best available upgrade and how it compares
  with the current hand weapon;
- a **combat role** (`CQB`, `Assault`, `Line`, `Marksman`) and preferred
  engagement distance, derived from the colonist's actual accuracy curve and genes,
  or fixed with a per-colonist override;
- a **backup slot** ranking of the opposite type, with Simple Sidearms'
  slot/mass/selection blockers and the currently carried backup;
- a full ranked list with a status per candidate and an expandable factor ledger;
- a separate `Cannot equip` list for biocoded/bonded/role-restricted weapons;
- an `Automation blocked:` or `Automation paused:` reason when the planner cannot
  act (manual weapon change, swap cooldown, drafted, pacifist, downed, bonded
  weapon, Simple Sidearms job, and so on).

Ranged and melee weapons are never compared in one ranking. A ranged pawn's
hand slot contains ranged weapons and its backup slot contains melee weapons;
a melee pawn is the mirror. This removes the old "the monosword is the top wish
for every shooter" artifact.

### Weapons
Every loose weapon on the map with:

- power and maximum range;
- best-fit colonist per slot;
- best use (`Hand`, `Backup`, `Hand (blocked)`, `Backup (blocked)`,
  `No colonist can use it`);
- a colonist-by-score detail view with deep checks (reachability, reservations) and
  separate hand/backup readiness.

Ranged power is scored at each pawn's preferred distance using RimWorld's real
accuracy curve. A shotgun and a sniper are not compared as if both could fire at
every range.

This tab lists **loose weapons on the map only**. A weapon that is equipped or
carried by a pawn is visible in that pawn's detail instead. Unbonded persona
weapons are listed, but Armory will not auto-equip them: bonding is permanent,
so the player chooses the pawn and equips the weapon manually.

`Hand (blocked)` means the weapon is the best hand fit for a colonist but cannot
currently be equipped (usually a Simple Sidearms hand-type block). A melee weapon
can be a `Backup` candidate for a ranged colonist even when it cannot be that
colonist's hand weapon; if Simple Sidearms' mass cap rejects it, it is
`Backup (blocked)` with the reason.

### Demand
The upgrade menu: every obtainable weapon that would improve a colonist, minus
what the colony already owns. It answers "what could we make, research, or buy
next?" instead of "who would pick up the leftovers on the floor?"

For every colonist and slot, Armory scores every weapon def the colony could
obtain: craftable now, craftable after pending research, buyable from a trader,
or already owned. **Every** def that beats the current weapon by the upgrade
threshold is listed. Rows are independent options, so one colonist can appear in
several of them. A gold star before the name marks the def as the best option in
its section for at least one colonist; all weapon names are white.

Research options must also beat the best currently obtainable option by the
threshold, so research never advertises a weapon that is worse than something
already buildable. Owned copies (equipped, carried, or loose) are subtracted:

```
Needed = max(0, Wanted by - owned copies that are not forbidden)
```

Columns:

- `Power`: normal-quality base strength before range and pawn fit;
- `Range`: maximum firing range in tiles (`-` for melee);
- `Score`: range-aware expected value for the colony, summed over the colonists
  who would take it; this is the main sort key;
- `Slot`: `hand`, `backup`, or how many of each;
- `Wanted by`: colonists who would improve with this def (may overlap other rows);
- `Owned`: copies in the colony (`2 · 1 forbidden` when some are forbidden);
- `Needed`: copies still missing; `enough` (green) when the def is the best option
  for someone but already covered;
- `Gain`: average improvement over the current weapon, assuming a newly made
  normal-quality copy; `new slot` when the colonist has nothing in that slot;
- `Source`: `craft · bench`, the research project, or `buy`. For craft rows the
  bench is the best **built** bench that can make it; if none is built, the best
  **unlocked** bench is shown instead. A craft row with missing copies and a
  built bench also gets a `Craft` button that queues one bill at that bench. It
  does not equip the weapon.

Sections follow the order of action: **Craft** (recipe unlocked now),
**Research** (a project unlocks it; `· buy` when a trader can also supply it),
and **Buy** (no crafting route, trader only). Within a section, rows are ordered
by `Score`, then total gain, best count and needed copies. This is not pure weapon
power: a weapon that suits more colonists can outrank a slightly stronger one
that helps only one. `Power` is the weapon alone; `Score` adds range, pawn fit,
role preference and a light squad-variety penalty.

Rows that are the best option for someone stay visible even when the requirement
is already covered; those show `enough` in green. A gold star before the name
marks the best option in its section for at least one colonist; all names are
white, so the star is the only distinction. Without that, an owned ideal (for
example the hatchet a colonist already carries) would disappear. Click a row to
see one detail line per contributing colonist, with their name, current weapon,
combat role, individual `Score`, `Slot`, and `Gain` aligned under the same
columns. Defs with no gap are otherwise hidden.

### Range and combat roles

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

The pawn detail has a `Change` button for a per-pawn override. Overrides are
saved with the colony (not the mod settings) and survive save/load. `Auto`
returns to the derived role.

A small deterministic personal preference and a sequential duplicate penalty
keep equal pawns from becoming clones. Both are controlled by **Squad variety**
in settings; set it to 0% to disable them. The duplicate penalty applies to
Demand rows and to the optional hand planner. LMGs and miniguns are treated as
support weapons and receive a 0.75 support penalty so their raw burst DPS does
not make every pawn want one.

`Owned` includes forbidden copies; `Needed` only counts usable ones, and its
tooltip shows how many unforbidding would cover. A research-locked def you
already own shows `more: <project>` when only extra copies need the research.
A pawn is never offered a second copy of a weapon def they already carry as a
backup; hand swaps of the same def are still allowed for quality upgrades.

Unlike the planner, Demand ignores transient state: a drafted, asleep, or
full-bagged colonist still counts, because the plan is about what the colony
should own, not what a job can do this second. Only permanent gates (age,
violence incapability, manipulation) remove a colonist. Manual-use ranged
weapons (such as a Molotov) are not offered as backups; biocoding and bonds do
not apply to hypothetical new copies.

### Log
Session-only decision log for the planner: time, colonist, action, weapon, and
the reason the action was taken. Actions are `Equip hand`, `Equip backup`,
`Drop backup`, `Skip upgrade`, `Drop failed`, and `Restore backup`. Newest first,
clearable.

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

`score = weapon power × pawn fit`

- **Power** from vanilla StatDefs and VerbProperties: melee DPS/AP; ranged
  DPS × burst/cycle, sustained fire, blended accuracy, armour penetration.
  Modded and Combat Extended weapons use their own numbers.
- Explosive/incendiary/EMP/smoke weapons get a **0.35× situational penalty**.
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
excluded from demand, best-fit, and automation.

### Ages

Vanilla has **no weapon-specific age gate**. `EquipmentUtility.CanEquip`
applies developmental-stage filtering only to apparel; `Violent` work tags come
from backstory/traits/genes/kind, not age. Children can equip weapons.

Default minimum age is **3**, slider range **0–18**. A stored value of `13`
from settings created before 0.2.2 is migrated to `3` once.

### Automation

Off by default. When enabled:

- only weapons that can be equipped now are used; **Armory never unforbids**;
- largest upgrades first, then next-best fallback if the top choice is claimed
  or unreachable;
- one claim per weapon instance (900 ticks), per-pawn swap cooldown;
- minimum upgrade gain gate;
- equip jobs are queued with `requestQueueing: true`, so work is not
  interrupted;
- manual weapon changes pause that pawn for a configurable period;
- skips pacifists, prisoners, slaves, downed/mental pawns, under-age pawns, and
  drafted pawns by default;
- for a bonded persona weapon, locks the hand only while the bonded weapon is
  equipped; if it is in the bag or on the ground, the hand can still be managed,
  with a warning that another weapon may trigger the persona mood penalty;
- if Auto Arm or Equipment Manager is loaded, automation stays paused unless
  `overrideCompetitor` is enabled.

Every hand and backup action, with its reason, goes to the **Log** tab.

### Simple Sidearms coexistence

Simple Sidearms is detected by packageId and read **entirely through
reflection**, so Armory still loads in a Core-only game and does not break when
Simple Sidearms changes.

SS keeps exactly one weapon in `equipment.Primary` — the **hand** — and extra
weapons in the pawn's inventory — the **bag**. Its per-pawn primary mode
(`Ranged`, `Melee`, `BySkill`) says which type SS keeps in hand; it is not a
separate inventory category, and **sidearm limits apply only to the bag**. That
is why a 2 kg Monosword can be a melee hand weapon but not a melee sidearm under
SS's Basic preset.

Armory therefore models a pawn as two typed slots:

- **Hand slot** = the colonist's Simple Sidearms hand type. Armory may upgrade it
  with a better weapon of the same type via the vanilla `Equip` job, which also
  updates Simple Sidearms' remembered preference.
- **Backup slot** = the opposite type. Armory keeps one legal opposite-type
  weapon in the bag via `EquipSecondary`. Ranged pawns get a melee backup; melee
  pawns get a ranged backup.

Ranged and melee are never ranked against each other. The hand baseline is SS's
own QuickSwitch choice among carried weapons (`findBestRangedWeapon` /
`findBestMeleeWeapon`), so a weapon SS would skip — manual use, dangerous when
SS skips dangerous weapons, or EMP — does not block a valid upgrade. If the
reflection call fails, Armory logs once and uses the current weapon.

When **Respect Simple Sidearms** is on (default):

- a forced weapon, forced unarmed, or prefer-unarmed state is an automation veto;
- Armory waits until SS has switched the pawn to its role type before adding a
  backup, because adding the other type while the wrong weapon is in hand invites
  SS to promote it and looks like a manual change;
- explicit hand blockers remain visible as `Requires a ranged hand` or
  `Requires a melee hand`;
- pawns currently running `EquipSecondary` / `ReequipSecondary` /
  `ReequipSecondaryCombat` are skipped.

Backup management is off by default; when enabled it:

- adds one opposite-type weapon via Simple Sidearms' `EquipSecondary` job;
- validates every candidate with `CanPickupSidearmInstance`, so SS slot, mass,
  selection, pacifist-tool, biocoding, and role rules all apply;
- upgrades only the worst carried weapon of the **same** type; it never drops the
  last weapon of a type and never drops one type to add the other;
- if SS has a free slot, picks up the better weapon first and drops the old one
  after; if slots are full, drops the old one first and restores it if SS still
  rejects the new one;
- uses the same claims, per-pawn cooldown, and manual-change protection as hand
  upgrades;
- never acts on a pawn with a forced/unarmed/prefer-unarmed SS state.

Armory does not add a second backup. Extra same-type weapons the player or SS
already carries are left alone. If the bag is full of extras, a missing backup
may have no free slot; Armory reports the blocker rather than dropping a
hand-type extra.

Simple Sidearms reports two different capacity failures with terse text. Armory
expands them so they cannot be confused:

- `All sidearm slots are full` — the SS slot **count** limit is reached;
- `Can't carry (X kg, Y kg free)` - the weapon does not fit in the pawn's
  remaining inventory **weight**, even when a slot is free.

SS's mass limits still matter. The **Basic** preset caps a melee sidearm at
1.9 kg and a ranged sidearm at 2.7 kg, with 3 total slots **including the
equipped weapon**. A 2 kg Monosword or Zeushammer is therefore **not a valid
melee backup** (it is still valid as a melee hand weapon), while a 0.85 kg
gladius is. The Weapons tab shows `Backup (blocked)` with Simple Sidearms'
reason, and the colonist detail lists the candidate under the backup slot with
that blocker.

Turning **Respect Simple Sidearms** off reverts to a single untyped hand ranking
and disables backup management; the two mods may then fight over primaries.

SS's primary mode is **per pawn**. Changing SS's global default only affects
newly generated pawns; existing colonists keep their own mode. Use the Simple
Sidearms gizmo on a colonist to switch them to Melee or By skill. A melee-mode
pawn gets a ranged backup, subject to SS's 2.7 kg ranged-sidearm cap.

---

## Settings reference

All settings are under **Options → Mod settings → Armory**. The page scrolls.

| Setting | Default | Range | Meaning |
|---|---:|---:|---|
| Enable automatic equipping | off | — | Master switch for the planner |
| Run anyway while another manager is loaded | off | — | Bypasses the competitor guard |
| Upgrade check interval | 250 ticks | 120–600 | How often Armory checks for upgrades |
| Wait after a weapon swap | 2500 ticks (~42 s) | 600–10000 | Wait after a weapon swap, per colonist |
| Minimum colonist age | 3 | 0–18 | Vanilla has no weapon age gate |
| Equip drafted colonists | off | — | Queues jobs for drafted colonists |
| Respect manual weapon changes | on | — | Pause after a player/other-mod change |
| Pause after a manual weapon change | 15000 ticks (~250 s) | 2500–30000 | Length of that pause |
| Respect Simple Sidearms | on | — | Typed hand/backup slots, Simple Sidearms mode, forced states, and hand baseline |
| Manage backup weapons | off | — | Keep one opposite-type weapon in the bag via `EquipSecondary` |
| Upgrade a worse backup | on | — | Replace the worst same-type backup when a better valid one exists; never cross-type |
| Minimum upgrade gain | 10% | 0–50% | Required improvement to recommend or equip a weapon |
| Ranged score weight | ×1.00 | 0.5–2.0 | Global scoring bias |
| Melee score weight | ×1.00 | 0.5–2.0 | Global scoring bias |
| Combat distance bias | 0 tiles | -5–+5 | Shift all preferred ranges closer (negative) or longer (positive) |
| Squad variety | 50% | 0–100% | Per-colonist weapon affinity and duplicate top-choice penalty; 0% disables both |
| Hand candidates shown per colonist | 10 | 5–100 | Hand list limit |
| Backup candidates shown per colonist | 5 | 1–50 | Backup list limit |
| Show blocked candidates by default | on | — | Include candidates that cannot be equipped now |
| Snapshot refresh interval | 120 ticks | 30–600 | How often the lists recalculate while open |
| Write a snapshot summary to the log when a map loads | off | — | One-time diagnostic in `Player.log` |

Internal: `settingsVersion` (currently 4) drives the age, typed-slot, list-limit
and range/variety migrations.

---

## Layout

```
Armory/
  About/About.xml
  About/Preview.png
  Defs/MainButtonDefs.xml          MainButtonDef "Armory", order 35
  Textures/UI/Buttons/MainButtons/IconArmory.png  main-tab icon
  Textures/UI/Icons/ArmoryBestStar.png  Demand best-option star
  Assemblies/Armory.dll            build output
  Source/Armory/
    Armory.csproj
    ArmoryMod.cs                   Mod entry, settings, competitor detection
    ArmorySettings.cs              settings + scrollable options UI
    ArmoryMapComponent.cs          snapshot cache + planner tick
    ArmorySnapshotBuilder.cs       pawn/weapon/demand analysis, pod lookup
    ArmoryPlanner.cs               opt-in equip jobs, claims, cooldowns
    ArmoryGameComponent.cs         session decision log (GameComponent)
    DebugActions_Armory.cs         log snapshot / unforbid / run planner
    WeaponFilter.cs                single definition of a real weapon
    Compatibility/SimpleSidearmsAdapter.cs  reflection-only SS state and rules
    Model/ArmoryModels.cs          snapshot/candidate/pawn/weapon models
    Scoring/WeaponPower.cs         pawn-independent power + situational penalty
    Scoring/PawnFit.cs             pawn fit + optional ledger
    Scoring/CombatScoring.cs       range profile, preferred distance, roles, variety
    Scoring/WeaponCatalog.cs       craftable/buyable defs + synthetic scoring probes
    Scoring/Eligibility.cs         soft/hard blockers
    UI/MainTabWindow_Armory.cs     Pawns/Weapons/Demand/Log tabs
    UI/TextUtil.cs                 rich-text safe measurement/truncation
  docs/RESEARCH.md                 decompiled-mod + API findings
  docs/RANGE_AND_ROLES.md          range/role research, design and decisions
  docs/HANDOFF.md                  exact state and next-step plan
  docs/TESTING.md                  quick + live-save testing procedure
  tools/Quicktest-Armory.ps1       isolated quicktest harness
  tools/Setup-LiveTest.ps1         copies a real save into an isolated folder
  tools/LiveTestScaffold.cs.txt    temporary auto-load/screenshot scaffold
  tools/Generate-ArmoryIcon.ps1    regenerates the tab icon
```

---

## Diagnostics

- Enable **Log snapshot summary on map load** to get a one-time summary in
  `Player.log`, including Simple Sidearms mode/sidearm state per colonist.
- Dev mode actions:
  - **Armory → Log weapon snapshot**
  - **Armory → Unforbid loose weapons**
  - **Armory → Run planner now**
- Log:
  `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`

### Isolated test harness

`tools/Quicktest-Armory.ps1` launches RimWorld with:

```
-quicktest -savedatafolder=C:\Users\mail\AppData\Local\Temp\opencode\armorytest
```

If the target folder has no `Config/ModsConfig.xml`, the script creates one with
Core + Armory. Pass `-IncludeSimpleSidearms` to create a config with Harmony and
Simple Sidearms. Your real mod list and settings are never touched. The script
waits for an `[Armory]` log line, prints Armory lines, and closes the game.

To enable automation and diagnostics in the isolated game, write
`Config/Mod_Armory_ArmoryMod.xml` in that folder:

```xml
<?xml version="1.0" encoding="utf-8"?>
<SettingsBlock>
  <ModSettings Class="Armory.ArmorySettings">
    <autoEquipEnabled>True</autoEquipEnabled>
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
opens the Demand tab, and captures `armory-live.png` using Unity's
`ScreenCapture` — no keystrokes, focus changes, or desktop capture. The real save
and config are never modified.

Full procedure, switches, pitfalls and cleanup:
[`docs/TESTING.md`](docs/TESTING.md).

---

## Known limitations

- **Backups are one per pawn and same-type upgrades only.** Armory does not add a
  second backup and does not drop a hand-type extra to make room for a missing
  backup, so a bag already full of extras can block the first backup. It does
  not compute exact mass/weight budgets itself; SS's `CanPickupSidearmInstance`
  is checked per candidate. Remembered weapons the pawn no longer carries are not
  used as candidates.
- **No duplicate backup types are added.** The planner and Demand skip a backup
  def the pawn already carries in the hand or bag, so a gladius is never followed
  by a second gladius. Existing duplicates are not removed automatically: the
  planner never drops a weapon without a replacement, so drop one manually, or it
  may be replaced if a better different def is available.
- **Drop-first upgrades have a brief gap.** At SS's slot cap Armory drops the old
  backup before queueing the new one, and restores the old weapon if SS still
  rejects the replacement. The dropped weapon is on the ground in the meantime.
  Armory pre-checks SS's mass/selection/pacifist rules first and suppresses
  retries for the same pawn+weapon for about 17 minutes after a refusal.
- **Container scanning** is limited to loose/spawned weapons plus ordinary
  shelves/stockpiles. Weapons inside transport pods or pawn inventory are not
  offered as hand candidates; pod occupants are still listed as unavailable
  pawns.
- **Demand assumes normal quality and default stuff.** A hypothetical weapon is
  scored as a normal-quality copy made from `GenStuff.DefaultStuffFor` (usually
  the cheapest valid stuff), so a high-skill crafter's excellent or masterwork
  output can beat the estimate, and a colony already holding masterwork gear can
  show less demand than it really has. Owned copies are counted per def; their
  individual quality only affects each colonist's current baseline, so owning one
  awful copy also hides the need to craft a better copy of the same def.
- **Demand is per def, not per item.** Every upgrade is listed, so wants overlap:
  a colonist can appear in several rows for the same slot. `Need` describes what
  one row alone would require, not a combined shopping list.
- **Demand does not create bills or buy anything.** `buy` comes from the def's
  `tradeability`; it does not know whether a suitable trader is currently
  available, and nothing is queued automatically. The **Buy** section is usually
  empty in vanilla because most weapon defs have recipes; it is for modded or
  quest weapons with no crafting route. Research-locked defs stay under
  Research even when a trader could also supply them.
- **Weapons carried by colonists in caravans** are not part of owned supply,
  because the snapshot only sees the current map.
- **Combat Extended** ammo state is not scored beyond its stat effects.
- **Other mods' forced weapons** are not read. Simple Sidearms forced states are;
  manual changes are detected heuristically and pause automation.
- **No auto-unforbid**, by design.
- **Dropped weapons** stay on the ground; no auto-haul yet.
- **No raid behaviour** yet (e.g., arm everyone when a raid starts).
- **Decision log is session-only** and records hand/backup equips, drops, and
  aborted upgrades, not every skip.
  Pawn automation pauses/blocks are shown in the Pawns tab and pawn detail.
- **English strings are hardcoded**; localization not started.
- **Workshop packaging** not started.
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
2. **Milestone 4 — Sidearm ranking and assignment** — done in 0.4.0;
   sidearm visibility in 0.5.0; replaced by the typed hand/backup model in 0.6.0.
   - one hand slot (Simple Sidearms hand type) and one opposite-type backup;
   - same-type-only upgrades, add-first or drop-first depending on SS slots;
   - mirrored for ranged and melee pawns;
   - SS slot/mass/selection checks on every candidate;
   - hand and backup shown separately, never in one cross-type ranking.
3. **Milestone 5 — Polish**
   - Demand as a build plan (0.7.0): ideal loadout vs owned, craftable/buyable
     defs, research-locked section, normal-quality estimates;
   - exotic container scanning;
   - auto-haul dropped weapons;
   - raid arming;
   - localization;
   - Workshop prep.
4. **Later** — deliberate variation (short/long mix, multiple backups) instead of
   one best-in-type per slot.

---

## Docs

- [`docs/RESEARCH.md`](docs/RESEARCH.md) — Auto Arm / Equipment Manager /
  Simple Sidearms internals, vanilla API notes, integration plan.
- [`docs/HANDOFF.md`](docs/HANDOFF.md) — current state, test evidence, exact
  next steps, open questions.
- [`docs/TESTING.md`](docs/TESTING.md) — quick harness and live-save testing,
  including the isolated copy + temporary scaffold procedure.
