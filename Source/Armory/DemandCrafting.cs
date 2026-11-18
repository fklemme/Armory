using System;
using System.Collections.Generic;
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
                Bill bill = source.Recipe.MakeNewBill();
                Bill_Production production = bill as Bill_Production;
                if (production != null)
                {
                    production.repeatMode = BillRepeatModeDefOf.RepeatCount;
                    production.repeatCount = 1;
                }
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

        /// <summary>
        /// Older Armory builds added a plain <see cref="Bill_Production"/> for recipes
        /// that use an unfinished thing. Vanilla's DoBill job casts those bills to
        /// <see cref="Bill_ProductionWithUft"/>, so the pawn throws and the ingredients
        /// vanish. Replace any such bill once per map so existing saves recover.
        /// </summary>
        public static int RepairMalformedBills(Map map)
        {
            if (map == null)
            {
                return 0;
            }

            List<KeyValuePair<IBillGiver, Bill>> broken =
                new List<KeyValuePair<IBillGiver, Bill>>();
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                IBillGiver giver = buildings[i] as IBillGiver;
                if (giver == null || giver.BillStack == null)
                {
                    continue;
                }
                for (int j = 0; j < giver.BillStack.Count; j++)
                {
                    Bill bill = giver.BillStack[j];
                    if (bill != null && bill.recipe != null &&
                        bill.recipe.UsesUnfinishedThing &&
                        !(bill is Bill_ProductionWithUft))
                    {
                        broken.Add(new KeyValuePair<IBillGiver, Bill>(giver, bill));
                    }
                }
            }

            int repaired = 0;
            for (int i = 0; i < broken.Count; i++)
            {
                IBillGiver giver = broken[i].Key;
                Bill old = broken[i].Value;
                Bill replacement;
                try
                {
                    replacement = old.recipe.MakeNewBill();
                }
                catch (Exception exception)
                {
                    Log.Warning("[Armory] could not repair bill " + old.recipe.defName +
                                ": " + exception.Message);
                    continue;
                }

                try
                {
                    CopyBillSettings(old, replacement);
                    giver.BillStack.Delete(old);
                    giver.BillStack.AddBill(replacement);
                    repaired++;
                }
                catch (Exception exception)
                {
                    Log.Warning("[Armory] could not replace bill " + old.recipe.defName +
                                ": " + exception.Message);
                }            }

            if (repaired > 0)
            {
                Log.Warning("[Armory] repaired " + repaired +
                            " malformed unfinished-thing bill(s) on this map.");
            }
            return repaired;
        }

        private static void CopyBillSettings(Bill old, Bill replacement)
        {
            replacement.suspended = old.suspended;
            replacement.ingredientSearchRadius = old.ingredientSearchRadius;
            replacement.allowedSkillRange = old.allowedSkillRange;
            if (old.PawnRestriction != null)
            {
                replacement.SetPawnRestriction(old.PawnRestriction);
            }
            else if (old.SlavesOnly)
            {
                replacement.SetAnySlaveRestriction();
            }
            else if (old.MechsOnly)
            {
                replacement.SetAnyMechRestriction();
            }
            else if (old.NonMechsOnly)
            {
                replacement.SetAnyNonMechRestriction();
            }

            Bill_Production oldProduction = old as Bill_Production;
            Bill_Production newProduction = replacement as Bill_Production;
            if (oldProduction == null || newProduction == null)
            {
                return;
            }

            newProduction.repeatMode = oldProduction.repeatMode;
            newProduction.repeatCount = oldProduction.repeatCount;
            newProduction.targetCount = oldProduction.targetCount;
            newProduction.pauseWhenSatisfied = oldProduction.pauseWhenSatisfied;
            newProduction.unpauseWhenYouHave = oldProduction.unpauseWhenYouHave;
            newProduction.includeEquipped = oldProduction.includeEquipped;
            newProduction.includeTainted = oldProduction.includeTainted;
            newProduction.hpRange = oldProduction.hpRange;
            newProduction.qualityRange = oldProduction.qualityRange;
            newProduction.limitToAllowedStuff = oldProduction.limitToAllowedStuff;
            newProduction.SetStoreMode(oldProduction.GetStoreMode(),
                oldProduction.GetSlotGroup());
        }
    }
}
