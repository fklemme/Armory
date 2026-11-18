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
        private bool billsRepaired;

        public ArmoryMapComponent(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            if (!billsRepaired)
            {
                billsRepaired = true;
                DemandCrafting.RepairMalformedBills(map);
            }

            if (!startupDiagnosticDone)
            {
                startupDiagnosticDone = true;
                if (ArmoryMod.Settings != null && ArmoryMod.Settings.debugLogging)
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
            Log.Message(string.Format("[Armory] {0}: {1} colonists, {2} loose weapons, {3} demand rows",
                source, snapshot.Pawns.Count, snapshot.Weapons.Count, snapshot.Demand.Count));
            Log.Message("[Armory]   Simple Sidearms integration: " +
                        (SimpleSidearmsAdapter.Active ? "active" : "inactive") +
                        ", respect setting: " +
                        (SimpleSidearmsAdapter.RespectEnabled ? "on" : "off"));
            for (int i = 0; i < snapshot.Pawns.Count && i < 5; i++)
            {
                PawnAnalysis pawn = snapshot.Pawns[i];
                Candidate top = pawn.TopOverall;
                Candidate ready = pawn.TopReady;
                Log.Message(string.Format(
                    "[Armory]   {0}: role {1}; {2} -> best hand {3} ({4:0}) [{5}]; ready {6}; hand ranked {7}, incompatible {8}; automation {9}; hand locked {10}",
                    pawn.Pawn.LabelShort,
                    pawn.HasRole
                        ? ((pawn.RoleIsRanged.Value ? "ranged" : "melee") + " (" + pawn.RoleSource + ")")
                        : "none",
                    pawn.Current == null ? "unarmed" : pawn.Current.LabelCap,
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
                    Log.Message(string.Format("[Armory]     hand {0}: score {1:0}, {2}",
                        candidate.Weapon.Label,
                        candidate.Score,
                        candidate.Ready ? "ready" : candidate.Blocker));
                }
                if (pawn.SidearmState != null)
                {
                    Log.Message("[Armory]     SS: " + pawn.SidearmState.Summary() +
                                "; carried weapons: " + pawn.SidearmText);
                }
                if (pawn.HasRole)
                {
                    Log.Message(string.Format(
                        "[Armory]     backup ({0}): carried {1}; best carried {2}; candidates {3}",
                        pawn.RoleIsRanged.Value ? "melee" : "ranged",
                        pawn.BestCarriedBackup == null ? "none" : pawn.BestCarriedBackup.LabelCap,
                        pawn.BestCarriedBackupScore,
                        pawn.SidearmCandidates.Count));
                    for (int j = 0; j < pawn.SidearmCandidates.Count && j < 3; j++)
                    {
                        SidearmCandidate sidearm = pawn.SidearmCandidates[j];
                        Log.Message(string.Format("[Armory]       backup {0}: score {1:0}, {2}{3}",
                            sidearm.Weapon.Label,
                            sidearm.Score,
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
            Log.Message(string.Format(
                "[Armory]   weapon catalog: {0} defs, {1} obtainable (craft/buy/research)",
                catalog.Count, obtainable));
            for (int i = 0; i < snapshot.Demand.Count && i < 10; i++)
            {
                DemandRow row = snapshot.Demand[i];
                string gain = row.NewCount == row.Wants.Count
                    ? "new"
                    : row.AverageGain.ToString("0%");
                Log.Message(string.Format(
                    "[Armory]   demand {0}: {1} | score {2:0} | power {3:0} | wanted {4} (best {5}) | owned {6} ({7} forbidden) | needed {8} | gain {9} | {10} | {11} | can craft {12} | bench {13}",
                    i + 1,
                    row.Label,
                    row.TotalScore,
                    row.Power,
                    row.Uses,
                    row.BestCount,
                    row.Owned,
                    row.Forbidden,
                    row.Need,
                    gain,
                    row.Section,
                    row.Section == DemandSection.Research
                        ? (row.MissingResearch ?? "locked")
                        : row.CraftableNow
                            ? "craft " + row.BenchLabel
                            : "buy",
                    row.CanCraft ? "yes" : "no",
                    row.BenchLabel ?? "-"));
            }
        }
    }
}





