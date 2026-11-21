# Handoff / project state

**Snapshot:** Armory **0.10.6**, RimWorld **1.6.4871**.
Build is clean (0 warnings, 0 errors). No test hooks remain in source.
Git repository on `main`; 0.2.3 through 0.10.6 are committed; `v0.6.7-stable`
tags the last pre-Demand-rework build.

**Links:** [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3842538224) ·
[GitHub](https://github.com/fklemme/Armory)

---

## Where we are

Milestones 0–4 are implemented:

- **0** — mod skeleton, `MainButtonDef`, tab, settings, build/junction.
- **1** - ranking, blockers, Colonists/Weapons/Upgrades tabs, factor ledger.
- **2** — planner (claims, cooldown, threshold, manual protection; default on
  since 0.9.0), session decision log, Log tab.
- **3** — Simple Sidearms primary coexistence via a reflection adapter:
  forced states, preferences, primary mode, carried-primary baseline, SS job
  guard, sidearm display.
- **4** — typed hand/backup slots (0.6.0): one hand weapon of the SS mode type
  and one opposite-type backup, mirrored for melee pawns; same-type-only
  upgrades; add-first or drop-first depending on SS slots; separate Hand/Backup
  rankings and Weapons/Upgrades status.

**Next:** milestone 5, polish and release prep. 0.7.0 turned the Upgrades view
into an ideal-loadout build plan; remaining polish is container scanning,
auto-haul, raid arming, localization, and Workshop prep. The first Workshop item
(0.9.2) is uploaded and hidden while Steam's content check runs. 0.10.0 added the
typed hand model, passion-based role assignment and automatic hand-type
alignment; 0.10.1 polished the UI and list limits; 0.10.2 hardens backup
management and unusable-backup baselines; 0.10.3 adds double-click map jumps;
0.10.4 adds weapon comparison, material-specific upgrade variants and role
filters; 0.10.5 polishes the accuracy columns, table widths and detail header;
0.10.6 shows accuracy as percentages and replaces text costs with icon/count
groups. Upload once the private test is good enough.

A range/role scoring pass shipped in 0.8.0, with an unbonded-persona guard in
0.8.1, a Source-column encoding fix in 0.8.2, an interface wording pass in
0.8.3, the Colonists/Upgrades tab rename in 0.8.4, the icon/tab-order pass in
0.8.5, the simplified crossed-weapons icon in 0.8.6, the release text, banner and
default-automation pass in 0.9.0, colonist-bar ordering in 0.9.1, and release
staging plus the `Best vs current` column in 0.9.2, the typed-hand/auto-align
pass in 0.10.0, UI/list-limit polish in 0.10.1, backup-safety fixes in 0.10.2,
double-click map jumps in 0.10.3, comparison/material/role insights in 0.10.4,
accuracy-column/table-layout polish in 0.10.5, and percentage accuracy/icon costs
in 0.10.6. Research, reference-mod behaviour, the shipped model and remaining
decisions are in [`docs/RANGE_AND_ROLES.md`](RANGE_AND_ROLES.md).

---

## 0.10.6 — percentage accuracy and icon costs

- Accuracy values in the separate Weapons columns now include `%`, matching AP.
  The detail line uses the same percentage format.
- Upgrades Cost now draws each ingredient as its item icon followed by an `x`
  count, like the crafting menu. Multiple ingredients fit in one cell; overflow
  shows `+N` and the tooltip still lists everything.
- Each count is orange when current stock cannot cover all needed copies; the
  redundant `!` suffix is gone.
- `Wanted by`, `Owned` and `Needed` are all 58 px wide for a clean integer
  column group; Cost grew to 136 px and Source remains 246 px.
- Verified on the isolated live save at 1600x900: obsidian/plasteel counts are
  orange, uranium is normal, and multi-ingredient rows show icons for both
  resources (e.g. plasteel x140 + advanced component x10). Build clean.

---

## 0.10.5 — accuracy columns and table-layout polish

Follow-up to 0.10.4 after inspecting the new screens on the live save.

- Upgrades header text moved up 2 px with a taller text rect so the descenders
  are no longer clipped; the role row and table header moved down 1–2 px to
  match.
- Upgrades `Wanted by` narrowed from 74 to 62 px and `Owned` from 92 to 76 px;
  `Source` grew from 214 to 246 px. Removed the redundant ` · shortage` suffix
  from Source (the orange cost `!` already marks it), so bench names now fit.
- Weapons table widened from 60% to 66%; each accuracy band is now its own
  sortable column (`aC`, `aS`, `aM`, `aL`) instead of one packed string.
- Weapons detail pane narrowed to 34%; its stat header is split into compact
  Tiny rows:
  - `Melee/Ranged | DPS | DPSA | AP | Mass`
  - `Rng | Acc C/S/M/L | Brst`
  - `Aim | Cooldown`
  This removes the clipped `Cooldown` value on ranged weapons.
- Verified with the isolated live save at 1600x900: Upgrades header fully
  visible and source names no longer truncated for normal benches; Weapons shows
  separate accuracy columns and a compact, complete detail header. Build clean.

---

## 0.10.4 — comparison stats, material variants, owned rows and role filters

Quartermaster research showed that its Weapons tab ranks a global catalog by an
intrinsic `DPSA x (1 + 0.75 AP)` score, not by per-colonist fit. 0.10.4 applies
the useful presentation and craft-planning lessons without replacing Armory's
per-pawn scoring.

- **Weapons comparison columns:** raw DPS, generic DPSA, AP, accuracy at
  Close/Short/Medium/Long, range and best-ready colonist. Headers sort the list;
  the default sort is DPSA. DPSA is explicitly catalog-only (unreachable bands
  count as zero), while the recommendation still uses the pawn's real range,
  skill and fit.
- **Role filters:** All / Melee / CQB / Assault / Line / Marksman on both
  Weapons and Upgrades. Weapons show the best pawn and role score; Upgrades
  restrict rows to wants from that role and show that role's score. The weapon
  detail adds a Best by role table.
- **Material-specific Upgrades:** stuffable weapons are keyed by def + stuff.
  Steel/plasteel/uranium/etc. are separate rows with their own power, gain,
  owned count, need, per-copy material cost and stock status. Only materials
  with stock/owned copies plus the highest-scoring material are shown, to keep
  the list manageable; Show owned reveals the rest of the owned copies. The planner scores
  a normal-quality probe of that material.
- **Material-aware bills:** `DemandCrafting.TryAddBill` accepts a stuff def and
  restricts the `Bill_Production.ingredientFilter` to it while keeping fixed
  ingredients allowed. Verified in the isolated save: an obsidian combat knife
  bill allowed obsidian and rejected steel/plasteel, with
  `limitToAllowedStuff=true`.
- **Cost and shortage display:** `Cost` shows the main material per copy and an
  orange `!` when stock cannot cover all needed copies; the tooltip lists every
  ingredient, current stock and the total for the needed copies. Bills can still
  be queued while short, matching the intended material trade-off.
- **Show owned:** adds an Owned section for owned material variants, using the
  best actual quality/material copy's label and power, with no crafting cost.
  Owned rows are optional and remain visible under role filters.
- **Score transparency:** the formula is in the Upgrades header and weapon
  detail; tooltips explain DPS/DPSA, AP, accuracy, cost and role scores.

Verified on the current save with the isolated live harness: 55 demand rows,
18 material variants, 39 owned rows; Upgrades renders material costs and
shortages, role filtering changes the score/order, Weapons shows DPSA and role
best fits, and the material-specific bill filter works. Build clean.

---

## 0.10.3 — double-click map jumps

Double-clicking a row in the **Colonists** or **Weapons** list now closes Armory,
moves the camera to that pawn or loose weapon, and selects it, so a listed item
can be found on the map in one gesture.

- Uses `CameraJumper.TryJumpAndSelect`, the same path as the colonist bar, so
  selecting an item on another map works if one is ever listed.
- Only spawned things jump. A despawned colonist (pod, casket, caravan, another
  map) is a no-op rather than a misleading camera move.
- Detection uses a left `MouseDown` with `clickCount == 2` inside the row and
  consumes that event so the normal single-click selection does not race it.
- Tab tooltips and the Weapons-tab note mention the double-click action.
- Verified with the isolated live harness on the current save: jumping to Trebo
  centered the camera on him with the inspect pane open; jumping to Talia's
  persona monosword did the same for the weapon. Both left Armory closed with
  `openTab=Inspect`. Build is clean.

---

## 0.10.2 — backup accumulation, biocoded guards and sane upgrade baselines

Investigating a save where Trebo (Green) ended up with three ranged backups:
Duskgutz's Howl, a revolver, and a biocoded sawed-off shotgun coded to another
pawn. Simple Sidearms' `rememberedWeapons` contained all four weapons (the bonded
monosword plus three ranged), so SS considered all three as sidearms. Armory's
decision log is session-only and was not in the save, so the exact add sequence
cannot be replayed, but the code had two concrete problems.

