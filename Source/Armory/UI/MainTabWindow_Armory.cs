using System.Collections.Generic;
using Armory.Compatibility;
using RimWorld;
using UnityEngine;
using Verse;

namespace Armory
{
    public class MainTabWindow_Armory : MainTabWindow
    {
        private enum ArmoryView
        {
            Colonists,
            Weapons,
            Upgrades,
            Log
        }

        private ArmoryView view = ArmoryView.Colonists;
        private int selectedPawn;
        private int selectedWeapon;
        private int selectedCandidate = -1;
        private Vector2 pawnListScroll;
        private Vector2 pawnDetailScroll;
        private Vector2 weaponListScroll;
        private Vector2 weaponDetailScroll;
        private Vector2 demandScroll;
        private Vector2 decisionScroll;
        private ThingDef expandedDemandDef;
        private bool refreshRequested;

        private readonly List<Candidate> visibleRanked = new List<Candidate>();
        private readonly List<PawnAnalysis> pawnScratch = new List<PawnAnalysis>();

        private const float RowHeight = 26f;
        private const float HeaderHeight = 22f;

        private static readonly Color HandSlotColor = new Color(0.70f, 0.90f, 0.70f);
        private static readonly Color BackupSlotColor = new Color(0.66f, 0.84f, 0.95f);
        private static readonly Color BothSlotColor = new Color(0.70f, 0.90f, 0.95f);
        private static readonly Color BlockedSlotColor = new Color(1f, 0.78f, 0.4f);

        private static int SidearmDisplayLimit
        {
            get
            {
                return ArmoryMod.Settings == null
                    ? 5
                    : ArmoryMod.Settings.EffectiveMaxSidearmCandidates;
            }
        }

        public override Vector2 RequestedTabSize
        {
            get { return new Vector2(1280f, 720f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Widgets.Label(inRect, "No map loaded.");
                return;
            }

            ArmoryMapComponent component = map.GetComponent<ArmoryMapComponent>();
            if (component == null)
            {
                Widgets.Label(inRect, "Armory map component is missing.");
                return;
            }

            ArmorySnapshot snapshot = component.GetSnapshot(refreshRequested);
            refreshRequested = false;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            float y = inRect.y;
            DrawTabBar(new Rect(inRect.x, y, inRect.width, 30f), snapshot);
            y += 34f;

            if (ArmoryMod.CompetitorLoaded)
            {
                Rect banner = new Rect(inRect.x, y, inRect.width, 32f);
                Widgets.DrawBoxSolid(banner, new Color(0.34f, 0.27f, 0.08f));
                bool previousWrap = Text.WordWrap;
                Text.WordWrap = false;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = new Color(1f, 0.9f, 0.62f);
                Widgets.Label(banner.ContractedBy(5f),
                    ArmoryMod.AutomationActive
                        ? "Automatic equipping is ON alongside another weapon manager. Competing jobs may cause weapon swapping."
                        : "Another weapon manager is loaded. Automatic equipping is off; use the override in Settings > Armory if you want Armory to act anyway.");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = previousWrap;
                y += 36f;
            }

            Rect content = new Rect(inRect.x, y, inRect.width, Mathf.Max(50f, inRect.yMax - y));
            switch (view)
            {
                case ArmoryView.Colonists:
                    DrawPawns(content, snapshot);
                    break;
                case ArmoryView.Weapons:
                    DrawWeapons(content, snapshot);
                    break;
                case ArmoryView.Upgrades:
                    DrawDemand(content, snapshot);
                    break;
                default:
                    DrawDecisions(content, snapshot);
                    break;
            }
        }

        private void DrawTabBar(Rect rect, ArmorySnapshot snapshot)
        {
            float x = rect.x;
            if (TabButton(new Rect(x, rect.y, 110f, rect.height), "Colonists", view == ArmoryView.Colonists,
                    "Each colonist's current hand and backup weapon, status and ranked alternatives. Listed in colonist-bar order; drag portraits to reorder. Double-click a row to jump to that colonist on the map."))
            {
                view = ArmoryView.Colonists;
                selectedCandidate = -1;
            }
            x += 114f;
            if (TabButton(new Rect(x, rect.y, 110f, rect.height), "Weapons", view == ArmoryView.Weapons,
                    "Loose weapons on this map, their best colonist and best slot. Double-click a row to jump to that weapon on the map."))
            {
                view = ArmoryView.Weapons;
                selectedCandidate = -1;
            }
            x += 114f;
            if (TabButton(new Rect(x, rect.y, 110f, rect.height), "Upgrades", view == ArmoryView.Upgrades,
                    "Upgrade options: what to craft, research or buy for the colony."))
            {
                view = ArmoryView.Upgrades;
                selectedCandidate = -1;
            }
            x += 114f;
            if (TabButton(new Rect(x, rect.y, 110f, rect.height), "Log", view == ArmoryView.Log,
                    "Automatic weapon decisions made this session, newest first."))
            {
                view = ArmoryView.Log;
                selectedCandidate = -1;
            }

            string info = string.Format("{0} colonists  |  {1} loose weapons  |  updated {2}",
                snapshot.Pawns.Count,
                snapshot.Weapons.Count,
                FormatSnapshotAge(snapshot));

            bool previousWrap = Text.WordWrap;
            Text.WordWrap = false;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Rect infoRect = new Rect(rect.xMax - 540f, rect.y, 370f, rect.height);
            Widgets.Label(infoRect, info);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = previousWrap;
            TooltipHandler.TipRegion(infoRect,
                "Colonists and loose weapons on this map. The age shows when the lists were last rebuilt; Armory rebuilds them after the snapshot refresh interval in Settings.");

            Rect refreshRect = new Rect(rect.xMax - 166f, rect.y + 2f, 74f, rect.height - 4f);
            if (Widgets.ButtonText(refreshRect, "Refresh"))
            {
                refreshRequested = true;
            }
            TooltipHandler.TipRegion(refreshRect,
                "Rebuild the colonist, weapon and upgrade lists now.");
            Rect settingsRect = new Rect(rect.xMax - 88f, rect.y + 2f, 88f, rect.height - 4f);
            if (Widgets.ButtonText(settingsRect, "Settings"))
            {
                Find.WindowStack.Add(new Dialog_ModSettings(ArmoryMod.Instance));
            }
            TooltipHandler.TipRegion(settingsRect, "Open Armory settings.");
        }

        private static string FormatSnapshotAge(ArmorySnapshot snapshot)
        {
            float age = (Find.TickManager.TicksGame - snapshot.BuiltTick) / 60f;
            return age < 1f ? "just now" : string.Format("{0:0}s ago", age);
        }

        private static bool TabButton(Rect rect, string label, bool selected, string tooltip)
        {
            if (selected)
            {
                Widgets.DrawBoxSolidWithOutline(rect, new Color(0.24f, 0.34f, 0.44f), new Color(0.7f, 0.8f, 0.9f));
            }
            else
            {
                Widgets.DrawBoxSolid(rect, new Color(0.16f, 0.16f, 0.16f));
            }
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(rect, tooltip);
            return Widgets.ButtonInvisible(rect);
        }

        // ---------------------------------------------------------------- Pawns

        private static bool IsRowDoubleClick(Rect row)
        {
            return Event.current.type == EventType.MouseDown &&
                   Event.current.button == 0 &&
                   Event.current.clickCount == 2 &&
                   Mouse.IsOver(row);
        }

        /// <summary>
        /// Move the camera to a map thing and select it, then close Armory. Used by
        /// double-clicking a colonist or loose weapon row.
        /// </summary>
        private static void JumpToThing(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Map == null)
            {
                return;
            }
            CameraJumper.TryJumpAndSelect(thing);
            Find.MainTabsRoot.EscapeCurrentTab(false);
        }

        private void DrawPawns(Rect area, ArmorySnapshot snapshot)
        {
            if (snapshot.Pawns.Count == 0)
            {
                Widgets.Label(area, "No free colonists on this map.");
                return;
            }
            if (selectedPawn < 0 || selectedPawn >= snapshot.Pawns.Count)
            {
                selectedPawn = 0;
            }

            float listWidth = Mathf.Floor(area.width * 0.56f);
            Rect listRect = new Rect(area.x, area.y, listWidth, area.height);
            Rect detailRect = new Rect(listRect.xMax + 8f, area.y, area.width - listWidth - 8f, area.height);

            Widgets.DrawBoxSolid(listRect, new Color(0.12f, 0.12f, 0.12f));
            Widgets.DrawBoxSolid(detailRect, new Color(0.12f, 0.12f, 0.12f));

            Rect listInner = listRect.ContractedBy(6f);
            Rect scrollRect = new Rect(listInner.x, listInner.y + HeaderHeight, listInner.width,
                listInner.height - HeaderHeight);
            float rowWidth = scrollRect.width - 20f;
            bool showBackup = SimpleSidearmsAdapter.Active;
            DrawPawnListHeader(new Rect(listInner.x, listInner.y, rowWidth, HeaderHeight), showBackup);
            Widgets.BeginScrollView(scrollRect, ref pawnListScroll,
                new Rect(0f, 0f, rowWidth, snapshot.Pawns.Count * RowHeight));

            Pawn jumpPawn = null;
            for (int i = 0; i < snapshot.Pawns.Count; i++)
            {
                Rect row = new Rect(0f, i * RowHeight, rowWidth, RowHeight);
                if (i == selectedPawn)
                {
                    Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.13f));
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(row);
                }
                if (IsRowDoubleClick(row))
                {
                    Event.current.Use();
                    jumpPawn = snapshot.Pawns[i].Pawn;
                }
                if (Widgets.ButtonInvisible(row) && selectedPawn != i)
                {
                    selectedPawn = i;
                    selectedCandidate = -1;
                    pawnDetailScroll = Vector2.zero;
                }
                DrawPawnRow(row, snapshot.Pawns[i], showBackup);
            }

