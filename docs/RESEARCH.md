# Research notes

Version-specific findings from the installed RimWorld 1.6.4871 setup, used to
design Armory and to plan Simple Sidearms support. Re-verify if the game or
these mods update.

---

## Environment and sources

| Item | Value |
|---|---|
| RimWorld | 1.6.4871 rev591, Unity 2022.3 / Mono, `net472` |
| DLCs | Core, Royalty, Ideology, Biotech, Anomaly, Odyssey |
| .NET SDK | 9.0.304 (MSBuild 17.14) |
| Mods studied | Auto Arm, Equipment Manager, Simple Sidearms |
| Decompiler | `ilspycmd` 8.2.0.7535 (requires `DOTNET_ROLL_FORWARD=Major` on .NET 9) |

Installed workshop paths (`...\Steam\steamapps\workshop\content\294100\`):

| Mod | Workshop ID | packageId | Notes |
|---|---:|---|---|
| Auto Arm | 3516409503 | `Snues.AutoArm` | 217 classes, large test suite |
| Equipment Manager | 2790435986 | `LordKuper.EquipmentManager` | needs `LordKuper.Common` (3531352422) |
| Simple Sidearms | 927155256 | `PeteTimesSix.SimpleSidearms` | 1.6 DLL in `v1.6\Assemblies` |
| Harmony | 2009463077 | `brrainz.harmony` | 1.6 assembly 2.4.3 |

Decompiled output used during research lived under
`C:\Users\mail\AppData\Local\Temp\opencode\decomp\`; do not rely on it
persisting. Re-decompile with `ilspycmd -p -o <out> <dll>`.

---

## Auto Arm

**Approach:** think-tree injection, not a manager tab.

`Patches/ThinkTree.xml` inserts into
`MainColonistBehaviorCore → ThinkNode_PrioritySorter → subNodes`:

```
AutoArm.ThinkNode_ConditionalWeaponStatus
  └─ AutoArm.Jobs.JobGiver_PickUpBetterWeapon
```

Custom jobs:

- `AutoArmSwapPrimary` → `JobDriver_SwapPrimary`
- `AutoArmSwapSidearm` → `JobDriver_SwapSidearm`

Scoring (`AutoArm/Scoring.cs`) roughly:

- base power: melee `AvgDPS × (1+AP)`; ranged `DPS × sustain × blendedAccuracy
  × (1+AP)`, ×10; situational ×0.3; ability +7–20%; damaged ×0.95;
- shooter fit from shooting accuracy factors and weapon cycle;
- skill mismatch ×0.7; skill bonus up to +500; hunter +500;
- persona bond ×1.2; forced weapon = 10000;
- CE ammo modifier when CE is active.

**The hidden gate:** weapon permission is derived from the pawn's **apparel
policy**:

```csharp
pawn.outfits.CurrentApparelPolicy.filter.Allows(weapon.def)
```

If disallowed, score becomes `-1000`. Weapons are injected into the apparel
policy dialog by patching `Listing_TreeThingFilter` and
`Dialog_ManageApparelPolicies`; `AutoArm.UI.WeaponTab` is not a real tab. This
is likely why players cannot see why a weapon is rejected.

**Top-weapons UI:** `StatusOverviewDataGatherer.GetTopWeapons` exists, but it is
only used by `DebugPanel`. There is no user-facing per-pawn ranking.

**Simple Sidearms:** handled by
`AutoArm.Compatibility.SimpleSidearmsCompat`, entirely via reflection
(`AccessTools.TypeByName`, cached `MethodInfo`, delegates). It checks
`CanPickupSidearmInstance`, `isValidSidearm`, `RememberedWeapons`, forced
states, and handles cross-type primary swaps.

**Settings include:** `onlyAutoEquipFromStorage`,
`upgradeEquippedWeapons`, `autoEquipSidearms`, `allowSidearmUpgrades`,
`allowForcedWeaponUpgrades`, `checkCEAmmo`, `weaponUpgradeThreshold`,
`weaponTypePreference`, `childrenMinAge`, `allowChildrenToEquipWeapons`,
`allowTemporaryColonists`, `disableDuringRaids`, `respectWeaponBonds`.

**Takeaway:** a very mature auto-equipper, but its decisions are hidden in
apparel policies and debug UI. Armory should stay an explainer first.

---

## Equipment Manager

**Approach:** policy/loadout driven, similar to outfits.

`Loadout` fields include:

- `Priority`, `Label`;
- `PrimaryWeaponType` (`None`, `RangedWeapon`, `MeleeWeapon`);
- `PrimaryRangedWeaponRuleId` / `PrimaryMeleeWeaponRuleId`;
- `RangedSidearmRules`, `MeleeSidearmRules`, `ToolRuleId`;
- trait requirements and work-capacity requirements;
- skill/stat weights and passion/capacity limits;
- `DropUnassignedWeapons`.

Default loadouts: Assault, Sniper, Support, Slasher, Crusher, Pacifist.

Assignment runs from `EquipmentManagerMapComponent.MapComponentTick`, with
methods such as `AssignPrimaryRangedWeapon`, `AssignPrimaryMeleeWeapon`,
`AssignBestTool`, `UpdateRangedSidearms`, `UpdateMeleeSidearms`.

Jobs:

```csharp
JobMaker.MakeJob(JobDefOf.Equip, weapon)
JobMaker.MakeJob(SidearmsDefOf.EquipSecondary, weapon)   // when SS is present
pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, requestQueueing);
```

UI: `ManageLoadoutsDialog`, `ManageWeaponRulesDialog`, import/export dialog, and
a pawn-column worker. Again policy-first, not explanation-first.

**Takeaway:** loadouts are powerful but complex. Armory deliberately does not
copy this; it ranks and explains, with a simple global threshold.

---

## Vanilla 1.6 API notes

### Adding a tab

`Defs/MainButtonDefs.xml`:

```xml
<MainButtonDef>
  <defName>Armory</defName>
  <label>Armory</label>
  <tabWindowClass>Armory.MainTabWindow_Armory</tabWindowClass>
  <order>65</order>
  <validWithoutMap>false</validWithoutMap>
