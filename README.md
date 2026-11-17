# Armory

**Version 0.2.3** · RimWorld **1.6** · packageId **`kruecke.armory`**

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
| Dependencies | None (Harmony not used yet) |
| Build target | `net472` |
| Workspace | `C:\Users\mail\Projects\Rimworld Mods\Armory` |
| Game install | `C:\Program Files (x86)\Steam\steamapps\common\RimWorld` |
| Junction | `...\RimWorld\Mods\Armory` → workspace |
| Current milestone | 0–2 complete; 3 (Simple Sidearms) next |

---

## What it does

### Pawns
For every free colonist, including pawns in pods/caskets:

- current weapon and its score;
- best usable weapon and the delta over current;
- best overall weapon even when blocked (`Forbidden`, `Outside area`, etc.);
- a full ranked list with a status per weapon and an expandable factor ledger;
- a separate `Incompatible` list for biocoded/bonded/role-restricted weapons.

### Weapons
Every loose weapon on the map with:

- power;
- best-fit colonist;
- status (`Wanted`, `Only blocked`, `No user`);
- a colonist-by-fit detail view with deep checks (reachability, reservations).

### Demand
Per weapon def: `Wanted`, `Blocked`, `Stock`, `Forbidden`, `Shortage`.
This distinguishes "build more" from "unforbid what you already own".

### Log
Session-only decision log for the planner: time, colonist, weapon, action,
detail. Newest first, clearable.

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
- skips pacifists, prisoners, slaves, downed/mental pawns, under-age pawns,
  pawns with a bonded weapon, and drafted pawns by default;
- if Auto Arm or Equipment Manager is loaded, automation stays paused unless
  `overrideCompetitor` is enabled.

Every queued job goes to the **Log** tab.

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
| Upgrade threshold | 10% | 0–50% | Required improvement to swap |
| Ranged preference | ×1.00 | 0.5–2.0 | Global scoring bias |
| Melee preference | ×1.00 | 0.5–2.0 | Global scoring bias |
| Ranked weapons shown per pawn | 25 | 5–100 | UI list limit |
| Show blocked weapons by default | on | — | Include non-ready rows |
| Rescan interval | 120 ticks | 30–600 | Snapshot rebuild interval |
| Log snapshot summary on map load | off | — | Writes a one-time diagnostic to `Player.log` |

Internal: `settingsVersion` (currently 1) drives the age migration.

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
  `Player.log`.
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
Simple Sidearms for milestone-3 testing. Your real mod list and settings are
never touched. The script waits for an `[Armory]` log line, prints Armory lines,
and closes the game.

Manual equivalent:

```powershell
& "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64.exe" `
  -quicktest -savedatafolder="C:\Users\mail\AppData\Local\Temp\opencode\armorytest"
```

---

## Known limitations

- **Simple Sidearms is not integrated yet.** Sidearm-capable weapons are ranked
  as primaries.
- **Container scanning** is limited to loose/spawned weapons plus ordinary
  shelves/stockpiles. Weapons inside transport pods or pawn inventory are not
  offered as candidates; pod occupants are still listed as unavailable pawns.
- **Combat Extended** ammo state is not scored beyond its stat effects.
- **Forced weapons** are not read from other mods. Manual changes are detected
  heuristically and pause automation.
- **No auto-unforbid**, by design.
- **Dropped weapons** stay on the ground; no auto-haul yet.
- **No raid behaviour** yet (e.g., arm everyone when a raid starts).
- **Decision log is session-only** and records queued jobs, not every skip.
- **Automation-skip reasons** (pacifist, drafted, bonded) are not shown in the
  pawn detail yet; only weapon blockers and off-map availability are.
- **English strings are hardcoded**; localization not started.
- **Workshop packaging** not started.
- **RimWorld 1.7** (Waylith/.NET 10) not targeted; the structure is ready for a
  `LoadFolders.xml` split later.
- Minor alignment imperfections in the tab remain.

---

## Roadmap

1. **Milestone 3 — Simple Sidearms primary coexistence**
   - reflection adapter, no hard reference;
   - respect SS slot/type rules so primary swaps do not loop;
   - surface SS blockers in statuses.
2. **Milestone 4 — Sidearm ranking and assignment**
   - rank sidearms separately from primaries;
   - show current/free sidearm slots;
   - `EquipSecondary` job support.
3. **Milestone 5 — Polish**
   - exotic container scanning;
   - auto-haul dropped weapons;
   - raid arming;
   - localization;
   - Workshop prep.
4. **Later** — deliberate variation (short/long mix, roles) instead of one top
   pick per pawn.

---

## Docs

- [`docs/RESEARCH.md`](docs/RESEARCH.md) — Auto Arm / Equipment Manager /
  Simple Sidearms internals, vanilla API notes, integration plan.
- [`docs/HANDOFF.md`](docs/HANDOFF.md) — current state, test evidence, exact
  next steps, open questions.
