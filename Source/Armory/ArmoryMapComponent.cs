using System;
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
            for (int i = 0; i < snapshot.Pawns.Count && i < 5; i++)
            {
                PawnAnalysis pawn = snapshot.Pawns[i];
                Candidate top = pawn.TopOverall;
                Candidate ready = pawn.TopReady;
                Log.Message(string.Format("[Armory]   {0}: {1} -> best {2} ({3:0}) [{4}]; ready {5}; ranked {6}, incompatible {7}",
                    pawn.Pawn.LabelShort,
                    pawn.Current == null ? "unarmed" : pawn.Current.LabelCap,
                    top == null ? "none" : top.Weapon.Label,
                    top == null ? 0f : top.Score,
                    top == null ? "n/a" : top.Ready ? "ready" : top.Blocker,
                    ready == null ? "none" : ready.Weapon.Label,
                    pawn.Ranked.Count,
                    pawn.Incompatible.Count));
                for (int j = 0; j < pawn.Ranked.Count && j < 4; j++)
                {
                    Candidate candidate = pawn.Ranked[j];
                    Log.Message(string.Format("[Armory]     {0}: score {1:0}, {2}",
                        candidate.Weapon.Label,
                        candidate.Score,
                        candidate.Ready ? "ready" : candidate.Blocker));
                }
            }
        }
    }
}





