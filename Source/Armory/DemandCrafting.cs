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
            return TryAddBill(def, null, map, out message);
        }

        public static bool TryAddBill(ThingDef def, ThingDef stuff, Map map, out string message)
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
                string itemLabel = stuff == null
                    ? source.Def.LabelCap
                    : GenLabel.ThingLabel(source.Def, stuff, 1).CapitalizeFirst();
                string billName = BillName(source.Def, itemLabel);
                if (production != null)
                {
                    production.repeatMode = BillRepeatModeDefOf.RepeatCount;
                    production.repeatCount = 1;
                    production.RenamableLabel = billName;
                    if (stuff != null)
                    {
                        RestrictToStuff(production, source.Recipe, def, stuff);
                    }
                }
                bench.BillStack.AddBill(bill);

                Thing benchThing = bench as Thing;
                string benchLabel = benchThing == null ? bench.LabelShort : benchThing.LabelShort;
                message = "Added a 1x " + billName + " bill at " + benchLabel + ".";
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
        /// A recognizable bill name. RimWorld's rename dialog caps names at 28 chars, so
        /// prefer "Armory: item", then "Armory item", then the base def label.
        /// </summary>
        private static string BillName(ThingDef def, string itemLabel)
        {
            const int maxLength = 28;
            string name = "Armory: " + itemLabel;
            if (name.Length <= maxLength)
            {
                return name;
            }
            name = "Armory " + itemLabel;
            if (name.Length <= maxLength)
            {
                return name;
            }
            name = "Armory: " + def.LabelCap;
            if (name.Length <= maxLength)
            {
                return name;
            }
            return name.Substring(0, maxLength).TrimEnd();
        }

        /// <summary>
        /// Restricts a production bill's ingredient filter to one chosen stuff while
        /// keeping all fixed ingredients (steel, components, ...) allowed.
        /// </summary>
        private static void RestrictToStuff(Bill_Production bill, RecipeDef recipe,
            ThingDef def, ThingDef stuff)
        {
            ThingFilter original = recipe.defaultIngredientFilter;
            ThingFilter filter = new ThingFilter();
            if (original != null)
            {
                filter.CopyAllowancesFrom(original);
            }
            else
            {
                filter.CopyAllowancesFrom(bill.ingredientFilter);
            }

            IEnumerable<ThingDef> allowedStuffs = GenStuff.AllowedStuffsFor(def);
            if (allowedStuffs != null)
            {
                foreach (ThingDef candidate in allowedStuffs)
                {
                    if (candidate != null)
                    {
                        filter.SetAllow(candidate, candidate == stuff);
                    }
                }
            }
            filter.SetAllow(stuff, true);
            bill.ingredientFilter = filter;
            bill.limitToAllowedStuff = true;
        }
    }
}
