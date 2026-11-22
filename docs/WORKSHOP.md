# Steam Workshop release copy and checklist

Status: **0.10.10 development build**. The Workshop item was created with 0.9.2
and updated to 0.10.8 on 21 November 2026; its page is being made public.
0.10.10 is not uploaded yet. Any remaining Steam automated content check is
external. 0.10.0 added typed hands,
passion-based role assignment and automatic hand-type alignment; 0.10.1
polished the UI and list limits; 0.10.2 hardens backup management and
unusable-backup baselines; 0.10.3 adds double-click map jumps; 0.10.4 adds
weapon comparison, material-specific upgrade variants and role filters; 0.10.5
polishes the accuracy columns and table widths; 0.10.6 shows accuracy as
percentages and costs as icon/count groups; 0.10.7 names Armory-created bills;
0.10.8 stops resting pawns from accumulating duplicate pickup jobs; 0.10.9
adds base market value columns; 0.10.10 moves Value second and polishes the
Weapons detail pane. Test locally before uploading an update. Do not bump to 1.0
yet.

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
- Check the mod icon, banner, tabs, version `0.10.10`, default-on automation,
  colonist-bar order, the `Best vs current` column, the `Change type` /
  `Change range` buttons, the Weapons percentage accuracy columns/role filters,
  the material-specific Upgrades rows/icon costs, and that Crafted bills are
  named `Armory: …`.
- Let a colonist with a pending backup pickup rest for several planner cycles
  and verify the queue never holds more than one `EquipSecondary` job. After
  waking, exactly one backup should be added.
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
  add the GitHub link to the Workshop page description manually.

## Workshop screenshots

Current gallery source: `docs/screenshots/` (3440x1440, captured with 0.10.8).
These predate the Value column and the 0.10.10 Weapons layout, so refresh them
before uploading 0.10.10. Upload in this order:

1. [`1_colonists.jpg`](screenshots/1_colonists.jpg) — Colonists tab: hand, role, status and ranked alternatives.
2. [`2_weapons.jpg`](screenshots/2_weapons.jpg) — Weapons tab: DPS, AP, per-range accuracy and the best colonist.
3. [`3_upgrades.jpg`](screenshots/3_upgrades.jpg) — Upgrades tab: material variants, per-copy costs and what to craft next.

Keep these files as the Workshop gallery source of truth and replace them when
the UI changes rather than adding version-numbered folders.

## Suggested Workshop description

The current page uses the concise version below. Steam keeps the page
description on later uploads, so paste this block manually when the copy
changes. The in-game description still comes from `About.xml`.

```text
[b]The right weapon, in the right hands.[/b]

Armory is a weapon inspector and automatic manager for RimWorld 1.6. It automatically equips each colonist with the best available weapon for their skills, passions, traits and combat role, and shows what to craft, research or buy next. It works especially well with Simple Sidearms.

This mod started as a fun side project. Bug reports, ideas and contributions are welcome:
https://github.com/fklemme/Armory

Thanks to the creators of Auto Arm, Equipment Manager, and Quartermaster for the inspiration.

[b]Optional integration[/b]
[list]
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=927155256]Simple Sidearms[/url] (recommended)
[/list]

[b]Known conflicts[/b]
[list]
[*]None so far.
[/list]
```

## Update checklist for future versions

1. Bump `modVersion` in `About.xml` (and README/HANDOFF).
2. Run `Publish-Armory.ps1 -Prepare`.
3. Upload with **Update on Steam Workshop**.
4. Paste the current description from **Suggested Workshop description** on
   Steam; re-uploading does not overwrite the page copy.
5. Replace `docs/screenshots/` and the Workshop gallery when the UI changes.
6. Keep `docs\workshop-id.txt` at `3842538224`; the prepare script seeds it into
   `About\PublishedFileId.txt`.
7. Run `-Restore`.