- **Add-first upgrades could accumulate.** When a free SS slot existed, Armory
  queued the replacement backup first and dropped the old one after. If the drop
  failed or SS re-retrieved the dropped weapon, two backups remained; a later pass
  could add a third. Backup upgrades now **drop the old backup first**; if the
  drop fails, nothing is added, and if SS rejects the replacement the old one is
  restored. If more than one backup is already carried, the planner stops adding
  and reports **`Multiple backups`**.
- **The worst-backup baseline counted unusable weapons.** The biocoded sawed-off
  shotgun had a near-zero score for Trebo, so the Upgrades tab showed thousands of
  percent gain for ordinary weapons. Carried weapons now record whether they are
  usable (`Eligibility.CanUseCarriedWeapon`), and unusable backups are excluded
  from `TryGetWorstBackup`, the best-carried backup, and one-per-pawn demand. The
  Backup list marks them `(unusable)` and the detail explains:
  `Multiple backups carried. Drop extras manually; Armory will not add more.`
- Added an explicit **biocoded-to-another-pawn** guard in `Eligibility`, so Armory
  can never add such a weapon even if another check misses it.
- Existing extra backups are **not** dropped automatically; that would risk
  deleting a player/SS-owned weapon. The status and detail tell the player to drop
  the extras.

Verified on the current save with SS enabled: Trebo and Gripe show
`Multiple backups`, the biocoded sawed-off is marked unusable, and the Upgrades
tab no longer shows five-digit gains (the worst remaining backup produces normal
percentages; the largest row was +156%). Build is clean.

Pending: after the user manually drops the extra/biocoded backups, verify that
backup management resumes with exactly one valid backup.

---

## 0.10.1 — hand-type order UI, current-weapon cleanup, backup column and list limits

UI and settings polish after the 0.10.0 typed-hand pass.

- Corrected the **Change type** tooltip: the automatic order is a bonded weapon,
  then a worn ranged-blocking shield belt, Brawler, the higher Shooting/Melee
  passion, and finally skill (melee needs a 3-level advantage; otherwise ranged).
  The implementation already followed that order; the tooltip incorrectly said
  "passion first, then Brawler, shield belt".
- Removed the current weapon name from the pawn detail's hand-type line. The
  current weapon already appears in the Colonists list's **Hand** column; the
  hand-type line now shows only the resolved type and source, with the current
  score available in the tooltip.
- The **Backup** column is hidden when Simple Sidearms is not loaded, and the
  **Hand** and **Status** columns widen to use the space. With Simple Sidearms
  loaded, the Backup column is unchanged. The pawn detail shows
  `Backup slot: requires Simple Sidearms.` when the slot is unavailable, or a
  note when SS has no sidearm data for that pawn.
- Unified slot labels across the tabs: `Hand`, `Backup` and `Both` are Title Case
  and share the same colors everywhere (green hand, blue backup, cyan both;
  blocked stays orange). This fixes the Weapons tab using color-coded Title Case
  while Upgrades used lowercase, same-color labels.
- Removed the redundant `carries the preferred type` suffix from the Simple
  Sidearms summary. It was shown for nearly every SS pawn and used a whole line
  without adding a decision. Specific preferred-pair text is still shown when SS
  actually has a carried default/preferred pair.
- Fixed a false **`Already carried`** status on persona/unique weapons. Ordinary
  weapons still compare by def, but persona (`CompBladelinkWeapon`) and unique
  (`CompUniqueWeapon`) weapons now compare by exact instance. Talia is no longer
  treated as a duplicate of Trebo's different bonded monosword. Unbonded persona
  weapons remain manual-only, and the backup planner explicitly skips them so it
  cannot create a permanent bond as a side effect.
- Shortened list statuses so they fit the status column:
  `Blocked: ...` instead of `Cannot automate: ...`; `Equipped (hand locked)` when a
  bonded weapon is intentionally kept and there is no pending action; `Hand locked`
  only when a hand upgrade is actually blocked; and `Backup ready` /
  `Backup upgrade ready` when the hand is locked. Added **`Backup waiting`** for
  the case where Simple Sidearms has not switched to the resolved hand type yet,
  so `Backup ready` now only means a backup can actually be added; the pawn detail
  explains the wait.
- Clarified the hand-lock detail:
  `Hand locked: bonded weapon in hand. Armory will not replace it; backups still run.`
  A Simple Sidearms **forced weapon** is a different state and shows its veto
  reason (`Forced weapon: X`), not `Hand locked`.
- Candidate limits now use one 1–50 range for both hand and backup. Both default
  to **Auto**:
  - hand: 15 without Simple Sidearms, 5 with it;
  - backup: 5.
  Explicit values are preserved; the old hand default (10) and backup default (5)
  become Auto on migration. `settingsVersion` is now 5.
- The hand table's hard minimum is now 1 instead of 5, so a custom value of 1–4
  is honored.

Verified on isolated live saves:
- **No SS:** effective limits logged `hand=15, backup=5`; the Backup column was
  hidden, Hand/Status widened, the detail showed
  `Backup slot: requires Simple Sidearms.`, and the hand list showed 15 entries.
- **SS enabled:** effective limits logged `hand=5, backup=5`; the Backup column
  returned and the backup list showed 5 entries.
- The Settings page showed the Auto checkboxes and the 15-hand label without SS;
  the SS run resolved 5/5.
- A live SS check confirmed the Upgrades `Slot` values render `Hand`/`Backup`
  with the same colors as the Weapons tab, and Jerry's Simple Sidearms summary no
  longer shows `carries the preferred type`.
- Trebo's hand-locked bonded melee weapon with SS mode ranged now shows
  `Backup waiting`, not `Backup ready`; the detail explains that the backup waits
  for the hand type to match.
- After switching Trebo to SS melee, his carried ranged backup is recognized and
  the list shows `Equipped (hand locked)`, not an alarming `Hand locked`.
- Talia now shows `Persona: bond manually` instead of `Already carried`.
- Build is clean.

Pending: actual equip-job completion and the hand-type override save/load
round-trip, as before.

---

## 0.10.0 — typed hands, passion-based role assignment and automatic alignment

The no-Simple-Sidearms path previously had **no hand type**: `ResolveRole` returned
null, `IsHandType` accepted every weapon, and melee DPS (computed at contact) was
ranked directly against ranged expected DPS (computed over a distance band). On a
shooter-heavy save this put maces, zeushammers and monoswords on top of ranged
pawns. The fix is a typed hand slot, not a cross-type score penalty.

- Added `HandType` (`Auto`, `Ranged`, `Melee`) and a per-pawn **Hand type**
  override in the colony game component. It is saved with the colony and survives
  save/load, alongside the existing ranged-role overrides.
- Hand type resolution without Simple Sidearms:
  1. explicit player override;
  2. a bonded persona weapon currently in hand;
  3. a worn shield belt whose `CompProperties_Shield.blocksRangedWeapons` is true;
  4. `Brawler`;
  5. higher Shooting/Melee passion (`Major` > `Minor` > `None`);
  6. melee only when Melee is at least 3 levels above Shooting; otherwise ranged
     is the default (close skills therefore stay ranged).
- Simple Sidearms remains authoritative when active and respected: its per-pawn
  primary mode wins, the hand type is not marked as a mismatch, and the per-pawn
  Armory override is ignored while SS is in control.
- Only weapons of the resolved type enter the hand ranking. A wrong-type equipped
  weapon is excluded from the baseline, so the planner automatically equips the
  best ready weapon of the chosen type; if none is ready, the current weapon is
  kept and the UI reports the mismatch.
- The planner logs type changes as
  `Switching hand type from melee to ranged` / `... ranged to melee`.
- Backups remain Simple Sidearms-only: `IsBackupType`, backup candidates, backup
  demand and the backup UI are gated by the SS state. No-SS pawns use only the
  hand slot.
- UI: the pawn detail now shows **Hand type** with a `Change type` menu, a
  mismatch line, and a separate **Ranged role** with `Change range` (shown only
  for ranged pawns). The Colonists `Best vs current` column shows `switch type`
  for wrong-type pawns instead of a meaningless percentage; the Upgrades detail
  uses `switch type` instead of `new slot` for the same case.
- Hand-type and ranged-role override changes invalidate the snapshot so the
  planner cannot queue a job from the old type before the next refresh.
- Version bumped to **0.10.0**; `settingsVersion` stayed 4 in that pass because no new
  settings were added. 0.10.1 moves it to 5 for the automatic list-limit migration.

Verified on isolated copies of the 13-colonist
`Northwestern Esora Humanlike Confederacy` save:

