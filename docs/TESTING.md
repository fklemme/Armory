# Testing Armory

Two levels of testing:

1. **Quick harness** — Core (+ optionally Simple Sidearms), a generated 3-colonist
   map. Fast, deterministic, no user data. Good for scoring/catalog/scanning
   regressions.
2. **Live save** — a copy of a real colony with the full mod list, loaded in an
   isolated save-data folder. Slower, but it exercises real weapons, modded defs,
   Simple Sidearms state, and the actual Upgrades tab.

Neither level touches the user's save or config, and neither uses keystrokes or
window focus.

---

## Quick harness

```powershell
.\tools\Quicktest-Armory.ps1
.\tools\Quicktest-Armory.ps1 -IncludeSimpleSidearms
```

It creates an isolated `Config/ModsConfig.xml` under
`%TEMP%\opencode\armorytest`, launches RimWorld with `-quicktest`, prints
`[Armory]` lines, and closes only that instance. The generated map is enough to
verify the weapon catalog, demand rows, and exceptions.

Armory settings for the isolated game live in
`%TEMP%\opencode\armorytest\Config\Mod_Armory_ArmoryMod.xml`.

---

## Live save

### Prerequisites

- RimWorld closed (the DLL is locked while it is running).
- The save must exist in the real save folder:
  `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves`.
- Time: loading 70+ mods and a ~19 MB save takes a minute or two.
- No extra tools are required. The game captures its own frame with Unity's
  `ScreenCapture`, which works even when the window is behind other apps.

### One-time/temporary scaffold

The live test needs a short-lived `[StaticConstructorOnStartup]` driver that
loads the save and captures the tab. The template is committed at:

```
tools\LiveTestScaffold.cs.txt
```

Copy it into the compiled source tree only while testing:

```powershell
Copy-Item .\tools\LiveTestScaffold.cs.txt .\Source\Armory\LiveTestScaffold.cs
```

It arms itself only when `armory-live.txt` exists in the active save-data folder,
so the same DLL is inert in the user's real profile. The marker text is the save
name (without `.rws`).

**Always delete it and rebuild before committing.** No test scaffold should stay
in `Source/`.

### Run

```powershell
# Copy config + save, configure the isolated folder, build, launch, wait, report
.\tools\Setup-LiveTest.ps1 -SaveName "Armory Testing" -Build -Launch -Wait
```

Or step by step:

```powershell
# 1. Prepare the isolated folder (copy Config + save, marker, DevMode, windowed,
#    Armory debug logging on and automation off)
.\tools\Setup-LiveTest.ps1 -SaveName "Armory Testing"

# 2. Add the scaffold and build
Copy-Item .\tools\LiveTestScaffold.cs.txt .\Source\Armory\LiveTestScaffold.cs
dotnet build .\Source\Armory\Armory.csproj

# 3. Launch
.\tools\Setup-LiveTest.ps1 -SaveName "Armory Testing" -Launch -Wait
```

Useful switches:

| Switch | Effect |
|---|---|
| `-Build` | Runs `dotnet build` before launching |
| `-Launch` | Starts the isolated game with `-quicktest -savedatafolder=...` |
| `-Wait` | Polls `Player.log` and prints Armory lines and the screenshot path |
| `-Force` | Stops an existing RimWorld process (only with explicit permission) |
| `-Width` / `-Height` | Isolated window size (default 1600×900) |
| `-TimeoutSeconds` | Wait limit for `-Wait` (default 300) |

### What the scaffold does

1. Waits for `-quicktest` to enter the play scene and generate a throwaway map.
2. Queues `SavedGameLoaderNow.LoadGameFromSaveFileNow(markerText)` **from the
   play scene**, once the quicktest map exists.
3. Waits for the load long event to finish (`LongEventHandler.AnyEventNowOrWaiting`
   becomes false) and logs the colony size.
4. Opens the Armory tab with `Find.MainTabsRoot.SetCurrentTab`, reflects the
   private `view` field to `Upgrades`, and calls `ArmoryMapComponent.LogSnapshot`.
5. Closes `Dialog_Debug` windows, captures `armory-live.png`, waits a second, and
   calls `Application.Quit()`.

### Results

- Log: `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`
- Screenshot: `%TEMP%\opencode\armory-live\armory-live.png`
- Isolated save/config: `%TEMP%\opencode\armory-live\`

Search the log for `[Armory] live test` to see the sequence, and for
`[Armory] map load` / `demand` for the snapshot itself.

### Cleanup

```powershell
Remove-Item .\Source\Armory\LiveTestScaffold.cs
dotnet build .\Source\Armory\Armory.csproj
```

Keep `%TEMP%\opencode\armory-live` if you want to rerun the same save; the setup
script recreates it from the real profile on the next run.

### Testing without Simple Sidearms or with a different load order

`Setup-LiveTest.ps1` wipes the isolated root and copies the real config, so
prepare **without** `-Launch`, edit the isolated `Config\ModsConfig.xml`, then
launch the game manually:

```powershell
.\tools\Setup-LiveTest.ps1 -SaveName "Armory Testing"

