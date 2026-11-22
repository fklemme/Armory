# Sidearm Loadouts (0.11.x)

Design and decision record for Armory's sidearm management.

## Goal

Armory owns the full carried weapon plan for each managed colonist:

| Slot | Type | Rule |
|---|---|---|
| **Hand** | Hand type | Best hand weapon, existing rules |
| **Sidearm 1** | Opposite type | Best valid Simple Sidearms sidearm |
| **Sidearm 2** | Ranged | Optional complement that covers a real range gap |

Priority is Hand, then Sidearm 1, then Sidearm 2. Armory never drops Sidearm 1
to make room for Sidearm 2.

For a ranged hand, Sidearm 1 is melee and Sidearm 2 is another ranged weapon.
For a melee hand, both sidearms are ranged.

## Why duplicates happened

Simple Sidearms stores remembered weapons as a list of def + material pairs.
The list is not a set and can contain the same weapon twice. When a pawn is
downed, Simple Sidearms deliberately keeps that memory so the pawn can recover
weapons afterwards. On recovery `JobGiver_RetrieveWeapon` retrieves one weapon
per remembered entry, including duplicates.

Armory's 0.10.8 guard only stopped Armory from adding another sidearm. It could
not stop Simple Sidearms from retrieving one independently. `Multiple backups`
was a safety report, not a repair. Armory now reconciles the carried set and
repairs extras instead of refusing to act.

## Simple Sidearms boundary

Armory does **not** rewrite Simple Sidearms' remembered-weapon list.

- Armory may call Simple Sidearms' own `DropSidearm` for a weapon it
  intentionally drops.
- The drop uses `intentionalDrop: true` and `unmemorise: true`. This is the
  sanctioned way to say "this drop was deliberate, do not retrieve it again".
  It is not a direct edit of the memory list.
- If Simple Sidearms still retrieves a weapon that is beyond the plan, Armory
  drops it on a later scan and restores the planned weapon if needed.
- Armory does not prune stale memory entries directly. This is deliberately
  less invasive and tolerates a short period of imperfection after a downing.

### Recommended Simple Sidearms setting

For the cleanest result, disable Simple Sidearms' automatic re-equip in its
settings. In Simple Sidearms, open **Settings > Automation** and turn off
**Re-equip best available** / the automatic re-equip option. Armory will restore
missing hand and sidearm weapons on its next scan.

This is a recommendation, not a requirement. Armory must remain correct when
Simple Sidearms' retrieval is enabled.

## Sidearm 2 selection

Sidearm 2 is not chosen by raw combat score. It is chosen by **range gap
coverage**:

1. Build a per-distance expected-DPS profile for the Hand and Sidearm 1 using
   the pawn's real aim curve, weapon hit chance, armor penetration, fit and
   support penalties.
2. For each distance, find the best result achievable by a valid ranged
   candidate.
3. A candidate gets credit only for improving distances where the current
   Hand + Sidearm 1 combination is weak. Improvements in already-strong bands
   receive little or no credit.
4. Require at least one weak band to improve by the normal upgrade threshold
   (10% by default). If no real gap exists, leave Sidearm 2 empty.
5. Explain the choice in the UI by band, for example:
   `Sidearm 2: Heavy SMG, covers close range (3-12), +38% there`.

The band names and centers follow the existing combat roles:

| Band | Distance |
|---|---|
| Close | 3-12 tiles |
| Short | 13-22 tiles |
| Medium | 23-30 tiles |
| Long | 31-40 tiles |

Examples:

- Sniper hand + melee sidearm: pistol or light SMG for close range.
- Shotgun or SMG hand + melee sidearm: rifle or sniper for long range.
- Assault rifle hand: often no useful second sidearm.
- Melee hand + rifle sidearm: pistol or light SMG for close range.
- Melee hand + sniper sidearm: pistol or light SMG for close range.

Armory excludes manual-use weapons, grenades and EMP-only items, unbonded
persona weapons, non-droppable weapons, and duplicate defs from automatic
Sidearm 2 selection. A weapon that Simple Sidearms rejects for mass or slot
limits remains visible with the Simple Sidearms reason.

