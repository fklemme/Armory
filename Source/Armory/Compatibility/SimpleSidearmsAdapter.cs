using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

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
        private const string WeaponAssignmentTypeName = "PeteTimesSix.SimpleSidearms.Utilities.WeaponAssingment";
        private const string GettersFiltersTypeName = "PeteTimesSix.SimpleSidearms.Utilities.GettersFilters";
        private const string ExtensionsTypeName = "PeteTimesSix.SimpleSidearms.Extensions";
        private const string PairTypeName = "SimpleSidearms.rimworld.ThingDefStuffDefPair";
        private const string SimpleSidearmsTypeName = "PeteTimesSix.SimpleSidearms.SimpleSidearms";

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

            /// <summary>"always ranged", "always melee", "by best skill (ranged)", "chosen when generated", ...</summary>
            public string PrimaryModeLabel = "unknown";

            /// <summary>Effective preferred primary type. Null when Simple Sidearms has no opinion (chosen when generated).</summary>
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
                string text = "primary mode: " + PrimaryModeLabel;
                if (AutomationVeto)
                {
                    text += " | " + VetoReason;
                }
                else if (PreferredPairLabel != null)
                {
                    text += " | " + PreferredPairReason;
                }
                return text;
            }
        }

        private static bool initialized;
        private static bool active;
        private static bool failed;
        private static bool preferredPrimaryCheckFailed;

        private static Type memoryType;
        private static Type statCalculatorType;
        private static Type extensionsType;

        private static MethodInfo getMemoryMethod;
        private static MethodInfo canPickupSidearmMethod;
        private static MethodInfo canUseSidearmMethod;
        private static MethodInfo dropSidearmMethod;
        private static MethodInfo equipFromInventoryMethod;
        private static MethodInfo findBestRangedMethod;
        private static MethodInfo findBestMeleeMethod;
        private static MethodInfo isValidSidearmMethod;
        private static ConstructorInfo pairConstructor;
        private static FieldInfo tupleItem1Field;
        private static MethodInfo skillPreferenceMethod;
        private static PropertyInfo settingsProperty;
        private static FieldInfo skipDangerousField;
        private static FieldInfo separateModesField;
        private static FieldInfo singleModeField;
        private static FieldInfo singleMeleeModeField;
        private static FieldInfo singleRangedModeField;
        private static FieldInfo singleRelativeField;
        private static FieldInfo singleMeleeRelativeField;
        private static FieldInfo singleRangedRelativeField;

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
        private static FieldInfo rememberedWeaponsField;

        public static bool Active
        {
            get
            {
                EnsureInitialized();
                return active;
            }
        }

        /// <summary>True when SS's sidearm allowance method resolved, so sidearm jobs can be validated.</summary>
        public static bool CanCheckSidearms
        {
            get
            {
                EnsureInitialized();
                return active && canPickupSidearmMethod != null;
            }
        }

        /// <summary>True when SS's own sidearm drop method resolved, so replacement can unmemorise cleanly.</summary>
        public static bool CanDropSidearms
        {
            get
            {
                EnsureInitialized();
                return active && dropSidearmMethod != null;
            }
        }

        /// <summary>
        /// True when SS's own "equip this carried weapon" helper resolved. Armory uses it only
        /// to put a planned hand weapon back after a sidearm was left in hand outside combat.
        /// </summary>
        public static bool CanEquipFromInventory
        {
            get
            {
                EnsureInitialized();
                return active && equipFromInventoryMethod != null;
            }
        }

        /// <summary>True when SS's single-weapon sidearm rule resolved.</summary>
        public static bool CanCheckSidearmType
        {
            get
            {
                EnsureInitialized();
                return active && isValidSidearmMethod != null;
            }
        }

        /// <summary>True when SS's per-instance sidearm usability rule resolved.</summary>
        public static bool CanCheckSidearmUse
        {
            get
            {
                EnsureInitialized();
                return active && canUseSidearmMethod != null;
            }
        }

        /// <summary>
        /// True when SS's relative-weight limit fields resolved. SS's `isValidSidearm`
        /// helper does not implement RelativeWeight, so Armory must check it separately.
        /// </summary>
        public static bool CanCheckRelativeLimits
        {
            get
            {
                EnsureInitialized();
                return active && settingsProperty != null && separateModesField != null &&
                       singleModeField != null && singleMeleeModeField != null &&
                       singleRangedModeField != null && singleRelativeField != null &&
                       singleMeleeRelativeField != null && singleRangedRelativeField != null;
            }
        }

        /// <summary>True after the SS carried-primary reflection call failed once.</summary>
        public static bool PreferredPrimaryCheckFailed
        {
            get
            {
                EnsureInitialized();
                return preferredPrimaryCheckFailed;
            }
        }

        /// <summary>Player setting; defaults to on so Armory never silently fights Simple Sidearms.</summary>
        public static bool RespectEnabled
        {
            get { return ArmoryMod.Settings == null || ArmoryMod.Settings.respectSimpleSidearms; }
        }

        /// <summary>
        /// True while Simple Sidearms is equipping or re-equipping a sidearm for this pawn,
        /// including a sidearm job waiting in the pawn's job queue. Queued jobs matter:
        /// TryTakeOrderedJob with requestQueueing only appends, so a resting pawn can
        /// otherwise accumulate several identical pickup jobs and execute them all later.
        /// </summary>
        public static bool IsSidearmJobRunning(Pawn pawn)
        {
            if (pawn == null || pawn.jobs == null)
            {
                return false;
            }
            if (IsSidearmJob(pawn.jobs.curJob))
            {
                return true;
            }
            if (pawn.jobs.jobQueue != null)
            {
                for (int i = 0; i < pawn.jobs.jobQueue.Count; i++)
                {
                    QueuedJob queued = pawn.jobs.jobQueue[i];
                    if (queued != null && IsSidearmJob(queued.job))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsSidearmJob(Job job)
        {
            if (job == null || job.def == null)
            {
                return false;
            }
            string defName = job.def.defName;
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
                reason = state.VetoReason ?? "Forced unarmed";
                return true;
            }

            if (state.ForcedWeaponDef != null)
            {
                if (!Matches(weapon, state.ForcedWeaponDef, state.ForcedWeaponStuff))
                {
                    reason = "Forced weapon: " + state.ForcedWeaponLabel;
                    return true;
                }
                return false;
            }

            if (state.PrefersUnarmed)
            {
                reason = state.VetoReason ?? "Prefers unarmed";
                return true;
            }

            // DefaultRangedWeapon / PreferredMeleeWeapon are deliberately NOT hard blockers.
            // SS only enforces them among carried weapons; Armory already includes the best
            // carried weapon of the preferred type in its upgrade baseline, so a loose weapon
            // must still beat it. Hard-blocking here locked every pawn to their default and
            // stopped all upgrades.

            if (state.PreferredPrimaryIsRanged.HasValue && state.CarriesPreferredPrimary &&
                !MatchesPrimaryType(weapon.def, state.PreferredPrimaryIsRanged.Value))
            {
                reason = state.PreferredPrimaryIsRanged.Value
                    ? "Requires a ranged hand"
                    : "Requires a melee hand";
                return true;
            }

            return false;
        }

        /// <summary>
        /// True when Simple Sidearms has this carried weapon in its remembered list.
        /// Armory uses this only to avoid restoring a hand SS treats as temporary and
        /// would immediately replace again. Missing SS or a reflection failure returns true.
        /// </summary>
        public static bool IsRemembered(Pawn pawn, ThingWithComps weapon)
        {
            if (!Active || weapon == null || weapon.def == null)
            {
                return true;
            }
            object comp = GetMemoryComp(pawn);
            if (comp == null || rememberedWeaponsField == null || pairThingField == null)
            {
                return true;
            }
            try
            {
                IEnumerable memories = rememberedWeaponsField.GetValue(comp) as IEnumerable;
                if (memories == null)
                {
                    return true;
                }
                for (IEnumerator enumerator = memories.GetEnumerator(); enumerator.MoveNext();)
                {
                    object entry = enumerator.Current;
                    if (entry == null)
                    {
                        continue;
                    }
                    ThingDef def = pairThingField.GetValue(entry) as ThingDef;
                    if (def != weapon.def)
                    {
                        continue;
                    }
                    ThingDef stuff = pairStuffField == null
                        ? null
                        : pairStuffField.GetValue(entry) as ThingDef;
                    if (stuff == weapon.Stuff)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception exception)
            {
                Disable("reading rememberedWeapons failed", exception);
                return true;
            }
        }

        private static object GetMemoryComp(Pawn pawn)
        {
            if (!Active || failed || pawn == null || pawn.RaceProps == null ||
                !pawn.RaceProps.Humanlike || getMemoryMethod == null)
            {
                return null;
            }
            try
            {
                return getMemoryMethod.Invoke(null, new object[] { pawn, true });
            }
            catch (Exception exception)
            {
                Disable("GetMemoryCompForPawn failed", exception);
                return null;
            }
        }

        /// <summary>Reads the pawn's Simple Sidearms memory. Returns null when the comp is absent.</summary>
        public static PawnState GetState(Pawn pawn)
        {
            object comp = GetMemoryComp(pawn);
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
                    ? "Forced unarmed"
                    : state.ForcedWeaponDef != null
                        ? "Forced weapon: " + state.ForcedWeaponLabel
                        : null;

                state.PrefersUnarmed = ReadBool(preferredUnarmedProperty, comp);
                if (state.PrefersUnarmed && state.VetoReason == null)
                {
                    state.VetoReason = "Prefers unarmed";
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
                                ? "Default ranged weapon: " + state.PreferredPairLabel + " (carried)"
                                : "Preferred melee weapon: " + state.PreferredPairLabel + " (carried)";
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

        /// <summary>
        /// Rewrites Simple Sidearms' terse pickup failures into Armory's own wording.
        /// SS returns localized text, so the same translation keys are resolved through
        /// <see cref="Translator"/> and compared; anything unrecognized passes through.
        /// </summary>
        public static string ExplainPickupFailure(Pawn pawn, ThingWithComps weapon, string ssReason)
        {
            if (string.IsNullOrEmpty(ssReason))
            {
                return "Simple Sidearms will not allow this sidearm";
            }

            if (MatchesTranslation(ssReason, "SidearmPickupFail_NoFreeSpace"))
            {
                if (pawn == null || weapon == null || weapon.def == null)
                {
                    return "No free carrying capacity";
                }
                // Match SS's own numbers: it compares the def's abstract mass (stuff is
                // ignored) with the pawn's free inventory capacity.
                float mass = weapon.def.GetStatValueAbstract(StatDefOf.Mass);
                float free = MassUtility.FreeSpace(pawn);
                return string.Format("Can't carry ({0:0.##} kg, {1:0.##} kg free)", mass, free);
            }

            if (MatchesTranslation(ssReason, "SidearmPickupFail_AllSlotsFull"))
            {
                return "All sidearm slots are full";
            }
            if (MatchesTranslation(ssReason, "SidearmPickupFail_MeleeSlotsFull"))
            {
                return "All melee sidearm slots are full";
            }
            if (MatchesTranslation(ssReason, "SidearmPickupFail_RangedSlotsFull"))
            {
                return "All ranged sidearm slots are full";
            }
            if (MatchesTranslation(ssReason, "BladelinkBondedToSomeoneElse"))
            {
                CompBladelinkWeapon blade = weapon == null ? null : weapon.GetComp<CompBladelinkWeapon>();
                if (blade != null && blade.Biocodable && blade.CodedPawn == null)
                {
                    // Simple Sidearms compares CodedPawn (null) with the pawn and reports
                    // "bonded to someone else" for an unbonded persona weapon.
                    return "Persona: bond manually";
                }
                return blade != null && !string.IsNullOrEmpty(blade.CodedPawnLabel)
                    ? "Bonded to " + blade.CodedPawnLabel
                    : "Bonded to someone else";
            }
            return ssReason;
        }

        private static bool MatchesTranslation(string text, string key)
        {
            string translated = Translator.Translate(key);
            return !string.IsNullOrEmpty(translated) &&
                   string.Equals(text, translated, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// SS's per-instance usability rule (biocoding, bonds, ideology role), without the
        /// slot/mass limits. Used to rule out drop-first upgrades that would still be denied.
        /// Returns true when the check is unavailable.
        /// </summary>
        public static bool CanUseSidearm(Pawn pawn, ThingWithComps weapon, out string reason)        {
            reason = null;
            EnsureInitialized();
            if (!active || canUseSidearmMethod == null || pawn == null || weapon == null)
            {
                return true;
            }
            try
            {
                object[] arguments = { weapon, pawn, null };
                bool result = (bool)canUseSidearmMethod.Invoke(null, arguments);
                reason = arguments[2] as string;
                return result;
            }
            catch (Exception exception)
            {
                Log.Warning("[Armory] Simple Sidearms sidearm usability check failed: " +
                            exception.GetType().Name + ": " + exception.Message);
                return true;
            }
        }

        /// <summary>
        /// Full single-weapon pre-check for a drop-first upgrade. SS's `isValidSidearm`
        /// covers absolute-weight and selection limits but **not** RelativeWeight, so this
        /// adds the relative mass check from `CanPickupSidearmType`. Returns true when the
        /// check is unavailable; callers that are about to drop a weapon must also require
        /// <see cref="CanCheckRelativeLimits"/>.
        /// </summary>
        public static bool IsValidSidearmForPawn(Pawn pawn, ThingWithComps weapon, out string reason)
        {
            reason = null;
            if (!IsValidSidearmType(weapon, out reason))
            {
                return false;
            }
            if (pawn == null || weapon == null || !CanCheckRelativeLimits)
            {
                return true;
            }

            if ((pawn.CombinedDisabledWorkTags & WorkTags.Violent) != 0 &&
                !IsTool(weapon.def))
            {
                reason = "Pacifist: only tools allowed";
                return false;
            }

            try
            {
                object settings = settingsProperty.GetValue(null, null);
                if (settings == null)
                {
                    return true;
                }

                bool separate = (bool)separateModesField.GetValue(settings);
                bool melee = weapon.def.IsMeleeWeapon;
                FieldInfo modeField = separate
                    ? (melee ? singleMeleeModeField : singleRangedModeField)
                    : singleModeField;
                FieldInfo relativeField = separate
                    ? (melee ? singleMeleeRelativeField : singleRangedRelativeField)
                    : singleRelativeField;

                int mode = Convert.ToInt32(modeField.GetValue(settings));
                if (mode != 1) // LimitModeSingleSidearm.RelativeWeight
                {
                    return true;
                }

                float relative = Convert.ToSingle(relativeField.GetValue(settings));
                float mass = weapon.GetStatValue(StatDefOf.Mass, true);
                float capacity = MassUtility.Capacity(pawn, null);
                if (mass >= relative * capacity)
                {
                    reason = "Too heavy for a sidearm" + (separate && melee ? " (melee)" : string.Empty);
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("[Armory] Simple Sidearms relative-sidearm check failed: " +
                            exception.GetType().Name + ": " + exception.Message);
                return true;
            }
        }

        private static bool IsTool(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }
            if (def.defName == "Gun_Fire_Ext" || def.defName == "VWE_Gun_FireExtinguisher")
            {
                return true;
            }
            return def.equippedStatOffsets != null && def.equippedStatOffsets.Count > 0;
        }

        /// <summary>
        /// Simple Sidearms' single-weapon sidearm rule: per-type mass, selection, and
        /// pacifist-tool limits, without the current slot count or free carrying capacity.
        /// Used to decide whether dropping an existing backup can make room for this weapon.
        /// Returns true when the check is unavailable.
        /// </summary>
        public static bool IsValidSidearmType(ThingWithComps weapon, out string reason)
        {
            reason = null;
            EnsureInitialized();
            if (!active || isValidSidearmMethod == null || pairConstructor == null ||
                weapon == null || weapon.def == null)
            {
                return true;
            }
            try
            {
                object pair = pairConstructor.Invoke(new object[] { weapon.def, weapon.Stuff });
                object[] arguments = { pair, null };
                bool result = (bool)isValidSidearmMethod.Invoke(null, arguments);
                reason = arguments[1] as string;
                return result;
            }
            catch (Exception exception)
            {
                Log.Warning("[Armory] Simple Sidearms sidearm-type check failed: " +
                            exception.GetType().Name + ": " + exception.Message);
                return true;
            }
        }

        /// <summary>
        /// Uses Simple Sidearms' own sidearm drop so the weapon is dropped and forgotten. Returns
        /// false when the method is unavailable or fails; callers should then leave the sidearm
        /// alone rather than risk SS retrieving it again.
        /// </summary>
        public static bool DropSidearm(Pawn pawn, ThingWithComps weapon, bool intentionalDrop, bool unmemorise)
        {
            if (!CanDropSidearms || pawn == null || weapon == null)
            {
                return false;
            }
            try
            {
                dropSidearmMethod.Invoke(null, new object[] { pawn, weapon, intentionalDrop, unmemorise });
                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("[Armory] Simple Sidearms DropSidearm failed: " +
                            exception.GetType().Name + ": " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// Equips a weapon that is already in the pawn's inventory through Simple Sidearms'
        /// own swap helper. The current weapon goes to the inventory, so this does not drop
        /// anything. Used only to restore the planned hand after SS left a sidearm in hand
        /// outside combat.
        /// </summary>
        public static bool EquipFromInventory(Pawn pawn, ThingWithComps weapon)
        {
            if (!CanEquipFromInventory || pawn == null || weapon == null ||
                pawn.equipment == null || pawn.equipment.Primary == weapon)
            {
                return false;
            }
            try
            {
                return (bool)equipFromInventoryMethod.Invoke(null,
                    new object[] { pawn, weapon, false, false });
            }
            catch (Exception exception)
            {
                Log.Warning("[Armory] Simple Sidearms equipping a carried hand weapon failed: " +
                            exception.GetType().Name + ": " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// Asks Simple Sidearms which carried weapon it would use as the primary for this
        /// pawn (QuickSwitch filters: manual use, dangerous, EMP, blocked weapons). This is
        /// the baseline Armory must beat; do not substitute "best carried by Armory score".
        /// Returns null when SS would not switch or the call is unavailable.
        /// </summary>
        public static ThingWithComps GetPreferredCarriedPrimary(Pawn pawn, bool ranged)
        {
            if (!Active || pawn == null || preferredPrimaryCheckFailed)
            {
                return null;
            }
            try
            {
                if (ranged)
                {
                    if (findBestRangedMethod == null || tupleItem1Field == null)
                    {
                        return null;
                    }
                    object result = findBestRangedMethod.Invoke(null,
                        new object[] { pawn, null, true, ShouldSkipDangerous(), true, true });
                    return tupleItem1Field.GetValue(result) as ThingWithComps;
                }

                if (findBestMeleeMethod == null)
                {
                    return null;
                }
                // SS's idle melee choice passes includeRangedWithBash: false.
                object[] arguments = { pawn, null, true, false, null };
                bool found = (bool)findBestMeleeMethod.Invoke(null, arguments);
                return found ? arguments[1] as ThingWithComps : null;
            }
            catch (Exception exception)
            {
                preferredPrimaryCheckFailed = true;
                Log.Warning("[Armory] Simple Sidearms carried-primary check failed; using the current " +
                            "primary as the baseline. " + exception.GetType().Name + ": " + exception.Message);
                return null;
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
                    state.PrimaryModeLabel = "always ranged";
                    state.PreferredPrimaryIsRanged = true;
                    break;
                case 1: // Melee
                    state.PrimaryModeLabel = "always melee";
                    state.PreferredPrimaryIsRanged = false;
                    break;
                case 2: // BySkill: Simple Sidearms resolves this per pawn at runtime
                    int preference = ReadSkillPreference(pawn);
                    if (preference == 0)
                    {
                        state.PrimaryModeLabel = "by best skill (ranged)";
                        state.PreferredPrimaryIsRanged = true;
                    }
                    else if (preference == 1)
                    {
                        state.PrimaryModeLabel = "by best skill (melee)";
                        state.PreferredPrimaryIsRanged = false;
                    }
                    else
                    {
                        state.PrimaryModeLabel = "by best skill";
                    }
                    break;
                case 3: // ByGenerated: no stable preference to respect
                    state.PrimaryModeLabel = "chosen when generated";
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

        /// <summary>
        /// SS's own "skip dangerous weapons" setting. The idle QuickSwitch uses it for
        /// player-controlled colonists, so Armory's hand baseline must use the same rule.
        /// </summary>
        private static bool ShouldSkipDangerous()
        {
            if (settingsProperty == null || skipDangerousField == null)
            {
                return true;
            }
            try
            {
                object settings = settingsProperty.GetValue(null, null);
                return settings == null || (bool)skipDangerousField.GetValue(settings);
            }
            catch
            {
                return true;
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
                rememberedWeaponsField = memoryType.GetField("rememberedWeapons");

                statCalculatorType = FindType(StatCalculatorTypeName);
                if (statCalculatorType != null)
                {
                    canPickupSidearmMethod = statCalculatorType.GetMethod(
                        "CanPickupSidearmInstance",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(ThingWithComps), typeof(Pawn), typeof(string).MakeByRefType() },
                        null);
                    canUseSidearmMethod = statCalculatorType.GetMethod(
                        "canUseSidearmInstance",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(ThingWithComps), typeof(Pawn), typeof(string).MakeByRefType() },
                        null);
                }

                Type sidearmPairType = FindType(PairTypeName);
                if (sidearmPairType != null)
                {
                    pairConstructor = sidearmPairType.GetConstructor(new[] { typeof(ThingDef), typeof(ThingDef) });
                    if (statCalculatorType != null && pairConstructor != null)
                    {
                        isValidSidearmMethod = statCalculatorType.GetMethod(
                            "isValidSidearm",
                            BindingFlags.Public | BindingFlags.Static,
                            null,
                            new[] { sidearmPairType, typeof(string).MakeByRefType() },
                            null);
                    }
                }

                Type weaponAssignmentType = FindType(WeaponAssignmentTypeName);
                if (weaponAssignmentType != null)
                {
                    dropSidearmMethod = weaponAssignmentType.GetMethod(
                        "DropSidearm",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(Pawn), typeof(ThingWithComps), typeof(bool), typeof(bool) },
                        null);
                    equipFromInventoryMethod = weaponAssignmentType.GetMethod(
                        "equipSpecificWeaponFromInventory",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(Pawn), typeof(ThingWithComps), typeof(bool), typeof(bool) },
                        null);
                }

                Type gettersType = FindType(GettersFiltersTypeName);
                if (gettersType != null)
                {
                    findBestRangedMethod = gettersType.GetMethod(
                        "findBestRangedWeapon",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[]
                        {
                            typeof(Pawn), typeof(LocalTargetInfo?), typeof(bool), typeof(bool),
                            typeof(bool), typeof(bool)
                        },
                        null);
                    findBestMeleeMethod = gettersType.GetMethod(
                        "findBestMeleeWeapon",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[]
                        {
                            typeof(Pawn), typeof(ThingWithComps).MakeByRefType(), typeof(bool),
                            typeof(bool), typeof(Pawn)
                        },
                        null);
                    if (findBestRangedMethod != null)
                    {
                        tupleItem1Field = findBestRangedMethod.ReturnType.GetField("Item1");
                    }
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

                Type modType = FindType(SimpleSidearmsTypeName);
                if (modType != null)
                {
                    settingsProperty = modType.GetProperty("Settings",
                        BindingFlags.Public | BindingFlags.Static);
                    if (settingsProperty != null)
                    {
                        Type settingsType = settingsProperty.PropertyType;
                        skipDangerousField = settingsType.GetField(
                            "SkipDangerousWeapons",
                            BindingFlags.Public | BindingFlags.Instance);
                        separateModesField = settingsType.GetField(
                            "SeparateModes",
                            BindingFlags.Public | BindingFlags.Instance);
                        singleModeField = settingsType.GetField(
                            "LimitModeSingle",
                            BindingFlags.Public | BindingFlags.Instance);
                        singleMeleeModeField = settingsType.GetField(
                            "LimitModeSingleMelee",
                            BindingFlags.Public | BindingFlags.Instance);
                        singleRangedModeField = settingsType.GetField(
                            "LimitModeSingleRanged",
                            BindingFlags.Public | BindingFlags.Instance);
                        singleRelativeField = settingsType.GetField(
                            "LimitModeSingle_RelativeMass",
                            BindingFlags.Public | BindingFlags.Instance);
                        singleMeleeRelativeField = settingsType.GetField(
                            "LimitModeSingleMelee_RelativeMass",
                            BindingFlags.Public | BindingFlags.Instance);
                        singleRangedRelativeField = settingsType.GetField(
                            "LimitModeSingleRanged_RelativeMass",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
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
