using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace Armory.Compatibility
{
    /// <summary>
    /// Read-only, reflection-based view of Simple Sidearms. There is deliberately no
    /// compile-time reference: Armory must load and run in a Core-only game, and must
    /// not break when Simple Sidearms changes its internals.
    ///
    /// What Armory uses this for (milestone 3):
    /// - do not fight a forced primary / forced unarmed state;
    /// - do not fight Simple Sidearms' primary weapon mode (ranged vs melee);
    /// - know when a sidearm job is already running.
    /// </summary>
    public static class SimpleSidearmsAdapter
    {
        private const string PackageId = "PeteTimesSix.SimpleSidearms";
        private const string MemoryTypeName = "SimpleSidearms.rimworld.CompSidearmMemory";
        private const string StatCalculatorTypeName = "PeteTimesSix.SimpleSidearms.Utilities.StatCalculator";
        private const string ExtensionsTypeName = "PeteTimesSix.SimpleSidearms.Extensions";

        /// <summary>SS state that matters for primary coexistence. Null when SS has no memory comp for the pawn.</summary>
        public sealed class PawnState
        {
            public bool ForcedUnarmed;

            public ThingDef ForcedWeaponDef;
            public ThingDef ForcedWeaponStuff;
            public string ForcedWeaponLabel;

            /// <summary>True when the pawn is marked "prefer unarmed" in Simple Sidearms.</summary>
            public bool PrefersUnarmed;

            /// <summary>Carried default-ranged / preferred-melee pair that SS will switch to.</summary>
            public ThingDef PreferredPairDef;
            public ThingDef PreferredPairStuff;
            public string PreferredPairLabel;
            public string PreferredPairReason;

            /// <summary>"ranged", "melee", "by skill (ranged)", "by generated", ...</summary>
            public string PrimaryModeLabel = "unknown";

            /// <summary>Effective preferred primary type. Null when SS has no opinion (by generated).</summary>
            public bool? PreferredPrimaryIsRanged;

            /// <summary>True when the pawn carries a weapon of the preferred type, so SS would switch back.</summary>
            public bool CarriesPreferredPrimary;

            /// <summary>True when Armory must leave this pawn's primary alone.</summary>
            public bool AutomationVeto
            {
                get { return ForcedUnarmed || ForcedWeaponDef != null || PrefersUnarmed; }
            }

            public string VetoReason;

            public string Summary()
            {
                string text = "primary " + PrimaryModeLabel;
                if (AutomationVeto)
                {
                    text += " | " + VetoReason;
                }
                else if (PreferredPairLabel != null)
                {
                    text += " | " + PreferredPairReason;
                }
                else if (CarriesPreferredPrimary)
                {
                    text += " | carries preferred type";
                }
                return text;
            }
        }

        private static bool initialized;
        private static bool active;
        private static bool failed;

        private static Type memoryType;
        private static Type statCalculatorType;
        private static Type extensionsType;

        private static MethodInfo getMemoryMethod;
        private static MethodInfo canPickupSidearmMethod;
        private static MethodInfo skillPreferenceMethod;

        private static PropertyInfo forcedUnarmedProperty;
        private static PropertyInfo forcedWeaponProperty;
        private static PropertyInfo forcedUnarmedDraftedProperty;
        private static PropertyInfo forcedWeaponDraftedProperty;
        private static PropertyInfo preferredUnarmedProperty;
        private static PropertyInfo defaultRangedProperty;
        private static PropertyInfo preferredMeleeProperty;
        private static FieldInfo primaryModeField;

        private static FieldInfo pairThingField;
        private static FieldInfo pairStuffField;

        public static bool Active
        {
            get
            {
                EnsureInitialized();
                return active;
            }
        }

        /// <summary>Player setting; defaults to on so Armory never silently fights Simple Sidearms.</summary>
        public static bool RespectEnabled
        {
            get { return ArmoryMod.Settings == null || ArmoryMod.Settings.respectSimpleSidearms; }
        }

        /// <summary>True while Simple Sidearms is equipping or re-equipping a sidearm for this pawn.</summary>
        public static bool IsSidearmJobRunning(Pawn pawn)
        {
            if (pawn == null || pawn.jobs == null || pawn.jobs.curJob == null ||
                pawn.jobs.curJob.def == null)
            {
                return false;
            }
            string defName = pawn.jobs.curJob.def.defName;
            return defName == "EquipSecondary" || defName == "ReequipSecondary" ||
                   defName == "ReequipSecondaryCombat";
        }

        /// <summary>
        /// Returns why Armory must not equip this weapon as a primary, or false when there is
        /// no Simple Sidearms objection. Only consulted while <see cref="RespectEnabled"/> is on.
        /// </summary>
        public static bool TryGetPrimaryBlocker(Pawn pawn, ThingWithComps weapon, out string reason)
        {
            reason = null;
            if (!RespectEnabled || weapon == null || !Active)
            {
                return false;
            }

            PawnState state = GetState(pawn);
            if (state == null)
            {
                return false;
            }

            if (state.ForcedUnarmed)
            {
                reason = state.VetoReason ?? "SS: forced unarmed";
                return true;
            }

            if (state.ForcedWeaponDef != null)
            {
                if (!Matches(weapon, state.ForcedWeaponDef, state.ForcedWeaponStuff))
                {
                    reason = "SS: forced " + state.ForcedWeaponLabel;
                    return true;
                }
                return false;
            }

            if (state.PrefersUnarmed)
            {
                reason = state.VetoReason ?? "SS: prefers unarmed";
                return true;
            }

            if (state.PreferredPairDef != null && !Matches(weapon, state.PreferredPairDef, state.PreferredPairStuff))
            {
                reason = state.PreferredPairReason;
                return true;
            }

            if (state.PreferredPrimaryIsRanged.HasValue && state.CarriesPreferredPrimary &&
                !MatchesPrimaryType(weapon.def, state.PreferredPrimaryIsRanged.Value))
            {
                reason = state.PreferredPrimaryIsRanged.Value ? "SS: ranged primary" : "SS: melee primary";
                return true;
            }

            return false;
        }

        /// <summary>Reads the pawn's Simple Sidearms memory. Returns null when the comp is absent.</summary>
        public static PawnState GetState(Pawn pawn)
        {
            if (!Active || failed || pawn == null || pawn.RaceProps == null ||
                !pawn.RaceProps.Humanlike)
            {
                return null;
            }

            object comp;
            try
            {
                comp = getMemoryMethod.Invoke(null, new object[] { pawn, true });
            }
            catch (Exception exception)
            {
                Disable("GetMemoryCompForPawn failed", exception);
                return null;
            }
            if (comp == null)
            {
                return null;
            }

            try
            {
                PawnState state = new PawnState();

                bool forcedUnarmed = ReadBool(forcedUnarmedProperty, comp);
                bool forcedUnarmedDrafted = ReadBool(forcedUnarmedDraftedProperty, comp);
                object forcedWeapon = ReadPair(forcedWeaponProperty, comp);
                object forcedWeaponDrafted = ReadPair(forcedWeaponDraftedProperty, comp);

                // Mirrors Simple Sidearms' own precedence: drafted unarmed, drafted weapon,
                // then the normal forced unarmed / forced weapon state.
                object chosenPair = null;
                if (pawn.Drafted && forcedUnarmedDrafted)
                {
                    state.ForcedUnarmed = true;
                }
                else if (pawn.Drafted && forcedWeaponDrafted != null)
                {
                    chosenPair = forcedWeaponDrafted;
                }
                else if (forcedUnarmed)
                {
                    state.ForcedUnarmed = true;
                }
                else if (forcedWeapon != null)
                {
                    chosenPair = forcedWeapon;
                }

                if (chosenPair != null)
                {
                    ThingDef def = pairThingField == null ? null : pairThingField.GetValue(chosenPair) as ThingDef;
                    ThingDef stuff = pairStuffField == null ? null : pairStuffField.GetValue(chosenPair) as ThingDef;
                    if (def != null)
                    {
                        state.ForcedWeaponDef = def;
                        state.ForcedWeaponStuff = stuff;
                        state.ForcedWeaponLabel = PairLabel(def, stuff);
                    }
                }

                state.VetoReason = state.ForcedUnarmed
                    ? "SS: forced unarmed"
                    : state.ForcedWeaponDef != null
                        ? "SS: forced " + state.ForcedWeaponLabel
                        : null;

                state.PrefersUnarmed = ReadBool(preferredUnarmedProperty, comp);
                if (state.PrefersUnarmed && state.VetoReason == null)
                {
                    state.VetoReason = "SS: prefers unarmed";
                }

                ResolvePrimaryMode(pawn, comp, state);

                // SS enforces the carried default ranged / preferred melee weapon when its
                // mode selects that role. Respect the pair instead of fighting it.
                if (!state.AutomationVeto && state.PreferredPrimaryIsRanged.HasValue)
                {
                    object preferredPair = state.PreferredPrimaryIsRanged.Value
                        ? ReadPair(defaultRangedProperty, comp)
                        : ReadPair(preferredMeleeProperty, comp);
                    if (preferredPair != null)
                    {
                        ThingDef def = pairThingField == null ? null : pairThingField.GetValue(preferredPair) as ThingDef;
                        ThingDef stuff = pairStuffField == null ? null : pairStuffField.GetValue(preferredPair) as ThingDef;
                        if (def != null && CarriesPair(pawn, def, stuff))
                        {
                            state.PreferredPairDef = def;
                            state.PreferredPairStuff = stuff;
                            state.PreferredPairLabel = PairLabel(def, stuff);
                            state.PreferredPairReason = state.PreferredPrimaryIsRanged.Value
                                ? "SS: default ranged " + state.PreferredPairLabel
                                : "SS: preferred melee " + state.PreferredPairLabel;
                        }
                    }
                }

                return state;
            }
            catch (Exception exception)
            {
                Disable("reading CompSidearmMemory failed", exception);
                return null;
            }
        }

        /// <summary>
        /// Wraps Simple Sidearms' own sidearm allowance check. Not used for primaries yet;
        /// milestone 4 uses it to validate sidearm candidates. Returns true when SS is absent
        /// or the check cannot be resolved.
        /// </summary>
        public static bool CanPickupSidearm(Pawn pawn, ThingWithComps weapon, out string reason)
        {
            reason = null;
            if (!Active || canPickupSidearmMethod == null || pawn == null || weapon == null)
            {
                return true;
            }

            object[] arguments = { weapon, pawn, null };
            try
            {
                bool result = (bool)canPickupSidearmMethod.Invoke(null, arguments);
                reason = arguments[2] as string;
                return result;
            }
            catch (Exception exception)
            {
                Disable("CanPickupSidearmInstance failed", exception);
                return true;
            }
        }

        private static void ResolvePrimaryMode(Pawn pawn, object comp, PawnState state)
        {
            if (primaryModeField == null)
            {
                return;
            }

            int mode = Convert.ToInt32(primaryModeField.GetValue(comp));
            switch (mode)
            {
                case 0: // Ranged
                    state.PrimaryModeLabel = "ranged";
                    state.PreferredPrimaryIsRanged = true;
                    break;
                case 1: // Melee
                    state.PrimaryModeLabel = "melee";
                    state.PreferredPrimaryIsRanged = false;
                    break;
                case 2: // BySkill: Simple Sidearms resolves this per pawn at runtime
                    int preference = ReadSkillPreference(pawn);
                    if (preference == 0)
                    {
                        state.PrimaryModeLabel = "by skill (ranged)";
                        state.PreferredPrimaryIsRanged = true;
                    }
                    else if (preference == 1)
                    {
                        state.PrimaryModeLabel = "by skill (melee)";
                        state.PreferredPrimaryIsRanged = false;
                    }
                    else
                    {
                        state.PrimaryModeLabel = "by skill";
                    }
                    break;
                case 3: // ByGenerated: no stable preference to respect
                    state.PrimaryModeLabel = "by generated";
                    break;
                default:
                    state.PrimaryModeLabel = "unknown";
                    break;
            }

            if (state.PreferredPrimaryIsRanged.HasValue)
            {
                state.CarriesPreferredPrimary = CarriesType(pawn, state.PreferredPrimaryIsRanged.Value);
            }
        }

        private static int ReadSkillPreference(Pawn pawn)
        {
            if (skillPreferenceMethod == null)
            {
                return -1;
            }
            try
            {
                return Convert.ToInt32(skillPreferenceMethod.Invoke(null, new object[] { pawn }));
            }
            catch (Exception exception)
            {
                Log.Warning("[Armory] Simple Sidearms skill preference check failed: " + exception.Message);
                return -1;
            }
        }

        private static bool CarriesType(Pawn pawn, bool ranged)
        {
            if (pawn == null)
            {
                return false;
            }
            if (pawn.equipment != null && pawn.equipment.Primary != null &&
                MatchesPrimaryType(pawn.equipment.Primary.def, ranged))
            {
                return true;
            }
            if (pawn.inventory != null && pawn.inventory.innerContainer != null)
            {
                for (int i = 0; i < pawn.inventory.innerContainer.Count; i++)
                {
                    ThingWithComps weapon = pawn.inventory.innerContainer[i] as ThingWithComps;
                    if (weapon != null && MatchesPrimaryType(weapon.def, ranged))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool Matches(ThingWithComps weapon, ThingDef def, ThingDef stuff)
        {
            return weapon.def == def && weapon.Stuff == stuff;
        }

        private static bool CarriesPair(Pawn pawn, ThingDef def, ThingDef stuff)
        {
            if (pawn == null)
            {
                return false;
            }
            if (pawn.equipment != null && pawn.equipment.Primary != null &&
                Matches(pawn.equipment.Primary, def, stuff))
            {
                return true;
            }
            if (pawn.inventory != null && pawn.inventory.innerContainer != null)
            {
                for (int i = 0; i < pawn.inventory.innerContainer.Count; i++)
                {
                    ThingWithComps weapon = pawn.inventory.innerContainer[i] as ThingWithComps;
                    if (weapon != null && Matches(weapon, def, stuff))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>True when the weapon counts as Simple Sidearms' preferred primary type.</summary>
        public static bool MatchesPrimaryType(ThingDef def, bool ranged)
        {
            if (def == null)
            {
                return false;
            }
            if (ranged)
            {
                return def.IsRangedWeapon;
            }
            // Simple Sidearms counts ranged weapons with a melee tool as melee-capable.
            return def.IsMeleeWeapon || def.IsWeapon && def.tools != null && def.tools.Count > 0;
        }

        private static string PairLabel(ThingDef def, ThingDef stuff)
        {
            string label = stuff != null ? stuff.LabelAsStuff + " " + def.label : def.label;
            return GenText.CapitalizeFirst(label);
        }

        private static bool ReadBool(PropertyInfo property, object instance)
        {
            return property != null && (bool)property.GetValue(instance, null);
        }

        private static object ReadPair(PropertyInfo property, object instance)
        {
            return property == null ? null : property.GetValue(instance, null);
        }

        private static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            if (!IsModActive())
            {
                return;
            }

            try
            {
                memoryType = FindType(MemoryTypeName);
                if (memoryType == null)
                {
                    Log.Warning("[Armory] Simple Sidearms is loaded but " + MemoryTypeName +
                                " was not found; coexistence is disabled.");
                    return;
                }

                getMemoryMethod = memoryType.GetMethod(
                    "GetMemoryCompForPawn",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Pawn), typeof(bool) },
                    null);
                forcedUnarmedProperty = memoryType.GetProperty("ForcedUnarmed");
                forcedWeaponProperty = memoryType.GetProperty("ForcedWeapon");
                forcedUnarmedDraftedProperty = memoryType.GetProperty("ForcedUnarmedWhileDrafted");
                forcedWeaponDraftedProperty = memoryType.GetProperty("ForcedWeaponWhileDrafted");
                preferredUnarmedProperty = memoryType.GetProperty("PreferredUnarmed");
                defaultRangedProperty = memoryType.GetProperty("DefaultRangedWeapon");
                preferredMeleeProperty = memoryType.GetProperty("PreferredMeleeWeapon");
                primaryModeField = memoryType.GetField("primaryWeaponMode");

                Type nullablePair = forcedWeaponProperty == null ? null : forcedWeaponProperty.PropertyType;
                Type pairType = nullablePair == null ? null : Nullable.GetUnderlyingType(nullablePair) ?? nullablePair;
                if (pairType != null)
                {
                    pairThingField = pairType.GetField("thing");
                    pairStuffField = pairType.GetField("stuff");
                }

                statCalculatorType = FindType(StatCalculatorTypeName);
                if (statCalculatorType != null)
                {
                    canPickupSidearmMethod = statCalculatorType.GetMethod(
                        "CanPickupSidearmInstance",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(ThingWithComps), typeof(Pawn), typeof(string).MakeByRefType() },
                        null);
                }

                extensionsType = FindType(ExtensionsTypeName);
                if (extensionsType != null)
                {
                    skillPreferenceMethod = extensionsType.GetMethod(
                        "getSkillWeaponPreference",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(Pawn) },
                        null);
                }

                bool usable = getMemoryMethod != null && forcedUnarmedProperty != null &&
                              forcedWeaponProperty != null && forcedUnarmedDraftedProperty != null &&
                              forcedWeaponDraftedProperty != null && pairThingField != null &&
                              pairStuffField != null;
                if (!usable)
                {
                    Log.Warning("[Armory] Simple Sidearms was found, but its memory API does not match " +
                                "this version; coexistence is disabled.");
                    return;
                }

                active = true;
                if (ArmoryMod.Settings != null && ArmoryMod.Settings.debugLogging)
                {
                    Log.Message("[Armory] Simple Sidearms integration active (reflection; " +
                                "forced states, preferences, and primary mode respected).");
                }
            }
            catch (Exception exception)
            {
                Disable("initialization failed", exception);
            }
        }

        private static bool IsModActive()
        {
            List<ModContentPack> mods = LoadedModManager.RunningModsListForReading;
            if (mods == null)
            {
                return false;
            }
            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i] != null &&
                    string.Equals(mods[i].PackageId, PackageId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static Type FindType(string fullName)
        {
            Type type = Type.GetType(fullName + ", SimpleSidearms", false);
            if (type != null)
            {
                return type;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    type = assemblies[i].GetType(fullName, false);
                }
                catch
                {
                    type = null;
                }
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        private static void Disable(string context, Exception exception)
        {
            if (failed)
            {
                return;
            }
            failed = true;
            active = false;
            Log.Warning("[Armory] Simple Sidearms integration disabled: " + context + ": " +
                        exception.GetType().Name + ": " + exception.Message);
        }
    }
}
