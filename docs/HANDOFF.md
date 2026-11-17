# Handoff / project state

**Snapshot:** Armory **0.3.0**, RimWorld **1.6.4871**.
Build is clean (0 warnings, 0 errors). No test hooks remain in source.
Git repository initialized on `main`; 0.2.3 is the first commit.

---

## Where we are

Milestones 0–3 are implemented:

- **0** — mod skeleton, `MainButtonDef`, tab, settings, build/junction.
- **1** — ranking, blockers, Pawns/Weapons/Demand tabs, factor ledger.
- **2** — opt-in planner (claims, cooldown, threshold, manual protection),
  session decision log, Log tab.
- **3** — Simple Sidearms primary coexistence via a reflection adapter:
  forced states, primary mode, carried-primary baseline, SS job guard,
  sidearm display.

**Next:** milestone 4, sidearm ranking/assignment (`EquipSecondary`).

The user was last testing 0.2.2 and reported:

- wider tab looks much better;
- pod pawns still show `Not on the map`;
- header alignment off by the scrollbar width;
- Weapons tab `Power`/`Best colonist` too close;
- (screenshot revealed) beer stacks counted as weapons.

0.2.3 fixed all five (map-wide pod lookup, scrollbar-aware headers, weapon
column gaps, drug/ingestible filter), but the user has not confirmed them on the
live save yet. 0.3.0 adds Simple Sidearms coexistence on top; both should be
confirmed in one pass.

---

## Verified behavior

### Isolated quicktest (Core + Armory)

Test folder:
`C:\Users\mail\AppData\Local\Temp\opencode\armorytest`
with its own `Config/ModsConfig.xml` (`ludeon.rimworld`, `kruecke.armory`).

Results:

- 3 colonists, 3 loose weapons.
- Planner queued all 3 (knife, rifle, revolver).
- Late snapshot: 0 loose weapons, all 3 equipped.
- `decisions logged: 3` from `ArmoryGameComponent`.
- No GameComponent, GUI, null-reference, or exception entries.
- A separate run with a pacifist: 2 queued, pacifist correctly skipped.
- A run with a Doomsday rocket launcher: fallback logic exercised; situational
  penalty was added afterwards (compiled, not runtime-verified).
- Settings dialog and Log tab rendered during tests without errors.

### Isolated quicktest (Core + Harmony + Simple Sidearms + Armory)

Test folder:
`C:\Users\mail\AppData\Local\Temp\opencode\armorytest-ss`
with `ludeon.rimworld`, `brrainz.harmony`, `PeteTimesSix.SimpleSidearms`,
`kruecke.armory`.

Results (0.3.0):

- `[Armory] Simple Sidearms integration active (reflection; forced states,
  preferences, and primary mode respected).`
- Every pawn read a mode: `primary by skill (ranged)` or `by skill (melee)`,
  matching Simple Sidearms' skill rule.
- Full planner run (temporary test-only eligibility bypass, reverted and
  rebuilt): 3 equip jobs queued, no exceptions from SS patches or Armory.
- Core-only run: `Simple Sidearms integration: inactive`, ranking unchanged.
- No `Exception`, GUI error, or Simple Sidearms error lines.
- Forced-weapon / forced-unarmed states were not exercised at runtime; they are
  read from the same properties and need a live-save check.

### User save

- 12 colonists, 40 weapons.
- Auto-equip works.
- Pacifist child/pawn skipped as intended.
- Auto Arm is installed; banner/guard behavior works.
- Pod pawns listed but still `Not on the map` in 0.2.2.
- Simple Sidearms is installed; 0.3.0 has not been run on this save yet.

---

## Milestone 3 (0.3.0) — what shipped

New files:

- `Source/Armory/Compatibility/SimpleSidearmsAdapter.cs`
  - reflection-only; resolves `CompSidearmMemory`, forced properties, the
    `primaryWeaponMode` field, `ThingDefStuffDefPair.thing/stuff`, the skill
    preference helper, and `StatCalculator.CanPickupSidearmInstance`;
  - degrades to inactive when SS is absent, types change, or reflection fails;
  - caches nothing across ticks (12 pawns is cheap), disables itself on error.
