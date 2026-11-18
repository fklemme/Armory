# Armory

**Version 0.6.7** · RimWorld **1.6** · packageId **`kruecke.armory`**

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
| Current milestone | 0–4 complete; 5 (polish) next |

---

## What it does

### Pawns
For every free colonist, including pawns in pods/caskets:

- current weapon and its score;
- a **hand slot** ranking of the pawn's Simple Sidearms type
  (`ranged` or `melee`), with the best usable upgrade and the delta over the
  hand baseline;
- a **backup slot** ranking of the opposite type, with SS's slot/mass/selection
  blockers and current carried backup;
- a full ranked list with a status per weapon and an expandable factor ledger;
- a separate `Incompatible` list for biocoded/bonded/role-restricted weapons;
- an `Automation:` reason when the planner is paused or blocked (manual weapon
  change, swap cooldown, drafted, pacifist, downed, bonded weapon, Simple
  Sidearms job, and so on).

Ranged and melee weapons are never compared in one ranking. A ranged pawn's
hand slot contains ranged weapons and its backup slot contains melee weapons;
a melee pawn is the mirror. This removes the old "the monosword is the top wish
for every shooter" artifact.

### Weapons
Every loose weapon on the map with:

- power;
- best-fit colonist per slot;
- status (`Hand`, `Backup`, `Hand blocked`, `Backup blocked`, `No user`);
- a colonist-by-fit detail view with deep checks (reachability, reservations) and
  separate hand/backup readiness.

`Hand blocked` means blocked as the pawn's **hand** weapon (usually an SS role
block). A melee weapon can be a `Backup` candidate for a ranged pawn even when it
cannot be that pawn's primary; if SS's mass cap rejects it, it is
`Backup blocked` with SS's reason.

### Demand
Per weapon def: `For` (`hand`, `backup`, or `both`), `Wanted`, `Blocked`,
`Stock`, `Forbidden`, `Shortage`.
This distinguishes "build more" from "unforbid what you already own", and
separates hand needs from opposite-type backup needs.

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

- `Ready` — can be picked up now;
- `Forbidden`, `Outside area`, `Unreachable`, `Reserved` — soft blockers the
  player can fix;
- biocoded/bonded/role restrictions — hard blockers, shown under `Incompatible`.

The header shows both lines when they differ:

```
Best usable:  bolt-action rifle    (+12%)
Best overall: plasteel knife       (+40%)  - Forbidden
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

- only `ready` weapons are used; **Armory never unforbids**;
- largest upgrades first, then next-best fallback if the top choice is claimed
  or unreachable;
- one claim per weapon instance (900 ticks), per-pawn swap cooldown;
- upgrade threshold gate;
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

- **Hand slot** = the SS mode type. Armory may upgrade it with a better weapon of
  the same type via the vanilla `Equip` job, which also updates SS's remembered
  preference.
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
- explicit hand blockers remain visible as `Melee blocked: SS ranged` or
  `Ranged blocked: SS melee`;
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
gladius is. The Weapons tab shows `Backup blocked` with SS's reason, and the pawn
detail lists the candidate under the backup slot with that blocker.

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
| Planner interval | 250 ticks | 120–600 | How often the planner runs |
| Swap cooldown | 2500 ticks (~42 s) | 600–10000 | Minimum time between swaps per pawn |
| Minimum colonist age | 3 | 0–18 | Vanilla has no weapon age gate |
| Equip drafted colonists | off | — | Queues jobs for drafted pawns |
| Respect manual weapon changes | on | — | Pause after a player/other-mod change |
| Manual-change pause | 15000 ticks (~250 s) | 2500–30000 | Length of that pause |
| Respect Simple Sidearms | on | — | Typed hand/backup slots, SS mode, forced states, and SS hand baseline |
| Manage backup weapons | off | — | Keep one opposite-type weapon in the bag via `EquipSecondary` |
| Upgrade a worse backup | on | — | Replace the worst same-type backup when a better valid one exists; never cross-type |
| Upgrade threshold | 10% | 0–50% | Required improvement to swap |
| Ranged preference | ×1.00 | 0.5–2.0 | Global scoring bias |
| Melee preference | ×1.00 | 0.5–2.0 | Global scoring bias |
| Ranked weapons shown per pawn | 25 | 5–100 | UI list limit |
| Show blocked weapons by default | on | — | Include non-ready rows |
| Rescan interval | 120 ticks | 30–600 | Snapshot rebuild interval |
| Log snapshot summary on map load | off | — | Writes a one-time diagnostic to `Player.log` |

Internal: `settingsVersion` (currently 2) drives the age and typed-slot
migrations.

---

## Layout

```
Armory/
  About/About.xml
  About/Preview.png
  Defs/MainButtonDefs.xml          MainButtonDef "Armory", order 65
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
    Scoring/Eligibility.cs         soft/hard blockers
    UI/MainTabWindow_Armory.cs     Pawns/Weapons/Demand/Log tabs
    UI/TextUtil.cs                 rich-text safe measurement/truncation
  docs/RESEARCH.md                 decompiled-mod + API findings
  docs/HANDOFF.md                  exact state and next-step plan
  tools/Quicktest-Armory.ps1       isolated quicktest harness
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

---

## Known limitations

- **Backups are one per pawn and same-type upgrades only.** Armory does not add a
  second backup and does not drop a hand-type extra to make room for a missing
  backup, so a bag already full of extras can block the first backup. It does
  not compute exact mass/weight budgets itself; SS's `CanPickupSidearmInstance`
  is checked per candidate. Remembered weapons the pawn no longer carries are not
  used as candidates.
- **Drop-first upgrades have a brief gap.** At SS's slot cap Armory drops the old
  backup before queueing the new one, and restores the old weapon if SS still
  rejects the replacement. The dropped weapon is on the ground in the meantime.
  Armory pre-checks SS's mass/selection/pacifist rules first and suppresses
  retries for the same pawn+weapon for about 17 minutes after a refusal.
- **Container scanning** is limited to loose/spawned weapons plus ordinary
  shelves/stockpiles. Weapons inside transport pods or pawn inventory are not
  offered as hand candidates; pod occupants are still listed as unavailable
  pawns.
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
   - one hand slot (SS mode type) and one opposite-type backup;
   - same-type-only upgrades, add-first or drop-first depending on SS slots;
   - mirrored for ranged and melee pawns;
   - SS slot/mass/selection checks on every candidate;
   - hand and backup shown separately, never in one cross-type ranking.
3. **Milestone 5 — Polish**
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
