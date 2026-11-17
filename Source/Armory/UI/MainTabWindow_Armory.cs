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
            Pawns,
            Weapons,
            Demand,
            Log
        }

        private ArmoryView view = ArmoryView.Pawns;
        private int selectedPawn;
        private int selectedWeapon;
        private int selectedCandidate = -1;
        private Vector2 pawnListScroll;
        private Vector2 pawnDetailScroll;
        private Vector2 weaponListScroll;
        private Vector2 weaponDetailScroll;
        private Vector2 demandScroll;
        private Vector2 decisionScroll;
        private bool refreshRequested;

        private readonly List<Candidate> visibleRanked = new List<Candidate>();
        private readonly List<PawnAnalysis> pawnScratch = new List<PawnAnalysis>();

        private const float RowHeight = 26f;
        private const float HeaderHeight = 22f;
        private const int SidearmDisplayLimit = 12;

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
                case ArmoryView.Pawns:
                    DrawPawns(content, snapshot);
                    break;
                case ArmoryView.Weapons:
                    DrawWeapons(content, snapshot);
                    break;
                case ArmoryView.Demand:
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
            if (TabButton(new Rect(x, rect.y, 110f, rect.height), "Pawns", view == ArmoryView.Pawns))
            {
                view = ArmoryView.Pawns;
                selectedCandidate = -1;
            }
            x += 114f;
            if (TabButton(new Rect(x, rect.y, 110f, rect.height), "Weapons", view == ArmoryView.Weapons))
            {
                view = ArmoryView.Weapons;
                selectedCandidate = -1;
            }
            x += 114f;
            if (TabButton(new Rect(x, rect.y, 110f, rect.height), "Demand", view == ArmoryView.Demand))
            {
                view = ArmoryView.Demand;
                selectedCandidate = -1;
            }
            x += 114f;
            if (TabButton(new Rect(x, rect.y, 110f, rect.height), "Log", view == ArmoryView.Log))
            {
                view = ArmoryView.Log;
                selectedCandidate = -1;
            }

            string info = string.Format("{0} colonists  |  {1} weapons  |  {2:0}s",
                snapshot.Pawns.Count,
                snapshot.Weapons.Count,
                (Find.TickManager.TicksGame - snapshot.BuiltTick) / 60f);

            bool previousWrap = Text.WordWrap;
            Text.WordWrap = false;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(new Rect(rect.xMax - 540f, rect.y, 370f, rect.height), info);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = previousWrap;

            if (Widgets.ButtonText(new Rect(rect.xMax - 166f, rect.y + 2f, 74f, rect.height - 4f), "Refresh"))
            {
                refreshRequested = true;
            }
            if (Widgets.ButtonText(new Rect(rect.xMax - 88f, rect.y + 2f, 88f, rect.height - 4f), "Settings"))
            {
                Find.WindowStack.Add(new Dialog_ModSettings(ArmoryMod.Instance));
            }
        }

        private static bool TabButton(Rect rect, string label, bool selected)
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
            return Widgets.ButtonInvisible(rect);
        }

        // ---------------------------------------------------------------- Pawns

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
            DrawPawnListHeader(new Rect(listInner.x, listInner.y, rowWidth, HeaderHeight));
            Widgets.BeginScrollView(scrollRect, ref pawnListScroll,
                new Rect(0f, 0f, rowWidth, snapshot.Pawns.Count * RowHeight));

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
                if (Widgets.ButtonInvisible(row) && selectedPawn != i)
                {
                    selectedPawn = i;
                    selectedCandidate = -1;
                    pawnDetailScroll = Vector2.zero;
                }
                DrawPawnRow(row, snapshot.Pawns[i], rowWidth);
            }

            Widgets.EndScrollView();
            DrawPawnDetail(detailRect.ContractedBy(8f), snapshot.Pawns[selectedPawn]);
        }

        private static void PawnColumns(Rect row, out Rect name, out Rect current, out Rect backup,
            out Rect status, out Rect delta)
        {
            float width = row.width;
            name = new Rect(row.x, row.y, width * 0.14f, row.height);
            current = new Rect(row.x + width * 0.14f, row.y, width * 0.24f, row.height);
            backup = new Rect(row.x + width * 0.38f, row.y, width * 0.24f, row.height);
            status = new Rect(row.x + width * 0.62f, row.y, width * 0.26f, row.height);
            delta = new Rect(row.x + width * 0.88f, row.y, width * 0.12f, row.height);
        }

        private static void WeaponColumns(Rect row, out Rect label, out Rect power, out Rect best, out Rect status)
        {
            const float gap = 8f;
            float width = row.width;
            float labelWidth = width * 0.50f;
            float powerWidth = width * 0.10f;
            float bestWidth = width * 0.20f;

            label = new Rect(row.x, row.y, labelWidth - gap, row.height);
            power = new Rect(row.x + labelWidth, row.y, powerWidth - gap, row.height);
            best = new Rect(row.x + labelWidth + powerWidth, row.y, bestWidth - gap, row.height);
            status = new Rect(row.x + labelWidth + powerWidth + bestWidth, row.y,
                width - labelWidth - powerWidth - bestWidth, row.height);
        }

        private static void DrawPawnListHeader(Rect rect)
        {
            PawnColumns(rect, out Rect name, out Rect current, out Rect backup, out Rect status, out Rect delta);
            Color color = new Color(0.68f, 0.68f, 0.68f);
            DrawCell(name, "Colonist", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(current, "Hand", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(backup, "Backup", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(status, "Status", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(delta, "Delta", TextAnchor.MiddleRight, GameFont.Tiny, color);
        }

        private static void DrawPawnRow(Rect rect, PawnAnalysis pawn, float width)
        {
            PawnColumns(rect, out Rect name, out Rect current, out Rect backup, out Rect status, out Rect delta);

            Color rowColor = pawn.Available ? Color.white : new Color(0.58f, 0.58f, 0.58f);
            DrawCell(name, pawn.Pawn.LabelShort, TextAnchor.MiddleLeft, GameFont.Small, rowColor);
            DrawCell(current, pawn.Current == null ? "unarmed" : pawn.Current.LabelCap,
                TextAnchor.MiddleLeft, GameFont.Small, rowColor);

            string backupText = CarriedBackupText(pawn);
            DrawCell(backup, backupText, TextAnchor.MiddleLeft, GameFont.Small,
                backupText == "none" ? new Color(0.75f, 0.75f, 0.75f) : rowColor);

            Color statusColor;
            string statusText = PawnStatus(pawn, out statusColor);
            DrawCell(status, statusText, TextAnchor.MiddleLeft, GameFont.Small, statusColor);

            Candidate top = pawn.TopReady;
            float deltaBase = pawn.HandBaselineScore;
            string deltaText = top == null
                ? ""
                : deltaBase > 0.01f ? FormatPct(top.Score / deltaBase - 1f) : "+100%";
            DrawCell(delta, deltaText, TextAnchor.MiddleRight,
                GameFont.Small, pawn.Available && top != null ? Color.white : rowColor);
        }

        private static string RoleLabel(PawnAnalysis pawn)
        {
            if (!pawn.HasRole)
            {
                return "not typed (Simple Sidearms inactive or respect off)";
            }
            return (pawn.RoleIsRanged.Value ? "ranged" : "melee") +
                   (string.IsNullOrEmpty(pawn.RoleSource) ? string.Empty : " - " + pawn.RoleSource);
        }

        private static string CarriedBackupText(PawnAnalysis pawn)
        {
            if (!pawn.HasRole)
            {
                return "-";
            }
            List<string> labels = new List<string>(2);
            for (int i = 0; i < pawn.CarriedSidearms.Count; i++)
            {
                CarriedSidearm carried = pawn.CarriedSidearms[i];
                if (pawn.IsBackupType(carried.Thing.def))
                {
                    labels.Add(carried.Thing.LabelCap);
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
                if (!pawn.IsBackupType(carried.Thing.def) || !IsDroppableBackup(carried.Thing))
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

            if (!pawn.HasRole)
            {
                Candidate ready = pawn.TopReady;
                if (ready != null)
                {
                    color = new Color(0.7f, 0.9f, 0.7f);
                    return "hand ready";
                }
                Candidate any = pawn.TopOverall;
                color = new Color(1f, 0.78f, 0.4f);
                return any == null ? "no weapons" : any.Blocker;
            }

            ArmorySettings settings = ArmoryMod.Settings;
            float threshold = settings == null ? 0.1f : settings.upgradeThreshold;
            float baseline = pawn.HandBaselineScore;
            Candidate topReady = pawn.TopReady;
            if (pawn.Current == null && topReady != null)
            {
                color = new Color(0.7f, 0.9f, 0.7f);
                return "hand ready";
            }
            if (pawn.Current == null)
            {
                color = new Color(1f, 0.78f, 0.4f);
                return "no hand weapon";
            }
            if (topReady != null && baseline > 0.01f &&
                topReady.Score > baseline * (1f + threshold))
            {
                color = new Color(0.7f, 0.9f, 0.7f);
                return "hand upgrade";
            }

            int backupCount = CountBackups(pawn);
            if (settings == null || !settings.manageSidearms)
            {
                color = new Color(0.72f, 0.72f, 0.72f);
                return backupCount > 0 ? "backup carried" : "backup management off";
            }
            if (backupCount == 0)
            {
                if (BestReadyBackup(pawn) != null)
                {
                    color = new Color(0.66f, 0.84f, 0.95f);
                    return "add backup";
                }
                color = new Color(1f, 0.78f, 0.4f);
                return pawn.SidearmCandidates.Count == 0 ? "no backup candidate" : "backup blocked";
            }

            if (BestBackupUpgrade(pawn) != null)
            {
                color = new Color(0.66f, 0.84f, 0.95f);
                return "backup upgrade";
            }

            color = new Color(0.78f, 0.78f, 0.78f);
            return "ok";
        }

        private void DrawPawnDetail(Rect rect, PawnAnalysis pawn)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 30f), pawn.Pawn.LabelShort);
            Text.Font = GameFont.Small;

            float y = rect.y + 30f;
            DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                string.Format("Role: {0}    Current: {1}    score {2:0}",
                    RoleLabel(pawn),
                    pawn.Current == null ? "unarmed" : pawn.Current.LabelCap,
                    pawn.CurrentScore),
                new Color(0.78f, 0.86f, 0.96f));
            y += 22f;

            if (pawn.Sidearms.Count > 0)
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    "Carried weapons: " + pawn.SidearmText, new Color(0.75f, 0.82f, 0.92f));
                y += 22f;
            }

            if (pawn.SidearmState != null)
            {
                bool veto = pawn.SidearmState.AutomationVeto && SimpleSidearmsAdapter.RespectEnabled;
                string text = "Simple Sidearms: " + pawn.SidearmState.Summary();
                if (veto)
                {
                    text += "  -  automation stays out";
                }
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f), text,
                    veto ? new Color(1f, 0.8f, 0.42f) : new Color(0.72f, 0.82f, 0.92f));
                y += 22f;

                if (pawn.BestCarriedPrimary != null && pawn.BestCarriedPrimary != pawn.Current)
                {
                    DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                        string.Format("Best carried hand weapon: {0}    score {1:0}",
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

            int configuredLimit = ArmoryMod.Settings == null ? 25 : ArmoryMod.Settings.maxCandidates;
            int limit = Mathf.Min(visibleRanked.Count, Mathf.Max(5, configuredLimit));
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
                    PawnFit.Compute(ledgerContext, candidate.Weapon.Thing, out ledger);
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
            string backupType = pawn.HasRole
                ? (pawn.RoleIsRanged.Value ? "melee" : "ranged")
                : "opposite type";

            Rect scrollRect = new Rect(rect.x, y, rect.width, Mathf.Max(40f, rect.yMax - y));
            float width = scrollRect.width - 20f;
            float contentHeight = PawnDetailHeight(pawn, limit, showBlocked);
            Widgets.BeginScrollView(scrollRect, ref pawnDetailScroll, new Rect(0f, 0f, width, contentHeight));

            float cursor = 0f;
            string handType = pawn.HasRole
                ? (pawn.RoleIsRanged.Value ? "ranged" : "melee")
                : "all weapons";
            SectionHeader(ref cursor, width, "Hand slot (" + handType + ")");

            if (bestReady != null)
            {
                DrawColoredLine(new Rect(0f, cursor, width, 22f),
                    string.Format("Best usable: {0}    score {1:0}  ({2})",
                        bestReady.Weapon.Label, bestReady.Score, FormatPct(pawn.TopReadyDeltaPct)),
                    new Color(0.75f, 0.92f, 0.75f));
                cursor += 22f;

                bool belowThreshold = baselineScore > 0.01f &&
                                      bestReady.Score <= baselineScore * (1f + threshold);
                if (belowThreshold)
                {
                    string baselineLabel = carriedIsBaseline
                        ? pawn.BestCarriedPrimary.LabelCap + " (best carried hand weapon)"
                        : "current weapon";
                    DrawColoredLine(new Rect(0f, cursor, width, 22f),
                        string.Format("Would keep {0}: improvement is below the {1}% threshold.",
                            baselineLabel, Mathf.RoundToInt(threshold * 100f)),
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
                DrawColoredLine(new Rect(0f, cursor, width, 22f),
                    string.Format("Best hand overall: {0}    score {1:0}  ({2})  - {3}",
                        bestAny.Weapon.Label, bestAny.Score, FormatPct(pawn.DeltaPct(bestAny)),
                        bestAny.Ready ? "ready" : bestAny.Blocker),
                    bestAny.Ready ? Color.white : new Color(1f, 0.8f, 0.42f));
                cursor += 22f;
            }

            Widgets.DrawLineHorizontal(0f, cursor, width);
            cursor += 8f;

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
                DrawCell(new Rect(32f, cursor, Mathf.Max(40f, width - 286f), 22f),
                    candidate.Weapon.Label, TextAnchor.MiddleLeft);
                DrawCell(new Rect(width - 246f, cursor, 120f, 22f),
                    candidate.Ready ? "ready" : candidate.Blocker, TextAnchor.MiddleRight,
                    GameFont.Small, StatusColor(candidate));
                DrawCell(new Rect(width - 122f, cursor, 48f, 22f), candidate.Score.ToString("0"),
                    TextAnchor.MiddleRight);
                DrawCell(new Rect(width - 70f, cursor, 66f, 22f), FormatPct(pawn.DeltaPct(candidate)),
                    TextAnchor.MiddleRight);
                cursor += 22f;

                if (selectedCandidate == i)
                {
                    DrawLedger(ref cursor, width, pawn, candidate);
                }
            }

            if (limit < visibleRanked.Count)
            {
                DrawCell(new Rect(32f, cursor, width - 32f, 20f),
                    string.Format("... {0} more (raise the limit in settings)", visibleRanked.Count - limit),
                    TextAnchor.MiddleLeft, GameFont.Tiny, new Color(0.65f, 0.65f, 0.65f));
                cursor += 20f;
            }

            if (pawn.Incompatible.Count > 0)
            {
                cursor += 8f;
                SectionHeader(ref cursor, width, "Incompatible with this pawn (" + pawn.Incompatible.Count + ")");
                int incompatibleLimit = Mathf.Min(pawn.Incompatible.Count, 40);
                for (int i = 0; i < incompatibleLimit; i++)
                {
                    Candidate blocked = pawn.Incompatible[i];
                    DrawCell(new Rect(32f, cursor, Mathf.Max(40f, width - 200f), 22f),
                        blocked.Weapon.Label, TextAnchor.MiddleLeft);
                    DrawCell(new Rect(width - 164f, cursor, 156f, 22f), blocked.Blocker,
                        TextAnchor.MiddleRight, GameFont.Small, new Color(1f, 0.55f, 0.55f));
                    cursor += 22f;
                }
            }

            if (pawn.HasRole)
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

                DrawCell(new Rect(4f, cursor, width - 8f, 18f),
                    string.Format("Carried: {0}    target: 1 {1} backup    {2}",
                        CarriedBackupText(pawn), backupType,
                        !manageBackups
                            ? "Backup management is off (Settings > Armory)."
                            : replaceWorse
                                ? "Upgrades replace the worst same-type backup."
                                : "Backups are added but never upgraded."),
                    TextAnchor.MiddleLeft, GameFont.Tiny, new Color(0.72f, 0.72f, 0.72f));
                cursor += 18f;

                if (pawn.SidearmCandidates.Count == 0)
                {
                    string message = !SimpleSidearmsAdapter.CanCheckSidearms
                        ? "Simple Sidearms sidearm checks are unavailable; backup candidates cannot be evaluated."
                        : "No loose " + backupType + " weapons on this map.";
                    DrawCell(new Rect(4f, cursor, width - 8f, 20f), message,
                        TextAnchor.MiddleLeft, GameFont.Tiny, new Color(0.68f, 0.68f, 0.68f));
                    cursor += 20f;
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
                        status = upgrade ? "upgrade" : "ready";
                    }
                    else
                    {
                        status = upgrade ? "upgrade (drop first)" : candidate.Blocker;
                    }
                    Color statusColor = upgrade
                        ? new Color(0.66f, 0.84f, 0.95f)
                        : candidate.Ready ? new Color(0.7f, 0.9f, 0.7f) : new Color(1f, 0.8f, 0.42f);

                    DrawCell(new Rect(32f, cursor, Mathf.Max(40f, width - 286f), 22f),
                        candidate.Weapon.Label, TextAnchor.MiddleLeft);
                    DrawCell(new Rect(width - 246f, cursor, 120f, 22f), status,
                        TextAnchor.MiddleRight, GameFont.Small, statusColor);
                    DrawCell(new Rect(width - 122f, cursor, 48f, 22f),
                        candidate.Score.ToString("0"), TextAnchor.MiddleRight);
                    DrawCell(new Rect(width - 70f, cursor, 66f, 22f),
                        candidate.Weapon.IsRanged ? "ranged" : "melee", TextAnchor.MiddleRight);
                    cursor += 22f;
                }

                if (pawn.SidearmCandidates.Count > backupLimit)
                {
                    DrawCell(new Rect(32f, cursor, width - 32f, 18f),
                        string.Format("... {0} more", pawn.SidearmCandidates.Count - backupLimit),
                        TextAnchor.MiddleLeft, GameFont.Tiny, new Color(0.65f, 0.65f, 0.65f));
                    cursor += 18f;
                }
            }

            Widgets.EndScrollView();
        }

        private static bool IsSSRoleBlock(string blocker)
        {
            return !string.IsNullOrEmpty(blocker) &&
                   (blocker.StartsWith("Melee blocked") || blocker.StartsWith("Ranged blocked"));
        }

        private static string SidearmWaitReason(PawnAnalysis pawn)
        {
            if (!pawn.HasRole)
            {
                return "Simple Sidearms backup management is inactive.";
            }
            if (pawn.Current == null)
            {
                return "No hand weapon yet; the backup waits until the pawn is armed.";
            }
            if (pawn.Current.def == null)
            {
                return null;
            }

            bool currentRanged = pawn.Current.def.IsRangedWeapon;
            bool currentMelee = pawn.Current.def.IsMeleeWeapon;
            if (!currentRanged && !currentMelee)
            {
                return null;
            }
            if (currentRanged == pawn.RoleIsRanged.Value)
            {
                return null;
            }

            return string.Format(
                "Waiting for Simple Sidearms to switch this pawn to a {0} hand weapon before adding the {1} backup.",
                pawn.RoleIsRanged.Value ? "ranged" : "melee",
                pawn.RoleIsRanged.Value ? "melee" : "ranged");
        }

        private float PawnDetailHeight(PawnAnalysis pawn, int limit, bool showBlocked)
        {
            float height = 26f + 66f + limit * 22f;
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
            if (pawn.HasRole)
            {
                int backupCount = Mathf.Min(pawn.SidearmCandidates.Count, SidearmDisplayLimit);
                height += 8f + 26f + 18f + backupCount * 22f;
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
                string.Format("power {0:0} x fit = {1:0}", candidate.Weapon.Power, candidate.Score),
                TextAnchor.MiddleLeft, GameFont.Tiny);
            cursor += 18f;
            DrawCell(new Rect(38f, cursor, Mathf.Max(40f, width - 46f), 18f),
                pawn.BestCarriedPrimary != null && pawn.BestCarriedPrimary != pawn.Current &&
                candidate.Score <= pawn.BestCarriedPrimaryScore
                    ? "Below the best carried hand weapon; Armory will not queue it."
                    : pawn.DeltaPct(candidate) >= 0f
                        ? "Better than the current weapon."
                        : "Worse than the current weapon.",
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
            Rect scrollRect = new Rect(inner.x, inner.y + HeaderHeight, inner.width,
                inner.height - HeaderHeight);
            float rowWidth = scrollRect.width - 20f;
            DrawWeaponListHeader(new Rect(inner.x, inner.y, rowWidth, HeaderHeight));
            Widgets.BeginScrollView(scrollRect, ref weaponListScroll,
                new Rect(0f, 0f, rowWidth, snapshot.WeaponRows.Count * RowHeight));

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
                if (Widgets.ButtonInvisible(rowRect) && selectedWeapon != i)
                {
                    selectedWeapon = i;
                    weaponDetailScroll = Vector2.zero;
                }

                PawnAnalysis best = row.BestHandPawn ?? row.BestBackupPawn ??
                                    row.BestAnyHandPawn ?? row.BestAnyBackupPawn;
                WeaponColumns(rowRect, out Rect label, out Rect power, out Rect bestPawn, out Rect status);
                DrawCell(label, row.Weapon.Label, TextAnchor.MiddleLeft);
                DrawCell(power, row.Weapon.Power.ToString("0"), TextAnchor.MiddleRight);
                DrawCell(bestPawn, best == null ? "-" : best.Pawn.LabelShort, TextAnchor.MiddleLeft);
                Color statusColor;
                if (row.BestHandPawn != null)
                {
                    statusColor = new Color(0.7f, 0.9f, 0.7f);
                }
                else if (row.BestBackupPawn != null)
                {
                    statusColor = new Color(0.66f, 0.84f, 0.95f);
                }
                else if (row.BestAnyHandPawn != null || row.BestAnyBackupPawn != null)
                {
                    statusColor = new Color(1f, 0.78f, 0.4f);
                }
                else
                {
                    statusColor = new Color(0.62f, 0.62f, 0.62f);
                }
                DrawCell(status, row.Status, TextAnchor.MiddleRight, GameFont.Small, statusColor);
            }

            Widgets.EndScrollView();
            DrawWeaponDetail(detailRect.ContractedBy(8f), snapshot, snapshot.WeaponRows[selectedWeapon]);
        }

        private static void DrawWeaponListHeader(Rect rect)
        {
            WeaponColumns(rect, out Rect label, out Rect power, out Rect best, out Rect status);
            Color color = new Color(0.68f, 0.68f, 0.68f);
            DrawCell(label, "Weapon", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(power, "Power", TextAnchor.MiddleRight, GameFont.Tiny, color);
            DrawCell(best, "Best colonist", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(status, "Status", TextAnchor.MiddleRight, GameFont.Tiny, color);
        }

        private void DrawWeaponDetail(Rect rect, ArmorySnapshot snapshot, WeaponAnalysis row)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 30f), row.Weapon.Label);
            Text.Font = GameFont.Small;

            float y = rect.y + 32f;
            Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                string.Format("{0}    power {1:0}", row.Weapon.IsRanged ? "Ranged" : "Melee", row.Weapon.Power));
            y += 24f;

            if (row.BestHandPawn != null)
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    string.Format("Best hand use: {0}  ({1})", row.BestHandPawn.Pawn.LabelShort,
                        FormatPct(DeltaFor(row.BestHandPawn, row.BestHandScore))),
                    new Color(0.75f, 0.92f, 0.75f));
            }
            else if (row.BestAnyHandPawn != null)
            {
                string primaryReason = ReasonFor(row.BestAnyHandPawn, row.Weapon);
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    string.Format("Hand fit: {0}, but blocked ({1})", row.BestAnyHandPawn.Pawn.LabelShort,
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
                        "That pawn's Simple Sidearms role excludes this weapon type while they carry the other type. Set the pawn's SS role in the Simple Sidearms gizmo, or turn off Respect Simple Sidearms.");
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
                    string.Format("Backup for {0}. Hand use is blocked or not needed; this weapon is eligible for the opposite-type backup slot.",
                        row.BestBackupPawn.Pawn.LabelShort),
                    new Color(0.66f, 0.84f, 0.95f));
                y += 22f;
            }
            else if (row.BestAnyBackupPawn != null)
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    string.Format("Backup fit: {0}, but blocked ({1})",
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
            float contentHeight = 26f + pawnScratch.Count * 22f + 20f;
            Widgets.BeginScrollView(scrollRect, ref weaponDetailScroll, new Rect(0f, 0f, width, contentHeight));

            float cursor = 0f;
            SectionHeader(ref cursor, width, "Colonists by fit");
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

                DrawCell(new Rect(4f, cursor, Mathf.Max(40f, width - 270f), 22f),
                    pawn.Pawn.LabelShort + (holdsIt ? "  (holds it)" : string.Empty),
                    TextAnchor.MiddleLeft);
                DrawCell(new Rect(width - 260f, cursor, 56f, 22f), score.ToString("0"),
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
                    status = "hand (equipped)";
                    color = new Color(0.75f, 0.8f, 0.9f);
                }
                else if (handCandidate != null)
                {
                    string reason;
                    bool hard;
                    bool ready = Eligibility.Check(pawn.Pawn, row.Weapon.Thing, true, out reason, out hard);
                    status = ready ? "hand ready" : reason;
                    color = ready ? new Color(0.7f, 0.9f, 0.7f) : new Color(1f, 0.62f, 0.62f);
                }
                else
                {
                    string reason;
                    bool hard;
                    bool ready = Eligibility.CheckSidearm(pawn.Pawn, row.Weapon.Thing, true,
                        out reason, out hard);
                    status = ready ? "backup ready" : reason;
                    color = ready ? new Color(0.66f, 0.84f, 0.95f) : new Color(1f, 0.62f, 0.62f);
                }
                DrawCell(new Rect(width - 198f, cursor, 194f, 22f), status, TextAnchor.MiddleRight,
                    GameFont.Small, color);
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

        private void DrawDemand(Rect area, ArmorySnapshot snapshot)
        {
            Widgets.DrawBoxSolid(area, new Color(0.12f, 0.12f, 0.12f));
            Rect inner = area.ContractedBy(8f);

            bool previousWrap = Text.WordWrap;
            Text.WordWrap = false;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.LabelEllipses(new Rect(inner.x, inner.y, inner.width, 22f),
                "Hand and opposite-type backup needs per colonist vs loose stock. Forbidden stock must be unforbidden before building more.");
            GUI.color = Color.white;
            Text.WordWrap = previousWrap;

            float headerY = inner.y + 26f;
            Rect scrollRect = new Rect(inner.x, headerY + HeaderHeight, inner.width,
                Mathf.Max(40f, inner.yMax - (headerY + HeaderHeight)));
            float rowWidth = scrollRect.width - 20f;
            DrawDemandHeader(new Rect(inner.x, headerY, rowWidth, HeaderHeight));
            Widgets.BeginScrollView(scrollRect, ref demandScroll,
                new Rect(0f, 0f, rowWidth, snapshot.Demand.Count * RowHeight + 10f));

            for (int i = 0; i < snapshot.Demand.Count; i++)
            {
                DemandRow row = snapshot.Demand[i];
                Rect rowRect = new Rect(0f, i * RowHeight, rowWidth, RowHeight);
                Widgets.DrawHighlightIfMouseover(rowRect);

                DrawCell(new Rect(4f, rowRect.y, rowWidth - 418f, rowRect.height), row.Label,
                    TextAnchor.MiddleLeft);
                DrawCell(new Rect(rowWidth - 414f, rowRect.y, 64f, rowRect.height), row.Uses,
                    TextAnchor.MiddleLeft, GameFont.Small, new Color(0.72f, 0.82f, 0.92f));
                DrawCell(new Rect(rowWidth - 346f, rowRect.y, 54f, rowRect.height), row.Wanted.ToString(),
                    TextAnchor.MiddleRight);
                DrawCell(new Rect(rowWidth - 286f, rowRect.y, 54f, rowRect.height), row.Blocked.ToString(),
                    TextAnchor.MiddleRight, GameFont.Small,
                    row.Blocked > 0 ? new Color(1f, 0.8f, 0.42f) : new Color(0.7f, 0.7f, 0.7f));
                DrawCell(new Rect(rowWidth - 226f, rowRect.y, 54f, rowRect.height), row.Stock.ToString(),
                    TextAnchor.MiddleRight);
                DrawCell(new Rect(rowWidth - 166f, rowRect.y, 70f, rowRect.height), row.Forbidden.ToString(),
                    TextAnchor.MiddleRight, GameFont.Small,
                    row.Forbidden > 0 ? new Color(1f, 0.8f, 0.42f) : new Color(0.7f, 0.7f, 0.7f));
                Color shortageColor = row.Shortage > 0
                    ? new Color(1f, 0.65f, 0.5f)
                    : new Color(0.7f, 0.9f, 0.7f);
                DrawCell(new Rect(rowWidth - 92f, rowRect.y, 88f, rowRect.height),
                    row.Shortage > 0 ? ("need " + row.Shortage) : "enough",
                    TextAnchor.MiddleRight, GameFont.Small, shortageColor);
            }

            Widgets.EndScrollView();
        }

        private static void DrawDemandHeader(Rect rect)
        {
            Color color = new Color(0.68f, 0.68f, 0.68f);
            DrawCell(new Rect(rect.x, rect.y, rect.width - 418f, rect.height), "Weapon",
                TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(new Rect(rect.xMax - 414f, rect.y, 64f, rect.height), "For",
                TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(new Rect(rect.xMax - 346f, rect.y, 54f, rect.height), "Wanted",
                TextAnchor.MiddleRight, GameFont.Tiny, color);
            DrawCell(new Rect(rect.xMax - 286f, rect.y, 54f, rect.height), "Blocked",
                TextAnchor.MiddleRight, GameFont.Tiny, color);
            DrawCell(new Rect(rect.xMax - 226f, rect.y, 54f, rect.height), "Stock",
                TextAnchor.MiddleRight, GameFont.Tiny, color);
            DrawCell(new Rect(rect.xMax - 166f, rect.y, 70f, rect.height), "Forbidden",
                TextAnchor.MiddleRight, GameFont.Tiny, color);
            DrawCell(new Rect(rect.xMax - 92f, rect.y, 88f, rect.height), "Shortage",
                TextAnchor.MiddleRight, GameFont.Tiny, color);
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
                status = "Automatic equipping is ON. Decisions are session-only.";
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
                    "No automatic decisions yet. Queued equip jobs and their reasons will appear here.");
                GUI.color = Color.white;
                return;
            }

            float headerY = inner.y + 32f;
            Rect scrollRect = new Rect(inner.x, headerY + HeaderHeight, inner.width,
                Mathf.Max(40f, inner.yMax - (headerY + HeaderHeight)));
            float rowWidth = scrollRect.width - 20f;
            float labelWidth = Mathf.Max(40f, rowWidth - 702f);
            Rect header = new Rect(inner.x, headerY, rowWidth, HeaderHeight);
            Color headerColor = new Color(0.68f, 0.68f, 0.68f);
            float headerLabelWidth = Mathf.Max(40f, header.width - 702f);
            DrawCell(new Rect(header.x, header.y, 60f, header.height), "Ago",
                TextAnchor.MiddleRight, GameFont.Tiny, headerColor);
            DrawCell(new Rect(header.x + 72f, header.y, 120f, header.height), "Colonist",
                TextAnchor.MiddleLeft, GameFont.Tiny, headerColor);
            DrawCell(new Rect(header.x + 196f, header.y, headerLabelWidth, header.height), "Weapon",
                TextAnchor.MiddleLeft, GameFont.Tiny, headerColor);
            DrawCell(new Rect(header.xMax - 502f, header.y, 120f, header.height), "Action",
                TextAnchor.MiddleRight, GameFont.Tiny, headerColor);
            DrawCell(new Rect(header.xMax - 378f, header.y, 374f, header.height), "Detail",
                TextAnchor.MiddleLeft, GameFont.Tiny, headerColor);

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
                DrawCell(new Rect(0f, row.y, 60f, row.height), (age / 60f).ToString("0") + "s",
                    TextAnchor.MiddleRight);
                DrawCell(new Rect(72f, row.y, 120f, row.height), decision.Colonist, TextAnchor.MiddleLeft);
                DrawCell(new Rect(196f, row.y, labelWidth, row.height), decision.Weapon, TextAnchor.MiddleLeft);
                DrawCell(new Rect(rowWidth - 502f, row.y, 120f, row.height), decision.Action,
                    TextAnchor.MiddleRight, GameFont.Small, new Color(0.72f, 0.92f, 0.72f));
                DrawCell(new Rect(rowWidth - 378f, row.y, 374f, row.height), decision.Detail,
                    TextAnchor.MiddleLeft);
            }

            Widgets.EndScrollView();
        }

        // -------------------------------------------------------------- Helpers

        private static void SectionHeader(ref float cursor, float width, string label)
        {
            GUI.color = new Color(0.72f, 0.8f, 0.88f);
            DrawCell(new Rect(0f, cursor, width, 24f), label, TextAnchor.MiddleLeft, GameFont.Small);
            GUI.color = Color.white;
            cursor += 26f;
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

        private static string FormatPct(float value)
        {
            int percent = Mathf.RoundToInt(value * 100f);
            return percent > 0 ? "+" + percent + "%" : percent + "%";
        }
    }
}


