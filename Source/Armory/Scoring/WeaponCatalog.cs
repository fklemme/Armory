using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Armory
{
    /// <summary>
    /// One weapon def the colony can obtain: craftable with a recipe, already owned, or
    /// both. Carries a synthetic normal-quality instance so demand can be scored for
    /// weapons that are not on the map yet.
    /// </summary>
    public sealed class WeaponSource
    {
        public ThingDef Def;
        public RecipeDef Recipe;

        /// <summary>Unspawned probe used for hypothetical scoring. May be null.</summary>
        public ThingWithComps Probe;

        public float ProbePower;

        /// <summary>All material variants, built lazily and cached for the session.</summary>
        public List<WeaponVariant> AllVariants;
        public string BenchLabel;

        /// <summary>Best work bench for this recipe right now (built first, then unlocked).</summary>
        public ThingDef BenchDef;

        /// <summary>All work-bench defs that can run the recipe, resolved once.</summary>
        public List<ThingDef> BenchUsers;

        public bool AvailableNow;
        public ResearchProjectDef MissingResearch;
        public bool Buyable;

        /// <summary>
        /// True when the colony has some normal route to another copy: craft now, research
        /// towards it, or buy it. Owned-only defs are counted as supply but never as a plan.
        /// </summary>
        public bool Obtainable
        {
            get { return AvailableNow || MissingResearch != null || Buyable; }
        }
    }

    /// <summary>
    /// Every weapon def the colony could plausibly own, built once per session. Recipes
    /// come from the def database (including recipes generated from ThingDef.recipeMaker),
    /// so modded weapons participate as long as they use the normal crafting system.
    /// </summary>
    public static class WeaponCatalog
    {
        private static List<WeaponSource> sources;
        private static Dictionary<ThingDef, WeaponSource> byDef;

        public static List<WeaponSource> All
        {
            get
            {
                EnsureBuilt();
                return sources;
            }
        }

        /// <summary>True when at least one catalog def has a route to another copy.</summary>
        public static bool HasObtainable
        {
            get
            {
                EnsureBuilt();
                for (int i = 0; i < sources.Count; i++)
                {
                    if (sources[i].Obtainable)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Gets the catalog entry for a def, adding it when the colony owns a weapon that
        /// has no recipe (persona weapons, quest rewards, modded drops).
        /// </summary>
        public static WeaponSource GetOrCreate(ThingDef def)
        {
            EnsureBuilt();
            if (def == null || !IsCandidate(def))
            {
                return null;
            }

            WeaponSource source;
            if (byDef.TryGetValue(def, out source))
            {
                return source;
            }

            source = Create(def);
            if (source == null)
            {
                return null;
            }
            byDef[def] = source;
            sources.Add(source);
            sources.Sort(CompareSources);
            return source;
        }

        public static bool TryGet(ThingDef def, out WeaponSource source)
        {
            EnsureBuilt();
            if (def == null)
            {
                source = null;
                return false;
            }
            return byDef.TryGetValue(def, out source);
        }

        /// <summary>
        /// Recomputes research availability and the best bench; finishing a project or
        /// building a bench must not require a restart.
        /// </summary>
        public static void RefreshAvailability(Map map)
        {
            EnsureBuilt();
            for (int i = 0; i < sources.Count; i++)
            {
                Refresh(sources[i], map);
            }
        }

        private static void EnsureBuilt()
        {
            if (sources != null)
            {
                return;
            }

            sources = new List<WeaponSource>();
            byDef = new Dictionary<ThingDef, WeaponSource>();

            Dictionary<ThingDef, RecipeDef> recipes = CollectRecipes();
            foreach (KeyValuePair<ThingDef, RecipeDef> pair in recipes)
            {
                WeaponSource source = Create(pair.Key);
                if (source == null)
                {
                    continue;
                }
                source.Recipe = pair.Value;
                source.BenchUsers = CollectBenchUsers(pair.Value);
                source.BenchDef = ResolveBench(source, null);
                source.BenchLabel = source.BenchDef == null ? null : source.BenchDef.LabelCap;
                byDef[pair.Key] = source;
                sources.Add(source);
            }

            // DefDatabase order is stable but not meaningful; sort for predictable ties.
            sources.Sort(CompareSources);
        }

        private static int CompareSources(WeaponSource a, WeaponSource b)
        {
            return string.Compare(a.Def.defName, b.Def.defName, StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<ThingDef, RecipeDef> CollectRecipes()
        {
            Dictionary<ThingDef, RecipeDef> result = new Dictionary<ThingDef, RecipeDef>();
            List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading;

            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDef recipe = recipes[i];
                if (recipe == null || recipe.products == null)
                {
                    continue;
                }

                bool available;
                try
                {
                    available = recipe.AvailableNow;
                }
                catch (Exception)
                {
                    available = false;
                }

                for (int j = 0; j < recipe.products.Count; j++)
                {
                    ThingDefCountClass product = recipe.products[j];
                    if (product == null || product.thingDef == null || !IsCandidate(product.thingDef))
                    {
                        continue;
                    }

                    RecipeDef existing;
                    if (!result.TryGetValue(product.thingDef, out existing))
                    {
                        result[product.thingDef] = recipe;
                        continue;
                    }

                    // Prefer the recipe the colony can already use; otherwise the first one
                    // is as good as any for a locked def.
                    if (available && !SafeAvailable(existing))
                    {
                        result[product.thingDef] = recipe;
                    }
                }
            }

            return result;
        }

        private static WeaponSource Create(ThingDef def)
        {
            if (def == null || !IsCandidate(def))
            {
                return null;
            }

            WeaponSource source = new WeaponSource
            {
                Def = def,
                Buyable = def.tradeability == Tradeability.Buyable ||
                          def.tradeability == Tradeability.All
            };

            ThingDef stuff = null;
            if (def.MadeFromStuff)
            {
                stuff = GenStuff.DefaultStuffFor(def);
                if (stuff == null)
                {
                    // A stuffable weapon without any valid stuff cannot be scored.
                    return null;
                }
            }

            try
            {
                ThingWithComps probe = ThingMaker.MakeThing(def, stuff) as ThingWithComps;
                if (probe == null)
                {
                    return null;
                }

                CompQuality quality = probe.TryGetComp<CompQuality>();
                if (quality != null)
                {
                    // Demand is about "what would an ordinary new copy do", so do not
                    // inherit whatever quality the maker happened to produce.
                    quality.SetQuality(QualityCategory.Normal, null);
                }

                source.Probe = probe;
                source.ProbePower = WeaponPower.Get(probe, null);
            }
            catch (Exception exception)
            {
                Log.Warning("[Armory] could not build a scoring probe for " + def.defName + ": " +
                            exception.GetType().Name + ": " + exception.Message);
                return null;
            }

            return source.ProbePower > 0f ? source : null;
        }

        private static void Refresh(WeaponSource source, Map map)
        {
            if (source.Recipe == null)
            {
                source.AvailableNow = false;
                source.MissingResearch = null;
                source.BenchDef = null;
                source.BenchLabel = null;
                return;
            }

            try
            {
                source.AvailableNow = source.Recipe.AvailableNow;
            }
            catch (Exception)
            {
                source.AvailableNow = false;
            }
            source.MissingResearch = FirstMissing(source.Recipe);
            source.BenchDef = ResolveBench(source, map);
            source.BenchLabel = source.BenchDef == null ? null : source.BenchDef.LabelCap;
        }

        private static bool SafeAvailable(RecipeDef recipe)
        {
            try
            {
                return recipe.AvailableNow;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static ResearchProjectDef FirstMissing(RecipeDef recipe)
        {
            if (recipe.researchPrerequisites != null)
            {
                for (int i = 0; i < recipe.researchPrerequisites.Count; i++)
                {
                    ResearchProjectDef project = recipe.researchPrerequisites[i];
                    if (project != null && !project.IsFinished)
                    {
                        return project;
                    }
                }
            }
            if (recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished)
            {
                return recipe.researchPrerequisite;
            }
            return null;
        }

        private static List<ThingDef> CollectBenchUsers(RecipeDef recipe)
        {
            List<ThingDef> result = new List<ThingDef>();
            if (recipe == null)
            {
                return result;
            }

            HashSet<ThingDef> seen = new HashSet<ThingDef>();
            try
            {
                foreach (ThingDef user in recipe.AllRecipeUsers)
                {
                    if (user == null || user.thingClass == null || !seen.Add(user))
                    {
                        continue;
                    }
                    if (!typeof(IBillGiver).IsAssignableFrom(user.thingClass))
                    {
                        continue;
                    }
                    result.Add(user);
                }
            }
            catch (Exception exception)
            {
                Log.Warning("[Armory] could not resolve bench users for " + recipe.defName +
                            ": " + exception.GetType().Name + ": " + exception.Message);
            }
            return result;
        }

        /// <summary>
        /// Picks the bench that should receive a new bill. A built bench always wins
        /// (the bill has somewhere real to go); otherwise prefer the best unlocked
        /// bench so a tribal crafting spot is not hidden behind an electric smithy.
        /// </summary>
        private static ThingDef ResolveBench(WeaponSource source, Map map)
        {
            if (source == null || source.Recipe == null || source.BenchUsers == null)
            {
                return null;
            }

            Thing built = FindBuiltBench(source, map) as Thing;
            if (built != null)
            {
                return built.def;
            }

            ThingDef best = null;
            int bestRank = int.MinValue;
            for (int i = 0; i < source.BenchUsers.Count; i++)
            {
                ThingDef user = source.BenchUsers[i];
                if (!IsBenchUnlocked(user))
                {
                    continue;
                }

                int rank = BenchRank(user);
                if (best == null || rank > bestRank ||
                    (rank == bestRank &&
                     string.Compare(user.defName, best.defName, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    best = user;
                    bestRank = rank;
                }
            }
            return best;
        }

        /// <summary>
        /// Finds a built, player-owned bench that can run the recipe. Prefer one that is
        /// currently usable, then the more advanced bench, then the least busy one.
        /// </summary>
        public static IBillGiver FindBuiltBench(WeaponSource source, Map map)
        {
            if (source == null || source.Recipe == null || map == null ||
                source.BenchUsers == null || source.BenchUsers.Count == 0)
            {
                return null;
            }

            IBillGiver best = null;
            int bestUsable = -1;
            int bestRank = int.MinValue;
            int bestBills = int.MaxValue;
            string bestDefName = null;

            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                if (building == null || building.Destroyed ||
                    !source.BenchUsers.Contains(building.def))
                {
                    continue;
                }

                IBillGiver giver = building as IBillGiver;
                if (giver == null || giver.BillStack == null)
                {
                    continue;
                }

                int usable = SafeUsable(giver) ? 1 : 0;
                int rank = BenchRank(building.def);
                int bills = giver.BillStack.Count;
                bool better = best == null ||
                              usable > bestUsable ||
                              (usable == bestUsable && rank > bestRank) ||
                              (usable == bestUsable && rank == bestRank && bills < bestBills) ||
                              (usable == bestUsable && rank == bestRank && bills == bestBills &&
                               string.Compare(building.def.defName, bestDefName,
                                   StringComparison.OrdinalIgnoreCase) < 0);
                if (!better)
                {
                    continue;
                }

                best = giver;
                bestUsable = usable;
                bestRank = rank;
                bestBills = bills;
                bestDefName = building.def.defName;
            }
            return best;
        }

        private static bool SafeUsable(IBillGiver giver)
        {
            try
            {
                return giver.CurrentlyUsableForBills();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Higher tech level first; uiOrder breaks ties when tech levels are unset.
        /// </summary>
        private static int BenchRank(ThingDef def)
        {
            return (int)def.techLevel * 100000 + (int)def.uiOrder;
        }

        private static bool IsBenchUnlocked(ThingDef def)
        {
            if (def.researchPrerequisites == null)
            {
                return true;
            }
            for (int i = 0; i < def.researchPrerequisites.Count; i++)
            {
                ResearchProjectDef project = def.researchPrerequisites[i];
                if (project != null && !project.IsFinished)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Weapons only. Tools and other verb-bearing items are not useful for the hand or
        /// backup slot model, and non-combat items should never appear in demand.
        /// </summary>
        private static bool IsCandidate(ThingDef def)
        {
            return WeaponFilter.IsWeaponDef(def) &&
                   (def.IsRangedWeapon || def.IsMeleeWeapon);
        }
    }
}