- **No SS:** clear shooters with melee equipped resolved ranged and were marked
  `mismatch`; the planner queued `Switching hand type from melee to ranged` for
  Rathat, Existence, Elsie, Hashoytt, Chef and Gripe. The bonded bladelink
  monosword pawn resolved melee via `bonded weapon`; a forced `Gripe = Melee`
  override resolved `melee (manual hand type)` and was not queued for a ranged
  weapon.
- **SS enabled:** integration active; every pawn resolved `ranged (Simple
  Sidearms mode)`, the forced Gripe override was ignored as intended, backup jobs
  still queued, and no Armory errors appeared.
- The temporary live-test scaffold was removed and the development build rebuilt
  clean.

Pending verification: a save/load round-trip for the hand-type override (the
scribe follows the already-tested role-override pattern), the new detail layout at
1280x720, and actual equip-job completion on a running colony rather than only the
queued decisions.

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

## 0.6.7 — fit the long reason, section headers, numbering, tab position

Follow-up polish after seeing 0.6.6 on the live save:

- The weight failure is now the compact `Can't carry (0.85 kg, 0.3 kg free)`
  instead of `No free carrying capacity (needs 0.85 kg, 0.3 kg free)`. It fits
  the widened by-fit column without losing the two numbers.
- `Colonists by fit`: the name column is clamped to 100–170 px (was 190), so the
  reason column gets the rest.
- Pawn detail backup list: removed the trailing `melee`/`ranged` column, which
  only repeated the section header, and moved the score to the right edge. The
  weapon labels and the reason column both gained room.
- Hand list: the status column is 170 px (labels recovered 40 px).
- Both slot lists are now numbered (`1.`, `2.`, ...); the backup list was not.
- `SectionHeader` draws a translucent full-width divider and pure white text, so
  `Hand slot (...)` / `Backup slot (...)` / `Colonists by fit` read as table
  headers instead of body text.
- `MainButtonDefs.xml`: `order` 65 → 35. Vanilla orders are Architect 1, Work 10,
  Schedule 20, Assign 30, Animals 40, ...; 65 put Armory next to Quests, and 35
  puts it between Assign and Animals.

Build clean. Layout-only plus one Def order change; no planner or ranking
behaviour changed.

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

Backup management is on by default since 0.9.0. The hand ranking is always active.

Behaviour:

1. **Role**: SS's per-pawn primary mode decides the hand type (`Ranged` /
   `Melee`; `BySkill` resolves through `getSkillWeaponPreference`). If SS has no
   mode, the current weapon type is used, then the pawn's better combat skill.
   When SS is absent or the respect setting is off, 0.10.0 resolves Armory's own
   hand type (override, bonded weapon, ranged-blocking shield, Brawler, passion,
   then a 3-level melee skill advantage) and still filters hand candidates to that
   type; see the 0.10.0 section.
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
    display. Demand uses the **worst** carried backup as its baseline, matching
    the planner's replace-worst rule.
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
38. **Demand does not use `AutomationBlocked`.** That flag includes transient
    pauses (drafted, downed, mental state, cooldowns, an equip job running) which
    must not remove a colonist from the loadout plan. `IsDemandEligible` applies
    only permanent gates: on the map, alive, minimum age, violence-capable, can
    manipulate, has a job tracker.
39. **Hypothetical weapons are normal quality with default stuff.** Probes are
    unspawned `ThingWithComps` created once per def; never spawn them and never
    save them. Only the pawn's current-weapon baseline is instance-accurate.
40. **Research takes precedence over buying in the Demand sectioning.** Vanilla
    weapons mostly default to `Tradeability.All`, so "buyable" cannot exclude a
    research-locked def or the Locked section would always be empty. Locked rows
    append `· buy` when the trader route also exists.
41. **Owned defs must be added to the catalog before reading `All`.** Owned-only
    defs (persona weapons, quest rewards) are supply, but `Obtainable` keeps
    them out of the pick, so they never become build advice. An owned def whose
    recipe is research-locked still joins the build-now universe, otherwise a
    colony holding a forbidden rifle would be told to craft a knife. The row
    still renders under Locked, with `more: <project>` when only additional
    copies need research.

---

## 0.7.0 — Demand becomes a build plan

The old tab aggregated loose stock: one row per def, `Wanted` counted only
colonists whose best *available* weapon was that def, and `Blocked` mixed SS
refusals into the same list. With the good weapons already equipped, "best
available" meant leftovers, so the tab recommended mediocre weapons; seven pawns
blocked from a 2 kg monosword appeared as demand for monoswords.

Demand now computes an ideal loadout first:

1. **Candidate universe** — every weapon def the colony can obtain:
   - craftable now (recipe available, research finished);
   - craftable after unfinished research (still scored, shown as locked);
   - buyable from traders (`ThingDef.tradeability`);
   - already owned, even without a recipe (persona weapons, quest rewards).

   `Scoring/WeaponCatalog.cs` builds this once per session and creates one
   unspawned probe instance per def at **normal quality** with
   `GenStuff.DefaultStuffFor`, caching `WeaponPower`. It scans
   `DefDatabase<RecipeDef>.AllDefsListForReading` products, so recipes generated
   from `ThingDef.recipeMaker` (vanilla melee weapons) and modded recipes are
   included.
2. **Pick** — for each colonist and slot, take the highest-scoring obtainable def
   that beats the current baseline by `upgradeThreshold`. Hand uses
   `HandBaselineScore`; backup uses the **worst** carried opposite-type backup,
   matching the planner's replace-worst rule. Manual-only ranged weapons are
   excluded; SS def-level type/mass/pacifist rules are checked on the probe.
   Biocoding, bonds, slot counts and free capacity are deliberately ignored:
   they belong to instances and to "can I do it this second", not to demand.

   Two universes are evaluated separately: **build-now** (available, buyable, or
   no research gate) and **research** (missing prerequisite). A pawn can appear
   in both. The research pick is kept only when it beats the build-now pick by
   another `upgradeThreshold`, so "build this now" and "research towards that
   later" stay distinct and do not repeat the same advice.
3. **Net** — `Owned` counts equipped + carried + loose copies.
   `Need = max(0, Want - (Owned - Forbidden))`. Rows with `Need <= 0` are hidden.
4. **Section** — `Build` when craftable now or buyable; otherwise `Locked` with
   the missing research project. Research takes precedence because it is the
   actionable path; `Source` appends `· buy` when the trader route also exists
   (vanilla weapons mostly default to `Tradeability.All`, so the other order
   would make the Locked section permanently empty).

Transient state is ignored: drafted, downed, asleep, mid-job and full-bag pawns
still count. Permanent gates (minimum age, violence incapability, manipulation,
not on the map) exclude them.

UI: fixed columns `Weapon | Slot | Want | Owned | Need | Gain | Source`, section
headers (`Build or buy`, `Locked by research`), sorting by summed gain, and
click-to-expand per-colonist lines (`Kei · hand · +24% · now charge rifle`).
`Gain` is the average relative improvement over the current weapon; `new` means
the slot is empty. Header tooltips explain `Need` and the normal-quality
assumption.

Model: `DemandSection`, `DemandWant`, and the extended `DemandRow` (`Owned`,
`Available`, `Need`, `AverageGain`, `Wants`). The old `Blocked`, `Stock`, and
`Shortage` fields are gone. `Armory → Log weapon snapshot` now also logs the
catalog size and the first ten demand rows.

Verified in the isolated Core+Armory quicktest: the catalog built **30 defs, 30
obtainable**, demand rows were produced, and there were no Armory exceptions.
That run also caught a real bug: research-locked defs that the colony already
owned were excluded from the build-now universe, so the plan recommended knives
while a forbidden bolt-action rifle sat on the ground. Owned defs now always join
the build-now pass (their row still renders under Locked, with `more: <project>`
when only extra copies need research).

Not yet verified: the tab's rendering, and the live Simple Sidearms save. The
quicktest map has no SS and the harness never opens the Demand tab.

Superseded by 0.7.1 below: the single-pick plan became an option menu, and the
sections were renamed to Craft / Research / Buy.

---

## 0.7.1 — Demand as an option menu with craft/research/buy

Two changes after seeing 0.7.0 on the live save.

**Every upgrade is listed.** 0.7.0 picked only each colonist's argmax, so the tab
collapsed to one row (the heavy charge blaster was best for all eight ranged
pawns) and hid the alternatives. `AddSlotWants` now adds a want for *every*
obtainable def that beats the baseline by `upgradeThreshold`, not just the best.
Rows are independent options and wants overlap: a colonist can appear in several
rows for the same slot. `Need` still describes what one row alone would require.

To keep the list navigable:

- `DemandWant.IsTop` / `DemandRow.BestCount` mark the best def in its group for
  each colonist. The weapon name is white when `BestCount > 0` and grey for an
  alternative; the tooltip says which.
