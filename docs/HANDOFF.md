# Handoff / project state

**Snapshot:** Armory **0.6.6**, RimWorld **1.6.4871**.
Build is clean (0 warnings, 0 errors). No test hooks remain in source.
Git repository on `main`; 0.2.3 through 0.6.6 are committed.

---

## Where we are

Milestones 0–4 are implemented:

- **0** — mod skeleton, `MainButtonDef`, tab, settings, build/junction.
- **1** — ranking, blockers, Pawns/Weapons/Demand tabs, factor ledger.
- **2** — opt-in planner (claims, cooldown, threshold, manual protection),
  session decision log, Log tab.
- **3** — Simple Sidearms primary coexistence via a reflection adapter:
  forced states, preferences, primary mode, carried-primary baseline, SS job
  guard, sidearm display.
- **4** — typed hand/backup slots (0.6.0): one hand weapon of the SS mode type
  and one opposite-type backup, mirrored for melee pawns; same-type-only
  upgrades; add-first or drop-first depending on SS slots; separate Hand/Backup
  rankings and Weapons/Demand status.

**Next:** milestone 5, polish (container scanning, auto-haul, raid arming,
automation-skip reasons, localization, Workshop prep).

---

## 0.4.1 hotfix — over-strict SS preference block

The user loaded their 0.4.0 save and reported that auto-equip did nothing. All
candidates showed `SS: default ranged ...` as the blocker, including weapons of
the pawn's own preferred type. The cause was 0.4.0 treating a **carried
`DefaultRangedWeapon` / `PreferredMeleeWeapon`** as a hard primary blocker.

SS only enforces those preferences among carried weapons; Armory already
includes the best carried weapon of the preferred type in the upgrade baseline.
Hard-blocking therefore locked every pawn to their current/default weapon and
stopped all upgrades. 0.4.1 removes the block. The pair remains in the pawn
detail as an informational preference (`SS default ranged: X (carried)`), and
`PreferredUnarmed` remains an automation veto.

Lesson: never turn an SS *preference* into a hard blocker unless SS itself
refuses the action. Forced states and unarmed are vetoes; type preferences are
baselines.

---

## 0.4.2 — settings column fix and SS role clarity

**Settings options disappearing:** `Listing_Standard` spills overflow controls
into a second column when the content is taller than the rect it was given.
Armory's settings page sits inside a scroll view whose height is measured from
the previous frame, so growing the page (for example by toggling "Manage
sidearms") could push every later control into an invisible second column and
collapse the measured height. `listing.maxOneColumn = true` is now set before
`Begin`. This also restores the hidden "Manage sidearms" controls.

**Monosword not equipped:** expected with SS's current per-pawn role. The
Monosword is melee; every pawn in the report has SS mode Ranged and carries a
ranged weapon, so melee primaries are blocked by design. SS's "colonist default
weapon mode" setting only applies to newly generated pawns; existing pawns keep
their per-pawn `primaryWeaponMode`. To make a melee specialist, open that
pawn's Simple Sidearms gizmo and select Melee (or By skill with a melee-
preferring pawn). Alternatively, turn off "Respect Simple Sidearms" to let
Armory override roles, accepting that SS may fight back.

Status text now reads `SS role: ranged` / `SS role: melee` instead of
`SS: ranged primary`, which was easy to misread as a weapon property.

---

## 0.5.0 — sidearm upgrades and sidearm visibility

The user correctly pointed out that SS exists to keep a ranged primary **and** a
melee sidearm, so the Monosword should be usable as a sidearm even though it is
blocked as a primary. Two problems were addressed:

1. **Weapons tab status was primary-only.** `Only blocked` meant "blocked as a
   primary", but read as "unusable". The Weapons tab now shows:
   - `Sidearm` when a ready sidearm pawn exists;
   - `Sidearm blocked` when only a blocked sidearm pawn exists;
   - `sidearm ready` in the colonist-by-fit list when the primary is blocked but
     the sidearm check passes.
   The detail explains "Sidearm for X. Primary use is blocked."
2. **Add-only sidearm management did nothing when the desired count was already
   met.** On the user's save, SS allows 2 melee and 3 total sidearms; pawns
   already carried sidearms. `desiredSidearms=1` therefore suppressed all
   additions, and the Monosword was never considered.