            Widgets.EndScrollView();
            DrawPawnDetail(detailRect.ContractedBy(8f), snapshot.Pawns[selectedPawn]);
            if (jumpPawn != null)
            {
                JumpToThing(jumpPawn);
            }
        }

        private static void PawnColumns(Rect row, bool showBackup, out Rect name, out Rect current,
            out Rect backup, out Rect status, out Rect delta)
        {
            float width = row.width;
            name = new Rect(row.x, row.y, width * 0.12f, row.height);
            current = new Rect(row.x + width * 0.12f, row.y, width * (showBackup ? 0.24f : 0.40f),
                row.height);
            if (showBackup)
            {
                backup = new Rect(row.x + width * 0.36f, row.y, width * 0.24f, row.height);
                status = new Rect(row.x + width * 0.60f, row.y, width * 0.26f, row.height);
            }
            else
            {
                backup = new Rect(row.x + width * 0.52f, row.y, 0f, row.height);
                status = new Rect(row.x + width * 0.52f, row.y, width * 0.34f, row.height);
            }
            delta = new Rect(row.x + width * 0.86f, row.y, width * 0.14f, row.height);
        }

        private static void WeaponColumns(Rect row, out Rect label, out Rect power, out Rect range,
            out Rect best, out Rect status)
        {
            const float gap = 8f;
            float width = row.width;
            float labelWidth = width * 0.42f;
            float powerWidth = width * 0.09f;
            float rangeWidth = width * 0.09f;
            float bestWidth = width * 0.19f;

            label = new Rect(row.x, row.y, labelWidth - gap, row.height);
            power = new Rect(row.x + labelWidth, row.y, powerWidth - gap, row.height);
            range = new Rect(row.x + labelWidth + powerWidth, row.y, rangeWidth - gap, row.height);
            best = new Rect(row.x + labelWidth + powerWidth + rangeWidth, row.y,
                bestWidth - gap, row.height);
            status = new Rect(row.x + labelWidth + powerWidth + rangeWidth + bestWidth, row.y,
                width - labelWidth - powerWidth - rangeWidth - bestWidth, row.height);
        }

        private static void DrawPawnListHeader(Rect rect, bool showBackup)
        {
            PawnColumns(rect, showBackup, out Rect name, out Rect current, out Rect backup,
                out Rect status, out Rect delta);
            Color color = new Color(0.68f, 0.68f, 0.68f);
            DrawCell(name, "Colonist", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(current, "Hand", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            if (showBackup)
            {
                DrawCell(backup, "Backup", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            }
            DrawCell(status, "Status", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(delta, "Best vs current", TextAnchor.MiddleRight, GameFont.Tiny, color);

            TooltipHandler.TipRegion(current,
                showBackup
                    ? "The weapon currently equipped in this colonist's hand."
                    : "The weapon currently equipped in this colonist's hand. The Backup column is hidden because Simple Sidearms is not loaded.");
            if (showBackup)
            {
                TooltipHandler.TipRegion(backup,
                    "Weapons this colonist carries for the opposite-type backup slot.");
            }
            TooltipHandler.TipRegion(status,
                "What Armory will do for this colonist, or why it is waiting. Hover a status for the full text.");
            TooltipHandler.TipRegion(delta,
                "The best available hand candidate compared with the current hand weapon. Negative means the current weapon is still better.");
        }

        private static void DrawPawnRow(Rect rect, PawnAnalysis pawn, bool showBackup)
        {
            PawnColumns(rect, showBackup, out Rect name, out Rect current, out Rect backup,
                out Rect status, out Rect delta);

            Color rowColor = pawn.Available ? Color.white : new Color(0.58f, 0.58f, 0.58f);
            DrawCell(name, pawn.Pawn.LabelShort, TextAnchor.MiddleLeft, GameFont.Small, rowColor);

            string currentText = pawn.Current == null ? "unarmed" : pawn.Current.LabelCap;
            DrawCell(current, currentText, TextAnchor.MiddleLeft, GameFont.Small, rowColor);
            TipCell(current, pawn.Current == null
                ? "No hand weapon equipped."
                : string.Format("Equipped hand weapon: {0} (score {1:0}).", currentText, pawn.CurrentScore));

            if (showBackup)
            {
                string backupText = CarriedBackupText(pawn);
                DrawCell(backup, backupText, TextAnchor.MiddleLeft, GameFont.Small,
                    backupText == "none" ? new Color(0.75f, 0.75f, 0.75f) : rowColor);
                TipCell(backup, pawn.SidearmState != null
                    ? "Carried backups: " + backupText
                    : "Simple Sidearms is not active for this colonist, so there is no managed backup slot.");
            }

            Color statusColor;
            string statusText = PawnStatus(pawn, out statusColor);
            DrawCell(status, statusText, TextAnchor.MiddleLeft, GameFont.Small, statusColor);
            TipCell(status, statusText);

            Candidate top = pawn.TopReady;
            float deltaBase = pawn.HandBaselineScore;
            string deltaText;
            if (pawn.HandTypeMismatch)
            {
                deltaText = top == null ? "no target" : "switch type";
            }
            else
            {
                deltaText = top == null
                    ? "-"
                    : deltaBase > 0.01f ? FormatPct(top.Score / deltaBase - 1f) : "new slot";
            }
            DrawCell(delta, deltaText, TextAnchor.MiddleRight,
                GameFont.Small, pawn.Available && top != null ? Color.white : rowColor);
            TipCell(delta, DeltaTooltip(pawn, top));
        }

        private static string DeltaTooltip(PawnAnalysis pawn, Candidate top)
        {
            if (pawn.HandTypeMismatch && pawn.RoleIsRanged.HasValue)
            {
                string target = pawn.RoleIsRanged.Value ? "ranged" : "melee";
                return top == null
                    ? "This colonist's hand type is " + target +
                      ", but no ready " + target + " hand weapon is available yet. The current weapon is kept."
                    : string.Format(
                        "This colonist's hand type is {0}. Armory will switch from the current {1} weapon to {2} (score {3:0}).",
                        target,
                        pawn.Current != null && pawn.Current.def != null && pawn.Current.def.IsMeleeWeapon
                            ? "melee"
                            : "ranged",
                        top.Weapon.Label, top.Score);
            }
            if (top == null)
            {
                return "No usable hand candidate is currently available.";
            }
            if (pawn.HandBaselineScore <= 0.01f)
            {
                return string.Format(
                    "Best available: {0} (score {1:0}). The colonist has no hand weapon, so this would fill the empty slot.",
                    top.Weapon.Label, top.Score);
            }

            int percent = Mathf.RoundToInt(Mathf.Abs(top.Score / pawn.HandBaselineScore - 1f) * 100f);
            return string.Format("Best available: {0} (score {1:0}) is {2}% {3} than the current hand weapon.",
                top.Weapon.Label, top.Score, percent,
                top.Score >= pawn.HandBaselineScore ? "better" : "worse");
        }

        private static string CarriedBackupText(PawnAnalysis pawn)
        {
            if (pawn.SidearmState == null)
            {
                return "-";
            }
            List<string> labels = new List<string>(2);
            for (int i = 0; i < pawn.CarriedSidearms.Count; i++)
            {
                CarriedSidearm carried = pawn.CarriedSidearms[i];
                if (pawn.IsBackupType(carried.Thing.def))
                {
                    labels.Add(carried.Usable
                        ? carried.Thing.LabelCap
                        : carried.Thing.LabelCap + " (unusable)");
                }
            }
            return labels.Count == 0 ? "none" : string.Join(", ", labels);
        }

        private static int CountBackups(PawnAnalysis pawn)
        {
            int count = 0;
            for (int i = 0; i < pawn.CarriedSidearms.Count; i++)
            {
                if (pawn.IsBackupType(pawn.CarriedSidearms[i].Thing.def))
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountUsableBackups(PawnAnalysis pawn)
        {
            int count = 0;
            for (int i = 0; i < pawn.CarriedSidearms.Count; i++)
            {
                CarriedSidearm carried = pawn.CarriedSidearms[i];
                if (carried.Usable && pawn.IsBackupType(carried.Thing.def))
                {
                    count++;
                }
            }
            return count;
        }

        private static bool IsDroppableBackup(ThingWithComps thing)
        {
            return thing != null && !thing.Destroyed && thing.def != null &&
                   (thing.def.IsRangedWeapon || thing.def.IsMeleeWeapon) &&
                   !thing.def.destroyOnDrop && thing.GetComp<CompBladelinkWeapon>() == null;
        }

        private static bool TryGetWorstBackup(PawnAnalysis pawn, out float score)
        {
            score = 0f;
            bool found = false;
            for (int i = 0; i < pawn.CarriedSidearms.Count; i++)
            {
                CarriedSidearm carried = pawn.CarriedSidearms[i];
                if (!carried.Usable || !pawn.IsBackupType(carried.Thing.def) ||
                    !IsDroppableBackup(carried.Thing))
                {
                    continue;
                }
                if (!found || carried.Score < score)
                {
                    score = carried.Score;
                    found = true;
                }
            }
            return found;
        }

        private static SidearmCandidate BestReadyBackup(PawnAnalysis pawn)
        {
            for (int i = 0; i < pawn.SidearmCandidates.Count; i++)
            {
                if (pawn.SidearmCandidates[i].Ready)
                {
                    return pawn.SidearmCandidates[i];
                }
            }
            return null;
        }

        private static SidearmCandidate BestBackupUpgrade(PawnAnalysis pawn)
        {
            ArmorySettings settings = ArmoryMod.Settings;
            if (settings != null && !settings.replaceWorseSidearms)
            {
                return null;
            }
            float worst;
            if (!TryGetWorstBackup(pawn, out worst))
            {
                return null;
            }
            float threshold = settings == null ? 0.1f : settings.upgradeThreshold;
            float minimum = Mathf.Max(worst, 0.01f) * (1f + threshold);
            for (int i = 0; i < pawn.SidearmCandidates.Count; i++)
            {
                SidearmCandidate candidate = pawn.SidearmCandidates[i];
                if ((candidate.Ready || candidate.UpgradeAfterDrop) && candidate.Score > minimum)
                {
                    return candidate;
                }
            }
            return null;
        }

        private static bool WaitsForHandType(PawnAnalysis pawn)
        {
            if (!pawn.RoleIsRanged.HasValue || pawn.Current == null || pawn.Current.def == null)
            {
                return false;
            }
            bool currentRanged = pawn.Current.def.IsRangedWeapon;
            bool currentMelee = pawn.Current.def.IsMeleeWeapon;
            if (!currentRanged && !currentMelee)
            {
                return false;
            }
            return currentRanged != pawn.RoleIsRanged.Value;
        }

        private static string PawnStatus(PawnAnalysis pawn, out Color color)
        {
            if (!pawn.Available)
            {
                color = new Color(0.6f, 0.6f, 0.6f);
                return pawn.UnavailableReason;
            }
            if (SimpleSidearmsAdapter.RespectEnabled && pawn.SidearmState != null &&
                pawn.SidearmState.AutomationVeto)
            {
                color = new Color(1f, 0.78f, 0.4f);
                return pawn.SidearmState.VetoReason;
            }
            if (pawn.AutomationBlocked && !string.IsNullOrEmpty(pawn.AutomationNote))
            {
                color = BlockedSlotColor;
                return "Blocked: " + pawn.AutomationNote;
            }

            if (pawn.SidearmState != null && CountBackups(pawn) > 1)
            {
                color = BlockedSlotColor;
                return "Multiple backups";
            }

            ArmorySettings settings = ArmoryMod.Settings;
            float threshold = settings == null ? 0.1f : settings.upgradeThreshold;
            float baseline = pawn.HandBaselineScore;
            Candidate topReady = pawn.TopReady;

            bool hasHandWish = (pawn.HandTypeMismatch && topReady != null) ||
                               (topReady != null &&
                                (pawn.Current == null ||
                                 (baseline > 0.01f &&
                                  topReady.Score > baseline * (1f + threshold))));

            bool handTypeWait = pawn.SidearmState != null && WaitsForHandType(pawn);
            int usableBackupCount = CountUsableBackups(pawn);
            bool hasBackupWish = false;
            if (pawn.SidearmState != null && (settings == null || settings.manageSidearms))
            {
                hasBackupWish = usableBackupCount == 0
                    ? BestReadyBackup(pawn) != null
                    : BestBackupUpgrade(pawn) != null;
                if (handTypeWait)
                {
                    hasBackupWish = false;
                }
            }

            if (!string.IsNullOrEmpty(pawn.AutomationNote) && (hasHandWish || hasBackupWish))
            {
                color = new Color(1f, 0.78f, 0.4f);
                return "Paused: " + pawn.AutomationNote;
            }

            if (pawn.HandLocked)
            {
                if (hasBackupWish)
                {
                    color = BackupSlotColor;
                    return usableBackupCount == 0
                        ? "Backup ready"
                        : "Backup upgrade ready";
                }
                if (handTypeWait && BestReadyBackup(pawn) != null)
                {
                    color = BlockedSlotColor;
                    return "Backup waiting";
                }
                if (handTypeWait || hasHandWish)
                {
                    color = BlockedSlotColor;
                    return "Hand locked";
                }
                color = new Color(0.78f, 0.78f, 0.78f);
                return "Equipped (hand locked)";
            }

            if (pawn.HandTypeMismatch)
            {
                string target = pawn.RoleIsRanged.HasValue && pawn.RoleIsRanged.Value
                    ? "ranged"
                    : "melee";
                if (topReady != null)
                {
                    color = pawn.RoleIsRanged.HasValue && pawn.RoleIsRanged.Value
                        ? HandSlotColor
                        : BackupSlotColor;
                    return "Switching to " + target;
                }
                color = BlockedSlotColor;
                return "No " + target + " weapon";
            }

            if (handTypeWait && BestReadyBackup(pawn) != null)
            {
                color = BlockedSlotColor;
                return "Backup waiting";
            }

            if (pawn.Current == null && topReady != null)
            {
                color = HandSlotColor;
                return "Hand weapon available";
            }
            if (pawn.Current == null)
            {
                color = BlockedSlotColor;
                return "No hand weapon";
            }
            if (hasHandWish)
            {
                color = HandSlotColor;
                return "Hand upgrade available";
            }
            if (pawn.SidearmState == null)
            {
                color = new Color(0.78f, 0.78f, 0.78f);
                return "Hand equipped";
            }

            int backupCount = CountBackups(pawn);
            if (settings == null || !settings.manageSidearms)
            {
                color = new Color(0.72f, 0.72f, 0.72f);
                return backupCount > 0 ? "Backup carried" : "Backup management off";
            }
            if (backupCount == 0)
            {
                if (BestReadyBackup(pawn) != null)
                {
                    color = BackupSlotColor;
                    return "Backup available";
                }
                color = BlockedSlotColor;
                return pawn.SidearmCandidates.Count == 0 ? "No backup weapon" : "No usable backup";
            }

            if (BestBackupUpgrade(pawn) != null)
            {
                color = BackupSlotColor;
                return "Backup upgrade available";
            }

            color = new Color(0.78f, 0.78f, 0.78f);
            return "Fully equipped";
        }

        private void DrawPawnDetail(Rect rect, PawnAnalysis pawn)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 30f), pawn.Pawn.LabelShort);
            Text.Font = GameFont.Small;

            float y = rect.y + 30f;
            bool handTypeEditable = !pawn.HandTypeFromSimpleSidearms;
            Rect handLine = new Rect(rect.x, y,
                handTypeEditable ? Mathf.Max(40f, rect.width - 106f) : rect.width, 22f);
            string handTypeLabel = pawn.RoleIsRanged.HasValue
                ? (pawn.RoleIsRanged.Value ? "ranged" : "melee")
                : "not set";
            string handSource = string.IsNullOrEmpty(pawn.RoleSource)
                ? string.Empty
                : " (" + pawn.RoleSource + ")";
            DrawColoredLine(handLine,
                string.Format("Hand type: {0}{1}", handTypeLabel, handSource),
                new Color(0.78f, 0.86f, 0.96f));
            TipCell(handLine, string.Format(
                "Source: {0}. Current hand weapon score: {1:0}.",
                string.IsNullOrEmpty(pawn.RoleSource) ? "automatic" : pawn.RoleSource,
                pawn.CurrentScore));
            if (handTypeEditable)
            {
                Rect typeButton = new Rect(rect.xMax - 96f, y + 1f, 96f, 20f);
                if (Widgets.ButtonText(typeButton, "Change type"))
                {
                    OpenHandTypeMenu(pawn);
                }
                TooltipHandler.TipRegion(typeButton,
                    "Choose this colonist's hand type. Auto respects a bonded weapon, then a worn shield belt, Brawler, the higher Shooting/Melee passion and finally skill; melee needs a 3-level advantage, otherwise ranged is the default. Your manual choice always wins.");
            }
            y += 22f;

            if (pawn.HandTypeMismatch && pawn.RoleIsRanged.HasValue)
            {
                string from = pawn.Current != null && pawn.Current.def != null &&
                              pawn.Current.def.IsMeleeWeapon
                    ? "melee"
                    : "ranged";
                string to = pawn.RoleIsRanged.Value ? "ranged" : "melee";
                string mismatch = pawn.TopReady == null
                    ? string.Format("Hand type mismatch: no {0} weapon available; keeping the {1} weapon.", to, from)
                    : string.Format("Hand type mismatch: switching from {0} to {1}.", from, to);
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f), mismatch,
                    new Color(1f, 0.82f, 0.45f));
                y += 22f;
            }

            if (pawn.RoleIsRanged == true)
            {
                string roleText = string.Format("Ranged role: {0} ({1}, prefers ~{2} tiles)",
                    CombatScoring.Label(pawn.CombatRole),
                    pawn.RoleOverridden ? "manual override" : "automatic",
                    pawn.PreferredRange);
                DrawColoredLine(new Rect(rect.x, y, Mathf.Max(40f, rect.width - 106f), 22f),
                    roleText, new Color(0.78f, 0.86f, 0.96f));
                Rect roleButton = new Rect(rect.xMax - 96f, y + 1f, 96f, 20f);
                if (Widgets.ButtonText(roleButton, "Change range"))
                {
                    OpenCombatRoleMenu(pawn);
                }
                TooltipHandler.TipRegion(roleButton,
                    "Choose this colonist's preferred engagement distance. Automatic uses the accuracy curve, skill and genes.");
                y += 26f;
            }

            if (!SimpleSidearmsAdapter.Active)
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    "Backup slot: requires Simple Sidearms.",
                    new Color(0.68f, 0.72f, 0.78f));
                y += 22f;
            }
            else if (pawn.SidearmState == null)
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    "Backup slot: Simple Sidearms sidearm data is unavailable for this colonist.",
                    new Color(0.68f, 0.72f, 0.78f));
                y += 22f;
            }

            if (!string.IsNullOrEmpty(pawn.AutomationNote))
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    (pawn.AutomationBlocked ? "Automation blocked: " : "Automation paused: ") +
                    pawn.AutomationNote,
                    pawn.AutomationBlocked
                        ? new Color(1f, 0.72f, 0.55f)
                        : new Color(1f, 0.82f, 0.45f));
                y += 22f;
            }

            if (pawn.HandLocked)
            {
                string handLockText = pawn.SidearmState != null && WaitsForHandType(pawn)
                    ? "Hand locked: bonded weapon. Backup waits for the hand type to match."
                    : "Hand locked: bonded weapon in hand. Armory will not replace it; backups still run.";
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f), handLockText,
                    new Color(1f, 0.82f, 0.45f));
                y += 22f;
            }
            else if (pawn.Pawn.equipment != null && pawn.Pawn.equipment.bondedWeapon != null)
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    "Bonded persona weapon not in hand: another weapon may cause a mood penalty.",
                    new Color(1f, 0.82f, 0.45f));
                y += 22f;
            }

            if (pawn.Sidearms.Count > 0)
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    "Carried weapons: " + pawn.SidearmText, new Color(0.75f, 0.82f, 0.92f));
                y += 22f;
            }

            if (pawn.SidearmState != null)
            {
                bool veto = pawn.SidearmState.AutomationVeto && SimpleSidearmsAdapter.RespectEnabled;
                string summary = pawn.SidearmState.Summary();
                string first = summary;
                string second = null;
                int split = summary.IndexOf(" | ");
                if (split >= 0)
                {
                    first = summary.Substring(0, split);
                    second = summary.Substring(split + 3);
                }
                if (veto)
                {
                    first += "  -  Armory will not change the hand weapon";
                }

                Color sidearmColor = veto
                    ? new Color(1f, 0.8f, 0.42f)
                    : new Color(0.72f, 0.82f, 0.92f);
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    "Simple Sidearms · " + first, sidearmColor);
                y += 22f;

                if (second != null)
                {
                    DrawColoredLine(new Rect(rect.x + 14f, y, rect.width - 14f, 22f),
                        second, sidearmColor);
                    y += 22f;
                }

                if (pawn.BestCarriedPrimary != null && pawn.BestCarriedPrimary != pawn.Current)
                {
                    DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                        string.Format("Best carried hand weapon: {0}    Score {1:0}",
                            pawn.BestCarriedPrimary.LabelCap, pawn.BestCarriedPrimaryScore),
                        new Color(0.72f, 0.82f, 0.92f));
                    y += 22f;
                }
            }

            if (!pawn.Available)
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    "Unavailable: " + pawn.UnavailableReason + ". Preferences are shown for reference.",
                    new Color(0.72f, 0.72f, 0.72f));
                y += 22f;
            }

            Candidate bestReady = pawn.TopReady;
            Candidate bestAny = pawn.TopOverall;
            float threshold = ArmoryMod.Settings == null ? 0.10f : ArmoryMod.Settings.upgradeThreshold;
            float baselineScore = pawn.HandBaselineScore;
            bool carriedIsBaseline = pawn.BestCarriedPrimary != null &&
                                     pawn.BestCarriedPrimary != pawn.Current &&
                                     pawn.BestCarriedPrimaryScore >= pawn.HandBaselineScore;

            visibleRanked.Clear();
            bool showBlocked = (ArmoryMod.Settings == null || ArmoryMod.Settings.showBlocked) || !pawn.Available;
            for (int i = 0; i < pawn.Ranked.Count; i++)
            {
                if (showBlocked || pawn.Ranked[i].Ready)
                {
                    visibleRanked.Add(pawn.Ranked[i]);
                }
            }

            int configuredLimit = ArmoryMod.Settings == null
                ? 15
                : ArmoryMod.Settings.EffectiveMaxCandidates;
            int limit = Mathf.Min(visibleRanked.Count, Mathf.Max(1, configuredLimit));
            if (selectedCandidate >= limit)
            {
                selectedCandidate = -1;
            }

            PawnFitContext ledgerContext = null;
            for (int i = 0; i < limit; i++)
            {
                Candidate candidate = visibleRanked[i];
                if (candidate.Ledger == null)
                {
                    if (ledgerContext == null)
                    {
                        ledgerContext = PawnFitContext.Create(pawn.Pawn);
                    }
                    string ledger;
                    CombatScoring.Compute(ledgerContext, candidate.Weapon.Thing, out ledger);
                    candidate.Ledger = ledger;
                }
            }

            float worstBackup;
            bool hasWorstBackup = TryGetWorstBackup(pawn, out worstBackup);
            float backupMinimum = Mathf.Max(worstBackup, 0.01f) * (1f + threshold);
            int backupLimit = Mathf.Min(pawn.SidearmCandidates.Count, SidearmDisplayLimit);
            bool manageBackups = ArmoryMod.Settings == null || ArmoryMod.Settings.manageSidearms;
            bool replaceWorse = manageBackups &&
                                (ArmoryMod.Settings == null || ArmoryMod.Settings.replaceWorseSidearms);
            string backupType = pawn.RoleIsRanged.HasValue
                ? (pawn.RoleIsRanged.Value ? "melee" : "ranged")
                : "opposite type";

            Rect scrollRect = new Rect(rect.x, y, rect.width, Mathf.Max(40f, rect.yMax - y));
            float width = scrollRect.width - 20f;
            float contentHeight = PawnDetailHeight(pawn, limit, showBlocked);
            Widgets.BeginScrollView(scrollRect, ref pawnDetailScroll, new Rect(0f, 0f, width, contentHeight));

            float cursor = 0f;
            string handType = pawn.RoleIsRanged.HasValue
                ? (pawn.RoleIsRanged.Value ? "ranged" : "melee")
                : "not set";
            SectionHeader(ref cursor, width, "Hand slot (" + handType + ")");

            if (bestReady != null)
            {
                string comparison = pawn.HandTypeMismatch
                    ? "type switch"
                    : FormatPct(pawn.TopReadyDeltaPct) + " vs current";
                DrawColoredLine(new Rect(0f, cursor, width, 22f),
                    string.Format("Best available: {0}    Score {1:0}  ({2})",
                        bestReady.Weapon.Label, bestReady.Score, comparison),
                    new Color(0.75f, 0.92f, 0.75f));
                cursor += 22f;

                bool belowThreshold = baselineScore > 0.01f &&
                                      bestReady.Score <= baselineScore * (1f + threshold);
                if (belowThreshold)
                {
                    string baselineLabel = carriedIsBaseline
                        ? pawn.BestCarriedPrimary.LabelCap + " (best carried hand weapon)"
                        : "current weapon";
                    float delta = bestReady.Score / baselineScore - 1f;
                    int percent = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(delta) * 100f));
                    int thresholdPercent = Mathf.RoundToInt(threshold * 100f);
                    string thresholdText = delta > 0f
                        ? string.Format("only {0}% better (required: {1}%)", percent, thresholdPercent)
                        : string.Format("{0}% worse", percent);
                    DrawColoredLine(new Rect(0f, cursor, width, 22f),
                        string.Format("Keeping {0}: best alternative is {1}.",
                            baselineLabel, thresholdText),
                        new Color(1f, 0.85f, 0.5f));
                    cursor += 22f;
                }
            }
            else
            {
                string text = !pawn.Available
                    ? "Cannot equip while unavailable."
                    : bestAny == null ? "No hand-slot weapons on this map." : "No usable hand weapon: every candidate is blocked.";
                DrawColoredLine(new Rect(0f, cursor, width, 22f), text,
                    new Color(1f, 0.7f, 0.7f));
                cursor += 22f;
            }

            if (bestAny != null && (bestReady == null || bestAny != bestReady))
            {
                string comparison = pawn.HandTypeMismatch
                    ? "type switch"
                    : FormatPct(pawn.DeltaPct(bestAny)) + " vs current";
                DrawColoredLine(new Rect(0f, cursor, width, 22f),
                    string.Format("Highest-scoring hand weapon: {0}    Score {1:0}  ({2})  - {3}",
                        bestAny.Weapon.Label, bestAny.Score, comparison,
                        bestAny.Ready ? "can equip" : bestAny.Blocker),
                    bestAny.Ready ? Color.white : new Color(1f, 0.8f, 0.42f));
                cursor += 22f;
            }

            Widgets.DrawLineHorizontal(0f, cursor, width);
            cursor += 8f;

            if (limit > 0)
            {
                Color columnColor = new Color(0.62f, 0.62f, 0.62f);
                DrawCell(new Rect(32f, cursor, Mathf.Max(40f, width - 332f), 18f),
                    "Weapon", TextAnchor.MiddleLeft, GameFont.Tiny, columnColor);
                DrawCell(new Rect(width - 292f, cursor, 170f, 18f),
                    "Status", TextAnchor.MiddleLeft, GameFont.Tiny, columnColor);
                DrawCell(new Rect(width - 122f, cursor, 48f, 18f),
                    "Score", TextAnchor.MiddleRight, GameFont.Tiny, columnColor);
                DrawCell(new Rect(width - 70f, cursor, 66f, 18f),
                    "vs current", TextAnchor.MiddleRight, GameFont.Tiny, columnColor);
                cursor += 18f;
            }

            for (int i = 0; i < limit; i++)
            {
                Candidate candidate = visibleRanked[i];
                Rect row = new Rect(0f, cursor, width, 22f);
                if (selectedCandidate == i)
                {
                    Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.13f));
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(row);
                }
                if (Widgets.ButtonInvisible(row))
                {
                    selectedCandidate = selectedCandidate == i ? -1 : i;
                }

                DrawCell(new Rect(4f, cursor, 24f, 22f), (i + 1) + ".", TextAnchor.MiddleRight);
                Rect candidateLabel = new Rect(32f, cursor, Mathf.Max(40f, width - 332f), 22f);
                DrawCell(candidateLabel, candidate.Weapon.Label, TextAnchor.MiddleLeft);
                TipCell(candidateLabel, candidate.Weapon.Label);
                string status = candidate.Ready ? "Can equip" : candidate.Blocker;
                Rect candidateStatus = new Rect(width - 292f, cursor, 170f, 22f);
                DrawCell(candidateStatus, status, TextAnchor.MiddleLeft,
                    GameFont.Small, StatusColor(candidate));
                TipCell(candidateStatus, status);
                Rect candidateScore = new Rect(width - 122f, cursor, 48f, 22f);
                DrawCell(candidateScore, candidate.Score.ToString("0"), TextAnchor.MiddleRight);
                TipCell(candidateScore, "Combat score for this colonist.");
                Rect candidateDelta = new Rect(width - 70f, cursor, 66f, 22f);
                DrawCell(candidateDelta, pawn.HandTypeMismatch
                        ? "switch"
                        : FormatPct(pawn.DeltaPct(candidate)),
                    TextAnchor.MiddleRight);
                TipCell(candidateDelta, pawn.HandTypeMismatch
                    ? "This candidate is the chosen hand type; the equipped weapon is the other type."
                    : pawn.HandBaselineScore <= 0.01f
                        ? "The colonist has no hand weapon; this would fill the empty slot."
                        : "Compared with the current hand weapon for this colonist.");
                cursor += 22f;

                if (selectedCandidate == i)
                {
                    DrawLedger(ref cursor, width, pawn, candidate);
                }
            }

            if (limit < visibleRanked.Count)
            {
                DrawCell(new Rect(32f, cursor, width - 32f, 20f),
                    string.Format("... {0} more (Settings > Armory)", visibleRanked.Count - limit),
                    TextAnchor.MiddleLeft, GameFont.Tiny, new Color(0.65f, 0.65f, 0.65f));
                cursor += 20f;
            }

            if (pawn.Incompatible.Count > 0)
            {
                cursor += 8f;
                SectionHeader(ref cursor, width, "Cannot equip (" + pawn.Incompatible.Count + ")");
                int incompatibleLimit = Mathf.Min(pawn.Incompatible.Count, 40);
                for (int i = 0; i < incompatibleLimit; i++)
                {
                    Candidate blocked = pawn.Incompatible[i];
                    DrawCell(new Rect(32f, cursor, Mathf.Max(40f, width - 244f), 22f),
                        blocked.Weapon.Label, TextAnchor.MiddleLeft);
                    DrawCell(new Rect(width - 204f, cursor, 200f, 22f), blocked.Blocker,
                        TextAnchor.MiddleLeft, GameFont.Small, new Color(1f, 0.55f, 0.55f));
                    cursor += 22f;
                }
            }

            if (pawn.SidearmState != null)
            {
                cursor += 8f;
                SectionHeader(ref cursor, width, "Backup slot (" + backupType + ")");

                string waitReason = SidearmWaitReason(pawn);
                if (waitReason != null)
                {
                    DrawCell(new Rect(4f, cursor, width - 8f, 18f), waitReason,
                        TextAnchor.MiddleLeft, GameFont.Tiny, new Color(1f, 0.82f, 0.45f));
                    cursor += 18f;
                }

                string backupRule = !manageBackups
                    ? "Backup management is off (Settings > Armory)."
                    : "Armory keeps one " + backupType + " backup.";
                int carriedBackupCount = CountBackups(pawn);
                DrawCell(new Rect(4f, cursor, width - 8f, 18f),
                    string.Format("{0}: {1}",
                        carriedBackupCount == 1 ? "Carried backup" : "Carried backups",
                        CarriedBackupText(pawn)),
                    TextAnchor.MiddleLeft, GameFont.Tiny, new Color(0.72f, 0.72f, 0.72f));
                cursor += 18f;
                if (carriedBackupCount > 1)
                {
                    DrawCell(new Rect(4f, cursor, width - 8f, 18f),
                        "Multiple backups carried. Drop extras manually; Armory will not add more.",
                        TextAnchor.MiddleLeft, GameFont.Tiny, BlockedSlotColor);
                    cursor += 18f;
                }
                DrawCell(new Rect(4f, cursor, width - 8f, 18f), backupRule,
                    TextAnchor.MiddleLeft, GameFont.Tiny, new Color(0.68f, 0.68f, 0.68f));
                cursor += 18f;
                if (manageBackups)
                {
                    DrawCell(new Rect(4f, cursor, width - 8f, 18f),
                        replaceWorse
                            ? "Upgrades replace the worst same-type backup."
                            : "New backups are added but never upgraded.",
                        TextAnchor.MiddleLeft, GameFont.Tiny, new Color(0.68f, 0.68f, 0.68f));
                    cursor += 18f;
                }

                if (pawn.SidearmCandidates.Count == 0)
                {
                    string message = !SimpleSidearmsAdapter.CanCheckSidearms
                        ? "Simple Sidearms sidearm checks are unavailable; backup candidates cannot be evaluated."
                        : "No loose " + backupType + " weapons on this map.";
                    DrawCell(new Rect(4f, cursor, width - 8f, 20f), message,
                        TextAnchor.MiddleLeft, GameFont.Tiny, new Color(0.68f, 0.68f, 0.68f));
                    cursor += 20f;
                }

                if (backupLimit > 0)
                {
                    Color columnColor = new Color(0.62f, 0.62f, 0.62f);
                    DrawCell(new Rect(32f, cursor, Mathf.Max(40f, width - 304f), 18f),
                        "Weapon", TextAnchor.MiddleLeft, GameFont.Tiny, columnColor);
                    DrawCell(new Rect(width - 264f, cursor, 200f, 18f),
                        "Status", TextAnchor.MiddleLeft, GameFont.Tiny, columnColor);
                    DrawCell(new Rect(width - 56f, cursor, 48f, 18f),
                        "Score", TextAnchor.MiddleRight, GameFont.Tiny, columnColor);
                    cursor += 18f;
                }

                for (int i = 0; i < backupLimit; i++)
                {
                    SidearmCandidate candidate = pawn.SidearmCandidates[i];
                    bool upgrade = manageBackups && hasWorstBackup && replaceWorse &&
                                   candidate.Score > backupMinimum &&
                                   (candidate.Ready || candidate.UpgradeAfterDrop);
                    string status;
                    if (candidate.Ready)
                    {
                        status = upgrade ? "Upgrade backup" : "Can equip";
                    }
                    else
                    {
                        status = upgrade ? "Upgrade, replaces old backup" : candidate.Blocker;
                    }
                    Color statusColor = upgrade
                        ? new Color(0.66f, 0.84f, 0.95f)
                        : candidate.Ready ? new Color(0.7f, 0.9f, 0.7f) : new Color(1f, 0.8f, 0.42f);

                    DrawCell(new Rect(4f, cursor, 24f, 22f), (i + 1) + ".", TextAnchor.MiddleRight);
                    Rect backupLabel = new Rect(32f, cursor, Mathf.Max(40f, width - 304f), 22f);
                    DrawCell(backupLabel, candidate.Weapon.Label, TextAnchor.MiddleLeft);
                    TipCell(backupLabel, candidate.Weapon.Label);
                    Rect backupStatus = new Rect(width - 264f, cursor, 200f, 22f);
                    DrawCell(backupStatus, status, TextAnchor.MiddleLeft, GameFont.Small, statusColor);
                    TipCell(backupStatus, status);
                    Rect backupScore = new Rect(width - 56f, cursor, 48f, 22f);
                    DrawCell(backupScore, candidate.Score.ToString("0"), TextAnchor.MiddleRight);
                    TipCell(backupScore, "Combat score for this colonist.");
                    cursor += 22f;
                }

                if (pawn.SidearmCandidates.Count > backupLimit)
                {
                    DrawCell(new Rect(32f, cursor, width - 32f, 18f),
                        string.Format("... {0} more (Settings > Armory)",
                            pawn.SidearmCandidates.Count - backupLimit),
                        TextAnchor.MiddleLeft, GameFont.Tiny, new Color(0.65f, 0.65f, 0.65f));
                    cursor += 18f;
                }
            }

            Widgets.EndScrollView();
        }

        private static bool IsSSRoleBlock(string blocker)
        {
            return !string.IsNullOrEmpty(blocker) &&
                   (blocker.StartsWith("Requires a ranged hand") ||
                    blocker.StartsWith("Requires a melee hand"));
        }

        private static string SidearmWaitReason(PawnAnalysis pawn)
        {
            if (pawn.SidearmState == null || !pawn.RoleIsRanged.HasValue)
            {
                return null;
            }
            if (pawn.Current == null)
            {
                return "No hand weapon yet; the backup waits until the colonist is armed.";
            }
            if (!WaitsForHandType(pawn))
            {
                return null;
            }

            return string.Format(
                "Waiting for Simple Sidearms to switch to a {0} hand before adding the {1} backup.",
                pawn.RoleIsRanged.Value ? "ranged" : "melee",
                pawn.RoleIsRanged.Value ? "melee" : "ranged");
        }

        private float PawnDetailHeight(PawnAnalysis pawn, int limit, bool showBlocked)
        {
            float height = 26f + 66f + limit * 22f;
            if (limit > 0)
            {
                height += 18f;
            }
            if (pawn.HandTypeMismatch)
            {
                height += 22f;
            }
            if (pawn.RoleIsRanged == true)
            {
                height += 26f;
            }
            if (!SimpleSidearmsAdapter.Active || pawn.SidearmState == null)
            {
                height += 22f;
            }
            if (limit < visibleRanked.Count)
            {
                height += 20f;
            }
            if (selectedCandidate >= 0 && selectedCandidate < limit)
            {
                height += LedgerHeight(visibleRanked[selectedCandidate]) + 24f;
            }
            if (pawn.Incompatible.Count > 0)
            {
                height += 34f + Mathf.Min(pawn.Incompatible.Count, 40) * 22f;
            }
            if (pawn.SidearmState != null)
            {
                int backupCount = Mathf.Min(pawn.SidearmCandidates.Count, SidearmDisplayLimit);
                bool manageBackups = ArmoryMod.Settings == null || ArmoryMod.Settings.manageSidearms;
                height += 8f + 26f + 18f + 18f + backupCount * 22f;
                if (CountBackups(pawn) > 1)
                {
                    height += 18f;
                }
                if (manageBackups)
                {
                    height += 18f;
                }
                if (backupCount > 0)
                {
                    height += 18f;
                }
                if (SidearmWaitReason(pawn) != null)
                {
                    height += 18f;
                }
                if (pawn.SidearmCandidates.Count == 0)
                {
                    height += 20f;
                }
                if (pawn.SidearmCandidates.Count > backupCount)
                {
                    height += 18f;
                }
            }
            return height + 20f;
        }

        private void OpenCombatRoleMenu(PawnAnalysis pawn)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            AddCombatRoleOption(options, pawn, CombatRole.Auto, "Auto (from accuracy)");
            AddCombatRoleOption(options, pawn, CombatRole.Cqb, "Close quarters (prefers 10 tiles)");
            AddCombatRoleOption(options, pawn, CombatRole.Assault, "Assault (prefers 17 tiles)");
            AddCombatRoleOption(options, pawn, CombatRole.Line, "Line (prefers 25 tiles)");
            AddCombatRoleOption(options, pawn, CombatRole.Marksman, "Marksman (prefers 34 tiles)");
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenHandTypeMenu(PawnAnalysis pawn)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            AddHandTypeOption(options, pawn, HandType.Auto, "Auto (passion, trait, skill)");
            AddHandTypeOption(options, pawn, HandType.Ranged, "Ranged hand");
            AddHandTypeOption(options, pawn, HandType.Melee, "Melee hand");
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void AddHandTypeOption(List<FloatMenuOption> options, PawnAnalysis pawn,
            HandType type, string text)
        {
            bool current = pawn.HandTypeOverride == type;
            string label = current ? "* " + text : "  " + text;
            options.Add(new FloatMenuOption(label, delegate
            {
                ArmoryGameComponent.SetHandTypeOverride(pawn.Pawn, type);
                refreshRequested = true;
            }));
        }

        private void AddCombatRoleOption(List<FloatMenuOption> options, PawnAnalysis pawn,
            CombatRole role, string text)
        {
            bool current = pawn.RoleOverridden
                ? pawn.CombatRole == role
                : role == CombatRole.Auto;
            string label = current ? "* " + text : "  " + text;
            options.Add(new FloatMenuOption(label, delegate
            {
                ArmoryGameComponent.SetRoleOverride(pawn.Pawn, role);
                refreshRequested = true;
            }));
        }

        private static void DrawLedger(ref float cursor, float width, PawnAnalysis pawn, Candidate candidate)
        {
            GUI.color = new Color(0.78f, 0.87f, 0.96f);
            string[] lines = (candidate.Ledger ?? string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                DrawCell(new Rect(38f, cursor, Mathf.Max(40f, width - 46f), 18f), lines[i],
                    TextAnchor.MiddleLeft, GameFont.Tiny);
                cursor += 18f;
            }
            DrawCell(new Rect(38f, cursor, Mathf.Max(40f, width - 46f), 18f),
                string.Format("Base power {0:0} | Combat score {1:0}", candidate.Weapon.Power, candidate.Score),
                TextAnchor.MiddleLeft, GameFont.Tiny);
            cursor += 18f;
            string verdict = pawn.HandTypeMismatch
                ? "This candidate has the chosen hand type; the equipped weapon is the other type."
                : pawn.BestCarriedPrimary != null && pawn.BestCarriedPrimary != pawn.Current &&
                  candidate.Score <= pawn.BestCarriedPrimaryScore
                    ? "Below the best carried hand weapon, so Armory will not equip it."
                    : pawn.DeltaPct(candidate) >= 0f
                        ? "Better than the current hand weapon."
                        : "Worse than the current hand weapon.";
            DrawCell(new Rect(38f, cursor, Mathf.Max(40f, width - 46f), 18f), verdict,
                TextAnchor.MiddleLeft, GameFont.Tiny);
            cursor += 22f;
            GUI.color = Color.white;
        }

        private static float LedgerHeight(Candidate candidate)
        {
            return (candidate.Ledger ?? string.Empty).Split('\n').Length * 18f + 40f;
        }

        // -------------------------------------------------------------- Weapons

        private void DrawWeapons(Rect area, ArmorySnapshot snapshot)
        {
            if (snapshot.WeaponRows.Count == 0)
            {
                Widgets.Label(area, "No loose weapons on this map.");
                return;
            }
            if (selectedWeapon < 0 || selectedWeapon >= snapshot.WeaponRows.Count)
            {
                selectedWeapon = 0;
            }

            float listWidth = Mathf.Floor(area.width * 0.55f);
            Rect listRect = new Rect(area.x, area.y, listWidth, area.height);
            Rect detailRect = new Rect(listRect.xMax + 8f, area.y, area.width - listWidth - 8f, area.height);

            Widgets.DrawBoxSolid(listRect, new Color(0.12f, 0.12f, 0.12f));
            Widgets.DrawBoxSolid(detailRect, new Color(0.12f, 0.12f, 0.12f));

            Rect inner = listRect.ContractedBy(6f);
            DrawCell(new Rect(inner.x, inner.y, inner.width, 18f),
                "Only loose weapons on this map are listed here. Double-click a row to jump to it. Equipped and carried weapons appear under Colonists.",
                TextAnchor.MiddleLeft, GameFont.Tiny, new Color(0.68f, 0.68f, 0.68f));
            Rect scrollRect = new Rect(inner.x, inner.y + 18f + HeaderHeight, inner.width,
                inner.height - 18f - HeaderHeight);
            float rowWidth = scrollRect.width - 20f;
            DrawWeaponListHeader(new Rect(inner.x, inner.y + 18f, rowWidth, HeaderHeight));
            Widgets.BeginScrollView(scrollRect, ref weaponListScroll,
                new Rect(0f, 0f, rowWidth, snapshot.WeaponRows.Count * RowHeight));

            Thing jumpThing = null;
            for (int i = 0; i < snapshot.WeaponRows.Count; i++)
            {
                WeaponAnalysis row = snapshot.WeaponRows[i];
                Rect rowRect = new Rect(0f, i * RowHeight, rowWidth, RowHeight);
                if (i == selectedWeapon)
                {
                    Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.13f));
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(rowRect);
                }
                if (IsRowDoubleClick(rowRect))
                {
                    Event.current.Use();
                    jumpThing = row.Weapon.Thing;
                }
                if (Widgets.ButtonInvisible(rowRect) && selectedWeapon != i)
                {
                    selectedWeapon = i;
                    weaponDetailScroll = Vector2.zero;
                }

                PawnAnalysis best = row.BestHandPawn ?? row.BestBackupPawn ??
                                    row.BestAnyHandPawn ?? row.BestAnyBackupPawn;
                WeaponColumns(rowRect, out Rect label, out Rect power, out Rect range,
                    out Rect bestPawn, out Rect status);
                DrawCell(label, row.Weapon.Label, TextAnchor.MiddleLeft);
                TipCell(label, row.Weapon.Label);
                DrawCell(power, row.Weapon.Power.ToString("0"), TextAnchor.MiddleRight);
                TipCell(power, "Normal-quality base strength before range and colonist fit.");
                DrawCell(range, RangeText(row.Weapon.Range), TextAnchor.MiddleRight,
                    GameFont.Small, new Color(0.72f, 0.82f, 0.92f));
                TipCell(range, "Maximum firing range in tiles.");
                DrawCell(bestPawn, best == null ? "-" : best.Pawn.LabelShort, TextAnchor.MiddleLeft);
                TipCell(bestPawn, best == null
                    ? "No colonist can use this weapon."
                    : "Best colonist for this weapon: " + best.Pawn.LabelShort);
                Color statusColor;
                if (row.BestHandPawn != null)
                {
                    statusColor = HandSlotColor;
                }
                else if (row.BestBackupPawn != null)
                {
                    statusColor = BackupSlotColor;
                }
                else if (row.BestAnyHandPawn != null || row.BestAnyBackupPawn != null)
                {
                    statusColor = BlockedSlotColor;
                }
                else
                {
                    statusColor = new Color(0.62f, 0.62f, 0.62f);
                }
                DrawCell(status, row.Status, TextAnchor.MiddleRight, GameFont.Small, statusColor);
                TipCell(status, WeaponStatusTip(row));
            }

            Widgets.EndScrollView();
            DrawWeaponDetail(detailRect.ContractedBy(8f), snapshot, snapshot.WeaponRows[selectedWeapon]);
            if (jumpThing != null)
            {
                JumpToThing(jumpThing);
            }
        }

        private static string WeaponStatusTip(WeaponAnalysis row)
        {
            if (row.BestHandPawn != null)
            {
                return "Best use is the hand slot. This is the strongest ready hand choice for at least one colonist.";
            }
            if (row.BestBackupPawn != null)
            {
                return "Best use is the backup slot. Hand use is blocked or not needed, but it is ready as an opposite-type backup.";
            }
            if (row.BestAnyHandPawn != null)
            {
                return "Best as a hand weapon, but that colonist cannot equip it right now. Open the detail for the blocker.";
            }
            if (row.BestAnyBackupPawn != null)
            {
                return "Best as a backup weapon, but that colonist cannot carry it right now. Open the detail for the blocker.";
            }
            return "No colonist can use this weapon: it is not a fit, or every candidate is permanently blocked.";
        }

        private static void DrawWeaponListHeader(Rect rect)
        {
            WeaponColumns(rect, out Rect label, out Rect power, out Rect range,
                out Rect best, out Rect status);
            Color color = new Color(0.68f, 0.68f, 0.68f);
            DrawCell(label, "Weapon", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(power, "Power", TextAnchor.MiddleRight, GameFont.Tiny, color);
            DrawCell(range, "Range", TextAnchor.MiddleRight, GameFont.Tiny, color);
            DrawCell(best, "Best colonist", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(status, "Best use", TextAnchor.MiddleRight, GameFont.Tiny, color);

            TooltipHandler.TipRegion(power,
                "Normal-quality base strength before range and colonist fit. The Upgrades tab's Score adds both.");
            TooltipHandler.TipRegion(range, "Maximum firing range in tiles.");
            TooltipHandler.TipRegion(best,
                "Colonist with the highest score for this weapon in its best ready slot.");
            TooltipHandler.TipRegion(status,
                "Where this loose weapon is the best choice: hand, backup, or blocked in that slot.");
        }

        private void DrawWeaponDetail(Rect rect, ArmorySnapshot snapshot, WeaponAnalysis row)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 30f), row.Weapon.Label);
            Text.Font = GameFont.Small;

            float y = rect.y + 32f;
            Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                string.Format("{0}    |    Power {1:0}{2}",
                    row.Weapon.IsRanged ? "Ranged" : "Melee", row.Weapon.Power,
                    row.Weapon.IsRanged ? "    |    Range " + row.Weapon.Range.ToString("0.0") + " tiles" : string.Empty));
            y += 24f;

            if (row.BestHandPawn != null)
            {
                string handDelta = row.BestHandPawn.HandTypeMismatch
                    ? "type switch"
                    : row.BestHandPawn.HandBaselineScore <= 0.01f
                        ? "new slot"
                        : FormatPct(DeltaFor(row.BestHandPawn, row.BestHandScore)) + " vs current";
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    string.Format("Best hand fit: {0}  ({1})", row.BestHandPawn.Pawn.LabelShort,
                        handDelta),
                    new Color(0.75f, 0.92f, 0.75f));
            }
            else if (row.BestAnyHandPawn != null)
            {
                string primaryReason = ReasonFor(row.BestAnyHandPawn, row.Weapon);
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    string.Format("Best hand fit: {0}, but blocked ({1})", row.BestAnyHandPawn.Pawn.LabelShort,
                        primaryReason),
                    new Color(1f, 0.8f, 0.42f));
                if (IsSSRoleBlock(primaryReason))
                {
                    y += 22f;
                    bool previousWrap = Text.WordWrap;
                    Text.WordWrap = true;
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = new Color(1f, 0.82f, 0.45f);
                    Widgets.Label(new Rect(rect.x, y, rect.width, 48f),
                        "This colonist's Simple Sidearms hand type is the other type. Change it in the Simple Sidearms gizmo, or turn off Respect Simple Sidearms in Armory settings.");
                    GUI.color = Color.white;
                    Text.WordWrap = previousWrap;
                    y += 44f;
                }
            }
            else
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    "No colonist can use it in the hand slot.", new Color(1f, 0.7f, 0.7f));
            }
            y += 24f;

            if (row.BestBackupPawn != null)
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    string.Format("Best as backup for {0} (hand use is blocked or not needed).",
                        row.BestBackupPawn.Pawn.LabelShort),
                    new Color(0.66f, 0.84f, 0.95f));
                y += 22f;
            }
            else if (row.BestAnyBackupPawn != null)
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    string.Format("Best backup fit: {0}, but blocked ({1})",
                        row.BestAnyBackupPawn.Pawn.LabelShort,
                        SidearmReasonFor(row.BestAnyBackupPawn, row.Weapon)),
                    new Color(1f, 0.8f, 0.42f));
                y += 22f;
            }
            y += 2f;

            Widgets.DrawLineHorizontal(rect.x, y, rect.width);
            y += 8f;

            pawnScratch.Clear();
            for (int i = 0; i < snapshot.Pawns.Count; i++)
            {
                pawnScratch.Add(snapshot.Pawns[i]);
            }
            pawnScratch.Sort((a, b) => ScoreFor(b, row.Weapon).CompareTo(ScoreFor(a, row.Weapon)));

            Rect scrollRect = new Rect(rect.x, y, rect.width, Mathf.Max(40f, rect.yMax - y));
            float width = scrollRect.width - 20f;
            float contentHeight = 26f + 18f + pawnScratch.Count * 22f + 20f;
            Widgets.BeginScrollView(scrollRect, ref weaponDetailScroll, new Rect(0f, 0f, width, contentHeight));

            float cursor = 0f;
            float nameWidth = Mathf.Clamp(width * 0.26f, 100f, 170f);
            float scoreX = 4f + nameWidth + 8f;
            float statusX = scoreX + 52f + 8f;
            float statusWidth = Mathf.Max(120f, width - statusX - 4f);
            SectionHeader(ref cursor, width, "Colonists by score");
            Color columnColor = new Color(0.62f, 0.62f, 0.62f);
            DrawCell(new Rect(4f, cursor, nameWidth, 18f), "Colonist", TextAnchor.MiddleLeft,
                GameFont.Tiny, columnColor);
            DrawCell(new Rect(scoreX, cursor, 52f, 18f), "Score", TextAnchor.MiddleRight,
                GameFont.Tiny, columnColor);
            DrawCell(new Rect(statusX, cursor, statusWidth, 18f), "Status", TextAnchor.MiddleLeft,
                GameFont.Tiny, columnColor);
            cursor += 18f;

            for (int i = 0; i < pawnScratch.Count; i++)
            {
                PawnAnalysis pawn = pawnScratch[i];
                Candidate handCandidate = FindCandidate(pawn, row.Weapon);
                SidearmCandidate backupCandidate = FindSidearmCandidate(pawn, row.Weapon);
                if (handCandidate == null && backupCandidate == null)
                {
                    continue;
                }

                bool available = pawn.Available;
                bool holdsIt = pawn.Current == row.Weapon.Thing;
                float score = handCandidate != null ? handCandidate.Score : backupCandidate.Score;

                DrawCell(new Rect(4f, cursor, nameWidth, 22f), pawn.Pawn.LabelShort,
                    TextAnchor.MiddleLeft);
                DrawCell(new Rect(scoreX, cursor, 52f, 22f), score.ToString("0"),
                    TextAnchor.MiddleRight);

                string status;
                Color color;
                if (!available)
                {
                    status = pawn.UnavailableReason;
                    color = new Color(0.6f, 0.6f, 0.6f);
                }
                else if (holdsIt)
                {
                    status = "Equipped in hand";
                    color = new Color(0.75f, 0.8f, 0.9f);
                }
                else if (handCandidate != null)
                {
                    string reason;
                    bool hard;
                    bool ready = Eligibility.Check(pawn.Pawn, row.Weapon.Thing, true, out reason, out hard);
                    status = ready ? "Can equip in hand" : reason;
                    color = ready ? HandSlotColor : new Color(1f, 0.62f, 0.62f);
                }
                else
                {
                    string reason;
                    bool hard;
                    bool ready = Eligibility.CheckSidearm(pawn.Pawn, row.Weapon.Thing, true,
                        out reason, out hard);
                    status = ready ? "Can carry as backup" : reason;
                    color = ready ? BackupSlotColor : new Color(1f, 0.62f, 0.62f);
                }
                Rect statusRect = new Rect(statusX, cursor, statusWidth, 22f);
                DrawCell(statusRect, status, TextAnchor.MiddleLeft, GameFont.Small, color);
                TipCell(statusRect, status);
                cursor += 22f;
            }

            Widgets.EndScrollView();
        }

        private static string ReasonFor(PawnAnalysis pawn, WeaponInfo weapon)
        {
            Candidate candidate = FindCandidate(pawn, weapon);
            return candidate == null || candidate.Ready ? "blocked" : candidate.Blocker;
        }

        private static float ScoreFor(PawnAnalysis pawn, WeaponInfo weapon)
        {
            Candidate candidate = FindCandidate(pawn, weapon);
            if (candidate != null)
            {
                return candidate.Score;
            }
            SidearmCandidate backup = FindSidearmCandidate(pawn, weapon);
            return backup == null ? 0f : backup.Score;
        }

        private static float DeltaFor(PawnAnalysis pawn, float score)
        {
            float baseline = pawn.HandBaselineScore;
            return baseline > 0.01f ? score / baseline - 1f : 1f;
        }

        private static Candidate FindCandidate(PawnAnalysis pawn, WeaponInfo weapon)
        {
            for (int i = 0; i < pawn.Ranked.Count; i++)
            {
                if (pawn.Ranked[i].Weapon == weapon)
                {
                    return pawn.Ranked[i];
                }
            }
            for (int i = 0; i < pawn.Incompatible.Count; i++)
            {
                if (pawn.Incompatible[i].Weapon == weapon)
                {
                    return pawn.Incompatible[i];
                }
            }
            return null;
        }

        private static SidearmCandidate FindSidearmCandidate(PawnAnalysis pawn, WeaponInfo weapon)
        {
            for (int i = 0; i < pawn.SidearmCandidates.Count; i++)
            {
                if (pawn.SidearmCandidates[i].Weapon == weapon)
                {
                    return pawn.SidearmCandidates[i];
                }
            }
            return null;
        }

        private static string SidearmReasonFor(PawnAnalysis pawn, WeaponInfo weapon)
        {
            SidearmCandidate candidate = FindSidearmCandidate(pawn, weapon);
            return candidate == null || candidate.Ready ? "blocked" : candidate.Blocker;
        }

        // --------------------------------------------------------------- Demand

        /// <summary>Fixed column layout shared by the Demand header and rows.</summary>
        private struct DemandColumns
        {
            public const float PowerWidth = 58f;
            public const float RangeWidth = 48f;
            public const float ScoreWidth = 64f;
            public const float SlotWidth = 64f;
            public const float WantedWidth = 80f;
            public const float OwnedWidth = 100f;
            public const float NeedWidth = 64f;
            public const float GainWidth = 64f;
            public const float SourceWidth = 238f;
            public const float CraftButtonWidth = 52f;
            public const float BestMarkerWidth = 20f;
            private const float Gap = 8f;

            public float WeaponX;
            public float WeaponWidth;
            public float PowerX;
            public float RangeX;
            public float ScoreX;
            public float SlotX;
            public float WantedX;
            public float OwnedX;
            public float NeedX;
            public float GainX;
            public float SourceX;

            public static DemandColumns For(float rowWidth)
            {
                DemandColumns columns = new DemandColumns();
                columns.SourceX = rowWidth - 4f - SourceWidth;
                columns.GainX = columns.SourceX - Gap - GainWidth;
                columns.NeedX = columns.GainX - Gap - NeedWidth;
                columns.OwnedX = columns.NeedX - Gap - OwnedWidth;
                columns.WantedX = columns.OwnedX - Gap - WantedWidth;
                columns.SlotX = columns.WantedX - Gap - SlotWidth;
                columns.ScoreX = columns.SlotX - Gap - ScoreWidth;
                columns.RangeX = columns.ScoreX - Gap - RangeWidth;
                columns.PowerX = columns.RangeX - Gap - PowerWidth;
                columns.WeaponX = 4f;
                columns.WeaponWidth = Mathf.Max(140f, columns.PowerX - Gap - columns.WeaponX);
                return columns;
            }
        }

        private void DrawDemand(Rect area, ArmorySnapshot snapshot)
        {
            Widgets.DrawBoxSolid(area, new Color(0.12f, 0.12f, 0.12f));
            Rect inner = area.ContractedBy(8f);

            bool previousWrap = Text.WordWrap;
            Text.WordWrap = false;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.LabelEllipses(new Rect(inner.x, inner.y, inner.width, 18f),
                "Upgrade options compared with the weapons you own. A colonist can appear in several rows.");
            Widgets.LabelEllipses(new Rect(inner.x, inner.y + 18f, inner.width, 18f),
                "Sections: Craft, Research, Buy. Forbidden stock counts only after you unforbid it.");
            GUI.color = Color.white;
            Text.WordWrap = previousWrap;

            if (snapshot.Demand.Count == 0)
            {
                string message = WeaponCatalog.HasObtainable
                    ? "Every colonist already has their best obtainable weapon for each slot."
                    : "No obtainable weapon defs were found. Run Armory > Log weapon snapshot and check the log.";
                GUI.color = new Color(0.72f, 0.72f, 0.72f);
                Widgets.Label(new Rect(inner.x, inner.y + 42f, inner.width, 24f), message);
                GUI.color = Color.white;
                return;
            }

            float headerY = inner.y + 40f;
            Rect scrollRect = new Rect(inner.x, headerY + HeaderHeight, inner.width,
                Mathf.Max(40f, inner.yMax - (headerY + HeaderHeight)));
            float rowWidth = scrollRect.width - 20f;
            DemandColumns columns = DemandColumns.For(rowWidth);
            DrawDemandHeader(new Rect(inner.x, headerY, rowWidth, HeaderHeight), columns);

            float contentHeight = 0f;
            DemandSection lastSection = (DemandSection)(-1);
            for (int i = 0; i < snapshot.Demand.Count; i++)
            {
                DemandRow row = snapshot.Demand[i];
                if (row.Section != lastSection)
                {
                    contentHeight += 28f;
                    lastSection = row.Section;
                }
                contentHeight += RowHeight;
                if (expandedDemandDef == row.Def)
                {
                    contentHeight += row.Wants.Count * 20f;
                }
            }

            Widgets.BeginScrollView(scrollRect, ref demandScroll,
                new Rect(0f, 0f, rowWidth, contentHeight + 4f));

            float cursor = 0f;
            lastSection = (DemandSection)(-1);
            for (int i = 0; i < snapshot.Demand.Count; i++)
            {
                DemandRow row = snapshot.Demand[i];
                if (row.Section != lastSection)
                {
                    SectionHeader(ref cursor, rowWidth, DemandSectionTitle(row.Section));
                    lastSection = row.Section;
                }

                Rect rowRect = new Rect(0f, cursor, rowWidth, RowHeight);
                Widgets.DrawHighlightIfMouseover(rowRect);

                Rect craftButton = new Rect(
                    columns.SourceX + DemandColumns.SourceWidth - DemandColumns.CraftButtonWidth,
                    cursor + 2f, DemandColumns.CraftButtonWidth, RowHeight - 4f);
                bool showCraft = row.CanCraft;
                Rect rowClickRect = new Rect(rowRect.x, rowRect.y,
                    rowRect.width - (showCraft ? DemandColumns.CraftButtonWidth + 4f : 0f),
                    rowRect.height);
                if (Widgets.ButtonInvisible(rowClickRect))
                {
                    expandedDemandDef = expandedDemandDef == row.Def ? null : row.Def;
                }

                Color needColor = row.Need <= 0
                    ? new Color(0.7f, 0.9f, 0.7f)
                    : row.Section == DemandSection.Craft
                        ? new Color(1f, 0.68f, 0.5f)
                        : new Color(0.78f, 0.72f, 0.62f);

                Rect weaponCell = new Rect(columns.WeaponX, cursor, columns.WeaponWidth, RowHeight);
                if (row.BestCount > 0)
                {
                    DrawBestStar(new Rect(columns.WeaponX + 2f, cursor + 5f, 16f, 16f));
                }
                DrawCell(new Rect(columns.WeaponX + DemandColumns.BestMarkerWidth, cursor,
                        columns.WeaponWidth - DemandColumns.BestMarkerWidth, RowHeight),
                    row.Label, TextAnchor.MiddleLeft, GameFont.Small, Color.white);
                if (row.BestCount > 0)
                {
                    TooltipHandler.TipRegion(weaponCell, string.Format(
                        "Best option in this section for {0} colonist{1}.",
                        row.BestCount, row.BestCount == 1 ? string.Empty : "s"));
                }
                else
                {
                    TooltipHandler.TipRegion(weaponCell,
                        "An upgrade, but not the best option in this section for any colonist.");
                }
                DrawCell(new Rect(columns.PowerX, cursor, DemandColumns.PowerWidth, RowHeight),
                    row.Power.ToString("0"), TextAnchor.MiddleRight, GameFont.Small,
                    new Color(0.82f, 0.82f, 0.82f));
                Rect rangeCell = new Rect(columns.RangeX, cursor, DemandColumns.RangeWidth, RowHeight);
                DrawCell(rangeCell, RangeText(row.Range), TextAnchor.MiddleRight, GameFont.Small,
                    new Color(0.72f, 0.82f, 0.92f));
                TooltipHandler.TipRegion(rangeCell, "Maximum firing range in tiles.");
                Rect scoreCell = new Rect(columns.ScoreX, cursor, DemandColumns.ScoreWidth, RowHeight);
                DrawCell(scoreCell, row.TotalScore.ToString("0"), TextAnchor.MiddleRight, GameFont.Small,
                    new Color(0.9f, 0.86f, 0.7f));
                TooltipHandler.TipRegion(scoreCell,
                    "Range-aware expected value for the colonists who want it: effective DPS at each colonist's preferred distance, multiplied by fit and variety. Expand the row to see each contribution.");
                Rect slotCell = new Rect(columns.SlotX, cursor, DemandColumns.SlotWidth, RowHeight);
                DrawCell(slotCell, SlotText(row), TextAnchor.MiddleLeft, GameFont.Small,
                    SlotColor(row));
                TipCell(slotCell, SlotTooltip(row));
                DrawCell(new Rect(columns.WantedX, cursor, DemandColumns.WantedWidth, RowHeight),
                    row.Wanted.ToString(), TextAnchor.MiddleRight);
                DrawCell(new Rect(columns.OwnedX, cursor, DemandColumns.OwnedWidth, RowHeight),
                    OwnedText(row), TextAnchor.MiddleRight, GameFont.Small,
                    row.Forbidden > 0 ? new Color(1f, 0.8f, 0.42f) : Color.white);
                Rect ownedCell = new Rect(columns.OwnedX, cursor, DemandColumns.OwnedWidth, RowHeight);
                TipCell(ownedCell, row.Forbidden > 0
                    ? string.Format("{0} owned copies, including {1} forbidden. Forbidden copies count as owned but cannot be used until you unforbid them.", row.Owned, row.Forbidden)
                    : string.Format("{0} owned copies.", row.Owned));
                Rect needCell = new Rect(columns.NeedX, cursor, DemandColumns.NeedWidth, RowHeight);
                DrawCell(needCell, row.Need <= 0 ? "enough" : row.Need.ToString(),
                    TextAnchor.MiddleRight, GameFont.Small, needColor);
                if (row.Need <= 0)
                {
                    TooltipHandler.TipRegion(needCell,
                        "Enough copies already owned. This row is the current ideal for at least one colonist, not a missing item.");
                }
                else if (row.Forbidden > 0)
                {
                    int afterUnforbid = Mathf.Max(0, row.Wanted - row.Owned);
                    string tip;
                    if (afterUnforbid >= row.Need)
                    {
                        tip = string.Format(
                            "{0} missing after counting owned copies; {1} of them are forbidden.",
                            row.Need, row.Forbidden);
                    }
                    else if (afterUnforbid == 0)
                    {
                        tip = string.Format(
                            "{0} missing while {1} owned {2} forbidden. Unforbid instead; no extra copies are needed.",
                            row.Need, row.Forbidden,
                            row.Forbidden == 1 ? "copy is" : "copies are");
                    }
                    else
                    {
                        tip = string.Format(
                            "{0} missing while {1} owned {2} forbidden. Unforbid first; then only {3} more needed.",
                            row.Need, row.Forbidden,
                            row.Forbidden == 1 ? "copy is" : "copies are", afterUnforbid);
                    }
                    TooltipHandler.TipRegion(needCell, tip);
                }
                DrawCell(new Rect(columns.GainX, cursor, DemandColumns.GainWidth, RowHeight),
                    GainText(row), TextAnchor.MiddleRight, GameFont.Small,
                    new Color(0.72f, 0.9f, 0.72f));
                Rect sourceTextRect = new Rect(columns.SourceX, cursor,
                    DemandColumns.SourceWidth -
                    (showCraft ? DemandColumns.CraftButtonWidth + 4f : 0f), RowHeight);
                DrawCell(sourceTextRect, SourceText(row), TextAnchor.MiddleLeft, GameFont.Small,
                    SourceColor(row));
                if (showCraft)
                {
                    if (Widgets.ButtonText(craftButton, "Craft"))
                    {
                        DemandCrafting.TryAddBill(row.Def, snapshot.Map, out _);
                    }
                    TooltipHandler.TipRegion(craftButton,
                        "Add a 1x bill at " + row.BenchLabel + ". This does not equip the weapon.");
                }

                cursor += RowHeight;

                if (expandedDemandDef == row.Def)
                {
                    for (int j = 0; j < row.Wants.Count; j++)
                    {
                        DrawWantLine(row.Wants[j], cursor, columns);
                        cursor += 20f;
                    }
                }
            }

            Widgets.EndScrollView();
        }

        private static Texture2D bestStar;

        private static Texture2D BestStar
        {
            get
            {
                if (bestStar == null)
                {
                    bestStar = ContentFinder<Texture2D>.Get("UI/Icons/ArmoryBestStar", false);
                }
                return bestStar;
            }
        }

        private static void DrawBestStar(Rect rect)
        {
            Texture2D star = BestStar;
            if (star == null)
            {
                return;
            }
            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, star, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }

        private static void DrawDemandHeader(Rect rect, DemandColumns columns)
        {
            Color color = new Color(0.68f, 0.68f, 0.68f);
            DrawCell(new Rect(rect.x + columns.WeaponX + DemandColumns.BestMarkerWidth, rect.y,
                    columns.WeaponWidth - DemandColumns.BestMarkerWidth, rect.height),
                "Weapon", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            Rect powerRect = new Rect(rect.x + columns.PowerX, rect.y, DemandColumns.PowerWidth,
                rect.height);
            DrawCell(powerRect, "Power", TextAnchor.MiddleRight, GameFont.Tiny, color);
            TooltipHandler.TipRegion(powerRect,
                "Normal-quality base strength before range and colonist fit. Score is the range-aware value used for sorting.");
            Rect rangeRect = new Rect(rect.x + columns.RangeX, rect.y, DemandColumns.RangeWidth,
                rect.height);
            DrawCell(rangeRect, "Range", TextAnchor.MiddleRight, GameFont.Tiny, color);
            TooltipHandler.TipRegion(rangeRect, "Maximum firing range in tiles.");
            Rect scoreRect = new Rect(rect.x + columns.ScoreX, rect.y, DemandColumns.ScoreWidth,
                rect.height);
            DrawCell(scoreRect, "Score", TextAnchor.MiddleRight, GameFont.Tiny, color);
            TooltipHandler.TipRegion(scoreRect,
                "Expected range-aware value for the colonists who want it: effective DPS at each colonist's preferred distance, multiplied by fit and variety. Rows are sorted by this inside each section.");
            Rect slotRect = new Rect(rect.x + columns.SlotX, rect.y, DemandColumns.SlotWidth,
                rect.height);
            DrawCell(slotRect, "Slot", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            TooltipHandler.TipRegion(slotRect,
                "Where the weapon is wanted: hand, backup, or both. Hover a slot for counts; expand a row to see each colonist.");
            Rect wantRect = new Rect(rect.x + columns.WantedX, rect.y, DemandColumns.WantedWidth,
                rect.height);
            DrawCell(wantRect, "Wanted by", TextAnchor.MiddleRight, GameFont.Tiny, color);
            TooltipHandler.TipRegion(wantRect,
                "Number of colonists who would improve with this weapon. A colonist can appear in several rows; each row is an independent option.");
            Rect ownedRect = new Rect(rect.x + columns.OwnedX, rect.y, DemandColumns.OwnedWidth,
                rect.height);
            DrawCell(ownedRect, "Owned", TextAnchor.MiddleRight, GameFont.Tiny, color);
            TooltipHandler.TipRegion(ownedRect,
                "Total owned copies, including forbidden copies. Forbidden stock counts as owned but cannot be used until you unforbid it.");
            Rect needRect = new Rect(rect.x + columns.NeedX, rect.y, DemandColumns.NeedWidth,
                rect.height);
            DrawCell(needRect, "Needed", TextAnchor.MiddleRight, GameFont.Tiny, color);
            TooltipHandler.TipRegion(needRect,
                "Copies still missing after counting owned weapons that are not forbidden. Unforbid stock before crafting more.");
            Rect gainRect = new Rect(rect.x + columns.GainX, rect.y, DemandColumns.GainWidth,
                rect.height);
            DrawCell(gainRect, "Gain", TextAnchor.MiddleRight, GameFont.Tiny, color);
            TooltipHandler.TipRegion(gainRect,
                "Improvement over the current weapon. 'new slot' means the colonist has no weapon in that slot yet; 'switch type' means they hold the other hand type. The row value is the average; expand the row to see each colonist.");
            Rect sourceRect = new Rect(rect.x + columns.SourceX, rect.y,
                DemandColumns.SourceWidth, rect.height);
            DrawCell(sourceRect, "Source", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            TooltipHandler.TipRegion(sourceRect,
                "Craft: the best built bench, or the best unlocked bench if none is built yet. Research: the named project unlocks it. Buy: a trader is the only source. Craft rows with a built bench also show a Craft button that creates one 1x bill.");
        }

        private static string DemandSectionTitle(DemandSection section)
        {
            switch (section)
            {
                case DemandSection.Research:
                    return "Research";
                case DemandSection.Buy:
                    return "Buy";
                default:
                    return "Craft";
            }
        }

        private static string OwnedText(DemandRow row)
        {
            string text = row.Owned.ToString();
            if (row.Forbidden > 0)
            {
                text += " · " + row.Forbidden + " forbidden";
            }
            return text;
        }

        private static string SlotText(DemandRow row)
        {
            if (row.WantedHand > 0 && row.WantedBackup > 0)
            {
                return "Both";
            }
            return row.WantedHand > 0 ? "Hand" : "Backup";
        }

        private static Color SlotColor(DemandRow row)
        {
            if (row.WantedHand > 0 && row.WantedBackup > 0)
            {
                return BothSlotColor;
            }
            return row.WantedHand > 0 ? HandSlotColor : BackupSlotColor;
        }

        private static string SlotTooltip(DemandRow row)
        {
            if (row.WantedHand > 0 && row.WantedBackup > 0)
            {
                string hand = row.WantedHand == 1
                    ? "1 Hand slot"
                    : row.WantedHand + " Hand slots";
                string backup = row.WantedBackup == 1
                    ? "1 Backup slot"
                    : row.WantedBackup + " Backup slots";
                return "Wanted for " + hand + " and " + backup + ".";
            }
            return row.WantedHand > 0 ? "Wanted for the Hand slot." : "Wanted for the Backup slot.";
        }

        private static string GainText(DemandRow row)
        {
            if (row.KnownCount > 0)
            {
                return FormatPct(row.AverageGain);
            }
            if (row.NewCount > 0)
            {
                return "new slot";
            }
            if (row.TypeSwitchCount > 0)
            {
                return "switch";
            }
            return "new slot";
        }

        private static string SourceText(DemandRow row)
        {
            switch (row.Section)
            {
                case DemandSection.Research:
                    string research = row.MissingResearch ?? "locked";
                    if (row.Owned > 0 && row.Need > 0)
                    {
                        // Some copies already exist; research only unlocks additional ones.
                        research = "more: " + research;
                    }
                    return row.Buyable ? research + " · buy" : research;
                case DemandSection.Buy:
                    return "buy";
                default:
                    return row.CraftableNow
                        ? (string.IsNullOrEmpty(row.BenchLabel)
                            ? "craft"
                            : "craft · " + row.BenchLabel)
                        : "buy";
            }
        }

        private static Color SourceColor(DemandRow row)
        {
            switch (row.Section)
            {
                case DemandSection.Research:
                    return new Color(0.95f, 0.78f, 0.5f);
                case DemandSection.Buy:
                    return new Color(0.85f, 0.8f, 0.65f);
                default:
                    return new Color(0.7f, 0.85f, 0.95f);
            }
        }

        private static void DrawWantLine(DemandWant want, float cursor, DemandColumns columns)
        {
            const float nameWidth = 96f;
            float nameX = columns.WeaponX + DemandColumns.BestMarkerWidth + 8f;
            DrawCell(new Rect(nameX, cursor, nameWidth, 20f), want.Colonist,
                TextAnchor.MiddleLeft, GameFont.Small, new Color(0.78f, 0.78f, 0.78f));
            TooltipHandler.TipRegion(new Rect(nameX, cursor, nameWidth, 20f),
                want.Role == "Melee"
                    ? string.Format("{0} - Melee hand", want.Colonist)
                    : string.Format("{0} - {1}, prefers ~{2} tiles", want.Colonist,
                        want.Role ?? "role not set", want.PreferredRange));

            float currentX = nameX + nameWidth;
            float currentWidth = Mathf.Max(40f, columns.WeaponWidth - (currentX - columns.WeaponX));
            DrawCell(new Rect(currentX, cursor, currentWidth, 20f),
                "now " + want.CurrentLabel, TextAnchor.MiddleLeft, GameFont.Small,
                new Color(0.62f, 0.62f, 0.62f));

            DrawCell(new Rect(columns.ScoreX, cursor, DemandColumns.ScoreWidth, 20f),
                want.Score.ToString("0"), TextAnchor.MiddleRight, GameFont.Small,
                new Color(0.82f, 0.8f, 0.68f));
            DrawCell(new Rect(columns.SlotX, cursor, DemandColumns.SlotWidth, 20f),
                want.Hand ? "Hand" : "Backup", TextAnchor.MiddleLeft, GameFont.Small,
                want.Hand ? HandSlotColor : BackupSlotColor);
            DrawCell(new Rect(columns.GainX, cursor, DemandColumns.GainWidth, 20f),
                want.TypeSwitch ? "switch" : want.IsNew ? "new slot" : FormatPct(want.Gain),
                TextAnchor.MiddleRight, GameFont.Small,
                new Color(0.68f, 0.82f, 0.68f));
        }

        // ------------------------------------------------------------------ Log

        private void DrawDecisions(Rect area, ArmorySnapshot snapshot)
        {
            Widgets.DrawBoxSolid(area, new Color(0.12f, 0.12f, 0.12f));
            Rect inner = area.ContractedBy(8f);

            string status;
            Color statusColor;
            ArmorySettings settings = ArmoryMod.Settings;
            if (ArmoryMod.AutomationActive)
            {
                status = "Automatic equipping is ON. This log is cleared when the game closes or a save loads.";
                statusColor = new Color(0.72f, 0.92f, 0.72f);
            }
            else if (settings != null && settings.autoEquipEnabled)
            {
                status = "Automatic equipping is paused while another weapon manager is loaded.";
                statusColor = new Color(1f, 0.8f, 0.42f);
            }
            else
            {
                status = "Automatic equipping is OFF. Enable it in Settings > Armory.";
                statusColor = new Color(1f, 0.8f, 0.42f);
            }
            DrawColoredLine(new Rect(inner.x, inner.y, inner.width - 120f, 22f), status, statusColor);

            if (Widgets.ButtonText(new Rect(inner.xMax - 110f, inner.y - 2f, 110f, 26f), "Clear log"))
            {
                ArmoryGameComponent.ClearDecisions();
            }

            List<ArmoryGameComponent.Decision> decisions = ArmoryGameComponent.Decisions;
            if (decisions == null || decisions.Count == 0)
            {
                GUI.color = new Color(0.72f, 0.72f, 0.72f);
                Widgets.Label(new Rect(inner.x, inner.y + 36f, inner.width, 24f),
                    "No automatic decisions yet. Hand-weapon and backup equips, drops and skips appear here with the reason.");
                GUI.color = Color.white;
                return;
            }

            float headerY = inner.y + 32f;
            Rect scrollRect = new Rect(inner.x, headerY + HeaderHeight, inner.width,
                Mathf.Max(40f, inner.yMax - (headerY + HeaderHeight)));
            float rowWidth = scrollRect.width - 20f;

            // Column layout: Ago | Colonist | Action | Weapon | Reason. The action comes
            // before the weapon so "Equip hand" / "Equip backup" reads as a sentence with
            // the weapon, and the reason gets the widest column.
            const float agoWidth = 60f;
            const float colonistX = 72f;
            const float colonistWidth = 120f;
            const float actionX = 204f;
            const float actionWidth = 140f;
            float weaponX = actionX + actionWidth + 12f;
            float reasonWidth = Mathf.Clamp(rowWidth - weaponX - 92f, 160f, 420f);
            float reasonX = rowWidth - 8f - reasonWidth;
            float weaponWidth = Mathf.Max(80f, reasonX - 12f - weaponX);

            Rect header = new Rect(inner.x, headerY, rowWidth, HeaderHeight);
            Color headerColor = new Color(0.68f, 0.68f, 0.68f);
            DrawCell(new Rect(header.x, header.y, agoWidth, header.height), "Ago",
                TextAnchor.MiddleRight, GameFont.Tiny, headerColor);
            DrawCell(new Rect(header.x + colonistX, header.y, colonistWidth, header.height),
                "Colonist", TextAnchor.MiddleLeft, GameFont.Tiny, headerColor);
            DrawCell(new Rect(header.x + actionX, header.y, actionWidth, header.height),
                "Action", TextAnchor.MiddleLeft, GameFont.Tiny, headerColor);
            DrawCell(new Rect(header.x + weaponX, header.y, weaponWidth, header.height),
                "Weapon", TextAnchor.MiddleLeft, GameFont.Tiny, headerColor);
            DrawCell(new Rect(header.x + reasonX, header.y, reasonWidth, header.height),
                "Reason", TextAnchor.MiddleLeft, GameFont.Tiny, headerColor);

            int now = Find.TickManager.TicksGame;
            Widgets.BeginScrollView(scrollRect, ref decisionScroll,
                new Rect(0f, 0f, rowWidth, decisions.Count * RowHeight));

            for (int i = 0; i < decisions.Count; i++)
            {
                ArmoryGameComponent.Decision decision = decisions[decisions.Count - 1 - i];
                Rect row = new Rect(0f, i * RowHeight, rowWidth, RowHeight);
                Widgets.DrawHighlightIfMouseover(row);

                int age = now - decision.Tick;
                if (age < 0)
                {
                    age = 0;
                }
                DrawCell(new Rect(0f, row.y, agoWidth, row.height), (age / 60f).ToString("0") + "s",
                    TextAnchor.MiddleRight);
                DrawCell(new Rect(colonistX, row.y, colonistWidth, row.height), decision.Colonist,
                    TextAnchor.MiddleLeft);
                DrawCell(new Rect(actionX, row.y, actionWidth, row.height),
                    DecisionLabel(decision.Kind), TextAnchor.MiddleLeft, GameFont.Small,
                    DecisionColor(decision.Kind));
                DrawCell(new Rect(weaponX, row.y, weaponWidth, row.height), decision.Weapon,
                    TextAnchor.MiddleLeft);
                DrawCell(new Rect(reasonX, row.y, reasonWidth, row.height), decision.Reason,
                    TextAnchor.MiddleLeft);
            }

            Widgets.EndScrollView();
        }

        /// <summary>Log-tab action label for a recorded decision.</summary>
        private static string DecisionLabel(DecisionKind kind)
        {
            switch (kind)
            {
                case DecisionKind.EquipHand:
                    return "Equip hand weapon";
                case DecisionKind.EquipBackup:
                    return "Equip backup";
                case DecisionKind.SkipUpgrade:
                    return "Skip upgrade";
                case DecisionKind.DropBackup:
                    return "Drop backup";
                case DecisionKind.DropFailed:
                    return "Drop failed";
                case DecisionKind.RestoreBackup:
                    return "Restore backup";
                default:
                    return kind.ToString();
            }
        }

        private static Color DecisionColor(DecisionKind kind)
        {
            switch (kind)
            {
                case DecisionKind.EquipHand:
                case DecisionKind.EquipBackup:
                    return new Color(0.72f, 0.92f, 0.72f);
                case DecisionKind.DropBackup:
                    return new Color(0.7f, 0.8f, 0.9f);
                default:
                    return new Color(1f, 0.78f, 0.45f);
            }
        }

        // -------------------------------------------------------------- Helpers

        private static void SectionHeader(ref float cursor, float width, string label)
        {
            // Draw a full-width divider plus white text so slot sections read as table
            // headers rather than as another body line.
            Widgets.DrawBoxSolid(new Rect(0f, cursor, width, 1f), new Color(1f, 1f, 1f, 0.22f));
            DrawCell(new Rect(0f, cursor + 3f, width, 22f), label, TextAnchor.MiddleLeft,
                GameFont.Small, Color.white);
            cursor += 28f;
        }

        private static void DrawColoredLine(Rect rect, string text, Color color)
        {
            DrawCell(rect, text, TextAnchor.MiddleLeft, GameFont.Small, color);
        }

        private static Color StatusColor(Candidate candidate)
        {
            if (candidate.Ready)
            {
                return new Color(0.7f, 0.9f, 0.7f);
            }
            return candidate.Hard ? new Color(1f, 0.55f, 0.55f) : new Color(1f, 0.8f, 0.42f);
        }

        private static void TipCell(Rect rect, string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                TooltipHandler.TipRegion(rect, TextUtil.StripTags(text));
            }
        }

        private static void DrawCell(Rect rect, string text, TextAnchor anchor)
        {
            DrawCell(rect, text, anchor, GameFont.Small, GUI.color);
        }

        private static void DrawCell(Rect rect, string text, TextAnchor anchor, GameFont font)
        {
            DrawCell(rect, text, anchor, font, GUI.color);
        }

        private static void DrawCell(Rect rect, string text, TextAnchor anchor, GameFont font, Color color)
        {
            if (text == null)
            {
                text = string.Empty;
            }

            Color previousColor = GUI.color;
            bool previousWrap = Text.WordWrap;
            GUI.color = color;
            Text.Font = font;
            Text.Anchor = anchor;
            Text.WordWrap = false;

            // Rich text (Quality Colors) is rendered as-is when the visible text fits.
            // Truncation would cut the tags, so only then fall back to plain text.
            string plain = TextUtil.StripTags(text);
            if (plain.Length > 0 && Text.CalcSize(plain).x <= rect.width - 2f)
            {
                Widgets.Label(rect, text);
            }
            else
            {
                Widgets.LabelEllipses(rect, plain);
            }

            Text.WordWrap = previousWrap;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = previousColor;
        }

        private static string RangeText(float range)
        {
            return range <= 0.01f ? "-" : range.ToString("0.0");
        }

        private static string FormatPct(float value)
        {
            int percent = Mathf.RoundToInt(value * 100f);
            return percent > 0 ? "+" + percent + "%" : percent + "%";
        }
    }
}