- Sorting is: section, then `BestCount > 0` first, then total gain, best count,
  need, def name.
- Research options must still beat the best available option by another
  threshold, so research never lists a weapon worse than something buildable.

**Sections follow action priority:** `Craft` (recipe available now), `Research`
(unfinished project; `· buy` appended when tradeable), `Buy` (no crafting route,
trader only). `DemandSection` is now `Craft` / `Research` / `Buy`, and the sort
relies on those enum values. In vanilla most weapon defs have recipes, so `Buy`
is usually empty; research-locked but buyable defs stay under `Research` by
design.

UI text: the header explains overlapping rows and the section order; `Want` and
`Source` have header tooltips. The row `Need` tooltip still explains that
unforbidding owned copies can replace some of the need.

Verified in the isolated quicktest: 17 demand rows for three unarmed colonists,
with `Knife (best 2)` first in Craft, grey alternatives after it, then Research
rows. No Armory exceptions.

---

## 0.7.2 — keep the best option visible

After 0.7.1 the live save showed only grey backup rows, and a steel hatchet that
Grant had been carrying was missing from the list. Both came from one bug: rows
with `Need <= 0` were dropped. A colonist's best hand or backup is often already
owned, so the white best row vanished and only grey alternatives remained.

- The filter is now `Need <= 0 && BestCount <= 0`. A row that is the best option
  in its section for at least one colonist always stays visible.
- Those rows render `enough` in green instead of `0`.
- The hatchet was the owned ideal: `Need` was 0, so it was hidden. After dropping
  it, the loose copy was forbidden, which raised `Need` and made the row appear.
  It should now be visible while equipped as well.
- The vanilla Axe only appears when its probe beats the equipped hatchet by the
  upgrade threshold. Once the pawn is unarmed the baseline is 0, so every melee
  option becomes an upgrade. That part is expected.

The tab rebuilds on the normal 2-second scan interval (`scanIntervalTicks`), so
a weapon change can take up to 2 s — or one `Refresh` click — to show. That is
not a logic delay.

---

## Live save test — verified 0.7.2

The isolated harness can load the user's real colony without touching their save
or config. The full procedure, switches, pitfalls and cleanup are in
[`docs/TESTING.md`](TESTING.md). The reusable pieces are committed:

- `tools/Setup-LiveTest.ps1` — copies one save + `Config` into an isolated folder,
  sets DevMode/windowed/debug logging, optionally builds and launches.
- `tools/LiveTestScaffold.cs.txt` — copy-in temporary driver that loads the save
  from the play scene, opens the Demand tab, logs the snapshot, captures
  `armory-live.png`, and quits. It is inert without the `armory-live.txt` marker
  and is removed before commit.

Recorded run (the `Armory Testing` save): **12 colonists, 29 loose weapons, 87
catalog defs (79 obtainable), 54 demand rows**, and no Armory exceptions. The
screenshot confirmed the 0.7.2 fix: the `Combat knife` backup row was white with
`enough` in green, followed by grey hand alternatives.

Critical pitfall: do **not** call `LoadGameFromSaveFileNow` from the entry scene
(main menu). That produced `InvalidCastException` in `Game.LoadGame` and a map
with 0 colonists and an uninitialised `WorldGrid`. Load from the play scene after
quicktest has initialised a map. The built-in
`SaveGameFilesUtility.GetAutostartSaveFile()` was unreliable (returned `none`
with DevMode on and exactly one save present).

`-quicktest` with the user's 73-mod config printed an "incompatible version"
warning for `PatientSomeone.StorageTypeCategories` but proceeded.

---

## 0.7.3 — main-tab icon

The Armory button was text-only. `MainButtonDef.Icon` loads `iconPath`, and Core
only ships paths for History, Factions and Menu. The icons on the other buttons
in the user's game come from **Vanilla Textures Expanded**
(`VanillaExpanded.VTEXE`), which patches `iconPath` for Architect, Work,
Schedule, Research, Assign, Animals, Wildlife, Quests and World, and replaces the
existing History/Factions/Menu paths (plus Mechs).

- Added `Textures/UI/Buttons/MainButtons/IconArmory.png`: 64×64, transparent,
  flat `#BBBBBB`, crossed swords, glyph bounding box ~47 px, matching the VTEX
  set (same size, fill and weight).
- Added `<iconPath>UI/Buttons/MainButtons/IconArmory</iconPath>` to
  `MainButtonDefs.xml`. Armory ships the texture itself, so the icon appears with
  or without VTEX.
- `tools/Generate-ArmoryIcon.ps1` redraws it with `System.Drawing`; no external
  image tooling is required.
- `MainButtonWorker.DoButton` draws an empty label when `Icon != null`, so the
  button becomes icon-only like the VTEX tabs. The tooltip still shows the label
  and description.

Verified live against the real mod list with VTEX: the bottom bar shows the
crossed swords at the neighbours' visual weight. Build clean.

---

## 0.7.4 — no duplicate backup weapon types

Rathat had a gladius and an axe as melee sidearms. The planner upgraded the axe
to another gladius, leaving two good gladiuses; the second sword added no
capability.

Cause: `BuildSidearmCandidates` only skipped `info.Thing == analysis.Current`
(the equipped hand). A loose weapon of a def already **carried in the bag** was
still a valid high-score backup candidate, and `RunSidearms` selected it.

Fix:

- `PawnAnalysis.AlreadyCarries(def)` is true when the def is the equipped hand or
  any `CarriedSidearms` entry.
- `SidearmCandidate.AlreadyCarried`; the builder marks such candidates with the
  soft blocker `Already carried` and clears `UpgradeAfterDrop`.
- `RunSidearms` skips `AlreadyCarried` candidates, so it chooses the best
  **different** backup or leaves the current one alone.
- Demand's backup scoring (`TryScoreCandidate`) also skips defs the pawn already
  carries, so the ideal never suggests a duplicate.
- The Weapons tab ignores duplicate candidates when computing "best backup user",
  so a loose gladius is not shown as wanted by a pawn who already has one.

Hand swaps of the **same def** remain allowed (for example normal → excellent);
that replaces the hand weapon rather than adding a second copy.

Existing duplicates are not auto-dropped: the planner's rule is never remove a
backup without a replacement. The pair stays until one is dropped manually or the
planner finds a better different def. Verified in the live save snapshot: loose
`Steel gladius` candidates for pawns already carrying one now read
`Already carried`.

Build clean.

---

## 0.7.5 — Demand ordered by expected strength

All-new wants contributed a flat `1.0` to `TotalGain`, and rows that were the
best for someone were sorted before every alternative. For an unarmed colonist
every weapon is "new", so a Short bow tied with a heavy blaster on the gain key,
and the best-group rule kept it near the top.

- `DemandRow.TotalScore` — sum of `ProbePower × PawnFit` over the contributing
  colonists. This is now the primary sort key inside a section: power, fit and
  breadth all count.
- `DemandRow.Power` — normal-quality, pawn-independent strength, shown as a new
  `Power` column between Weapon and Slot.
- The best-for-someone group is no longer forced to the top. White still marks
  the best option in a section; grey marks alternatives.
- Sort: section, `TotalScore` desc, `TotalGain` desc, `BestCount` desc, `Need`
  desc, def name.
- `Gain` remains the average relative improvement; `Power` makes "new" rows
  comparable. The debug demand log now includes `power`.

Deliberately not pure `WeaponPower`: a broad medium upgrade can outrank a narrow
high-power one, which is what a build plan wants. The `Power` and `Want` columns
expose the trade-off.

Build clean.

---

## 0.7.6 — gold star for best rows

White versus grey weapon names were too subtle. All weapon names are now white;
the best option in each section gets a **gold star** in a fixed gutter before the
name.

- `Textures/UI/Icons/ArmoryBestStar.png`: 24×24, transparent, gold five-point
  star, drawn at 16×16.
- `DrawBestStar` loads it through `ContentFinder`; if the texture is missing the
  row still renders, without a marker.
- The `Weapon` header is indented to match the labels. Non-best rows keep the
  empty gutter so the table stays aligned.
- The weapon-cell tooltip still distinguishes “best option in this section for N
  colonists” from an alternative. The ordering logic is unchanged.

Verified live: the `Heavy charge blaster` row shows the star at the top and all
names are white. Build clean.

---

## 0.7.7 — Score column and aligned detail rows

`Power` looked like the sort key, but the actual key is `Power × PawnFit` summed
over the colonists who want the def. A combat knife could therefore sit between
power-46 and power-43 rows without any visible explanation. The score driving the
order was hidden.

- `DemandRow.TotalScore` is now the `Score` column. Its header explains that it is
  `power × fit` summed over the wanted colonists and that rows are sorted by it
  inside each section.
