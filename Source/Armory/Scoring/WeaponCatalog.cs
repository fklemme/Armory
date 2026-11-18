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
        public string BenchLabel;

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
        /// Recomputes research availability; finishing a project must not require a restart.
        /// </summary>
        public static void RefreshAvailability()
        {
            EnsureBuilt();
            for (int i = 0; i < sources.Count; i++)
            {
                Refresh(sources[i]);
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
                source.BenchLabel = ResolveBench(pair.Value);
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

        private static void Refresh(WeaponSource source)
        {
            if (source.Recipe == null)
            {
                source.AvailableNow = false;
                source.MissingResearch = null;
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

        private static string ResolveBench(RecipeDef recipe)
        {
            try
            {
                foreach (ThingDef user in recipe.AllRecipeUsers)
                {
                    if (user != null)
                    {
                        return user.LabelCap;
                    }
                }
            }
            catch (Exception)
            {
                // Fall through to the plain recipeUsers list.
            }

            if (recipe.recipeUsers != null)
            {
                for (int i = 0; i < recipe.recipeUsers.Count; i++)
                {
                    ThingDef user = recipe.recipeUsers[i];
                    if (user != null)
                    {
                        return user.LabelCap;
                    }
                }
            }
            return null;
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
