using System;
using System.Collections.Generic;
using Armory.Compatibility;
using RimWorld;
using Verse;

namespace Armory
{
    /// <summary>Caches the current map's analysis and rebuilds it on a timer.</summary>
    public class ArmoryMapComponent : MapComponent
    {
        private ArmorySnapshot snapshot;
        private bool startupDiagnosticDone;

        public ArmoryMapComponent(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            if (!startupDiagnosticDone)
            {
                startupDiagnosticDone = true;
                if (ArmoryMod.Settings != null &&
                    (ArmoryMod.Settings.debugLogging || ArmoryMod.Settings.diagnosticLogging))
                {
                    LogSnapshot(GetSnapshot(true), "map load");
                }
            }

            ArmorySettings settings = ArmoryMod.Settings;
            if (settings == null || !settings.autoEquipEnabled || !ArmoryMod.AutomationActive)
            {
                return;
            }
            int interval = settings.plannerIntervalTicks < 60 ? 60 : settings.plannerIntervalTicks;
            if (Find.TickManager.TicksGame % interval != 0)
            {
                return;
            }
            ArmoryPlanner.Run(map);
        }

        public ArmorySnapshot GetSnapshot(bool force = false)
        {
            int now = Find.TickManager.TicksGame;
            int interval = ArmoryMod.Settings == null ? 120 : ArmoryMod.Settings.scanIntervalTicks;

            if (force || snapshot == null || now - snapshot.BuiltTick >= interval)
            {
                try
                {
                    snapshot = ArmorySnapshotBuilder.Build(map);
                }
                catch (Exception exception)
                {
                    Log.Error("[Armory] Failed to build snapshot: " + exception);
                    if (snapshot == null)
                    {
                        snapshot = new ArmorySnapshot { Map = map, BuiltTick = now };
                    }
                    else
                    {
                        snapshot.BuiltTick = now;
                    }
                }
            }

            return snapshot;
        }

        public void Invalidate()
        {
            snapshot = null;
        }