- `DemandWant.Score` records each colonist's contribution. Expanded rows now draw
  the colonist name, current weapon, individual `Score`, `Slot` and `Gain` in the
  same columns as the parent row. The detail scores add up to the row score.
- Header names: `Want` → `Wanted by`, `Need` → `Needed`, plus the new `Score`.
- Column widths were retuned for the extra column (`Slot` is narrower; `Wanted
  by`, `Owned` and `Needed` were adjusted). The star gutter and white names are
  unchanged.
- The debug demand log now prints `score` before `power`.

Verified live on the real save: `Heavy charge blaster` shows score 1817; its
expanded values 170 + 240 + 234 + 205 + 229 + 252 + 221 + 266 = 1817, with
`Score`, `Slot` and `Gain` aligned under their headers.

---

## Pending verification (0.7.7)

- The `Score` column equals the sum of the expanded per-colonist scores.
- `Wanted by` and `Needed` labels fit at 1600×900 and at narrower widths.
- Detail-row current-weapon labels do not truncate; `Machine pistol (normal 98%)`
  fits after the 110px name column.
- Hovering `Score` explains the ordering; hovering `Power` distinguishes raw
  strength from expected value.
- Best-row star and white names still render correctly beside the new columns.

Verified live on the real save:

- Demand rendering, column alignment, section dividers and `Source` text at
  1600×900.
- Craft / Research / Buy ordering (Buy was empty, as expected in vanilla).
- Best-row stars, white names, and the covered best backup row showing `enough`
  in green.
- `Score` and per-colonist detail alignment; no Armory exceptions.

---

## 0.7.8 — real bench selection and Craft button

Demand showed `Electric smithy` for a recurve bow even though the colony had a
crafting spot and Electricity was not researched. `ResolveBench` returned the
first `AllRecipeUsers` entry, which is arbitrary and can be a locked bench.

- `WeaponSource.BenchUsers` caches the recipe's work benches once, using
  `RecipeDef.AllRecipeUsers` and `IBillGiver`.
- `ResolveBench` prefers a **built** player bench on the current map (usable
  first, then the more advanced/least busy one), then the best **unlocked**
  bench (all `researchPrerequisites` finished). A tribal crafting spot therefore
  no longer hides behind an electric smithy.
- `DemandRow.CanCraft` is true only when the recipe is unlocked, at least one
  copy is missing, and a built bench exists.
- Craft rows show a `Craft` button in the Source column. It queues a single 1x
  `Bill_Production` at the same bench named in the row (`DemandCrafting`). It
  does not equip the weapon or manage the bill afterwards.
- The Source tooltip explains built vs unlocked and the button.
- The live test harness now waits for `LongEventHandler.AnyEventNowOrWaiting` to
  clear instead of requiring `Map.uniqueID` to change; a save whose first map is
  id 0 no longer times out.

Verified live:

- New Tribe8: `Recurve bow` shows `craft · Crafting spot`, `can craft yes`, and
  `TryAddBill` added one recurve bow bill to the crafting spot (bills 1).
- Armory Testing: the built electric smithy was selected and received the bill
  (bills 3).
- Craft buttons render in the Source column on both saves; no Armory exceptions.

---

## Pending verification (0.7.8)

- On New Tribe8, the recurve bow row shows `Crafting spot`, not a locked
  Electric smithy.
- Clicking `Craft` at a built bench adds exactly one bill and shows the
  confirmation message.
- No `Craft` button when no valid bench is built, when the recipe is
  research-locked, or when `Needed` is `enough`.
- If several benches are built, the bill goes to the same bench named in the
  row (usable and more advanced first).
- Clicking `Craft` does not toggle the row's expanded details.

---

## 0.7.9 — fix bills that ate ingredients

The first Craft button created a plain `Bill_Production` for every recipe. A
recipe with an unfinished thing (the recurve bow uses `UnfinishedWeapon`) needs
`Bill_ProductionWithUft`; vanilla's `MakeUnfinishedThingIfNeeded` casts the bill
to that type. The cast threw `InvalidCastException` after the ingredients were
taken, so the pawn reset with no weapon and the bill retried forever.

- `DemandCrafting` now uses `RecipeDef.MakeNewBill()`, the same factory the
  vanilla bills tab uses. It returns `Bill_ProductionWithUft` when the recipe
  needs a UFT, and `Bill_Production` otherwise.
- `RepairMalformedBills` found any plain `Bill_Production` on a player work table
  whose recipe used an unfinished thing and replaced it with `MakeNewBill()`,
  copying repeat mode, count, search radius, skill range, restrictions and store
  settings. It ran once per map in `ArmoryMapComponent.MapComponentTick`. This was
  a one-time migration and was **removed in 0.7.10** after the affected save was
  fixed; the permanent fix is `RecipeDef.MakeNewBill()`.
- Ingredients already lost are not restored; the old bill's job may end on the
  replacement tick.

Verified live on New Tribe8: injecting a plain `Bill_Production` produced
`repaired 1, last type Bill_ProductionWithUft`; a fresh Craft bill was also
`Bill_ProductionWithUft`; no `InvalidCastException` in the log.

---

## Pending verification (0.7.9)

- With a recurve bow bill, the pawn carries wood to the crafting spot, starts
  work and produces a bow instead of losing the ingredients.
- Recipes without an unfinished thing still get a normal `Bill_Production`.
- The one-time malformed-bill repair was removed in 0.7.10; it was only for saves
  broken by 0.7.8 and had already repaired the affected save.

---

## 0.7.10 — two-line pawn detail, temporary migration removed

- The Pawn detail used to draw `Simple Sidearms: primary ... | SS default ...`
  as one line, which was always truncated. It now draws the role summary on one
  line and the SS preference/veto on an indented second line.
- The backup summary is also split into `Carried / target` and the upgrade-rule
  line, so `Upgrades replace the worst same-type backup` is no longer clipped.
  `PawnDetailHeight` accounts for the extra line.
- Removed the temporary malformed-bill repair from `DemandCrafting` and
  `ArmoryMapComponent`; new Craft bills still use `RecipeDef.MakeNewBill()`.

Verified live on New Tribe8: `Simple Sidearms: primary by skill (ranged)` and
`SS default ranged: Pila (carried)` render on separate lines, and the backup
`Carried` / rule lines both fit. Build clean.

---

## Pending verification (0.7.10)

- Pawn detail shows the SS preference on its own indented line, with no
  ellipsis at 1600×900.
- Backup `Carried` and upgrade-rule lines are separate and the lower panels are
  still reachable.
- A forced/blocked SS state also wraps onto the second line.
- No `LiveTestScaffold` or repair-migration code remains in `Source/`.

---

## 0.7.11 — separate hand and backup list limits

`maxCandidates` only limited the hand list; backups used a hard-coded
`SidearmDisplayLimit = 12`.

- `ArmorySettings.maxCandidates` default is now **10** for the hand list.
- New `ArmorySettings.maxSidearmCandidates` defaults to **5** for the backup
  list and is saved independently.
- Settings now has `Hand weapons shown per pawn` (5–100) and
  `Backup weapons shown per pawn` (1–50).
- `MainTabWindow_Armory.SidearmDisplayLimit` reads the setting; both the list
  rows and the `... N more` height use it.
- Settings migration 3 moves the old default 25 and the common manual minimum 5
  to the new hand default 10; other explicit hand values are preserved. The new
  backup value defaults to 5.

Verified live on New Tribe8 with the isolated config: the pawn detail shows up
to 10 hand candidates and 5 backup candidates, with the remaining backups
collapsed into `... N more`. Build clean.

---

## Pending verification (0.7.11)

- The hand list respects 10 by default and the backup list 5.
- The two sliders change their lists independently.
- `... N more` appears when a list is longer than its limit, and the backup
  panel remains reachable.
- Existing saves with hand 5/25 migrate to 10; other explicit hand values are
  preserved.

Still worth a look:

- Whether the 54-row option menu is too long for normal use; the top white rows
  are the plan, the grey rows are alternatives. If it feels noisy, the next
  option is a "best only" toggle.
- The expanded per-row detail (colonist, slot, gain, current weapon) was not
  clicked in the automated run.
- The seven pawns who were `blocked` on the monosword no longer create monosword
  demand (the probe is too heavy for their SS backup cap).
- `Armory → Log weapon snapshot` reports a non-zero catalog size and prints
  demand rows with sane `want (best N)` / `owned` / `need` numbers.
- If the catalog is empty or tiny, the likely cause is recipe enumeration or
  probe construction failing; the log line is the first thing to check.

---

## 0.8.0 — range-aware scoring, combat roles and squad variety

This replaces the pawn-independent `Power × PawnFit` score with a range-aware
combat score. The old blend sampled short/medium/long accuracy even when a weapon
could not reach those distances; it also ignored RimWorld's range-specific
`ShootingAccuracyFactor_*` stats, so the Biotech `Nearsighted` gene had no effect
on advice.

