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

Without Simple Sidearms, the Colonists list should hide the **Backup** column,
widen **Hand** and **Status**, and show `Backup slot: requires Simple Sidearms.`
in the pawn detail. The default hand limit is 15. With Simple Sidearms loaded,
the Backup column returns and the automatic limits are 5 hand / 5 backup. Both
Settings sliders use the same 1–50 range; uncheck **Automatic ... candidate limit**
to set an explicit value.

With Simple Sidearms enabled and respected, every pawn should resolve
`(Simple Sidearms mode)`, the Armory hand-type override should be ignored, and
backup jobs should still queue.

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
