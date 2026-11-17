using LudeonTK;
using RimWorld;
using Verse;

namespace Armory
{
    public static class DebugActions_Armory
    {
        [DebugAction("Armory", "Log weapon snapshot", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void LogSnapshot()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            ArmoryMapComponent component = map.GetComponent<ArmoryMapComponent>();
            if (component == null)
            {
                Log.Warning("[Armory] no ArmoryMapComponent on this map.");
                return;
            }
            ArmoryMapComponent.LogSnapshot(component.GetSnapshot(true), "debug action");
        }

        [DebugAction("Armory", "Unforbid loose weapons", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void UnforbidLooseWeapons()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            ArmoryMapComponent component = map.GetComponent<ArmoryMapComponent>();
            if (component == null)
            {
                return;
            }

            ArmorySnapshot snapshot = component.GetSnapshot(true);
            int count = 0;
            for (int i = 0; i < snapshot.Weapons.Count; i++)
            {
                ThingWithComps weapon = snapshot.Weapons[i].Thing;
                if (weapon.IsForbidden(Faction.OfPlayer))
                {
                    weapon.SetForbidden(false);
                    count++;
                }
            }
            Log.Message("[Armory] unforbade " + count + " loose weapons.");
        }

        [DebugAction("Armory", "Run planner now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RunPlannerNow()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            ArmoryPlanner.Run(map);
            Log.Message("[Armory] planner run finished.");
        }
    }
}