- `CombatScoring` samples effective DPS at 3–45 tiles using
  `ShotReport.HitFactorFromShooter` and `VerbProperties.GetHitChanceFactor`.
  The weapon's own range gates every sample; weapon quality and pawn genes/traits
  are included automatically.
- Each pawn gets an effective reach: the farthest distance where aim is still
  half its 10-tile value. Auto preferred range is `reach - 8`, clamped 8–34,
  with a colony-wide `Combat distance bias` of -5..+5.
- Roles are `CQB` (≤12), `Assault` (13–22), `Line` (23–30) and `Marksman` (31+).
  The pawn detail shows the role, preferred distance, a `Change` menu and an
  `Auto` option. Overrides are per-pawn and saved in `ArmoryGameComponent` by
  `thingIDNumber`, so they survive save/load.
- `CombatScoring` applies a ±5% deterministic personal affinity (at the default
  50% squad variety); Demand and the hand planner apply a sequential duplicate
  top-choice penalty. `Squad variety` at 0% disables both.
- `burstShotCount >= 6` (LMG, minigun, beam repeater) gets a 0.75 support
  penalty so raw burst DPS does not make every pawn want a minigun.
- Demand and Weapons show a `Range` column; the pawn ledger shows role, preferred
  distance and profile DPS. Demand detail lines show the pawn's role on hover.

Live test on `Armory Testing` at 1600×900: role line and `Change` button fit; a
forced Marksman override produced `Marksman (override, prefers ~34 tiles)`;
Demand showed the Range column and range-aware ordering; no Armory errors.

### Pending verification (0.8.0)

- A role override survives save/load and can be reset to `Auto`.
- Demand top rows are varied enough on a homogeneous colony; adjust `Squad
  variety` if not.
- LMG/minigun no longer dominate hand recommendations.
- The Weapons tab Range column and detail header fit at 1600×900.
- Settings scroll still reaches the diagnostics after the two new sliders.

---

## 0.9.2 — release staging, refreshed icon/banner, Best vs current

First private Workshop release candidate. Do **not** publish as 1.0.- Refreshed the crossed-weapons artwork: the rifle is now the foreground weapon
  with a readable stepped stock, grip, magazine, trigger guard and scope; the
  sword sits behind it. The tab glyph, `ModIcon.png` and `About/Preview.png` were
  regenerated from the same scripts, and the banner icon is aligned with the
  text block.
- Renamed the Colonists list header from `vs current` to **`Best vs current`**
  and widened that column slightly so the label fits at 1600x900. It still
  ellipsizes at 1280x720 in the existing narrow layout. The value is still the
  best available hand candidate compared with the current hand weapon; negative
  means the current weapon is better.
- Added `tools/Publish-Armory.ps1`:
  - `-Prepare` builds Release, stages only runtime files in
    `C:\Users\mail\Projects\Rimworld Mods\ArmoryRelease\Armory`, removes the
    development junction from `RimWorld\Mods`, and installs the clean release
    copy for the in-game uploader.
  - `-Restore` removes the release copy and recreates the development junction.
  - It preserves `PublishedFileId.txt` and can seed it from
    `docs/workshop-id.txt` on future releases.
- Added `docs/WORKSHOP.md` with the exact upload workflow, private-test checklist,
  release-folder contents, and a Steam-ready Workshop description.
- First private Workshop upload completed:
  [Armory on Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3842538224),
  item ID **3842538224**. Steam is holding it hidden while its automated content
  check runs; no action is needed until that clears.
- The development junction was restored after the upload, and the clean release
  copy remains at `C:\Users\mail\Projects\Rimworld Mods\ArmoryRelease\Armory`
  with `About\PublishedFileId.txt`. The durable ID lives in
  `docs\workshop-id.txt`.
- Added two-way links: GitHub source/issues in `About.xml` (`<url>` and the
  description), README, and the Workshop page draft; the Workshop link is in the
  README and handoff.

Verified: Release build clean; the icon, ModIcon and banner are reproducible from
their scripts; the first upload created item 3842538224. Pending: Steam's
automated content check, a clean subscriber test, and making the item public.

### Pending verification (0.9.2)

- Wait for Steam's automated content check to finish.
- Test the subscribed Workshop copy with and without Simple Sidearms.
- Set the item to Public when the private test passes.
- Add the GitHub link and current description to the Workshop page in Steam.

### Simple Sidearms save/removal and load-order test (2026-11-20)

Tested on isolated copies of the 13-colonist `Northwestern Esora Humanlike
Confederacy` save:

- **SS removed** from `ModsConfig.xml`: the save loaded, all 13 colonists
  appeared, the Armory tab and snapshot worked, and the log reported
  `Simple Sidearms integration: inactive, respect setting: on`. No SS class or
  XML errors. SS sidearm memory is not available without the mod, as expected.
- **SS before Armory** (the normal order): integration active, `SS active: True`,
  SS hand types and hand rankings present.
- **Armory before SS**: integration active, `SS active: True`; the logged active
  mod order confirmed Armory loaded before SS.
- A `Could not resolve reference ... Thing_Human1490067` warning appeared in both
  SS-on and SS-off runs, so it is unrelated to removing SS.

Conclusion: **load order is not important**, and SS does not need to be an
optional dependency or `loadAfter` entry. The adapter is lazy and scans all
loaded assemblies when first used.

---

## 0.9.1 — Colonists list follows the colonist bar

The Colonists tab sorted by `LabelShort`, which did not match the portraits at
the top of the screen. It now uses `playerSettings.displayOrder`, the same key
as vanilla's colonist bar.

- The bar order defaults to join order, and dragging a portrait updates
  `displayOrder`, so Armory follows manual reordering too.
- Unassigned pawns fall back to `joinTick`, then name, so the list stays stable
  before the bar first processes them.
- The Colonists tab tooltip now states the order and mentions portrait drag.

Answer to the original question: the old list was **alphabetical by short name**
(`Banks`, `Chef`, `Elsie`, …), not join order.

Verified live at 1600x900 on the 13-colonist save: the colonist-bar order and
the Armory list matched exactly — `Kruecke, Rathat, Existence, Huber, Elsie,
Hashoytt, Hoss, Grant, Banks, Jerry, Trebo, Chef, Gripe`.

---

## 0.9.0 — release text, banner and default-on management

First public release preparation.

- Automatic equipping and backup management are now **on by default** for new
  installs. No settings migration was added: this is still a development
  version, so existing development settings files keep their saved values.
- `About/About.xml` was rewritten for the release. It uses the **Upgrades** tab
  name, describes management as default-on, and covers range-aware roles, typed
  hand/backup loadouts, the Craft/Research/Buy plan, and optional Simple
  Sidearms support.
- `About/Preview.png` was regenerated at 640x360 with the current
  crossed-weapons icon and the tagline **"The right weapon, in the right
  hands."** The author name was removed from the banner; the footer now names
  RimWorld 1.6.
- `tools/Generate-ArmoryBanner.ps1` reproduces the banner from
  `About/ModIcon.png`, so a future icon change can be propagated with one run.
- README and HANDOFF text now reflect the Upgrades tab name, default-on
  automation, the Craft button behavior, and the icon/banner layout.
- The icon refactor from the parallel session is included: a symmetric
  45-degree crossed sword and rifle with a tighter cutout.

Verified: build clean; the isolated live save loaded at 1600x900 with the
Upgrades tab open and no Armory errors; tab icon 64x64, ModIcon 512x512, banner
640x360, with transparent icon corners. Pending: visual check in the mod list
and a fresh settings file after restart.

### Pending verification (0.9.0)

- A fresh settings file starts with automatic equipping and backup management on.
- The new banner appears correctly in the mod list; the tab icon still reads at
  64px.

---

## 0.8.6 — simpler crossed-weapons icon, no background

The seal and star were more frame than icon at tab size, and the 512px ModIcon's
dark rounded square did not match the transparent logos in the mod list.

- The tab glyph is now only the crossed sword and rifle. No ring, no star, no
  badge background.
- The sword was scaled up and its blade widened so it no longer reads as the
  smaller weapon next to the rifle. The rifle uses the simplified silhouette
  (stock, receiver, barrel, handguard, magazine) and stays correctly oriented:
  left-pointing before its 35-degree rotation, so the magazine is below the
  barrel.
- The two-weapon composition is centered and fills the tab button; the
  transparent separation gap around the sword still makes the cross legible at
  64px.
- `About/ModIcon.png` is now the same 512px transparent glyph with no background,
  matching the other mod logos.
- `tools/Generate-ArmoryIcon.ps1` now writes both outputs from one drawing:
  `About/ModIcon.png` and the 64px tab icon.