# remove this <li> to test a save with SS disabled:
#   <li>petetimessix.simplesidearms</li>
#
# or move this <li> before the SS entry to test Armory loading first:
#   <li>kruecke.armory</li>

& "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64.exe" `
  -quicktest -savedatafolder="$env:TEMP\opencode\armory-live"
```

Wait for the scaffold to finish and quit, then check the log for
`[Armory] Simple Sidearms integration: active` / `inactive` and the save-loaded
line. Do not run `Setup-LiveTest.ps1 -Launch` after editing the config: it will
overwrite your changes.

### Testing the header counters (0.12.0)

- Open the Armory tab and leave the game running. When `updated ...` changes from
  `just now` to `1s ago`, `2s ago`, the
  `N colonists | M loose weapons | updated` block must stay in the same place.
  Only the age value inside its reserved field may change.
- Check at 1920x1080 with UI scale 1.0 and 1.25. The Workshop gallery uses 1.25
  for legibility; both scales use the same 1280x720 window layout.

### Testing sidearm loadouts (0.11.x-0.12.0)

Use the isolated save with Simple Sidearms enabled. The test config should have
three total SS slots including the hand, otherwise Sidearm 2 cannot fit.

- **Multi-line rows:** each colonist shows a Hand line, Sidearm 1 line and
  Sidearm 2 line. `Hand` is green; both sidearm labels are blue; blocked/missing
  candidates are orange; a useful empty slot is gray.
- **Duplicate cleanup:** pick a colonist carrying two copies of the same sidearm.
  After a planner pass, exactly one is carried and any extra is dropped. The
  accepted state is one keeper plus a legal Sidearm 2 when one is useful.
- **Downing/recovery:** down that colonist, let Simple Sidearms drop and restore
  weapons, heal the pawn, and let Armory run several passes. The pawn must end
  with the planned set, not a persistent duplicate. Armory may temporarily show
  `Sidearm job`; it should settle to `Fully equipped`.
- **Drawn sidearm:** draft the pawn in combat or otherwise make Simple Sidearms
  draw Sidearm 2. The hand line should keep the logical hand and show `(stowed)`;
  the sidearm line should show `(drawn)`; status is `Sidearm drawn`; Armory must
  not report a manual weapon change or add a duplicate. After SS stows the
  sidearm, the status returns to normal.
- **Left-over unremembered sidearm outside combat (0.11.1):** load a save where a
  colonist has an **unremembered** sidearm equipped while not drafted and the
  remembered planned hand is in the inventory. After a planner pass the hand is
  restored through Simple Sidearms, the old sidearm is in the inventory, and a
  sidearm that no longer covers a gap is dropped. Status passes through
  `Restoring hand` / `Cleaning up sidearms` and ends at `Fully equipped` or
  `No useful Sidearm 2`.
- **Remembered sidearm in hand (0.11.1 / 0.12.0):** with **Align Simple
  Sidearms hand preference** off, a remembered sidearm that still fills a gap is
  left to Simple Sidearms. With alignment on, Armory sets the SS hand preference
  to the planned hand and restores it, leaving the old weapon as a sidearm. Armory
  must not fight SS by re-equipping the same pair every pass.
- **Remembered no-gap Sidearm 2 (0.11.1):** add the sidearm def to SS's remembered
  list in a test save. If the equipped Sidearm 2 no longer fills a gap, Armory
  should still restore the remembered hand and drop the sidearm instead of
  leaving an SS slot occupied.
- **No-gap Sidearm 2 (0.11.1):** if the hand or Sidearm 1 changed and the carried
  Sidearm 2 has `no useful gap`, it must be dropped after any useful replacement
  is tried. An equipped weapon is moved to inventory first rather than dropped
  from the hand.
- **Persistent diagnostics (0.11.1):** the persistent log is on by default.
  Open **Log**, click **Log snapshot**, then **Open logs**. The session file in
  `ArmoryLogs` should contain the version/RimWorld/SS/settings header, decision
  lines, and the snapshot. The newest five session files are kept.
- **Range-gap selection:** a marksman with a sniper hand should prefer a light
  close-range Sidearm 2 (pistol/light SMG), not a second sniper. A balanced
  assault rifle often has no useful Sidearm 2.
- **Capacity/mass:** a heavy candidate must show the Simple Sidearms mass
  blocker. If no slot is free, the pawn still keeps Sidearm 1 and the UI explains
  why Sidearm 2 is empty.
