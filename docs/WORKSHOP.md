# Steam Workshop release copy and checklist

Status: **0.10.5 development build**. The Workshop item was created with 0.9.2
and is still hidden while Steam's automated content check runs. 0.10.0 added
typed hands, passion-based role assignment and automatic hand-type alignment;
0.10.1 polished the UI and list limits; 0.10.2 hardens backup management and
unusable-backup baselines; 0.10.3 adds double-click map jumps; 0.10.4 adds
weapon comparison, material-specific upgrade variants and role filters; 0.10.5
polishes the accuracy columns and table widths. Test locally before uploading an
update. Do not bump to 1.0 yet.

- Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3842538224
- Source and issues: https://github.com/fklemme/Armory
- Workshop ID: `3842538224` (stored in `docs/workshop-id.txt`)

`tools/Publish-Armory.ps1` builds a clean release copy and swaps it into the
game's `Mods` folder. The development workspace stays untouched.

## What gets uploaded

Only these runtime files belong in the release copy:

```text
Armory/
  About/
    About.xml
    Preview.png
    ModIcon.png
    PublishedFileId.txt      (created by the first upload; keep it)
  Assemblies/
    Armory.dll
  Defs/
    MainButtonDefs.xml
  Textures/
    UI/Buttons/MainButtons/IconArmory.png
    UI/Icons/ArmoryBestStar.png
```

Never upload the development root: it contains `.git`, `Source`, `docs`,
`tools`, `obj`, and test artifacts. The in-game uploader sends the whole mod
root folder to Steam, not just `Assemblies` and `Defs`.

## Preparing the upload copy

Close RimWorld, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Publish-Armory.ps1 -Prepare
```

The script:

1. builds `Release`;
2. stages `ArmoryRelease\Armory`;
3. preserves an existing `PublishedFileId.txt` from the release copy, the game
   copy, `docs\workshop-id.txt`, or the workspace `About` folder;
4. removes the development junction at `RimWorld\Mods\Armory`;
5. copies the clean release copy there and writes a restore marker.

Then launch RimWorld **through Steam**, enable **Dev Mode** in Options, open
**Options → Mods**, select **Armory**, and use **More actions → Upload to Steam
Workshop**. Leave **Tag as translation** unchecked and confirm that you are the
content author.

After the upload:

1. the generated ID is preserved with the release copy and in
   `docs\workshop-id.txt` (already done for `3842538224`);
2. set the Workshop item visibility to **Hidden** or **Private** for the first
   private test;
3. run `Publish-Armory.ps1 -Restore` to remove the local release copy and
   recreate the development junction before the subscriber test.

`-Restore` also preserves the generated ID with the release copy, but
`docs\workshop-id.txt` is the durable repo copy.

## Private test

- The page must first clear Steam's automated content check; no action is needed
  while it says the item is awaiting analysis.
- Set the item to Hidden/Private, then subscribe to it.
- For a true subscriber test, temporarily move the local release copy out of
  `RimWorld\Mods` (or use a separate RimWorld install/Steam account), restart,
  and load a colony with only the Workshop copy active.
- Check the mod icon, banner, tabs, version `0.10.5`, default-on automation,
  colonist-bar order, the `Best vs current` column, the `Change type` /
  `Change range` buttons, the Weapons accuracy columns/role filters, and the
  material-specific Upgrades rows/costs.
- Test the automatic hand type with and without Simple Sidearms:
  - without SS, a clear shooter holding a melee weapon should be marked
    `switch type` and the planner should queue a ranged weapon;
  - a pawn with higher melee passion, Brawler, a shield belt or a bonded melee
    weapon should resolve as melee instead;
  - `Change type` should persist across save/load and immediately rebuild the
    recommendations.
- Test once with Simple Sidearms loaded and once without it: Simple Sidearms is
  optional and must never become a hard dependency.
- Check `Player.log` for Armory errors.

When the private test passes, set the item to **Public**.

## Title, description, preview and tags

- **Title:** from `<name>` in `About.xml` (`Armory`).
- **Preview:** `About/Preview.png` (640x360). It is re-uploaded on every update,
  so change this file rather than the image on the Steam page.
- **Tags:** set automatically to `Mod` and `1.6`. The in-game uploader has no
  custom tag field.
- **Description:** set from `About.xml` only when the item is created. Later
  updates do **not** overwrite the Workshop page description, so it is safe to
  polish it on Steam after the first upload. The in-game description still comes
  from `About.xml`.
- **Links:** `About.xml` now links to the GitHub source, and the README links to
  both the Workshop item and GitHub. Because the Workshop item already exists,
  add the GitHub link to the Workshop page description manually (the Links block
  in the suggested description below).

## Suggested Workshop description

Use this as a starting point on the Workshop page after the first upload. The
first upload will use the plain-text `About.xml` description; the BBCode below
is for the richer Steam page.

```text
[h1]The right weapon, in the right hands.[/h1]