Verified live at 1600x900: the crossed sword/rifle glyph appears in the bottom
bar at the intended size and position; no Armory errors. The tab icon is 64x64
and the ModIcon is a 512x512 transparent PNG.

### Pending verification (0.8.6)

- The transparent ModIcon appears correctly in the mod list after a restart.

---

## 0.8.5 — Armory seal icon, ModIcon and tab order

The Armory identity is now a round armory seal: a riveted brass ring, star, and
a crossed sword and rifle.

- The tab glyph and `About/ModIcon.png` use the same seal direction. The tab file
  is a 64x64 transparent glyph; the mod icon is a 512x512 badge for the mod list
  and loading screen.
- The rifle is drawn **left-pointing before its 35-degree rotation**, so the
  scope stays above the barrel and the magazine below. Rotating a right-pointing
  rifle by 215 degrees turned it upside-down.
- The tab glyph uses a simplified rifle silhouette and a transparent separation
  gap around the sword, so the cross survives at 64px.
- `About/ModIcon.png` replaces the generic puzzle-piece icon in the mod list /
  loading screen.
- `MainButtonDef` order changed from **35 to 25**, one visible slot left. Vanilla
  neighbors are Schedule (20) and Assign (30); no active mod in the current list
  uses 25.
- `tools/Generate-ArmoryIcon.ps1` now regenerates the seal tab glyph. The
  README layout lists both icon assets.

Verified live at 1600x900 on `Northwestern Esora Humanlike Confederacy`: the new
seal appears in the bottom bar, the tab order is the intended one, and there are
no Armory errors. `IconArmory.png` is 64x64 and `ModIcon.png` is 512x512.

### Pending verification (0.8.5)

- The new ModIcon appears in the mod list after a restart, replacing the puzzle
  piece.

---

## 0.8.4 — Colonists/Upgrades tabs and a wider weapon column

The last two jargon tab names and the cramped Upgrades weapon column are fixed.
No scoring or planner behaviour changed.

- **Pawns** is now **Colonists** and **Demand** is now **Upgrades**. The internal
  `Demand` model, `DemandRow` and `expandedDemandDef` names are unchanged.
- Tab tooltips and README/TESTING references follow the new names.
- The Upgrades `Slot` column shrank from 132 px to 64 px. It shows `hand`,
  `backup`, or `both`; hovering a slot shows the exact hand/backup counts.
- The colonist-name gutter in expanded Upgrades rows shrank from 110 px to 96 px.
  Combined with the slot change, the `now <weapon>` text has 82 px more room, so
  names such as `now Marksman rifle (normal)` and
  `now Vakazron's EMP Gatling Gun` fit without ellipses.

Verified live at 1600x900 with the anti-materiel row expanded:
`Colonists`, `Weapons`, `Upgrades`, `Log` render correctly; `Slot` shows `hand`;
the long current-weapon labels are fully visible. Build clean.

### Pending verification (0.8.4)

- A row wanted for both slots displays `both`, and its hover tooltip reports the
  correct hand/backup counts.
- The narrower `Slot` column still fits `backup` at the minimum window width.

---

## 0.8.3 — interface wording and status clarity

A read-through of every visible label found several broad or internal terms. The
pass changes wording and adds tooltips; it does not change planning, scoring or
eligibility.

**Colonists**

- `Status: ok` is now `Fully equipped`.
- `hand ready` / `hand upgrade` / `add backup` / `backup upgrade` became
  `Hand weapon available`, `Hand upgrade available`, `Backup available` and
  `Backup upgrade available`.
- `backup blocked` became `No usable backup`; `no backup candidate` became
  `No backup weapon`; `backup carried` became `Backup carried (not managed)`;
  `No hand upgrade` became `No worthwhile upgrade`.
- `blocked: ...` / `paused: ...` became `Cannot automate: ...` /
  `Paused: ...`; swap and manual-change notes lost their redundant parentheses.
- The `Delta` column is now `vs current`, shows `new slot` instead of `+100%`,
  and has a tooltip explaining the comparison.
- `Role:` is now `Hand type:`, with `<type> (Simple Sidearms mode)` and explicit
  sources (`from current weapon`, `from combat skill`).
- The role button reads `Change role`; menu labels use `Close quarters` instead
  of `CQB`.
- The hand and backup candidate tables now have `Weapon` / `Status` / `Score`
  (`vs current`) headers. `ready` became `Can equip`.
- `Best usable` became `Best available`; `Best hand overall` became
  `Highest-scoring hand weapon`; `Incompatible` became `Cannot equip`.
- The keep-current line now states whether the best alternative is worse, or
  only a small improvement below the required gain.
- Hand, backup, status and delta cells have hover tooltips, so quality names and
  long blockers are readable when a column truncates them.
- Tab buttons explain what each view contains; the snapshot age reads
  `updated 1s ago` / `updated just now`.

**Weapons**

- The `Status` column is now `Best use`: `Hand`, `Backup`, `Hand (blocked)`,
  `Backup (blocked)`, `No colonist can use it`.
- `Best hand use` became `Best hand fit ... (x% vs current)`; backup text is a
  single sentence.
- `Colonists by score` has column headers, and its statuses read `Equipped in
  hand`, `Can equip in hand` and `Can carry as backup`.
- The `(holds it)` suffix is gone; the status carries that information.

**Upgrades**

- The `Slot` column is narrower and shows `hand`, `backup`, or `both`; its
  tooltip gives the exact hand/backup counts. The freed width goes to the weapon
  and expanded current-weapon text.
- The caption is two lines and says `compared with the weapons you own`,
  `Sections: Craft, Research, Buy` and `counts only after you unforbid it`.
- `new` in `Gain` is now `new slot`.
- `forb.` became `forbidden`; owned and gain cells have explanatory tooltips.

**Settings and Log**

- `Upgrade threshold` -> `Minimum upgrade gain`; `Ranged preference` / `Melee
  preference` -> `Ranged score weight` / `Melee score weight`.
- `Hand weapons shown per pawn` -> `Hand candidates shown per colonist` (and the
  backup equivalent); `Show blocked weapons` -> `Show blocked candidates`.
- `only ready weapons` -> `weapons it can use now`.
- Log actions read `Equip hand weapon`; the ON banner explains that the log is
  cleared when the game closes or a save loads.

**Supporting text**

- `Outside area` -> `Outside allowed area`; `Unreachable` -> `No path to
  weapon`; `Reserved` -> `Reserved by another job`.
- `Unbonded persona: equip manually to bond` -> `Persona: bond manually`.
- `SS: forced ...` became `Forced unarmed` / `Forced weapon: X` / `Prefers
  unarmed`; `SS mode` became `Simple Sidearms mode`.
- Simple Sidearms primary modes now read `always ranged`, `always melee`,
  `by best skill (ranged)`, `chosen when generated`.

Verified live at 1600x900 on `Northwestern Esora Humanlike Confederacy`: the
Colonists list shows `Fully equipped` / `Hand upgrade available`, `Hand type:
ranged (Simple Sidearms mode)`, candidate headers, `Can equip` and `Persona:
bond manually`. The Weapons tab shows `Best use` with `Backup (blocked)` and
`Colonists by score`; the Upgrades caption wraps correctly and `craft · bench`
remains intact. The Settings dialog renders the new labels and descriptions
without overlap. Build clean.

### Pending verification (0.8.3)

- `vs current` values are readable at the default window size and the status
  column still fits `Backup upgrade available (hand locked)`.
- The `forbidden` suffix fits the Owned column on a row with forbidden stock.
- Hovering a truncated hand, backup, status or weapon cell shows the full text.
- The Settings scroll still reaches Diagnostics after the new descriptive lines.

---

## 0.8.2 — Source column no longer shows a replacement glyph

The Demand tab's Source column rendered `craft ♦ Fabrication bench`: three
separator strings contained U+FFFD (the Unicode replacement character) instead
of the intended middle dot. The corrupted bytes came from an earlier edit, not
from the game font.

- Restored `craft · <bench>`, `more: <research> · buy`, and `<n> · <n> forb.`
  to U+00B7 separators.
- Scanned all source, docs and metadata for U+FFFD; no other occurrences remain.

Verified live at 1600x900: the Demand tab now reads
`craft · Fabrication bench` and `craft · Machining table`. Build clean.

---

## 0.8.1 — unbonded persona weapons are manual-only

A quest dropped a second persona monosword. The loose copy was in the Weapons
tab all along, but the already-bonded copy was carried by its owner and therefore
correctly absent from the loose-weapon list. Two problems made this confusing:

- Simple Sidearms reports an **unbonded** persona weapon as
  `BladelinkBondedToSomeoneElse` because its `CodedPawn != pawn` check treats
  `null` as "someone else". Armory repeated that misleading text.
- Nothing stopped the planner from equipping an unbonded persona weapon, which
  would permanently bond it as a side effect.

