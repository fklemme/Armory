using System.Collections.Generic;
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

        private static void PawnColumns(Rect row, out Rect name, out Rect current, out Rect best,
            out Rect status, out Rect delta)
        {
            float width = row.width;
            name = new Rect(row.x, row.y, width * 0.15f, row.height);
            current = new Rect(row.x + width * 0.15f, row.y, width * 0.25f, row.height);
            best = new Rect(row.x + width * 0.40f, row.y, width * 0.26f, row.height);
            status = new Rect(row.x + width * 0.66f, row.y, width * 0.22f, row.height);
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
            PawnColumns(rect, out Rect name, out Rect current, out Rect best, out Rect status, out Rect delta);
            Color color = new Color(0.68f, 0.68f, 0.68f);
            DrawCell(name, "Colonist", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(current, "Current", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(best, "Best wish", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(status, "Status", TextAnchor.MiddleLeft, GameFont.Tiny, color);
            DrawCell(delta, "Delta", TextAnchor.MiddleRight, GameFont.Tiny, color);
        }

        private static void DrawPawnRow(Rect rect, PawnAnalysis pawn, float width)
        {
            PawnColumns(rect, out Rect name, out Rect current, out Rect best, out Rect status, out Rect delta);

            Color rowColor = pawn.Available ? Color.white : new Color(0.58f, 0.58f, 0.58f);
            DrawCell(name, pawn.Pawn.LabelShort, TextAnchor.MiddleLeft, GameFont.Small, rowColor);
            DrawCell(current, pawn.Current == null ? "unarmed" : pawn.Current.LabelCap,
                TextAnchor.MiddleLeft, GameFont.Small, rowColor);

            Candidate top = pawn.TopOverall;
            DrawCell(best, top == null ? "none" : top.Weapon.Label,
                TextAnchor.MiddleLeft, GameFont.Small, rowColor);
            DrawCell(status, top == null ? "" : top.Ready ? "ready" : top.Blocker,
                TextAnchor.MiddleLeft, GameFont.Small, !pawn.Available
                    ? rowColor
                    : top == null || top.Ready ? new Color(0.7f, 0.9f, 0.7f) : new Color(1f, 0.78f, 0.4f));
            DrawCell(delta, top == null ? "" : FormatPct(pawn.TopDeltaPct), TextAnchor.MiddleRight,
                GameFont.Small, pawn.Available && (top == null || top.Ready) ? Color.white : rowColor);
        }

        private void DrawPawnDetail(Rect rect, PawnAnalysis pawn)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 30f), pawn.Pawn.LabelShort);
            Text.Font = GameFont.Small;

            float y = rect.y + 30f;
            Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                string.Format("Current: {0}    score {1:0}",
                    pawn.Current == null ? "unarmed" : pawn.Current.LabelCap,
                    pawn.CurrentScore));
            y += 22f;

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

            if (bestReady != null)
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    string.Format("Best usable: {0}    score {1:0}  ({2})",
                        bestReady.Weapon.Label, bestReady.Score, FormatPct(pawn.TopReadyDeltaPct)),
                    new Color(0.75f, 0.92f, 0.75f));
                y += 22f;

                if (pawn.TopReadyDeltaPct < threshold)
                {
                    DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                        string.Format("Would keep current: improvement is below the {0}% threshold.",
                            Mathf.RoundToInt(threshold * 100f)),
                        new Color(1f, 0.85f, 0.5f));
                    y += 22f;
                }
            }
            else
            {
                string text = !pawn.Available
                    ? "Cannot equip while unavailable."
                    : bestAny == null ? "No weapons on this map." : "No usable weapon: every candidate is blocked.";
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f), text,
                    new Color(1f, 0.7f, 0.7f));
                y += 22f;
            }

            if (bestAny != null && (bestReady == null || bestAny != bestReady))
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    string.Format("Best overall: {0}    score {1:0}  ({2})  - {3}",
                        bestAny.Weapon.Label, bestAny.Score, FormatPct(pawn.TopDeltaPct),
                        bestAny.Ready ? "ready" : bestAny.Blocker),
                    bestAny.Ready ? Color.white : new Color(1f, 0.8f, 0.42f));
                y += 22f;
            }

            Widgets.DrawLineHorizontal(rect.x, y, rect.width);
            y += 8f;

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

            Rect scrollRect = new Rect(rect.x, y, rect.width, Mathf.Max(40f, rect.yMax - y));
            float width = scrollRect.width - 20f;
            float contentHeight = PawnDetailHeight(pawn, limit, showBlocked);
            Widgets.BeginScrollView(scrollRect, ref pawnDetailScroll, new Rect(0f, 0f, width, contentHeight));

            float cursor = 0f;
            SectionHeader(ref cursor, width, "Ranked weapons");

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
                SectionHeader(ref cursor, width, "Incompatible (" + pawn.Incompatible.Count + ")");
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

            Widgets.EndScrollView();
        }

        private float PawnDetailHeight(PawnAnalysis pawn, int limit, bool showBlocked)
        {
            float height = 30f + limit * 22f;
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
                pawn.DeltaPct(candidate) >= 0f ? "Better than the current weapon." : "Worse than the current weapon.",
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

                PawnAnalysis best = row.BestPawn ?? row.BestAnyPawn;
                WeaponColumns(rowRect, out Rect label, out Rect power, out Rect bestPawn, out Rect status);
                DrawCell(label, row.Weapon.Label, TextAnchor.MiddleLeft);
                DrawCell(power, row.Weapon.Power.ToString("0"), TextAnchor.MiddleRight);
                DrawCell(bestPawn, best == null ? "-" : best.Pawn.LabelShort, TextAnchor.MiddleLeft);
                DrawCell(status, row.Status, TextAnchor.MiddleRight, GameFont.Small,
                    row.BestPawn != null ? new Color(0.7f, 0.9f, 0.7f) : new Color(1f, 0.78f, 0.4f));
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

            if (row.BestPawn != null)
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    string.Format("Best usable: {0}  ({1})", row.BestPawn.Pawn.LabelShort,
                        FormatPct(row.BestPawn.TopReadyDeltaPct)),
                    new Color(0.75f, 0.92f, 0.75f));
            }
            else if (row.BestAnyPawn != null)
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    string.Format("Best fit: {0}, but blocked ({1})", row.BestAnyPawn.Pawn.LabelShort,
                        ReasonFor(row.BestAnyPawn, row.Weapon)),
                    new Color(1f, 0.8f, 0.42f));
            }
            else
            {
                DrawColoredLine(new Rect(rect.x, y, rect.width, 22f),
                    "No colonist can use this weapon.", new Color(1f, 0.7f, 0.7f));
            }
            y += 24f;

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
                Candidate candidate = FindCandidate(pawn, row.Weapon);
                if (candidate == null)
                {
                    continue;
                }

                bool available = pawn.Available;
                bool holdsIt = pawn.Current == row.Weapon.Thing;
                string reason = null;
                bool hard = false;
                bool ready = available && !holdsIt &&
                             Eligibility.Check(pawn.Pawn, row.Weapon.Thing, true, out reason, out hard);

                DrawCell(new Rect(4f, cursor, Mathf.Max(40f, width - 270f), 22f),
                    pawn.Pawn.LabelShort + (holdsIt ? "  (holds it)" : string.Empty),
                    TextAnchor.MiddleLeft);
                DrawCell(new Rect(width - 260f, cursor, 56f, 22f), candidate.Score.ToString("0"),
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
                    status = "equipped";
                    color = new Color(0.75f, 0.8f, 0.9f);
                }
                else if (ready)
                {
                    status = "available";
                    color = new Color(0.7f, 0.9f, 0.7f);
                }
                else
                {
                    status = string.IsNullOrEmpty(reason) ? candidate.Blocker : reason;
                    color = new Color(1f, 0.62f, 0.62f);
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
            return candidate == null ? 0f : candidate.Score;
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

        // --------------------------------------------------------------- Demand

        private void DrawDemand(Rect area, ArmorySnapshot snapshot)
        {
            Widgets.DrawBoxSolid(area, new Color(0.12f, 0.12f, 0.12f));
            Rect inner = area.ContractedBy(8f);

            bool previousWrap = Text.WordWrap;
            Text.WordWrap = false;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.LabelEllipses(new Rect(inner.x, inner.y, inner.width, 22f),
                "Best usable weapon per colonist vs loose stock. Forbidden stock must be unforbidden before building more.");
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

                DrawCell(new Rect(4f, rowRect.y, rowWidth - 350f, rowRect.height), row.Label,
                    TextAnchor.MiddleLeft);
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
            DrawCell(new Rect(rect.x, rect.y, rect.width - 350f, rect.height), "Weapon",
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
            float labelWidth = Mathf.Max(40f, rowWidth - 662f);
            Rect header = new Rect(inner.x, headerY, rowWidth, HeaderHeight);
            Color headerColor = new Color(0.68f, 0.68f, 0.68f);
            float headerLabelWidth = Mathf.Max(40f, header.width - 662f);
            DrawCell(new Rect(header.x, header.y, 60f, header.height), "Ago",
                TextAnchor.MiddleRight, GameFont.Tiny, headerColor);
            DrawCell(new Rect(header.x + 72f, header.y, 120f, header.height), "Colonist",
                TextAnchor.MiddleLeft, GameFont.Tiny, headerColor);
            DrawCell(new Rect(header.x + 196f, header.y, headerLabelWidth, header.height), "Weapon",
                TextAnchor.MiddleLeft, GameFont.Tiny, headerColor);
            DrawCell(new Rect(header.xMax - 466f, header.y, 80f, header.height), "Action",
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
                DrawCell(new Rect(rowWidth - 466f, row.y, 80f, row.height), decision.Action,
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


