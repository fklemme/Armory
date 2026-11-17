# Handoff / project state

**Snapshot:** Armory **0.2.3**, RimWorld **1.6.4871**.
Build is clean (0 warnings, 0 errors). No test hooks remain in source.

---

## Where we are

Milestones 0–2 are implemented:

- **0** — mod skeleton, `MainButtonDef`, tab, settings, build/junction.
- **1** — ranking, blockers, Pawns/Weapons/Demand tabs, factor ledger.
- **2** — opt-in planner (claims, cooldown, threshold, manual protection),
  session decision log, Log tab.

**Next:** milestone 3, Simple Sidearms primary coexistence. Then milestone 4,
sidearm ranking/assignment.

The user was last testing 0.2.2 and reported:

- wider tab looks much better;
- pod pawns still show `Not on the map`;
- header alignment off by the scrollbar width;
- Weapons tab `Power`/`Best colonist` too close;
- (screenshot revealed) beer stacks counted as weapons.

0.2.3 addresses all five but has **not yet been confirmed by the user**:

1. map-wide pod/building search for unavailable pawns;
2. headers use `rowWidth` (scrollbar-aware);
3. 8 px gaps in the Weapons columns;
4. exclude drugs/ingestibles/medicine/apparel/shells;
5. version bump only (age/columns were 0.2.2).

Ask the user to confirm the pod label and age migration on their save.

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

### User save

- 12 colonists, 40 weapons.
- Auto-equip works.
- Pacifist child/pawn skipped as intended.
- Auto Arm is installed; banner/guard behavior works.
- Pod pawns listed but still `Not on the map` in 0.2.2.

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

---

## Pending verification (0.2.3)

Ask the user to confirm:

- Pod pawns now show `In a biosculpter pod` (or another container label).
- The 11-year-old is now managed; minimum age shows 3.
- Pawn/Weapon headers line up with rows (scrollbar offset fixed).
- Weapons `Power` / `Best colonist` no longer touch.
- Beer/drug stacks are gone from Weapons, rankings, and Demand.

If the pod label still fails, get the containment type or a screenshot of the
pawn's `ParentHolder` path; possible next fallback is a direct
`CompBiosculpterPod.Occupant == pawn` scan.

---

## Milestone 3 plan — Simple Sidearms primary coexistence

Reference: `docs/RESEARCH.md` → Simple Sidearms.

1. **Adapter**
   - New `Source/Armory/Compatibility/SimpleSidearmsAdapter.cs`.
   - Detect active mod (`PeteTimesSix.SimpleSidearms`) and types via reflection.
   - Cache `MethodInfo`/`PropertyInfo`/`FieldInfo`; return false/null if absent.
   - No compile-time reference to `SimpleSidearms.dll`.

2. **Read SS state**
   - `CompSidearmMemory.GetMemoryCompForPawn(pawn)`.
   - `ForcedUnarmed`, `ForcedWeapon`, `ForcedWeaponWhileDrafted`,
     `primaryWeaponMode`, `RememberedWeapons`.
   - Surface forced states as blockers in Pawn detail/status.

3. **Primary policy**
   - Before issuing `Equip`, if SS manages the pawn, check
     `CanPickupSidearmInstance` / type rules to avoid SS cross-type swap loops.
   - Do not override SS forced primary/unarmed.
   - Do not duplicate SS's own retrieval job.

4. **Model/UI**
   - Add a `Sidearms` section or columns to Pawns.
   - Show current sidearms (from `pawn.inventory.innerContainer`) and free slots.
   - Add settings: `manageSidearms` (off by default), maybe `desiredSidearms`.

5. **Testing**
   - New isolated folder `armorytest-ss` with Core + Harmony + Simple Sidearms +
     Armory, because the current harness has no SS.
   - ModsConfig entries:
     - `brrainz.harmony`
     - `ludeon.rimworld`
     - `PeteTimesSix.SimpleSidearms`
     - `kruecke.armory`
   - Matrix: SS absent/present, forced unarmed, forced primary, slot limit,
     drafted, cross-type swap.

**Open design questions for the user**

- Sidearms for all colonists or only Armory-managed ones?
- Sidearm count: SS setting, per-pawn, or global?
- Should sidearm ranking prefer type diversity (ranged + melee) over raw score?
- How much SS forced-state UI is wanted?

---

## Milestone 4 sketch — sidearms

- Candidate set includes pawn inventory and remembered sidearms.
- Rank sidearms separately; respect slot limits.
- `JobDefs "EquipSecondary"` to add a sidearm.
- UI: current sidearms, free slots, ranked sidearm list, statuses.
- Settings: enable sidearm management, desired count.
- Handle SS `primaryWeaponMode` and forced states.
- Test with SS's own auto-pickup on/off.

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
- **Simple Sidearms**: primary coexistence first, full sidearm management later.
  Sidearms should not be bolted on before the primary flow is solid.
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
| `Source/Armory/Compatibility/` | **Does not exist yet** — M3 adapter goes here |