## Full reconciliation

Every planner pass, for each managed colonist:

1. Wait while a Simple Sidearms sidearm or equip job is running. Do not act
   while downed, drafted (unless enabled), or in a mental state.
2. Count all carried combat weapons, including the equipped weapon. Simple
   Sidearms can temporarily draw a sidearm, so physical equipment/inventory
   location is not the same as the logical slot.
3. Choose keepers:
   - best carried copy for each planned slot;
   - one usable fallback of a missing type until a legal replacement exists;
   - do not drop bonded persona weapons, quest-lodger weapons, tools with stat
     offsets, or destroy-on-drop weapons.
4. Drop duplicates and weapons beyond the plan with Simple Sidearms'
   `DropSidearm`.
5. Add missing planned sidearms in priority order, after Simple Sidearms'
   own capacity and legality checks.
6. Record every drop, addition and failure in the Log tab.

The existing `Multiple backups` status becomes a transient cleanup state.
Hashoytt's two steel gladiuses become one gladius plus a legal ranged sidearm
when capacity allows; if no legal ranged sidearm exists, one gladius is kept
and Sidearm 2 reports why it is empty.

### Fallback rule

Armory drops duplicates and extras immediately. It keeps one usable
wrong-type sidearm until a legal replacement is ready, so it never weakens a
pawn just to match the ideal plan. There is no opt-out setting.

### Overriding Simple Sidearms preferences

Simple Sidearms' default ranged weapon and preferred melee weapon are treated
as automatic bookkeeping, not player intent. Armory ignores them when choosing
the plan and may replace those weapons. Forced weapon and forced unarmed states
remain hard vetoes, as before.

### Drawn-sidearm guard

Simple Sidearms can temporarily draw Sidearm 2 as the primary. Armory must not
interpret that as a missing sidearm or a hand downgrade:

- sidearm counts come from the carried set, not inventory alone;
- while the pawn is drafted, Armory waits for Simple Sidearms to stow a drawn
  sidearm before queuing a hand swap;
- outside combat a **remembered** sidearm that still fills a gap is left to
  Simple Sidearms;
- an **unremembered** sidearm, or a remembered **no-gap Sidearm 2**, is
  temporary: Armory puts the remembered planned hand back through Simple
  Sidearms' own inventory-equip helper, then removes the stale sidearm;
- the UI may show the currently held sidearm on the Hand line in the meantime,
  marked `(sidearm in hand)`, while the planned hand is shown in the detail.

## UI

The Colonists list uses multi-line rows:

```text
Hashoytt                 Hand       Marksman rifle (good)          Fully equipped     -16%
                         Sidearm 1  Steel gladius (excellent)
                         Sidearm 2  Heavy SMG (normal)             covers close
```

- Name on the left, slot lines to the right.
- Status and `Best vs current` on the first line, right-aligned.
- `Hand` green, `Sidearm 1/2` blue, multiple slots cyan, blocked orange.
- Variable row height: one line without Simple Sidearms, three with it.
- Empty Sidearm 2 shows `no useful gap`, `no free slot`, or the Simple
  Sidearms reason.
- The whole block is selectable and double-click still jumps to the colonist.

The detail pane gets separate `Hand slot`, `Sidearm 1` and `Sidearm 2`
sections. Sidearm 2 shows its coverage band and gain, not only the generic
combat score.

The Weapons tab uses `Sidearm 1` and `Sidearm 2` best-use statuses. The Upgrades
tab uses `Hand`, `Sidearm 1`, `Sidearm 2` and `Both` (cyan), and includes
Sidearm 2 demand.

All user-facing `Backup` text becomes `Sidearm`. Internal names are renamed
where safe; serialized settings field names stay unchanged.

## Capacity

Armory uses the installed Simple Sidearms limits. The current test configuration
has three total slots including the hand, separate melee/ranged allowances, and
relative-mass caps per sidearm. That permits hand plus two sidearms, but often
requires a pistol-sized Sidearm 2. If Simple Sidearms allows only one sidearm,
Sidearm 2 remains empty with the Simple Sidearms reason.