Changes:

- `Eligibility.IsUnbondedPersona` detects a persona weapon with no bonded pawn.
- All Armory candidate checks now return
  `Unbonded persona: equip manually to bond`; the planner will not equip it.
- Demand never plans a new unbonded persona weapon.
- The Weapons tab has a visible note: `Loose weapons only. Equipped and carried
  weapons appear under Pawns.`
- `SimpleSidearmsAdapter.ExplainPickupFailure` now says
  `Unbonded persona: equip manually to bond` for this SS case and names the
  bonded pawn when the weapon really is bonded elsewhere.

Verified live on `Northwestern Esora Humanlike Confederacy`: `Talia, persona
monosword (good)` is the loose top row; every ranged pawn's backup entry now
shows the manual-bond reason; `Silencesender` remains with Trebo and is not a
loose weapon. Build clean.

### Pending verification (0.8.1)

- Manually equipping the loose persona weapon bonds it to the chosen pawn, after
  which Armory can treat it as that pawn's bonded weapon.
- A persona weapon already bonded elsewhere still shows the bonded pawn's name
  and remains hard-blocked.
- The planner skips unbonded persona weapons and does not silently bond them.

Still open from 0.6.7:

- Weapons detail, `Colonists by fit`: the full reason text is visible for long
  blockers such as `Can't carry (0.85 kg, 0.3 kg free)`, and the colonist name
  column is narrower.
- The hand and backup lists are both numbered; `Hand slot (...)` /
  `Backup slot (...)` headers are white with a divider above them.
- The Armory button sits between `Assign` and `Animals` on the bottom bar.
- A blocked backup shows `All sidearm slots are full` when SS's slot count is
  reached, and `Can't carry (X kg, Y kg free)` when the pawn's inventory weight
  is the limit. The two must never be used interchangeably.

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
- Deliberate weapon variation and roles are now part of 0.8.0; refine the
  duplicate penalty and support-weapon handling after live feedback.

---

## Product decisions and user preferences

- **Full auto-manager is the goal**, not only an inspector, but the ranking must
  stay simple and understandable. Fancy per-role loadouts come later, if at all.
  The first release ships with automatic equipping and backup management on by
  default so the manager works out of the box.
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
| 0.6.7 | Shorten weight blocker to `Can't carry (...)`, number the backup list, white section headers with dividers, move the main tab next to Assign |
| 0.7.0 | Demand rework: ideal loadout vs owned copies over all craftable/buyable/owned defs, normal-quality scoring probes, `Build`/`Locked by research` sections, `Owned`/`Need`/`Gain`/`Source`, expandable per-colonist detail |
| 0.7.1 | Demand lists every upgrade as an independent option (overlapping wants), white/grey best-vs-alternative markers, and Craft / Research / Buy sections in action order |
| 0.7.2 | Keep the best option visible when already covered (`enough` in green), so owned ideals and white backup rows no longer disappear behind grey alternatives |
| 0.7.3 | Add a crossed-swords main-tab icon matching the Vanilla Textures Expanded button set |
| 0.7.4 | Never add a second backup of a weapon def the pawn already carries (soft `Already carried` blocker; planner, Demand and Weapons tab all skip it) |
| 0.7.5 | Order Demand by total expected score (power × fit across colonists), add a `Power` column, and stop forcing best-for-someone rows above stronger alternatives |
| 0.7.6 | Make all Demand weapon names white; mark best-in-section rows with a gold star in a fixed left gutter |
| 0.7.7 | Show the real sort key as a `Score` column, align expanded per-colonist `Score`/`Slot`/`Gain` under the headers, and rename `Want`/`Need` to `Wanted by`/`Needed` |
| 0.7.8 | Show the best built/unlocked bench instead of an arbitrary recipe user, and add a `Craft` button that queues one bill at that bench |
| 0.7.9 | Use `RecipeDef.MakeNewBill()` so unfinished-thing recipes get `Bill_ProductionWithUft`, and repair bills created by 0.7.8 on load |
| 0.7.10 | Split the SS preference and backup-rule text across lines; remove the temporary malformed-bill migration |
| 0.7.11 | Separate hand/backup list limits: default 10 hand and 5 backup, with independent sliders |
| 0.8.0 | Range-aware combat scores, auto/override roles, squad variety and a Range column |
| 0.8.1 | Unbonded persona weapons are manual-only; loose-weapons note; clearer SS persona blocker |
| 0.8.2 | Restore middle-dot separators in the Demand Source column (U+FFFD fix) |
| 0.8.3 | Interface wording pass: explicit statuses, hand type, vs current, candidate headers and tooltips |
| 0.8.4 | Narrow the Upgrades `Slot` column to `hand`/`backup`/`both`, widen current-weapon text, and finish the tab rename |
| 0.8.5 | Round-seal Armory icon, `About/ModIcon.png`, `MainButtonDef` order 25, and reproducible tab-glyph generator |
| 0.8.6 | Remove the seal/star, rebalance crossed sword and rifle, and make `ModIcon.png` a transparent backdrop-free glyph |
| 0.9.0 | Release text and banner pass: default-on auto-equip and backups, new tagline/icon banner, Upgrades naming, reproducible banner generator |
| 0.9.1 | Colonists list follows the colonist-bar display order (join order by default) instead of alphabetical name order |
| 0.9.2 | Refreshed icon/banner, `Best vs current` column, clean release staging and the first private Workshop upload checklist |
| 0.10.0 | Typed hand model without Simple Sidearms, passion-first role assignment, Brawler/shield/bonded handling, automatic hand-type alignment and per-pawn hand-type overrides |
| 0.10.1 | Corrected hand-type order tooltip, removed duplicate current-weapon text, hid the no-SS Backup column, unified slot labels/colors, dropped the redundant SS "carries the preferred type" line, shortened list statuses, fixed false `Already carried` on persona/unique weapons, and unified Auto candidate limits (15 hand without SS; 5/5 with SS) |
| 0.10.2 | Drop-first backup upgrades, stop adding when multiple backups are carried, explicit biocoded-owner guard, and exclude unusable backups from the worst-backup/demand baseline |
| 0.10.3 | Double-click a Colonists or Weapons row to close Armory, jump the camera to that pawn or weapon, and select it |
| 0.10.4 | Weapon comparison columns and sorting, role filters and Best by role, material-specific upgrade variants with per-copy costs and material-locked bills, and an optional Owned section |
| 0.10.5 | Split accuracy into sortable columns, widened the Weapons/Upgrades tables, narrowed the detail pane, moved the Upgrades header text up, and compacted the ranged stat header |
| 0.10.6 | Show accuracy as percentages, render Upgrades costs as icon/count groups with orange shortage counts, and equalize Wanted/Owned/Needed widths |

---

## Files that matter

| File | Why |
|---|---|
| `README.md` | User/dev overview, settings, build, roadmap |
| `docs/RESEARCH.md` | Auto Arm / Equipment Manager / vanilla / SS findings |
| `docs/RANGE_AND_ROLES.md` | Range/role research, shipped model and pending decisions |
| `docs/HANDOFF.md` | This file |
| `docs/TESTING.md` | Quick harness + live-save procedure, pitfalls, cleanup |
| `docs/WORKSHOP.md` | Clean release contents, private upload checklist and Workshop page copy |
| `tools/Setup-LiveTest.ps1` | Copies a real save/config into an isolated folder |
| `tools/LiveTestScaffold.cs.txt` | Temporary load/screenshot scaffold template |
| `tools/Generate-ArmoryIcon.ps1` | Regenerates the 64px tab glyph and transparent ModIcon |
| `tools/Generate-ArmoryBanner.ps1` | Regenerates `About/Preview.png` from the ModIcon |
| `tools/Publish-Armory.ps1` | Stages a clean Workshop copy and swaps/restores the dev junction |
| `Source/Armory/ArmoryPlanner.cs` | Automation decisions and job issuing |
| `Source/Armory/ArmorySnapshotBuilder.cs` | Ranking, pod lookup, weapon filter, demand |
| `Source/Armory/Scoring/*` | Range profile, roles, power, fit, blockers, weapon catalog/probes |
| `Source/Armory/DemandCrafting.cs` | Queues a Demand weapon bill at the chosen built bench |
| `Source/Armory/UI/MainTabWindow_Armory.cs` | All tabs and layout |
| `Source/Armory/ArmorySettings.cs` | Settings + migration + scroll UI |
| `Source/Armory/ArmoryGameComponent.cs` | Session log, planner reset, role overrides |
| `Source/Armory/Compatibility/SimpleSidearmsAdapter.cs` | Reflection-only SS state/rules; M3 core |
| `Source/Armory/WeaponFilter.cs` | One shared weapon-definition filter |
| `Source/Armory/ArmoryMod.cs` | Mod entry, settings, competitor/SS detection |