- **Pacifist:** a colonist incapable of violence is not managed for hand or
  sidearm weapons. Candidate blockers may be shown for reference, but no equip
  or drop job should be issued.
- **Upgrades:** the Slot column fits `Sidearm 1` and `Sidearm 2` without
  ellipsizing, and Sidearm 2 demand rows appear when a useful complement is
  craftable or buyable.

### Testing SS hand alignment (0.12.0)

Use the isolated save with Simple Sidearms enabled and **Align Simple Sidearms
hand preference** on.

- **Default/preferred weapon:** pick a colonist whose current SS default is a
  sidearm but whose Armory Hand slot plans another carried weapon. After a
  planner pass, the detail line `Simple Sidearms - Default ranged weapon: ...`
  must name the Armory hand, and the pawn must hold it. The old sidearm stays in
  the bag when it still covers a gap.
- **White dots and icon order:** open the SS gizmo. The two white dots must be on
  the Armory hand. Because the hand is equipped, it is the first icon; the other
  icons follow SS's equipped-first/value sort. Armory does not reorder icons.
- **Unremembered hand:** choose a hand that SS shows with the diagonal temporary
  marker. After alignment it must be in SS's remembered list, the diagonal marker
  must disappear, and it must remain equipped through a later planner pass.
- **Melee pawn:** a melee colonist's `Preferred melee weapon` must be the Armory
  hand (for example a persona monosword), not a stale ranged default.
- **Alignment off:** turn the setting off and repeat. No `Align SS hand` log
  entries should appear; SS's default and the equipped weapon must remain
  unchanged.
- **Forced states:** force a weapon or force unarmed in the SS gizmo, run several
  planner passes, and verify Armory changes neither the default nor the forced
  state. Remove the force and verify alignment resumes.
- **No hand-chasing:** after alignment, watch for several planner intervals. The
  hand, SS default and Armory Hand line must stay on the same weapon; there must
  be no alternating align/equip cycle.

### Testing the hand type and auto-align (0.10.0)

The setup script leaves automatic equipping **off** in the isolated config. For
an auto-align test, prepare without `-Launch`, then set `autoEquipEnabled` to
`True` in `%TEMP%\opencode\armory-live\Config\Mod_Armory_ArmoryMod.xml`.

With Simple Sidearms removed from `ModsConfig.xml`:

- a clear shooter holding a melee weapon should resolve `ranged` with
  `mismatch yes`, and the planner should log
  `Switching hand type from melee to ranged`;
- a pawn with a bonded melee weapon, `Brawler`, a ranged-blocking shield belt or
  higher melee passion should resolve `melee`;
- use **Change type** on a pawn, set **Melee**, and verify the role changes to
  `melee (manual hand type)`, the ranged switch is not queued, and the choice
  survives save/load;
- close and reopen the tab after changing the type; the snapshot must rebuild
  immediately rather than keeping the old type for one planner pass.

Without Simple Sidearms, the Colonists list should show only the Hand line and
the pawn detail should say `Sidearm slots: require Simple Sidearms.` The default
hand limit is 15. With Simple Sidearms loaded, the Sidearm 1 and Sidearm 2 lines
return and the automatic limits are 5 hand / 5 sidearm. Both Settings sliders use
the same 1-50 range; uncheck **Automatic ... candidate limit** to set an explicit
value.

With Simple Sidearms enabled and respected, every pawn should resolve
`(Simple Sidearms mode)`, the Armory hand-type override should be ignored, and
sidearm jobs should still queue.

### Testing comparison, materials, role filters and value (0.10.4-0.11.x)

- **Weapons:** the list shows DPS, DPSA, AP, separate percentage
  `aC`/`aS`/`aM`/`aL` columns, Range, Best colonist and Best use. Click each
  header to sort; the active header shows `v`/`^`. DPSA is the generic four-band score, not the
  colonist-fit score. Switch the role buttons to Line/Marksman and confirm the
  Best column shows that role's pawn and score. Open a ranged weapon and confirm
  the right detail header shows complete Aim and Cooldown values without
  clipping.
- **Upgrades:** stuffable weapons appear once per material. Hover a Cost cell for
  the full ingredients, current stock and total for the needed copies. Each
  ingredient is shown as an icon with an `x` count; an orange count means stock
  cannot cover all needed copies. Select a role and verify the list is limited to
  rows with wants from that role and the Score column changes to that role's
  score. Turn on Show owned and confirm the Owned section lists actual
  quality/material copies with no cost.
- **Material bills:** click Craft on a specific material row and verify the bill's
  ingredient filter allows only that stuff (plus fixed ingredients), with
  `limitToAllowedStuff` set. Verify the bill is named `Armory: <material item>`
  (or `Armory <material item>` when the 28-character rename limit requires it)
  instead of the generic recipe name. The bill should still be queueable when
  the material is short.