Armory is a weapon inspector and automatic manager for RimWorld 1.6. It shows what every colonist wants to carry, why they are not carrying it, and what the colony should craft, research or buy next.

[h2]Four tabs[/h2]
[list]
[*][b]Colonists[/b] - each pawn's hand and backup weapon, hand type, ranged role, blockers, ranked alternatives, and a Best vs current comparison. Double-click a row to jump to that colonist on the map.
[*][b]Weapons[/b] - every loose weapon on the map, with raw DPS, generic DPSA, armor penetration, accuracy at four ranges, sortable comparison columns and role filters. Double-click a row to jump to that weapon on the map.
[*][b]Upgrades[/b] - every obtainable weapon variant that would improve a colonist, grouped into Craft, Research and Buy. Stuffable weapons are split by material (steel knife vs plasteel knife) with per-copy cost, stock shortage markers, owned and missing copies, score, gain and source. A Show owned toggle adds an Owned comparison section.
[*][b]Log[/b] - the automatic decisions Armory made this session, with the reason for each.
[/list]

[h2]Range-aware scoring[/h2]
Ranged weapons are scored at each pawn's preferred engagement distance using RimWorld's real accuracy curve. Each ranged pawn gets a ranged role - CQB, Assault, Line or Marksman - derived from their accuracy, skill and genes, with a per-colonist override.

[h2]Ranged and melee hands[/h2]
Every colonist has a hand type: Ranged or Melee. Armory chooses it from an explicit per-colonist override, a bonded weapon, a ranged-blocking shield belt, Brawler, shooting/melee passion, and finally skill. Only weapons of that type compete for the hand; ranged and melee are never ranked against each other. A colonist holding the wrong type is automatically switched to the best ready weapon of the chosen type. Simple Sidearms' own primary mode wins when it is loaded and respected.

[h2]Hand and backup loadouts[/h2]
With Simple Sidearms loaded, Armory also models one opposite-type backup. Blockers stay visible: forbidden, outside the allowed area, unreachable, reserved, biocoded, role restrictions, ideoligion dislike, and Simple Sidearms slot or mass limits.

[h2]Automatic management, on by default[/h2]
When no other weapon manager is active, Armory queues equip jobs for the best ready weapon, claims each weapon for one pawn, respects a swap cooldown, never unforbids anything, and treats unbonded persona weapons as manual-only. With Simple Sidearms, backup management keeps one legal opposite-type weapon and upgrades only the worst same-type weapon. Both automatic equipping and backup management can be disabled in settings.

[h2]Compatibility[/h2]
No hard dependencies. Simple Sidearms support is optional and read through reflection, so Armory works without it. No Harmony dependency.

[h2]Known limitations[/h2]
[list]
[*]One backup per pawn; same-type upgrades only. A bag full of extras can block the first backup.
[*]Upgrades assume normal quality, so a high-skill crafter's output can beat the estimate.
[*]Material variants are limited to stock/owned copies plus the best-scoring material; enable Show owned to compare existing copies.
[*]The DPSA column is a generic all-range comparison, not the per-colonist recommendation.
[*]No auto-unforbid, auto-haul, or raid arming yet.
[*]Caravans and weapons inside transport pods are not part of owned supply.
[*]Combat Extended ammo state is not scored beyond its stat effects.
[*]English only; localization not started.
[/list]

[h2]Version[/h2]
0.10.5 - separate sortable accuracy columns, wider tables, compact ranged detail header and Upgrades spacing polish.

[h2]Links[/h2]
[list]
[*][url=https://github.com/fklemme/Armory]Source code and issue tracker on GitHub[/url]
[/list]
```

## Update checklist for future versions

1. Bump `modVersion` in `About.xml` (and README/HANDOFF).
2. Run `Publish-Armory.ps1 -Prepare`.
3. Upload with **Update on Steam Workshop**.
4. Edit the change note and Workshop description on Steam if needed.
5. Keep `docs\workshop-id.txt` at `3842538224`; the prepare script seeds it into
   `About\PublishedFileId.txt`.
6. Run `-Restore`.
