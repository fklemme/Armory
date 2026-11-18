using System;
using RimWorld;
using Verse;

namespace Armory
{
    /// <summary>
    /// Queues a single production bill for a Demand weapon at a built work bench.
    /// Demand itself never invents bills automatically; this only runs when the player
    /// clicks the Craft button on a row.
    /// </summary>
    public static class DemandCrafting
    {
        public static bool TryAddBill(ThingDef def, Map map, out string message)
        {
            message = null;
            if (def == null || map == null)
            {
                message = "No map or weapon selected.";
                return false;
            }

            WeaponSource source;
            if (!WeaponCatalog.TryGet(def, out source) || source == null || source.Recipe == null)
            {
                message = "No crafting recipe is known for this weapon.";
                return false;
            }

            bool available;
            try
            {
                available = source.Recipe.AvailableNow;
            }
            catch (Exception)
            {
                available = false;
            }
            if (!available)
            {
                message = source.MissingResearch == null
                    ? "The recipe is not unlocked yet."
                    : "Research " + source.MissingResearch.LabelCap + " first.";
                Messages.Message(message, MessageTypeDefOf.RejectInput);
                return false;
            }

            IBillGiver bench = WeaponCatalog.FindBuiltBench(source, map);
            if (bench == null)
            {
                message = source.BenchLabel == null
                    ? "Build a work bench that can make this first."
                    : "Build a " + source.BenchLabel + " first.";
                Messages.Message(message, MessageTypeDefOf.RejectInput);
                return false;
            }

            try
            {
                Bill_Production bill = new Bill_Production(source.Recipe);
                bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                bill.repeatCount = 1;
                bench.BillStack.AddBill(bill);

                Thing benchThing = bench as Thing;
                string benchLabel = benchThing == null ? bench.LabelShort : benchThing.LabelShort;
                message = "Added a 1x " + source.Def.LabelCap + " bill at " + benchLabel + ".";
                Messages.Message(message,
                    benchThing == null ? null : new LookTargets(benchThing),
                    MessageTypeDefOf.PositiveEvent);

                if (ArmoryMod.Settings != null && ArmoryMod.Settings.debugLogging)
                {
                    Log.Message("[Armory] craft bill: " + message);
                }
                return true;
            }
            catch (Exception exception)
            {
                message = "Could not add the bill: " + exception.Message;
                Log.Warning("[Armory] " + message);
                Messages.Message(message, MessageTypeDefOf.RejectInput);
                return false;
            }
        }
    }
}