        public static void LogSnapshot(ArmorySnapshot snapshot, string source)
        {
            if (snapshot == null)
            {
                return;
            }
            SnapshotLog(string.Format(
                "[Armory] {0} (world {1}): {2} colonists, {3} loose weapons, {4} demand rows at tick {5}",
                source, ArmoryDiagnostics.CurrentWorldName, snapshot.Pawns.Count,
                snapshot.Weapons.Count, snapshot.Demand.Count, snapshot.BuiltTick));
            SnapshotLog("[Armory]   Simple Sidearms integration: " +
                        (SimpleSidearmsAdapter.Active ? "active" : "inactive") +
                        ", respect setting: " +
                        (SimpleSidearmsAdapter.RespectEnabled ? "on" : "off"));
            for (int i = 0; i < snapshot.Pawns.Count && i < 30; i++)
            {
                PawnAnalysis pawn = snapshot.Pawns[i];
                Candidate top = pawn.TopOverall;
                Candidate ready = pawn.TopReady;
                SnapshotLog(string.Format(
                    "[Armory]   {0}: role {1}; combat {2} ({3}, prefers {4}); current {5}; logical hand {6}; best hand {7} ({8:0}) [{9}]; ready {10}; hand ranked {11}, incompatible {12}; automation {13}; hand locked {14}",
                    pawn.Pawn.LabelShort,
                    pawn.HasRole
                        ? ((pawn.RoleIsRanged.Value ? "ranged" : "melee") + " (" + pawn.RoleSource + ")")
                        : "none",
                    pawn.RoleIsRanged == true ? CombatScoring.Label(pawn.CombatRole) : "n/a",
                    pawn.RoleOverridden ? "override" : "auto",
                    pawn.RoleIsRanged == true ? pawn.PreferredRange + "t" : "n/a",
                    pawn.Current == null ? "unarmed" : pawn.Current.LabelCap,
                    pawn.LogicalHand == null ? "none" : pawn.LogicalHand.LabelCap,
                    top == null ? "none" : top.Weapon.Label,
                    top == null ? 0f : top.Score,
                    top == null ? "n/a" : top.Ready ? "ready" : top.Blocker,
                    ready == null ? "none" : ready.Weapon.Label,
                    pawn.Ranked.Count,
                    pawn.Incompatible.Count,
                    pawn.AutomationNote ?? "ok",
                    pawn.HandLocked ? "yes" : "no"));
                for (int j = 0; j < pawn.Ranked.Count && j < 4; j++)
                {
                    Candidate candidate = pawn.Ranked[j];
                    SnapshotLog(string.Format("[Armory]     hand {0}: score {1:0}, {2}",
                        candidate.Weapon.Label,
                        candidate.Score,
                        candidate.Ready ? "ready" : candidate.Blocker));
                }
                if (pawn.SidearmState != null)
                {
                    SnapshotLog("[Armory]     SS: " + pawn.SidearmState.Summary() +
                                "; carried weapons: " + pawn.SidearmText);
                }
                if (pawn.SidearmState != null)
                {
                    SnapshotLog(string.Format(
                        "[Armory]     Sidearm 1 ({0}): carried {1}; score {2:0}; candidates {3}",
                        pawn.RoleIsRanged.Value ? "melee" : "ranged",
                        pawn.BestCarriedBackup == null ? "none" : pawn.BestCarriedBackup.LabelCap,
                        pawn.BestCarriedBackupScore,
                        pawn.SidearmCandidates.Count));
                    for (int j = 0; j < pawn.SidearmCandidates.Count && j < 3; j++)
                    {
                        SidearmCandidate sidearm = pawn.SidearmCandidates[j];
                        SnapshotLog(string.Format("[Armory]       Sidearm 1 {0}: score {1:0}, {2}{3}",
                            sidearm.Weapon.Label,
                            sidearm.SlotScore,
                            sidearm.Ready ? "ready" : sidearm.Blocker,
                            sidearm.UpgradeAfterDrop ? " [drop-first]" : string.Empty));
                    }

                    SnapshotLog(string.Format(
                        "[Armory]     Sidearm 2 (ranged): carried {0}; coverage {1:0.0}; gain {2:0}%; band {3}; candidates {4}; held {5}; combat draw {6}; sidearm remembered {7}; hand remembered {8}",
                        pawn.BestCarriedSidearmTwo == null
                            ? "none"
                            : pawn.BestCarriedSidearmTwo.LabelCap,
                        pawn.CarriedSidearmTwoCoverage,
                        pawn.CarriedSidearmTwoGain * 100f,
                        pawn.CarriedSidearmTwoBand.HasValue
                            ? pawn.CarriedSidearmTwoBand.Value.ToString()
                            : "none",
                        pawn.SidearmTwoCandidates.Count,
                        pawn.DrawnSidearm ? pawn.DrawnSidearmName : "no",
                        pawn.CombatSidearmDraw ? "yes" : "no",
                        pawn.CurrentSidearmRemembered ? "yes" : "no",
                        pawn.PlannedHandRemembered ? "yes" : "no"));
                    for (int j = 0; j < pawn.SidearmTwoCandidates.Count && j < 3; j++)
                    {
                        SidearmCandidate sidearm = pawn.SidearmTwoCandidates[j];
                        SnapshotLog(string.Format(
                            "[Armory]       Sidearm 2 {0}: coverage {1:0.0}, gain {2:0}%, {3}{4}",
                            sidearm.Weapon.Label,
                            sidearm.SlotScore,
                            sidearm.ComplementGain * 100f,
                            sidearm.Ready ? "ready" : sidearm.Blocker,
                            sidearm.UpgradeAfterDrop ? " [drop-first]" : string.Empty));
                    }
                }
            }

            List<WeaponSource> catalog = WeaponCatalog.All;
            int obtainable = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i].Obtainable)
                {
                    obtainable++;
                }
            }
            SnapshotLog(string.Format(
                "[Armory]   weapon catalog: {0} defs, {1} obtainable (craft/buy/research)",
                catalog.Count, obtainable));
            for (int i = 0; i < snapshot.Demand.Count && i < 10; i++)
            {
                DemandRow row = snapshot.Demand[i];
                string gain = row.NewCount == row.Wants.Count
                    ? "new"
                    : row.AverageGain.ToString("0%");
                SnapshotLog(string.Format(
                    "[Armory]   demand {0}: {1} | score {2:0} | power {3:0} | range {4:0.0} | wanted {5} (best {6}) | owned {7} ({8} forbidden) | needed {9} | gain {10} | {11} | {12} | can craft {13} | bench {14}",
                    i + 1,
                    row.Label,
                    row.TotalScore,
                    row.Power,
                    row.Range,
                    row.Uses,
                    row.BestCount,
                    row.Owned,
                    row.Forbidden,
                    row.Need,
                    gain,
                    row.Section,
                    row.Section == DemandSection.Owned
                        ? "owned"
                        : row.Section == DemandSection.Research
                            ? (row.MissingResearch ?? "locked")
                            : row.CraftableNow
                                ? "craft " + row.BenchLabel
                                : "buy",
                    row.CanCraft ? "yes" : "no",
                    row.BenchLabel ?? "-"));
            }
        }

        private static void SnapshotLog(string message)
        {
            Log.Message(message);
            ArmoryDiagnostics.Write("Snapshot", message);
        }
    }
}