0.5.0 adds `replaceWorseSidearms` (default off, because the upgrade path could
not be runtime-tested while the user's game was open):

- if the pawn is at the desired count, find the best ready candidate that beats
  the worst droppable sidearm by the upgrade threshold;
- prefer dropping a same-type sidearm (so a melee upgrade does not remove the
  only ranged sidearm, and vice versa);
- queue `EquipSecondary` for the better weapon **first**, then drop the old one
  via SS's `WeaponAssingment.DropSidearm(pawn, weapon, true, true)` so SS does
  not immediately retrieve it;
- skip the upgrade when SS's slot limits leave no room for one more, or when the
  old sidearm is a persona weapon / `destroyOnDrop`;
- show both actions in the Log: `Queued sidearm` (replacing X) and
  `Dropped sidearm`.

The user's SS config was confirmed from
`Mod_927155256_SimpleSidearms.xml`: `SeparateModes=True`,
`LimitModeSingleMelee=RelativeWeight`, `LimitModeAmountMelee=Slots`,
`LimitModeAmountTotal=Slots` (3 total). The serialized melee relative mass is
`0.0542857125`, which is the Basic preset's `1.9 / InferredValues.maxCapacity`
(35), i.e. a **1.9 kg** single-melee-sidearm cap. A Monosword is **2 kg** and a
Zeushammer is also **2 kg**, so SS itself rejects them as sidearms
(`CanPickupSidearmInstance` returns "too heavy"). A gladius is 0.85 kg and
passes. This is the actual reason the Monosword is never a sidearm; Armory
cannot override SS's limit without ignoring SS.

**Not runtime-tested:** the user's game was running when this build was made, so
the isolated replacement test could not be run. The add path and reflection
method resolution compile clean; the upgrade path needs a live check.

---

## 0.5.1 — explain SS role blocks; use SS's actual carried choice

The user reported that `SS role: ranged` was not helpful: it did not say why a
weapon was excluded. The screenshot also showed a carried sidearm
("The Wargun of Delight", 112 by Armory's score) becoming the upgrade baseline
even though SS might never promote it, blocking a valid Carbine upgrade.

Changes:

- Blocker text is now explicit: `Melee blocked: SS ranged` or
  `Ranged blocked: SS melee`.
- Pawn detail shows a wrapped explanation when the top candidate is blocked by
  the SS role: the pawn's SS role, why the other type is excluded, and how to
  change it (SS gizmo → Melee/Ranged, or turn off Respect Simple Sidearms).
- Weapon detail shows the same explanation for the best blocked primary, plus
  the sidearm-blocked reason (e.g. `Too heavy for a sidearm melee`).
- The carried baseline is now **SS's own QuickSwitch choice** via
  `GettersFilters.findBestRangedWeapon` / `findBestMeleeWeapon` (reflection).
  This applies SS's manual-use, dangerous, EMP, and blocked-weapon filters, so
  an unusable carried weapon no longer blocks a valid upgrade. If the call
  fails, Armory logs once and uses the current primary as the baseline.
- Labels changed from "carried Simple Sidearms primary" to "carried for the
  SS role".

Not runtime-tested for the same reason (game running); reflection signatures
match the decompiled 1.6 methods.

---

## 0.6.0 — typed hand/backup slots

The user asked to rethink the equipment concept after a melee weapon kept
appearing at the top of every ranged pawn's list and was then reported as
SS-blocked. The agreed model:

- SS has **one equipped hand** (`equipment.Primary`) and a **bag** (inventory).
  Its primary mode chooses which type SS keeps in hand. Sidearm limits apply only
  to the bag; a loose weapon equipped via vanilla `Equip` is not checked against
  those limits. That is the only sense in which the hand has a "wider selection".
- Armory no longer compares ranged and melee scores. Each pawn has:
  - a **hand slot** of the SS mode type (`Ranged` / `Melee`; `BySkill` resolves
    through SS's own skill rule, otherwise current weapon, then combat skill);
  - one **backup slot** of the opposite type.
  A ranged pawn gets a melee backup; a melee pawn gets a ranged backup.
- Hand upgrades use vanilla `Equip` and SS's remembered preference updates.
  Backups use `EquipSecondary`.
- Upgrades are **same-type only**: replace the worst carried weapon of that type.
  Never drop the last weapon of a type; never drop one type to add the other.
- At SS's slot/free-space cap, **drop the old backup first**, re-check SS, then
  queue the new one. If SS still refuses, requeue the old weapon. With a free
  slot, keep the safer add-first-then-drop order.
- `desiredSidearms` and `preferSidearmDiversity` were removed. `manageSidearms`
  now means "keep one opposite-type backup"; `replaceWorseSidearms` defaults on.
  `settingsVersion` is 2 and the migration enables upgrades once.
- UI: Pawns columns are Hand / Backup / Status; pawn detail has separate
  `Hand slot (type)` and `Backup slot (type)` sections; Weapons status is
  `Hand` / `Backup` / `Hand blocked` / `Backup blocked` / `No user`; Demand has a
  `For` column (`hand` / `backup` / `both`).
- The hand baseline still uses SS's QuickSwitch choice. It now reads SS's
  `SkipDangerousWeapons` setting and passes `includeRangedWithBash: false` for
  the melee call, matching SS's idle choice.
- Drop-first is pre-checked with `StatCalculator.isValidSidearm`,
  `canUseSidearmInstance`, and an Armory-side relative-mass check. SS's
  `isValidSidearm` silently ignores `RelativeWeight` (it handles only
  `AbsoluteWeight` and `Selection`), so the extra check is required; without it
  a 2 kg Monosword passed the pre-check, was dropped in, failed SS's real
  `CanPickupSidearmInstance`, and was restored every cooldown. The pacifist tool
  rule is also part of the pre-check. If SS's post-drop check still fails, the
  old backup is requeued and the failure is remembered per pawn+weapon for
  60,000 ticks so it cannot spam drop/restore pairs.
- `SidearmCandidate.UpgradeAfterDrop` marks candidates that are blocked only by
  SS capacity, so the UI can say `upgrade (drop first)` without guessing.

Not runtime-tested: the game was running. Build is clean and the reflection
signatures match the decompiled 1.6 methods. The first live checks should be:

1. A ranged pawn's detail shows only ranged weapons under `Hand slot` and only
   melee weapons under `Backup slot`; the Monosword is absent from the hand list
   and appears under backup as `Too heavy...` for Basic-preset pawns.
2. A melee pawn shows the mirror: melee hand list, ranged backup candidates.
3. With a free SS slot, a better same-type backup is picked up first and the old
   one dropped after (`Queued backup`, `Dropped backup`).
4. At the slot cap, the log shows `replacing X (+N%, old one dropped first)` and
   no `Restored backup` unless SS genuinely refuses.
5. No repeated swap loop and no `Simple Sidearms carried-primary check failed`
   warning.

---

## 0.6.1 — fix drop-first pre-check and log spam

The user loaded 0.6.0 and saw repeated pairs of entries: `Backup upgrade
skipped` + `Restored backup` for six pawns, every swap cooldown. Armory was
dropping each pawn's good gladius/knife, failing to add the Monosword, and
restoring the old backup 41 seconds later.

Root cause: **`StatCalculator.isValidSidearm` does not implement
`LimitModeSingleSidearm.RelativeWeight`.** Its switch handles only
`AbsoluteWeight` and `Selection`, while `CanPickupSidearmType` separately checks
the relative mass. The user's SS preset uses
`LimitModeSingleMelee=RelativeWeight` (1.9 kg on a body-size-1 pawn). The
drop-first pre-check therefore accepted a 2 kg Monosword, dropped the old
backup, then SS's real `CanPickupSidearmInstance` rejected it with
"Too heavy for a sidearm melee".

Fixes:

- `SimpleSidearmsAdapter.IsValidSidearmForPawn` calls `isValidSidearm`, then
  applies the missing relative-mass check from SS's settings
  (`SeparateModes`, `LimitModeSingle*`, `*_RelativeMass`) and
  `MassUtility.Capacity`. It also mirrors SS's pacifist tool rule
  (`CombinedDisabledWorkTags & Violent`, fire extinguishers/stat-offset tools).
- Drop-first now requires `CanCheckSidearmType`, `CanCheckSidearmUse`, and
  `CanCheckRelativeLimits`; if any API is missing, Armory refuses to drop
  rather than guessing.
- Failed drop-first attempts are remembered per pawn+weapon for 60,000 ticks,
  so an unforeseen post-drop refusal cannot spam drop/restore pairs.
- The failure is one Log entry (`Upgrade skipped`) with the SS reason first,
  instead of two truncated entries. The Action column is wider.
- `UpgradeAfterDrop` is false for too-heavy or pacifist-blocked candidates, so
  the UI no longer offers `upgrade (drop first)` for a Monosword that SS will
  reject.

Verified in the isolated game with a temporary scaffold (steel knives in
inventory + a heavy melee weapon spawned, then reverted):

- `Steel longsword ... Too heavy for a melee sidearm` with **no**
  `[drop-first]` marker.
- The planner did not attempt the heavy weapon.
- A valid same-type upgrade still queued (`replacing Steel knife (+40%)`).
- Build clean, no exceptions.

The live drop-first success path (a valid better backup at the slot cap) still
needs a live-save check.

---

## 0.6.2 — show why automation is not acting

The user reset pawns by right-clicking weapons in the Simple Sidearms gizmo.
Some pawns were re-armed, but Trebo (unarmed, every candidate `ready`) got no
job. Armory had no UI for automation-skip reasons, so "hand ready" was
unexplained.

Added `PawnAnalysis.AutomationNote` / `AutomationBlocked`, computed read-only by
`ArmoryPlanner.GetAutomationNote`:

- hard gates: not on map, not free colonist, dead, downed, mental state, under
  minimum age, incapable of violence (pacifist), cannot manipulate, drafted with
  equip-drafted off, no job tracker, already equipping, SS sidearm job running,
  bonded persona weapon;
- per-pawn pauses: manual weapon change (`Ns` remaining), swap cooldown (`Ns`).

UI:

- Pawns status shows `paused: ...` when there is an actionable hand/backup wish,
  or `blocked: ...` for a hard gate.
- Pawn detail shows `Automation: ...` above the weapon sections.
- Demand ignores hard-blocked pawns (pacifists, children, downed, etc.) so their
  impossible wishes no longer inflate the shopping list.

This is diagnostic only; the planner's behavior is unchanged. The most likely
explanations for Trebo are the manual-change pause from the gizmo reset, being
drafted with `Equip drafted colonists` off, or a bonded persona weapon, which
Armory deliberately leaves alone. The new `Automation:` line names the exact one.

---

## 0.6.3 — narrow the bonded-persona rule

The 0.6.2 diagnostic identified Trebo as `blocked: bonded persona weapon`.
`Pawn_EquipmentTracker.bondedWeapon` is set when a persona weapon is coded to a
pawn and persists even when the weapon is in the bag or on the ground. Armory
was treating any non-null `bondedWeapon` as "do not manage this pawn at all," so
after the user dropped the persona monosword Trebo stayed unarmed indefinitely.

The rule is now narrower:

- `GetHardBlock` no longer rejects bonded-weapon owners.
- `PawnAnalysis.HandLocked` is true only when the bonded weapon is the **currently
  equipped** weapon (`equipment.Primary == equipment.bondedWeapon`).
- Hand-locked pawns skip the hand pass; the backup pass still runs. The UI shows
  `Hand locked: bonded persona weapon in hand` and `backup ... (hand locked)`.
- If the bonded weapon is in the bag or on the ground, the hand is not locked:
  Armory may equip a different hand weapon. This can trigger the persona
  weapon's "wielded another weapon" mood penalty, so the detail warns about it.
- The backup pass never drops a persona weapon (`CompBladelinkWeapon` is excluded
  from drop targets), so narrowing the hand rule cannot break a bond.

Verified in the isolated game with a temporary scaffold (reverted and rebuilt):

- Bonded weapon in inventory, pawn unarmed: `automation ok; hand locked no`, and
  Armory queued a hand weapon.
- Bonded weapon equipped: `automation ok; hand locked yes`, and Armory did not
  queue a hand job for that pawn; other pawns still received jobs.
- No exceptions, build clean.

Live check: Trebo should now be armed after the persona weapon is dropped, and
the detail should show `Bonded persona weapon is not in hand; equipping another
weapon may cause a mood penalty.` If the persona weapon is re-equipped, the hand
locks again and backups remain managed.

---

## 0.6.4 — clearer decision log

The Log tab's Action and Detail columns read as one run-on phrase
(`Queued unarmed`, `Queued backup first melee backup`) because Action was
right-aligned against Detail. The log now says what happened and why:

- columns are `Ago | Colonist | Action | Weapon | Reason`;
- actions are `Equip hand`, `Equip backup`, `Drop backup`, `Skip upgrade`,
  `Drop failed`, `Restore backup`;
- reasons are short English statements instead of codes:
  `No hand weapon equipped`, `36% better than equipped Carbine (excellent)`,
  `No melee backup carried`, `38% better than carried Steel mace (good)`,
  `... (old one dropped first)`, `Replaced by Chain shotgun (good)`,
  `Simple Sidearms refused it: <reason>; the old backup was restored`;
- equip actions are green, `Drop backup` is blue-grey, failures/skips are amber;
- `DecisionKind` replaces the free-form action string, so labels and colours are
  mapped in one place (`MainTabWindow_Armory.DecisionLabel/DecisionColor`).

No planner behaviour changed. Build clean.

Observation from the user's live log: the same pawn could be re-queued every
~41 s (the 2500-tick swap cooldown) when the queued job had not been taken yet
(pawns sleeping/busy, or a job the pawn cannot start). That is expected retry
behaviour, not a swap loop; if it persists while a pawn is idle and awake, the
next suspect is Simple Sidearms re-promoting its remembered primary.

---

## 0.6.5 — explain Simple Sidearms' capacity blockers

The Weapons tab showed SS's raw, terse failures side by side — `Slots full` and
`No free space` — and they read like the same thing.

They are not:

- `SidearmPickupFail_AllSlotsFull` / `MeleeSlotsFull` / `RangedSlotsFull` — SS's
  **slot count** limit (`LimitModeAmount* == Slots`) is reached.
- `SidearmPickupFail_NoFreeSpace` — `MassUtility.FreeSpace(pawn)` is smaller
  than or equal to the weapon's abstract mass. This is the pawn's inventory
  **weight** capacity, not map space and not slots. SS checks this **before**
  counting slots, so a pawn can be at the slot cap and still report only the
  weight failure.

`SimpleSidearmsAdapter.ExplainPickupFailure` rewrites the recognized keys:

- `All sidearm slots are full` (or `All melee/ranged sidearm slots are full`);
- `No free carrying capacity (needs X kg, Y kg free)`, using the same abstract
  def mass and `MassUtility.FreeSpace` that SS compares, so the numbers match
  SS's own check.

Localization-safe: the comparison resolves SS's keys through `Translator` and
compares the localized text, so it works in any language. Unknown reasons pass
through unchanged. Display only; SS remains the authority for decisions.

Used by `Eligibility.CheckSidearmRules`, so the Pawns/Weapons detail, `By fit`
lists and Demand all show the clarified text. Build clean.

---

## 0.6.6 — column widths for long reasons

The longer blocker text exposed a layout problem in the Weapons detail's
`Colonists by fit` list: the reason column was fixed at 194 px while the name
column took everything else (`width - 270`), so `No free carrying capacity (...)`
was truncated.

- `Colonists by fit`: colonist name is now clamped to 100–190 px (28 % of the
  pane), score 52 px, and the reason/status column takes the remaining width
  (~380 px at the default detail width), left-aligned.
- Pawn detail: hand and backup candidate status columns widened from 120 to
  200 px (left-aligned); the `Incompatible with this pawn` reason column widened
  from 156 to 200 px. Weapon-label columns shrank accordingly.
- No text changed; this only reallocates space.

Build clean.

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

Sidearm assignment test (0.4.0):

- Test-only fourth weapon spawned and forbidden check bypassed (both reverted
  before commit).
- Primary jobs queued first; on a later planner pass Armory queued
  `sidearm Steel knife for Keller (1/1 sidearms; opposite type)`.
- No exceptions from `EquipSecondary`, SS's job driver, or Armory.
- SS slot/mass checks were exercised through `CanPickupSidearmInstance`.
- A later run exposed a real edge case: a ranged-role pawn got a melee primary
  because the ranged weapons were claimed, then Armory added a ranged sidearm;
  SS promoted it and Armory logged a manual change. Fixed by requiring the
  primary to match SS's preferred type before adding sidearms, and by not
  arming unarmed pawns with sidearms. Re-run is stable (no manual-change
  pause).
- Final run: two sidearms queued (an opposite-type knife for a ranged pawn, and
  a same-type revolver when melee stock was exhausted); candidate fallback
  across claimed weapons worked; no manual-change pause.
- Exact free-slot display and mass-budget UI still need a live-save check.

Typed slot test (0.6.0), with a temporary unforbid scaffold (reverted and
rebuilt before commit):

- 3 colonists, 8 loose weapons, SS active.
- Roles resolved from SS mode: two melee pawns, one ranged pawn.
- Melee pawns' hand lists contained only the melee knife; their backup lists
  contained the bolt-action rifle (`Too heavy for a ranged sidearm`) and the
  revolver (`ready`).
- The ranged pawn's hand list contained the rifle and revolver; its backup list
  contained the melee knife.
- Planner queued a knife for a melee pawn, a rifle for the ranged pawn, and a
  revolver as the first ranged backup for the melee pawn.
- No cross-type entries in the hand lists and no exceptions.
- The drop-first upgrade path and slot-cap restore were not exercised because
  generated pawns had no carried backups; live-save check pending.

### User save

- 12 colonists, 40 weapons.
- Auto-equip works.
- Pacifist child/pawn skipped as intended.
- Auto Arm is installed; banner/guard behavior works.
- Pod pawns listed but still `Not on the map` in 0.2.2.
- Simple Sidearms is installed; 0.3.0/0.4.0 have not been run on this save yet.

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
2. **Per-pawn preferences**: `PreferredUnarmed` is an automation veto. A
   carried `DefaultRangedWeapon` / `PreferredMeleeWeapon` is shown as a
   preference and is included in the carried-weapon baseline, but deliberately
   is **not** a hard blocker — SS only enforces it among carried weapons, and
   blocking on it stopped every upgrade on a live save.
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

Not done in M3 (now in M4): choosing/equipping sidearms and SS limit handling.

---

## Milestone 4 (0.4.0, revised in 0.6.0) — typed hand/backup slots

Backup management is off by default. The hand ranking is always active.

Behaviour:

1. **Role**: SS's per-pawn primary mode decides the hand type (`Ranged` /
   `Melee`; `BySkill` resolves through `getSkillWeaponPreference`). If SS has no
   mode, the current weapon type is used, then the pawn's better combat skill.
   When SS is absent or the respect setting is off, there is no typed model and
   all weapons compete for the single vanilla hand slot.
2. **Hand slot**: loose weapons of the role type only. `Eligibility.Check` keeps
   the SS forced/role gates and the normal world checks. The baseline is SS's own
   QuickSwitch choice (`findBestRangedWeapon` / `findBestMeleeWeapon`,
   dangerous-skip setting respected, `includeRangedWithBash: false` for melee),
   plus the current weapon if it is the role type. Hand upgrades use the vanilla
   `Equip` job; SS's patched equip updates its remembered preference.
3. **Backup slot**: loose weapons of the opposite type, validated by
   `CanPickupSidearmInstance` (slots, mass, selection, pacifist tools, biocoding,
   role). Hard failures are dropped from the list; soft blockers remain visible.
   Target is exactly one backup; extras already carried are left alone.
4. **Upgrades**: only the worst carried weapon of the **same** type is replaced.
   Never drop the last weapon of a type; never drop one type to add the other.
   With a free SS slot, queue `EquipSecondary` first, then
   `DropSidearm(old, true, true)`. At the cap, drop first, re-check SS, queue the
   new weapon, and requeue the old one if the check still fails.
5. **Jobs**: `DefDatabase<JobDef>.GetNamedSilentFail("EquipSecondary")`, then
   `JobMaker.MakeJob(def, weapon)` + `TryTakeOrderedJob(..., true)`. If the def
   is absent, backup management disables itself with one warning.
6. **Safety**: any SS forced/unarmed/prefer-unarmed state suppresses backup
   jobs. Persona weapons and `destroyOnDrop` items are never drop targets; a
   backup is not dropped until the replacement passes the type/usability
   pre-checks. Armory never unforbids.
7. **UI**: Pawns list shows Hand / Backup / Status. Pawn detail has separate
   `Hand slot (type)` and `Backup slot (type)` sections. The Weapons tab shows
   `Hand`, `Backup`, `Hand blocked`, `Backup blocked`, `No user`; per-colonist
   status is `hand ready`, `hand (equipped)`, `backup ready`. Demand has a `For`
   column. Settings expose manage and upgrade only.
8. **Log**: `Equip hand` / `Equip backup` (with the reason it is better or why
   it is needed), `Drop backup`, and `Skip upgrade` / `Drop failed` /
   `Restore backup` when a drop-first upgrade is aborted.

Known gaps: no second backup; a bag full of extras can block the first backup
because Armory will not drop a hand-type extra to make room; no exact free-slot
display for mass-based SS limits (per-candidate checks only);
remembered-but-absent weapons are not candidates.

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
19. **SS per-pawn preferences:** `PreferredUnarmed` is a veto. A carried
    `DefaultRangedWeapon` (ranged role) or `PreferredMeleeWeapon` (melee role)
    is informational only and is covered by the carried-weapon baseline. Do not
    make it a hard blocker: SS enforces it only among carried weapons, and
    hard-blocking locked every pawn to their default on a live save.
20. **SS primary mode:** `primaryWeaponMode` values are `Ranged=0`, `Melee=1`,
    `BySkill=2`, `ByGenerated=3`. Resolve `BySkill` with SS's own
    `getSkillWeaponPreference` (passion, then level, ties → ranged). Only block
    the other type when the pawn already carries the preferred type.
21. **SS hand baseline:** `BestCarriedPrimary` is SS's own QuickSwitch choice
    (`GettersFilters.findBestRangedWeapon` / `findBestMeleeWeapon`), scored with
    Armory's model. `PawnAnalysis.HandBaselineScore` is the current hand weapon
    score (only if it matches the role type) or `BestCarriedPrimaryScore`,
    whichever is higher. Do not compare the equipped weapon's score when it is
    the other type: SS may not have switched yet and the scores are not
    comparable. If the reflection call fails, log once and use the current hand
    weapon.
22. **SS job guard:** skip pawns whose `curJob.def.defName` is
    `EquipSecondary`, `ReequipSecondary`, or `ReequipSecondaryCombat`.
23. **Do not call SS methods that mutate state** except `DropSidearm` for an
    approved upgrade. The adapter otherwise reads properties/fields and calls
    `GetMemoryCompForPawn`, `getSkillWeaponPreference`,
    `CanPickupSidearmInstance`, `canUseSidearmInstance`, and `isValidSidearm`.
24. **Backup pass runs after the hand pass.** `CanManage` still rejects pawns
    whose current job is `Equip`, so backups are normally added on the next
    planner pass. That is intentional; do not remove the guard to force same-tick
    backups.
25. **`EquipSecondary` is resolved through `DefDatabase<JobDef>`**, never
    referenced by type. If SS is absent, the def is missing, or
    `CanPickupSidearmInstance` did not resolve (`CanCheckSidearms` false),
    backup management disables itself.
26. **Backups use `Eligibility.CheckSidearmRules` + `CheckSidearmWorld`**, not
    `Check`: SS primary-mode/forced-state blockers must not leak into backup
    candidates, but SS's sidearm limits must. Keeping the world check separate
    lets the planner tell "SS has no room" apart from "the pawn cannot reach it".
27. **Backup count is derived from inventory combat weapons of the opposite
    type** (`PawnAnalysis.CarriedSidearms` + `IsBackupType`). Tools do not count.
    A stale snapshot is harmless because `lastSidearm` enforces the swap cooldown
    before another action.
28. **Backups are never removed without a replacement, and upgrades are
    same-type only.** There is no standalone removal and no unforbidding. Never
    drop the last weapon of a type, and never drop one type to add the other.
29. **Backups require the pawn to be holding the role type.** If SS prefers
    ranged and the current primary is melee, skip backups: adding a ranged backup
    makes SS promote it, which Armory then reports as a manual change. Also skip
    backups for unarmed pawns; they need a hand weapon first.
30. **Hand/backup typing uses strict `IsRangedWeapon` / `IsMeleeWeapon`**, not
    SS's broader "melee-capable" rule, so a gun with a stock bash is a ranged
    backup for a melee pawn, not a hand weapon.
31. **Weapons tab status is slot-based.** `Hand`/`Backup` mean a ready user in
    that slot; `Hand blocked`/`Backup blocked` mean only blocked users exist;
    `No user` means neither slot can use it. It is not a demand claim.
32. **Backup upgrades are add-first when there is room, drop-first at the cap.**
    With a free SS slot: check the candidate, queue `EquipSecondary`, then
    `DropSidearm(old, true, true)`. At the cap: pre-check
    `isValidSidearm` + `canUseSidearmInstance`, drop the worst same-type backup,
    re-check `CanPickupSidearmInstance`, queue the new weapon, and requeue the old
    one if SS still refuses. This is the only place Armory removes a weapon.
33. **Carried backup scores** come from `PawnAnalysis.CarriedSidearms` (combat
    weapons only). `BestCarriedBackup` is the highest-scoring one and is used for
    display/demand, not as a hard gate.
34. **Drop targets exclude** persona weapons (`CompBladelinkWeapon`) and
    `destroyOnDrop` items. The worst same-type backup is chosen; the equipped
    hand weapon is never a drop target.
35. **SS's `isValidSidearm` ignores `RelativeWeight`.** It handles only
    `AbsoluteWeight` and `Selection`; `CanPickupSidearmType` has the relative
    mass check. Use `SimpleSidearmsAdapter.IsValidSidearmForPawn`, which adds the
    relative check and the pacifist tool rule. Do not drop-first on
    `isValidSidearm` alone: a 2 kg Monosword under Basic passes it but fails the
    real check, producing drop/restore spam.
36. **Failed drop-first attempts are remembered** per pawn+weapon for 60,000
    ticks (`failedDropUntil`). This is session-only; clear it by restarting or
    letting it expire. Add-first failures are not remembered because nothing was
    removed.
37. **Do not force-close the user's running game for quicktests.** The isolated
    test script refuses by default; pass `-Force` only with explicit permission.

---

## Pending verification (0.6.6)

The immediate checks are:

- Weapons detail, `Colonists by fit`: the full reason text is visible for long
  blockers such as `No free carrying capacity (needs 2 kg, 0.7 kg free)`, and the
  colonist name column is narrower.

- A blocked backup shows `All sidearm slots are full` when SS's slot count is
  reached, and `No free carrying capacity (needs X kg, Y kg free)` when the
  pawn's inventory weight is the limit. The two must never be used
  interchangeably.

- The Log header reads `Ago | Colonist | Action | Weapon | Reason`; actions are
  `Equip hand` / `Equip backup`, and reasons read as sentences
  (`No hand weapon equipped`, `No melee backup carried`,
  `36% better than equipped ...`).
- On the reset save, unarmed pawns that are not being armed show a `paused:` or
  `blocked:` reason in the Pawns list and an `Automation:` line in the detail.
  If it is `manual weapon change (Ns)`, that is the 250-second pause from
  dropping weapons through the Simple Sidearms gizmo; wait it out or lower the
  pause in Settings.
- If it is `drafted`, enable **Equip drafted colonists** or undraft.
- A bonded persona weapon is no longer a blanket block. If it is the equipped
  hand, the pawn shows `hand locked: bonded ...` but backups are still managed.
  If it is in the bag or on the ground, Armory can arm the hand; the detail warns
  about the persona mood penalty. Trebo should be armed after dropping the
  persona weapon.
- A ranged pawn's detail shows only ranged weapons under `Hand slot (ranged)`
  and only melee weapons under `Backup slot (melee)`. The Monosword is absent
  from the hand list and, for a Basic-preset pawn, appears under backup with
  SS's too-heavy reason.
- A melee-mode pawn shows the mirror: melee hand list and ranged backup
  candidates.
- On the reported save, the Weapons tab no longer names a shooter as the
  Monosword's best user unless it is a valid melee backup; the status should be
  `Hand` for a melee pawn or `Backup blocked` for a ranged pawn.
- With a free SS slot, a better same-type backup is picked up first and the old
  one dropped after. The Log shows `Equip backup` and `Drop backup`.
- At SS's slot cap, the Log shows `Equip backup` with
  `... (old one dropped first)`; `Restore backup` only appears if SS genuinely
  refuses the replacement.
- A too-heavy candidate (Monosword under Basic) shows `Too heavy...` and does
  **not** produce drop/restore entries. The Log should stay quiet for it.
- The old backup stays on the ground and is not immediately retrieved by SS.
- No repeated swapping against SS over a few minutes; no
  `Simple Sidearms carried-primary check failed` warning.
- Then confirm the earlier items:
  - Simple Sidearms is detected (pawn detail shows `Simple Sidearms: primary ...`);
  - forced primaries / forced unarmed / prefer unarmed are shown and the pawn
    is left alone;
  - the hand list never mixes the other type, and the role line names the
    source (`SS mode`, `current weapon`, `combat skill`);
  - SS slot/mass/selection limits are respected (candidates are blocked, not
    forced);
  - 0.2.3 items: pod label, age 3, header alignment, weapon column gaps, no beer.

If a forced state is not detected, log a snapshot (debug setting or dev action)
and check the `SS:` lines. The adapter disables itself with one warning if the
SS memory API does not match.

---

## Milestone 5 plan — polish and packaging

Milestone 4 shipped in 0.4.0; its implementation notes are above. Next work is
polish, not new core behaviour.

1. **Backup refinements**
   - show exact free capacity when SS uses `Slots`;
   - show a mass/weight estimate when SS uses weight limits;
   - optional second backup / trimming extras, if the user wants it;
   - include remembered-but-absent SS weapons as candidates when they exist on
     the map.
2. **Automation transparency**
   - add an `AutomationNote` per pawn (pacifist, drafted, bonded, under age,
     competitor guard, SS forced state, SS job running, cooldown, below
     threshold) and show it in the pawn detail;
   - optionally log one skip reason per pawn per session to the Log tab.
3. **Container scanning**
   - include weapons in transport pods and other player containers as
     candidates, with the container as the reachability target;
   - keep pawn inventory out of primary candidates (those are sidearms).
4. **Dropped weapons**
   - optional auto-haul of dropped weapons to a stockpile, or a one-click
     "haul dropped weapons" action. Do not expand this into general hauling.
5. **Raid behaviour**
   - an explicit button/setting to arm everyone from ready stock when a raid
     starts; respect SS forced states/manual protection and never unforbid.
6. **UI polish**
   - finish minor alignment; review the 1280×720 layout on smaller screens;
   - consider a sidearm column/count marker in the Pawns list.
7. **Localization and packaging**
   - move English literals to keys;
   - `LoadFolders.xml` for future versions;
   - Workshop metadata/preview and a release checklist.
8. **1.7 / Waylith**
   - re-verify SS and vanilla APIs; net10.0/Harmony 3 split.

**Open design questions**

- Sidearms for all free colonists (current) or only pawns Armory gave a primary?
- Should Armory ever remove/replace a sidearm? (Current: no.)
- Should raid arming be automatic or a button?
- Is the sidearm candidate list in the pawn detail enough, or is a dedicated
  tab/section wanted?

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
- **Simple Sidearms**: primary coexistence shipped in 0.3.0; sidearm
  assignment shipped in 0.4.0, with upgrades in 0.5.0. Standalone removal is out
  of scope; upgrades always add before dropping. Simple Sidearms' own QuickSwitch think node enforces its primary
  preference among carried weapons, so Armory must not queue a primary SS would
  undo.
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
| 0.4.0 | Add-only sidearm assignment via `EquipSecondary`, SS limit checks, type diversity, desired count, sidearm candidate UI |
| 0.4.1 | Fix over-strict SS default-ranged/preferred-melee block that stopped all primary upgrades; pair is now informational |
| 0.4.2 | Fix settings page multi-column overflow hiding later options; clarify `SS role: ranged/melee`; document per-pawn SS mode |
| 0.5.0 | Sidearm upgrades (replace worst same-type), Weapons tab sidearm status, per-pawn `sidearm ready`, replace setting |
| 0.5.1 | Explicit SS role blockers + detail explanations; baseline uses SS's actual QuickSwitch choice |
| 0.6.0 | Typed hand/backup slots; same-type-only upgrades; mirrored ranged/melee loadouts; add-first or drop-first at SS's cap; separate Hand/Backup UI and Demand column |
| 0.6.1 | Fix drop-first pre-check (SS `isValidSidearm` ignores RelativeWeight; pacifist tools); remember failed drop-first attempts; clearer log |
| 0.6.2 | Show automation-skip reasons (`paused:`/`blocked:`) in Pawns and detail; demand ignores hard-blocked pawns |
| 0.6.3 | Narrow bonded-persona rule: only lock the hand while the bonded weapon is equipped; allow hand/backup management otherwise |
| 0.6.4 | Clearer Log tab: Action before Weapon, readable spacing, `Equip hand`/`Equip backup` labels, `Reason` column with plain-language reasons |
| 0.6.5 | Expand Simple Sidearms' `Slots full` / `No free space` into distinct explanations (`All sidearm slots are full`, `No free carrying capacity (needs X kg, Y kg free)`) |
| 0.6.6 | Widen reason/status columns (Weapons `Colonists by fit`, pawn hand/backup/incompatible lists) so long blockers fit |

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