</MainButtonDef>
```

- `MainButtonDef` fields: `workerClass` (default `MainButtonWorker_ToggleTab`),
  `tabWindowClass`, `buttonVisible`, `order`, `defaultHotKey`,
  `validWithoutMap`, `minimized`, `iconPath`, `closesWorldView`.
- `MainTabWindow : Window`; implement `DoWindowContents(Rect)`, optionally
  override `RequestedTabSize`, `Anchor`, `PreOpen`/`PostOpen`.
- Example mods: Farming Hysteresis (simple), Research Reinvented.

### Equip mechanics

`JobDriver_Equip`:

- target A is the weapon;
- reserves the target (or the outfit stand);
- `Pawn_EquipmentTracker.MakeRoomFor` drops the current primary at the pawn's
  position and unforbids the dropped item.

So a plain `JobDefOf.Equip` job is a complete swap; no custom driver required
for primaries.

`Pawn_JobTracker.TryTakeOrderedJob`:

```csharp
public bool TryTakeOrderedJob(Job job, JobTag? tag = JobTag.Misc,
                              bool requestQueueing = false)
```

- sets `job.playerForced = true`;
- with `requestQueueing: true`, a non-idle pawn queues it after the current job
  instead of being interrupted;
- may clear existing queued jobs when it interrupts an idle pawn.

### Eligibility

`EquipmentUtility.CanEquip(thing, pawn, out cantReason, checkBonded: true)`
checks:

- bladelink bonded to another pawn;
- biocoded to another pawn;
- already bonded to another weapon;
- role restrictions (`RoleEffect_NoMeleeWeapons` / `NoRangedWeapons`);
- **apparel only**: developmental stage filter.

It does not check age, forbidden status, reachability, reservations, or allowed
areas. Armory layers those on.

### Pawn lists

- `map.mapPawns.FreeColonistsSpawned` — spawned only (excludes pod occupants).
- `map.mapPawns.FreeColonists` — `FreeHumanlikesOfFaction`, based on `AllPawns`,
  which includes `AllPawnsUnspawned` found via
  `ThingOwnerUtility.GetAllThingsRecursively(map, Pawn)`.
- Pod occupants therefore need `FreeColonists` plus holder resolution.

### The "Weapon" group is broad

`ThingListGroupHelper` maps `ThingRequestGroup.Weapon` to `def.IsWeapon`.

`ThingDef.IsWeapon` is:

```
category == Item && (!verbs.NullOrEmpty() || !tools.NullOrEmpty()) && !IsApparel
```

Consequences:

- wood/steel inherit `ResourceVerbBase` and count as weapons;
- beer and meals have ingestible verbs and can count as weapons;
- `IsStuff` filters resources, but drugs/ingestibles need explicit exclusion.

Armory filters:

```csharp
def.category == Item
&& !def.IsStuff && !def.IsDrug && !def.IsIngestible
&& !def.IsMedicine && !def.IsApparel && !def.IsShell
```

### Biosculpter pods

`CompBiosculpterPod : ThingComp, IThingHolder, ...` holds its occupant in

```csharp
innerContainer = new ThingOwner<Thing>(this);
public Pawn Occupant => ... innerContainer[0] as Pawn
```

The `ThingOwner` is owned by the **comp**, not the building. `ParentHolder` can
therefore lead to `ThingOwner → CompBiosculpterPod → parent building`, or be
unusable. Armory:

1. walks `ThingOwner.Owner`, `ThingComp.parent`, and `Building`;
2. falls back to scanning `map.listerBuildings.allBuildingsColonist` and each
   building's `IThingHolder` comps for the pawn.

`Building_CryptosleepCasket` and growth vats use the same building/comp-holder
pattern.

### UI details learned the hard way

- `Widgets.BeginScrollView(Rect, ref Vector2, Rect)` returns **void** in 1.6;
  draw content at content coordinates.
- `Listing_Standard` has no scroll helper. Pattern used:
  ```csharp
  Rect viewRect = new Rect(0, 0, inRect.width - 18f, contentHeight);
  Widgets.BeginScrollView(inRect, ref scroll, viewRect);
  var listing = new Listing_Standard();
  listing.Begin(viewRect);
  ...
  float measured = listing.CurHeight;
  listing.End();
  contentHeight = Mathf.Max(400f, measured + 20f);
  Widgets.EndScrollView();
  ```
- `Widgets.LabelEllipses` calls `Text.ClampTextWithEllipsis` **before**
  rendering. Truncating a Quality Colors label cuts the closing tag and leaves
  raw markup. Measure `StripTags(label)` with `Text.CalcSize`; if it fits, draw
  the original (rich text); otherwise draw `LabelEllipses` on the stripped text.
- `MainTabWindow` size is clamped to screen width/height.
- Table headers must use the same width as rows, i.e. subtract the scrollbar
  gutter (`rowWidth = scrollRect.width - 20f`), or columns drift.

### Components

- `GameComponent` is created with `Activator.CreateInstance(type, game)`.
  A subclass **must** have a `(Game)` constructor; the base has an implicit
  parameterless constructor, so do **not** call `: base(game)`.
- `MapComponent` constructor takes `Map`; use `MapComponentTick`.
- `Map.GetComponent<T>()`, `Game.GetComponent<T>()`.

### Ideoligion mood

`pawn.Ideo.GetDispositionForWeapon(ThingDef)` returns `Noble`, `Despised`, or
`None`. Vanilla `ThoughtWorker_Precept_WieldingNobleOrDespisedWeapon` turns that
into a mood thought. Armory applies +10% / −15% fit.

### Age and violence

- No weapon-specific age gate.
- `CombinedDisabledWorkTags` comes from backstory/traits/genes, kind defs, and
  royal titles; no age component.
- Armory's age setting is a product choice, not a game restriction.

---

## Simple Sidearms 1.6

Assembly: `...\927155256\v1.6\Assemblies\SimpleSidearms.dll`.

### Memory component

`SimpleSidearms.rimworld.CompSidearmMemory : ThingComp`

Key fields:

```csharp
public static Dictionary<int, CompSidearmMemory> _cache;
public List<ThingDefStuffDefPair> rememberedWeapons;
public bool forcedUnarmedEx;
public ThingDefStuffDefPair? forcedWeaponEx;
public bool forcedUnarmedWhileDraftedEx;
public ThingDefStuffDefPair? forcedWeaponWhileDraftedEx;
public bool preferredUnarmedEx;
public ThingDefStuffDefPair? defaultRangedWeaponEx;
public ThingDefStuffDefPair? preferredMeleeWeaponEx;
public Enums.PrimaryWeaponMode primaryWeaponMode; // default BySkill
public bool currentJobWeaponReequipDelayed;
```

Key properties/methods:

```csharp
public List<ThingDefStuffDefPair> RememberedWeapons { get; }
public bool ForcedUnarmed { get; set; }
public ThingDefStuffDefPair? ForcedWeapon { get; set; }
public ThingDefStuffDefPair? ForcedWeaponWhileDrafted { get; set; }
public static CompSidearmMemory GetMemoryCompForPawn(Pawn pawn, bool fillExistingIfCreating = true);
public bool IsCurrentWeaponForced(bool alsoCountPreferredOrDefault);
public void SetWeaponAsForced(ThingDefStuffDefPair weapon, bool drafted);
public void UnsetForcedWeapon(bool drafted);
```

`ThingDefStuffDefPair` has `thing` and `stuff` fields.

### Rules and assignment

`PeteTimesSix.SimpleSidearms.Utilities.StatCalculator` exposes:

- `CanPickupSidearmInstance(ThingWithComps weapon, Pawn pawn, out string reason)`
  (Auto Arm also probes `canCarrySidearmInstance` and `CanPickupSidearm`);
- `canUseSidearmInstance`;
- `isValidSidearm`.

`PeteTimesSix.SimpleSidearms.Utilities.WeaponAssingment` (spelling is
intentional) handles assignment internally.

### Jobs and think tree

JobDefs in `Defs/JobDefs/Jobs_Misc.xml`:

- `EquipSecondary` → `SimpleSidearms.rimworld.JobDriver_EquipSidearm`
  (walks to the weapon, moves it into `pawn.inventory`, memorizes it);
- `ReequipSecondary` → `JobDriver_ReequipSidearm`;
- `ReequipSecondaryCombat` → `JobDriver_ReequipSidearmCombat`.

Think tree patches (`Patches/thinkTreePatches.xml`):

- `JobGiver_QuicklySwitchWeapons` is inserted before `JobGiver_Work`
  (`emergency=true`) inside `ThinkNode_ConditionalColonist`. Its
  `TryGiveJobStatic` calls
  `WeaponAssingment.equipBestWeaponFromInventoryByPreference(pawn, Calm)`.
  This runs continuously for colonists and enforces the pawn's primary mode
  among **carried** weapons. It never looks at loose ground weapons.
- `JobGiver_RetrieveWeapon` is inserted before the `ChangingApparel` tagger.
  It re-retrieves **remembered** weapons that are missing, from the map.

Consequences for Armory:

- After Armory equips a loose weapon, the old primary drops to the ground and
  the new one is memorized by SS's `JobDriver_Equip` transpiler.
- SS's QuickSwitch only compares carried weapons, so it can switch back to a
  carried sidearm if the new primary is the wrong type or scores lower by SS's
  own DPS metric. This is the main source of primary fights.
- `equipBestWeaponFromInventoryByPreference` precedence:
  drafted unarmed / forced drafted weapon → forced unarmed → forced weapon →
  ranged primary (default ranged, else best carried ranged) → preferred melee →
  best carried melee.
- Forced drafted states are cleared on undraft (`InformOfUndraft`).

`primaryWeaponMode` enum: `Ranged=0`, `Melee=1`, `BySkill=2`, `ByGenerated=3`.
`BySkill` is resolved with `Extensions.getSkillWeaponPreference`: higher passion
wins, then higher level, ties → ranged.

### Storage

Sidearms live in the pawn's **inventory** (`innerContainer`), not equipment.
A complete view must include `pawn.inventory.innerContainer` and the memory
component's remembered weapons.

### Auto Arm's reflection pattern (reuse this idea)

`AutoArm.Compatibility.SimpleSidearmsCompat` resolves everything by name:

```csharp
AccessTools.TypeByName("SimpleSidearms.rimworld.CompSidearmMemory");
AccessTools.Method(memoryType, "GetMemoryCompForPawn", ...);
AccessTools.Method(statCalculatorType, "CanPickupSidearmInstance", ...);
AccessTools.Property(memoryType, "RememberedWeapons");
AccessTools.FieldRefAccess<bool>(memoryType, "forcedUnarmedEx");
```

It caches delegates and degrades gracefully if a method is missing. Do the same:
no compile-time reference, no hard dependency, safe in Core-only games.

---

## Integration plan (Armory milestones 3–4)

### M3 — primary coexistence (implemented in 0.3.0)

1. Detect SS by packageId `PeteTimesSix.SimpleSidearms`; resolve types by name
   with graceful failure.
2. `SimpleSidearmsAdapter` reads `CompSidearmMemory` for each pawn:
   `ForcedUnarmed`, `ForcedWeapon`, drafted variants, `primaryWeaponMode`.
3. Forced states follow SS's precedence and become **soft blockers plus an
   automation veto**. The player can clear them; Armory never does.
4. Per-pawn preferences are read too: a carried `DefaultRangedWeapon` /
   `PreferredMeleeWeapon` for the active role locks the allowed pair;
   `PreferredUnarmed` is a veto.
5. Primary mode produces a preferred type (`Ranged`, `Melee`, or `BySkill`
   resolved with SS's own rule). Armory blocks the other type only when the pawn
   already carries a weapon of the preferred type, i.e. when SS would switch
   back.
6. The best carried weapon of the preferred type is scored with Armory's model
   and used as the upgrade baseline.
7. Pawns running `EquipSecondary`, `ReequipSecondary`, or
   `ReequipSecondaryCombat` are skipped.
8. Verified: SS absent (inactive, no change), SS present (all three pawns read a
   mode, jobs queue), no exceptions. Forced and preference states still need a
   live-save check.

What was deliberately **not** done: using SS's `CanPickupSidearmInstance` for
primaries (it governs sidearm slots, not primaries), and assigning sidearms.

### M4 — sidearm ranking and assignment

1. Candidate set: loose weapons, excluding the primary and already-carried
   weapons. Validate every candidate with `CanPickupSidearmInstance`.
2. Rank with the same power/fit model, but prefer a sidearm type different from
   the primary, then score.
3. UI: current sidearms, free slots, ranked sidearm list, statuses; settings for
   enable + desired count.
4. Assignment: `JobDef "EquipSecondary"` (resolved via `DefDatabase`, no hard
   reference), same claims/cooldown/manual protections, sidearm-specific log.
5. Respect SS slot/mass/selection limits through
   `CanPickupSidearmInstance`, and never remove/replace in the first version.
6. Test with SS auto-pickup on/off and watch for add/drop oscillation.

### Open questions

- Should Armory manage sidearms only for its own primary choices, or all pawns?
- How many sidearms per pawn: SS setting, per-pawn, or global?
- Should sidearm ranking value **type diversity** (e.g. ranged primary + melee
  sidearm) rather than raw score?
- How to present SS forced states without adding too much UI?
- CE ammo compatibility for sidearms.

---

## Caveats

- All findings are from installed 1.6 builds; mods may change/update.
- Decompiled code is for research only. Armory's implementation is original.
- 1.7 (Waylith, .NET 10, Harmony 3) is a different target; re-verify before
  supporting it.
