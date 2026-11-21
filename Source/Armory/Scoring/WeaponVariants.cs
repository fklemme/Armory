using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Armory
{
    /// <summary>
    /// Builds per-material weapon variants. Stuffable weapons get one probe per allowed
    /// material so the Upgrades tab can compare a steel knife with a plasteel knife and
    /// queue the bill for the material the player actually chose.
    /// </summary>
    public static class WeaponVariants
    {
        public static List<WeaponVariant> For(WeaponSource source, Map map,
            HashSet<WeaponVariantKey> ownedKeys)
        {
            List<WeaponVariant> all = All(source);
            if (all.Count == 0)
            {
                return all;
            }

            Refresh(all, map);
            if (map == null)
            {
                return all;
            }

            WeaponVariant best = null;
            for (int i = 0; i < all.Count; i++)
            {
                WeaponVariant variant = all[i];
                if (variant.Power > 0f && (best == null || variant.Power > best.Power))
                {
                    best = variant;
                }
            }

            ThingDef defaultStuff = source.Def.MadeFromStuff
                ? GenStuff.DefaultStuffFor(source.Def)
                : null;
            List<WeaponVariant> result = new List<WeaponVariant>();
            for (int i = 0; i < all.Count; i++)
            {
                WeaponVariant variant = all[i];
                if (variant.Power <= 0f)
                {
                    continue;
                }

                bool include = variant.Stuff == null || variant == best ||
                               variant.Stuff == defaultStuff ||
                               (ownedKeys != null && ownedKeys.Contains(variant.Key)) ||
                               map.resourceCounter.GetCount(variant.Stuff) > 0;
                if (include)
                {
                    result.Add(variant);
                }
            }
            return result;
        }

        public static List<WeaponVariant> All(WeaponSource source)
        {
            if (source == null || source.Def == null)
            {
                return new List<WeaponVariant>();
            }
            if (source.AllVariants != null)
            {
                return source.AllVariants;
            }

            List<WeaponVariant> result = new List<WeaponVariant>();
            ThingDef def = source.Def;
            if (!def.MadeFromStuff)
            {
                AddVariant(result, source, null);
            }
            else
            {
                HashSet<ThingDef> seen = new HashSet<ThingDef>();
                foreach (ThingDef stuff in GenStuff.AllowedStuffsFor(def))
                {
                    if (stuff == null || stuff.stuffProps == null || !seen.Add(stuff))
                    {
                        continue;
                    }
                    AddVariant(result, source, stuff);
                }

                if (result.Count == 0)
                {
                    AddVariant(result, source, GenStuff.DefaultStuffFor(def));
                }
            }

            result.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
            source.AllVariants = result;
            return result;
        }

        private static void AddVariant(List<WeaponVariant> list, WeaponSource source, ThingDef stuff)
        {
            try
            {
                ThingWithComps probe = ThingMaker.MakeThing(source.Def, stuff) as ThingWithComps;
                if (probe == null)
                {
                    return;
                }

                CompQuality quality = probe.TryGetComp<CompQuality>();
                if (quality != null)
                {
                    quality.SetQuality(QualityCategory.Normal, null);
                }

                WeaponVariant variant = new WeaponVariant
                {
                    Source = source,
                    Def = source.Def,
                    Stuff = stuff,
                    Probe = probe,
                    Power = WeaponPower.Get(probe, null),
                    Range = WeaponPower.GetRange(probe),
                    Label = GenLabel.ThingLabel(source.Def, stuff, 1).CapitalizeFirst()
                };

                BuildCosts(variant, source.Recipe, stuff);
                list.Add(variant);
            }
            catch (Exception exception)
            {
                Log.Warning("[Armory] could not build a weapon variant for " +
                            source.Def.defName + " / " +
                            (stuff == null ? "no stuff" : stuff.defName) + ": " +
                            exception.GetType().Name + ": " + exception.Message);
            }
        }

        /// <summary>
        /// Ingredients for one copy, including the chosen stuff. Uses the live recipe so
        /// modded costs are included, not just ThingDef.costList.
        /// </summary>
        private static void BuildCosts(WeaponVariant variant, RecipeDef recipe, ThingDef stuff)
        {
            Dictionary<ThingDef, int> counts = new Dictionary<ThingDef, int>();
            int productCount = 1;

            if (recipe != null)
            {
                if (recipe.products != null)
                {
                    for (int i = 0; i < recipe.products.Count; i++)
                    {
                        ThingDefCountClass product = recipe.products[i];
                        if (product != null && product.thingDef == variant.Def && product.count > 0)
                        {
                            productCount = product.count;
                            break;
                        }
                    }
                }

                if (recipe.ingredients != null)
                {
                    for (int i = 0; i < recipe.ingredients.Count; i++)
                    {
                        IngredientCount ingredient = recipe.ingredients[i];
                        if (ingredient == null)
                        {
                            continue;
                        }

                        ThingDef costDef = null;
                        if (ingredient.IsFixedIngredient)
                        {
                            costDef = ingredient.FixedIngredient;
                        }
                        else if (stuff != null && ingredient.filter != null &&
                                 ingredient.filter.Allows(stuff))
                        {
                            costDef = stuff;
                        }

                        if (costDef == null)
                        {
                            continue;
                        }

                        int count;
                        try
                        {
                            count = ingredient.CountRequiredOfFor(costDef, recipe);
                        }
                        catch (Exception)
                        {
                            count = Mathf.CeilToInt(ingredient.GetBaseCount());
                        }
                        AddCost(counts, costDef, count, productCount);
                    }
                }
            }

            if (stuff != null && !counts.ContainsKey(stuff) && variant.Def.CostStuffCount > 0)
            {
                AddCost(counts, stuff, variant.Def.CostStuffCount, productCount);
            }

            if (recipe == null && variant.Def.CostList != null)
            {
                for (int i = 0; i < variant.Def.CostList.Count; i++)
                {
                    ThingDefCountClass cost = variant.Def.CostList[i];
                    if (cost != null && cost.thingDef != null && cost.count > 0)
                    {
                        AddCost(counts, cost.thingDef, cost.count, productCount);
                    }
                }
            }

            List<ThingDefCountClass> costs = new List<ThingDefCountClass>();
            foreach (KeyValuePair<ThingDef, int> pair in counts)
            {
                if (pair.Key != null && pair.Value > 0)
                {
                    costs.Add(new ThingDefCountClass(pair.Key, pair.Value));
                }
            }
            costs.Sort((a, b) =>
            {
                if (a.thingDef == stuff)
                {
                    return b.thingDef == stuff ? 0 : -1;
                }
                if (b.thingDef == stuff)
                {
                    return 1;
                }
                int byCount = b.count.CompareTo(a.count);
                return byCount != 0
                    ? byCount
                    : string.Compare(a.thingDef.defName, b.thingDef.defName,
                        StringComparison.OrdinalIgnoreCase);
            });

            variant.Costs.Clear();
            variant.Costs.AddRange(costs);
        }

        private static void AddCost(Dictionary<ThingDef, int> counts, ThingDef def, int count,
            int productCount)
        {
            if (def == null || count <= 0)
            {
                return;
            }
            int perCopy = Mathf.Max(1, Mathf.CeilToInt(count / (float)Mathf.Max(1, productCount)));
            int existing;
            counts.TryGetValue(def, out existing);
            counts[def] = existing + perCopy;
        }

        private static void Refresh(List<WeaponVariant> variants, Map map)
        {
            for (int i = 0; i < variants.Count; i++)
            {
                Refresh(variants[i], map);
            }
        }

        private static void Refresh(WeaponVariant variant, Map map)
        {
            if (variant == null)
            {
                return;
            }

            if (variant.Costs.Count == 0)
            {
                variant.CostText = "-";
                variant.FullCostText = "no material cost";
                variant.MaterialsAvailable = true;
                variant.CraftableCopies = -1;
                variant.CostTooltip = "No material cost is known for this variant.";
                return;
            }

            variant.CostText = variant.Costs[0].count + " " + variant.Costs[0].thingDef.label;
            List<string> parts = new List<string>();
            for (int i = 0; i < variant.Costs.Count; i++)
            {
                parts.Add(variant.Costs[i].count + " " + variant.Costs[i].thingDef.label);
            }
            variant.FullCostText = string.Join(", ", parts);

            if (map == null)
            {
                variant.MaterialsAvailable = true;
                variant.CraftableCopies = -1;
                variant.CostTooltip = "Cost per copy: " + variant.FullCostText +
                                      "\nMaterial stock is only known on a map.";
                return;
            }

            bool all = true;
            int copies = int.MaxValue;
            List<string> shortParts = new List<string>();
            List<string> stockParts = new List<string>();
            for (int i = 0; i < variant.Costs.Count; i++)
            {
                ThingDefCountClass cost = variant.Costs[i];
                int stock = map.resourceCounter.GetCount(cost.thingDef);
                stockParts.Add(stock + " " + cost.thingDef.label);
                if (stock < cost.count)
                {
                    all = false;
                    shortParts.Add((cost.count - stock) + " " + cost.thingDef.label);
                    copies = 0;
                }
                else if (cost.count > 0)
                {
                    copies = Mathf.Min(copies, stock / cost.count);
                }
            }

            variant.MaterialsAvailable = all;
            variant.CraftableCopies = copies == int.MaxValue ? -1 : copies;
            string tooltip = "Cost per copy: " + variant.FullCostText +
                             "\nStock: " + string.Join(", ", stockParts);
            if (!all)
            {
                tooltip += "\nMissing: " + string.Join(", ", shortParts) +
                           ". You can still queue the bill.";
            }
            else if (copies > 0)
            {
                tooltip += "\nEnough for " + copies + " cop" + (copies == 1 ? "y." : "ies.");
            }
            variant.CostTooltip = tooltip;
        }
    }
}