- `Source/Armory/WeaponFilter.cs` — one shared definition of a real weapon.

Behaviour changes:

1. **Forced states** (`ForcedUnarmed`, `ForcedWeapon`, drafted variants) follow
   SS's precedence: drafted unarmed → drafted forced weapon → normal forced
   unarmed → normal forced weapon. Armory marks the pawn and skips automation.
2. **Per-pawn preferences**: a carried `DefaultRangedWeapon` or
   `PreferredMeleeWeapon` for the active role becomes the only allowed primary
   pair; `PreferredUnarmed` is an automation veto. These are the settings SS's
   own QuickSwitch enforces.
3. **Primary mode** is read per pawn. `Ranged`/`Melee` and `BySkill` (resolved
   with SS's own `getSkillWeaponPreference`) produce a preferred type. Armory
   blocks the other type only when the pawn already carries a weapon of the
   preferred type, because that is when SS would switch back.
4. **Carried-primary baseline**: for SS pawns, the best carried weapon matching
   the preferred type is scored with Armory's normal model and used as the
   upgrade baseline. Loose weapons must beat it by the threshold.
5. **SS job guard**: pawns running `EquipSecondary`, `ReequipSecondary`, or
   `ReequipSecondaryCombat` are skipped.
6. **UI**: pawn detail lists inventory sidearms, SS primary mode, forced state
   (`automation stays out`), preferences, and the best carried SS primary. The
   Pawns row status shows the SS veto reason.
7. **Settings**: `respectSimpleSidearms` (default on), with a Simple Sidearms
   section that explains the trade-off.
8. **Diagnostics**: map-load snapshot logs SS integration state and per-pawn SS
   mode/sidearms.

Not done (milestone 4): choosing/equipping sidearms, SS slot/weight limits in
the UI, `EquipSecondary` jobs.

---

## Build / run

```
dotnet build "C:\Users\mail\Projects\Rimworld Mods\Armory\Source\Armory\Armory.csproj"
```

- Output: `Armory\Assemblies\Armory.dll`
- Target: `net472`
- Refs: `Krafs.Rimworld.Ref` 1.6.4871, `Microsoft.NETFramework.ReferenceAssemblies` 1.0.3
- No Harmony reference yet.

**Close RimWorld before building** — the loaded DLL is locked.

Junction:

```
cmd /c mklink /J "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Armory" "C:\Users\mail\Projects\Rimworld Mods\Armory"
```

Quicktest:

```
powershell -ExecutionPolicy Bypass -File "C:\Users\mail\Projects\Rimworld Mods\Armory\tools\Quicktest-Armory.ps1"
```

For milestone-3 testing, the script can create an isolated config with Harmony
and Simple Sidearms:

```
powershell -ExecutionPolicy Bypass -File "C:\Users\mail\Projects\Rimworld Mods\Armory\tools\Quicktest-Armory.ps1" -Root "$env:TEMP\armorytest-ss" -IncludeSimpleSidearms
```

Log:
`%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`

Debug actions (dev mode):

- `Armory → Log weapon snapshot`
- `Armory → Unforbid loose weapons`
- `Armory → Run planner now`

---

## Critical implementation notes

These are easy to break; keep them in mind.

1. **`GameComponent` constructor** must be `public ArmoryGameComponent(Game game)`
   and must **not** call `: base(game)`; the base has an implicit parameterless
   constructor. RimWorld uses `Activator.CreateInstance(type, game)`.
2. **`ArmoryGameComponent`** owns the session-only decision log and calls
   `ArmoryPlanner.Reset()` on construction. Log max 300 entries, not scribed.
3. **`ArmoryMapComponent`** caches the snapshot (`scanIntervalTicks`) and runs
   the planner every `plannerIntervalTicks` when automation is active.
4. **Planner ordering:** largest `TopReadyDeltaPct` first; within a pawn, walk
   `Ranked` downward, skip non-ready/claimed/unreachable, then break below the
   threshold. This gives next-best fallback.
5. **`TryTakeOrderedJob(job, JobTag.Misc, true)`** sets `playerForced` and queues
   after the current job. It can clear queued jobs when interrupting an idle
   pawn.
6. **Vanilla `Equip` drops the old primary** at the pawn's feet via
   `MakeRoomFor`; no custom swap job is used.
7. **Manual override detection** is heuristic: compare current weapon to
   `lastKnownWeapon`; if it changed and does not match `lastIssuedWeapon`, pause.
8. **No auto-unforbid.** `TopReady` requires `ForbidUtility.IsForbidden == false`.
9. **Bonded pawns are never managed** (`equipment.bondedWeapon != null`).
10. **Competitor guard:** Auto Arm (`snues.autoarm`) or Equipment Manager
    (`lordkuper.equipmentmanager`) loaded → `AutomationActive` false unless
    `overrideCompetitor`.
11. **`FreeColonists`, not `FreeColonistsSpawned`**, for pod pawns.
12. **Pod lookup fallback:** holder chain then `allBuildingsColonist` +
    `IThingHolder` comps. If a pod still says `Not on the map`, inspect that
    container's holder type and add a targeted check.
13. **Weapon filter:** `Item && !IsStuff && !IsDrug && !IsIngestible &&
    !IsMedicine && !IsApparel && !IsShell`.
14. **Age:** default 3, range 0–18; migration from 13 on `settingsVersion < 1`.
15. **UI:** tab 1280×720; proportional pawn/weapon columns; headers use
    `rowWidth`; rich-text labels measured after stripping tags.
16. **Settings page** has its own `Widgets.BeginScrollView` and measures
    `Listing_Standard.CurHeight`.
17. **Simple Sidearms adapter** is reflection-only and lives in
    `Compatibility/SimpleSidearmsAdapter.cs`. It must stay loadable when SS is
    absent; every entry point checks `Active` first.
18. **SS forced-state precedence:** drafted unarmed → drafted forced weapon →
    normal forced unarmed → normal forced weapon. Armory sets
    `PawnState.AutomationVeto` and the planner leaves the pawn alone.
19. **SS per-pawn preferences:** a carried `DefaultRangedWeapon` (ranged role) or
    `PreferredMeleeWeapon` (melee role) is the only allowed primary pair;
    `PreferredUnarmed` is a veto. SS's QuickSwitch enforces these when carried.
20. **SS primary mode:** `primaryWeaponMode` values are `Ranged=0`, `Melee=1`,
    `BySkill=2`, `ByGenerated=3`. Resolve `BySkill` with SS's own
    `getSkillWeaponPreference` (passion, then level, ties → ranged). Only block
    the other type when the pawn already carries the preferred type.
21. **SS carried baseline:** `BestCarriedPrimary` is the best carried weapon of
    the preferred type, scored with Armory's model. The planner uses
    `max(CurrentScore, BestCarriedPrimaryScore)` as the upgrade baseline.
22. **SS job guard:** skip pawns whose `curJob.def.defName` is
    `EquipSecondary`, `ReequipSecondary`, or `ReequipSecondaryCombat`.
23. **Do not call SS methods that mutate state.** The adapter only reads
    properties/fields and calls `GetMemoryCompForPawn`,
    `getSkillWeaponPreference`, and `CanPickupSidearmInstance`.

---

## Pending verification (0.3.0)

Ask the user to confirm on the live save:

- Simple Sidearms is detected (pawn detail shows `Simple Sidearms: primary ...`);
- forced primaries / forced unarmed in SS are shown and Armory leaves those
  pawns alone;
- pawns with a ranged SS primary do not get melee upgrades while carrying a
  ranged sidearm (and vice versa);
- carried sidearms are listed correctly;
- no repeated weapon swapping against SS over a few minutes;
- 0.2.3 items: pod label, age 3, header alignment, weapon column gaps, no beer.

If a forced state is not detected, log a snapshot (debug setting or dev action)
and check the `SS:` lines. The adapter disables itself with one warning if the
SS memory API does not match.

---

## Milestone 4 plan — sidearm ranking and assignment

Reference: `docs/RESEARCH.md` → Simple Sidearms; implementation of M3 in
`Compatibility/SimpleSidearmsAdapter.cs`.

Goal: when sidearm management is enabled, choose which loose weapons go into
which colonists' inventory via `EquipSecondary`, without violating SS limits or
fighting its own assignment logic.

### 1. SS limit data to read (reflection)

On `PeteTimesSix.SimpleSidearms.SimpleSidearms.Settings`:

- `SeparateModes` (bool);
- single-sidearm mode: `LimitModeSingle`, `LimitModeSingle_AbsoluteMass`,
  `LimitModeSingle_RelativeMass`, `LimitModeSingle_Selection`;
- amount mode: `LimitModeAmount`, `LimitModeAmount_AbsoluteMass`,
  `LimitModeAmount_RelativeMass`, `LimitModeAmount_Slots`;
- per-type variants for melee and ranged when `SeparateModes` is true
  (`LimitModeAmountMelee*`, `LimitModeAmountRanged*`, and the single-type modes).

Do not reimplement the checks: `StatCalculator.CanPickupSidearmInstance(weapon,
pawn, out reason)` already covers mass, stash space, selection lists, slot
counts, pacifist tools, biocoding, role restrictions, and bonds. Use it for
every sidearm candidate.

### 2. Candidate set and ranking

- Loose map weapons that are not the pawn's primary candidate (same scoring).
- Exclude the current primary and anything already carried.
- Require `CanPickupSidearmInstance` to pass.
- Rank by `WeaponPower.Get * PawnFit.Compute`, with two policy filters:
  - **type diversity first**: if the pawn's primary is ranged, prefer a melee
    sidearm (and vice versa); raw score is only a tie-breaker;
  - **do not duplicate** a def/stuff the pawn already carries.
- Slots: derive free capacity from SS settings + carried weapons. If the limit
  mode is `Slots`, the difference is exact. For mass modes, use SS's own check
  per candidate and stop at the desired count.

### 3. Planner

- New setting `manageSidearms` (off by default) and `desiredSidearms` (global,
  default 1, range 0–4).
- Issue `JobMaker.MakeJob(JobDefOf "EquipSecondary", weapon)`; resolve the
  `JobDef` through `DefDatabase<JobDef>.GetNamed("EquipSecondary", false)` so
  there is still no hard reference.
- Use the same claims/cooldown/manual-change protection as primaries.
- Log `Queued sidearm` with the reason (type diversity, upgrade, empty slot).
- Skip pawns with a forced unarmed state or a forced weapon that is not already
  the current primary (SS owns that decision).
- Never issue a sidearm job while an SS job is running.

### 4. UI

- Pawn detail: sidearm slots (`2/3 used`), ranked sidearm list with
  `ready` / `SS: <reason>` statuses, and the current sidearms.
- Maybe a small `S` marker in the Pawns list for pawns with free sidearm slots.
- Settings: enable/disable, desired count, "prefer a different type than the
  primary" (default on).
- Log: sidearm actions must be distinguishable from primary equips.

### 5. Tests

- Isolated folder `armorytest-ss` already exists (Core + Harmony + SS + Armory).
- Matrix: SS absent (settings hidden), SS present, forced unarmed, forced
  primary, slot limit 1/2, mass limit, drafted, primary already the same type,
  sidearm job already running, SS auto-pickup on/off.
- Watch for oscillation: Armory adds a sidearm, SS drops it, Armory re-adds.
  The manual-change/cooldown guard applies, but sidearm-specific last-known
  tracking may be needed.

**Open design questions for the user**

- Sidearms for all colonists or only Armory-managed ones?
- Sidearm count: SS setting only, per-pawn, or a global desired count?
- Should type diversity beat raw score? (Plan: yes, by default.)
- Should Armory ever remove/replace a sidearm, or only add? (Plan: only add in
  the first version.)

---

## Remaining backlog

- Exotic container scanning (transport pods, pawn inventory) as weapon sources.
- Auto-haul dropped weapons.
- Raid behaviour ("arm everyone before a raid").
- Show automation-skip reasons (pacifist, drafted, bonded, under age) in Pawn
  detail.
- Localization (all strings currently English literals).
- Workshop packaging (`PublishedFileId.txt`, preview requirements).
- RimWorld 1.7 / Waylith planning (`LoadFolders.xml`, net10.0, Harmony 3).
- Deliberate weapon variation (short/long mix, roles) after v1 proves itself.

---

## Product decisions and user preferences

- **Full auto-manager is the goal**, not only an inspector, but the ranking must
  stay simple and understandable. Fancy per-role loadouts come later, if at all.
- **Manager-agnostic**: Armory should not depend on Auto Arm's or Equipment
  Manager's internals. Operationally, only one auto-equipper should act, hence
  the competitor guard and override.
- **Simple Sidearms**: primary coexistence shipped in 0.3.0; sidearm assignment is
  next. Sidearms should not be bolted on before the primary flow is solid.
  Simple Sidearms' own QuickSwitch think node enforces its primary preference
  among carried weapons, so Armory must not queue a primary SS would undo.
- **No auto-unforbid**: forbidden weapons stay visible and explained, but the
  player decides when to allow them.
- **Explainability matters**: the user's original pain was not knowing why a
  weapon was or was not equipped. The factor ledger, blocker status, demand view,
  and decision log exist for that reason.
- **UI**: wider tab and meaningful columns are preferred over aggressive
  ellipsizing. Minor alignment polish can wait.
- **Variation later**: a deliberate short-range/long-range mix or role-based
  variation is explicitly a "very end" feature, not v1.
- **Testing**: the user tests against a real 12-colonist save with ~40 weapons,
  Auto Arm, Equipment Manager, Simple Sidearms, Quality Colors, Gravship Storage,
  and many other workshop mods.

## Feedback-driven decisions (do not regress)

- Forbidden weapons are **soft-blocked and still ranked**, never hidden.
- Pod/casket pawns must remain listed, greyed out, with a container label.
- Children can equip weapons in vanilla; the age setting is a product choice.
- Quality Colors / rich-text tags must render when they fit and never leak as
  raw markup when truncated.
- Beer, meals, medicine, and shells are not weapons.
- Table headers must use the row width (scrollbar-aware), not the full pane.
- The mod settings page must scroll; automation options grow the content.
- Pacifists, prisoners, slaves, the under-age, the bonded, and (by default)
  drafted pawns are skipped by automation.
- A second weapon manager must not silently fight Armory.

---

## Version history

| Version | Changes |
|---|---|
| 0.1.0 | Initial read-only inspector: tab, scoring, Pawns/Weapons/Demand |
| 0.1.1 | Rich-text-safe cells, header/banner spacing, wider delta columns |
| 0.2.0 | Opt-in planner, claims/cooldown, decision log, Log tab, situational weapon penalty |
| 0.2.1 | Settings scroll view, all free colonists listed, grey unavailable pawns, column widths |
| 0.2.2 | Age default 3 + migration, pod holder chain, tab 1280×720, proportional columns |
| 0.2.3 | Map-wide pod lookup fallback, header/row width fix, weapon column gaps, beer/drug filter |
| 0.3.0 | Simple Sidearms coexistence: reflection adapter, forced states, primary mode, carried baseline, SS job guard, sidearm display |

---

## Files that matter

| File | Why |
|---|---|
| `README.md` | User/dev overview, settings, build, roadmap |
| `docs/RESEARCH.md` | Auto Arm / Equipment Manager / vanilla / SS findings |
| `docs/HANDOFF.md` | This file |
| `Source/Armory/ArmoryPlanner.cs` | Automation decisions and job issuing |
| `Source/Armory/ArmorySnapshotBuilder.cs` | Ranking, pod lookup, weapon filter, demand |
| `Source/Armory/Scoring/*` | Power, fit, blockers |
| `Source/Armory/UI/MainTabWindow_Armory.cs` | All tabs and layout |
| `Source/Armory/ArmorySettings.cs` | Settings + migration + scroll UI |
| `Source/Armory/ArmoryGameComponent.cs` | Session log, planner reset |
| `Source/Armory/Compatibility/SimpleSidearmsAdapter.cs` | Reflection-only SS state/rules; M3 core |
| `Source/Armory/WeaponFilter.cs` | One shared weapon-definition filter |
| `Source/Armory/ArmoryMod.cs` | Mod entry, settings, competitor/SS detection |