## Version

This is **0.11.x**, the sidearm-loadout milestone. The initial 0.11.0 release
shipped reconciliation, rename, multi-line UI, Sidearm 2 selection and demands
together. 0.11.1 is a bugfix for a sidearm left in hand outside combat and for a
stale Sidearm 2 whose range gap disappeared.

## Decisions

- Sidearm 2 is added only when it closes a real range gap.
- Melee pawns get two ranged sidearms.
- Extras and duplicates are dropped; one usable fallback is kept until a
  replacement is ready; no opt-out.
- Armory does not rewrite Simple Sidearms memory. It uses Simple Sidearms' own
  intentional-drop API and tolerates later retrieval.
- Simple Sidearms' default/preferred weapons are overridden; forced states are
  respected. No opt-out.
- Colonist rows are multi-line; labels are `Sidearm 1` and `Sidearm 2`.
- One 0.11.0 release with both implementation stages.

## 0.11.1 follow-up

During post-release testing Kruecke had an unremembered unique weapon equipped
(Simple Sidearms' diagonal marker). It was not an Armory sidearm pickup, but
Armory classified it as Sidearm 2 and its `DrawnSidearm` guard blocked every
correction outside combat.

The fix:

- A remembered sidearm that still fills a gap is Simple Sidearms' business;
  Armory leaves it alone outside combat. A sidearm in hand only pauses the
  planner while the pawn is drafted.
- An **unremembered** sidearm in hand is temporary. Outside combat Armory puts
  the remembered planned hand back through Simple Sidearms'
  `equipSpecificWeaponFromInventory` helper; the old sidearm goes to inventory.
  A remembered **no-gap Sidearm 2** is also restored and removed so the stale
  weapon does not occupy an SS slot.
- A Sidearm 2 that no longer covers a gap is dropped after trying a useful
  replacement. The UI appends `no useful gap` and shows `Cleaning up sidearms`.
- The UI distinguishes `Sidearm drawn` (combat), `Sidearm in hand` (remembered
  and useful), and `Restoring hand` (temporary or no-gap weapon being replaced).
- The persistent `ArmoryLogs` file records the decisions and snapshots needed to
  diagnose the next case.

Live test on the same save: Kruecke's Burngun was moved to inventory, Seeking
Justice was restored as the hand, and the Burngun was dropped as a no-gap
Sidearm 2. The test was repeated with the Burngun added to SS's remembered list;
it was still restored and removed. Chef and Rathat still carried remembered
sidearms that covered a gap; Armory left those to Simple Sidearms. Useful
Sidearm 2 weapons were kept. Build clean: 0 warnings, 0 errors.

## Implementation status

Implemented in 0.11.0. Live-tested on an isolated copy of the current save at
1600x900 with Simple Sidearms enabled:

- Hashoytt started with two steel gladiuses; Armory kept one, dropped the extra,
  and equipped a legal SMG as Sidearm 2.
- A forced downing/recovery cycle did not leave a persistent duplicate; the
  planned set was restored after Simple Sidearms retrieval.
- Sidearm 2 was equipped for Hashoytt, Trebo, Chef, Elsie and Rathat; heavy
  candidates remained visible with SS's mass blocker.
- Rathat's drawn Light SMG showed the anti-materiel rifle as the stowed hand,
  marked the SMG `(drawn)`, and did not trigger the manual-change pause.
- The Upgrades Slot column fits `Sidearm 1`/`Sidearm 2`.
- Build clean: 0 warnings, 0 errors.

Refreshed `docs/screenshots/` at 3440x1440 for 0.11.1. Pending: verify in the
user's normal game at their normal resolution.

0.11.1 re-tested the same save after the stale-sidearm fix. Kruecke's
unremembered Burngun was replaced by the remembered hand and dropped as a no-gap
Sidearm 2. Chef and Rathat had remembered sidearms in hand; Armory left them to
Simple Sidearms. Useful Sidearm 2 weapons remained. The persistent diagnostic
log captured the restore and drop. Build clean: 0 warnings, 0 errors.