- **Upgrades layout (0.10.5):** the first explanatory line must not be clipped at
  its bottom, Wanted by/Owned should be compact, and normal bench names in Source
  should fit without a redundant shortage suffix.
- **Market value (0.10.9–0.10.10):** `Value` is the second column in both tables.
  The Weapons `Value` matches the item's market value and changes with quality
  and material; click the header to sort. Amounts such as `$4,300` must not be
  ellipsized. The Upgrades `Value` shows a normal-quality per-copy value for
  Craft, Research and Buy rows; with Show owned, Owned rows use the actual best
  copy. Tooltips must say the value is before negotiator skill and buy/sell
  multipliers.
- **Weapons detail pane (0.10.10-0.11.x):** at the normal window size the
  `Score = ...` formula is fully visible with no clipped bottom. A blocked best
  hand or sidearm fit shows a short headline and an indented `Reason: ...` line
  that fits the narrower pane. When both hand and sidearm fits are blocked, no
  extra blank line appears between the hand reason and the sidearm headline.
- **Pending pickup jobs (0.10.8-0.11.x):** put a colonist who needs a sidearm in
  bed or otherwise busy for several planner cycles. The pawn must have at most
  one `EquipSecondary` job in `pawn.jobs.jobQueue` at any time; Armory should add
  no further pickup jobs while it is queued. Wake the pawn and verify exactly one
  sidearm is added. A save that already contains duplicate queued jobs should
  drain without Armory adding more; Armory now also removes surviving extras
  through Simple Sidearms' intentional-drop API.
- **No-SS test:** the Weapons and Upgrades comparisons still work without Simple
  Sidearms; sidearm-only rows may disappear, as expected.

### Testing double-click map jumps (0.10.3)

Double-click a spawned colonist row in **Colonists** and a loose weapon row in
**Weapons**. Armory should close, the camera should center on that pawn or item,
and the inspect pane should show it selected. The first click still selects the
row normally. A despawned colonist (pod, casket, caravan, another map) does not
jump because there is no spawned thing to select; that is intentional. The live
harness can call `MainTabWindow_Armory.JumpToThing` through reflection to verify
the camera and selection, but it does not synthesize the double-click itself.

---

## Pitfalls (learned the hard way)

- **Never load from the entry scene.** Calling
  `SavedGameLoaderNow.LoadGameFromSaveFileNow` while the main menu is up produced
  `InvalidCastException` in `Game.LoadGame`, `WorldGrid` index errors from Colony
  Manager Redux, and a map with **0 colonists**. Loading from the play scene,
  after `-quicktest` has initialised a map, works.
- **The built-in autostart is unreliable.** `SaveGameFilesUtility.GetAutostartSaveFile()`
  returns `Prefs.DevMode ? AllSavedGameFiles.FirstOrDefault() : null`, but it
  returned `null` on the isolated quicktest even with DevMode on and exactly one
  save present. Do not depend on it.
- `LoadGameFromSaveFileNow` takes the **name without `.rws`**;
  `GenFilePaths.FilePathForSavedGame` appends the extension.
- **Wait for the quicktest map before queuing the load.** The quicktest map
  initialises the world grid; queueing earlier reintroduces the 0-colonist load.
- **Do not key load completion on `Map.uniqueID` changing.** A save's first map
  can also have id `0`, the same id the quicktest map receives; that looked like
  "still loading" and timed out. Wait for
  `LongEventHandler.AnyEventNowOrWaiting` to become false instead.
- Dev mode auto-opens the **debug log window**; close `Dialog_Debug` before the
  screenshot (`Find.WindowStack.TryRemove`).
- The user's mod list may print an "incompatible version" warning (in this setup,
  `PatientSomeone.StorageTypeCategories`); `-quicktest` still proceeds.
- The isolated save is a **copy**. Dropping/equipping in automation affects only
  the copy.
- Do not force-close the user's running game. The script refuses without
  `-Force`.
- Remove the scaffold and rebuild before committing; `Assemblies/*.dll` is
  gitignored, but leaving a test hook in source is not acceptable.

---

## UI interaction

The scaffold can open the Armory tab and select Upgrades programmatically, but it
does not click arbitrary UI. For a clicked row or a different tab, set the
relevant private field by reflection in the scaffold:

- `MainTabWindow_Armory.view` — `ArmoryView` (`Colonists`, `Weapons`, `Upgrades`, `Log`).
- `MainTabWindow_Armory.expandedDemandDef` — `ThingDef` to expand an Upgrades row.

Avoid `SendInput`/key emulation; it steals focus and can interfere with the user.
Unity's `ScreenCapture` and the reflection hooks are sufficient for unattended
checks.
