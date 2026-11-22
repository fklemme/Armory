using System;
using System.Collections.Generic;
using Armory.Compatibility;
using RimWorld;
using UnityEngine;
using Verse;

namespace Armory
{
    /// <summary>Builds one immutable snapshot of pawns, weapons, and demand for a map.</summary>
    public static class ArmorySnapshotBuilder
    {
        public static ArmorySnapshot Build(Map map)
        {
            ArmorySnapshot snapshot = new ArmorySnapshot
            {
                Map = map,
                BuiltTick = Find.TickManager.TicksGame
            };

            Dictionary<int, float> powerCache = new Dictionary<int, float>();

            List<Thing> things = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon);
            for (int i = 0; i < things.Count; i++)
            {
                ThingWithComps weapon = things[i] as ThingWithComps;
                if (!IsRealWeapon(weapon))
                {
                    continue;
                }
                if (weapon.holdingOwner != null && weapon.holdingOwner.Owner is Pawn)
                {
                    continue;
                }
                if (ThingOwnerUtility.IsEnclosingContainer(weapon.ParentHolder))
                {
                    continue;
                }
                snapshot.Weapons.Add(new WeaponInfo
                {
                    Thing = weapon,
                    Label = weapon.LabelCap,
                    IsRanged = weapon.def.IsRangedWeapon,
                    Power = WeaponPower.Get(weapon, powerCache),
                    Range = WeaponPower.GetRange(weapon),
                    MarketValue = weapon.MarketValue,
                    Comparison = WeaponPower.GetComparison(weapon)
                });
            }
            snapshot.Weapons.Sort((a, b) => b.Power.CompareTo(a.Power));

            Dictionary<WeaponInfo, WeaponAnalysis> rows = new Dictionary<WeaponInfo, WeaponAnalysis>();
            for (int i = 0; i < snapshot.Weapons.Count; i++)
            {
                WeaponInfo info = snapshot.Weapons[i];
                rows[info] = new WeaponAnalysis
                {
                    Weapon = info,
                    Status = "No colonist can use it"
                };
            }

            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                PawnAnalysis analysis = AnalyzePawn(map, pawn, snapshot.Weapons, powerCache);
                snapshot.Pawns.Add(analysis);

                if (!analysis.Available)
                {
                    continue;
                }

                for (int i = 0; i < analysis.Ranked.Count; i++)
                {
                    Candidate candidate = analysis.Ranked[i];
                    WeaponAnalysis row = rows[candidate.Weapon];
                    row.PawnCandidates.Add(candidate);

                    if (candidate.Score > row.BestAnyHandScore)
                    {
                        row.BestAnyHandScore = candidate.Score;
                        row.BestAnyHandPawn = analysis;
                    }
                    if (candidate.Ready && candidate.Score > row.BestHandScore)
                    {
                        row.BestHandScore = candidate.Score;
                        row.BestHandPawn = analysis;
                    }
                }

                for (int i = 0; i < analysis.SidearmCandidates.Count; i++)
                {
                    SidearmCandidate sidearm = analysis.SidearmCandidates[i];
                    if (sidearm.AlreadyCarried)
                    {
                        // A duplicate of a weapon the pawn already carries is not a real
                        // candidate, so it should not make a loose weapon look "wanted".
                        continue;
                    }
                    WeaponAnalysis row = rows[sidearm.Weapon];

                    if (sidearm.Score > row.BestAnyBackupScore)
                    {
                        row.BestAnyBackupScore = sidearm.Score;
                        row.BestAnyBackupPawn = analysis;
                    }
                    if (sidearm.Ready && sidearm.Score > row.BestBackupScore)
                    {
                        row.BestBackupScore = sidearm.Score;
                        row.BestBackupPawn = analysis;
                    }
                }

                for (int i = 0; i < analysis.SidearmTwoCandidates.Count; i++)
                {
                    SidearmCandidate sidearm = analysis.SidearmTwoCandidates[i];
                    if (sidearm.AlreadyCarried || sidearm.SlotScore <= 0.001f)
                    {
                        continue;
                    }
                    WeaponAnalysis row = rows[sidearm.Weapon];
                    if (sidearm.SlotScore > row.BestAnySidearmTwoScore)
                    {
                        row.BestAnySidearmTwoScore = sidearm.SlotScore;
                        row.BestAnySidearmTwoPawn = analysis;
                    }
                    if (sidearm.Ready && sidearm.SlotScore > row.BestSidearmTwoScore)
                    {
                        row.BestSidearmTwoScore = sidearm.SlotScore;
                        row.BestSidearmTwoPawn = analysis;
                    }
                }
            }

            snapshot.Pawns.Sort(CompareColonistOrder);

            for (int i = 0; i < snapshot.Weapons.Count; i++)
            {
                WeaponAnalysis row = rows[snapshot.Weapons[i]];
                row.PawnCandidates.Sort((a, b) => b.Score.CompareTo(a.Score));
                if (row.BestHandPawn != null)
                {
                    row.Status = "Hand";
                }
                else if (row.BestBackupPawn != null)
                {
                    row.Status = "Sidearm 1";
                }
                else if (row.BestSidearmTwoPawn != null)
                {
                    row.Status = "Sidearm 2";
                }
                else if (row.BestAnyHandPawn != null)
                {
                    row.Status = "Hand (blocked)";
                }
                else if (row.BestAnyBackupPawn != null)
                {
                    row.Status = "Sidearm 1 (blocked)";
                }
                else if (row.BestAnySidearmTwoPawn != null)
                {
                    row.Status = "Sidearm 2 (blocked)";
                }
                else
                {
                    row.Status = "No colonist can use it";
                }
                snapshot.WeaponRows.Add(row);
            }

            BuildDemand(snapshot);
            return snapshot;
        }

        /// <summary>
        /// Wood and other resources inherit ResourceVerbBase and therefore count as weapons to
        /// the vanilla thing group. Keep only actual weapons, not stuffable resources.
        /// </summary>
        private static bool IsRealWeapon(ThingWithComps weapon)
        {
            return weapon != null && weapon.Spawned && WeaponFilter.IsWeaponDef(weapon.def);
        }

        private static string DescribeUnavailable(Pawn pawn, Map map)
        {
            if (pawn == null || pawn.Spawned)
            {
                return null;
            }

            if (pawn.CarriedBy != null)
            {
                return "Carried by " + pawn.CarriedBy.LabelShort;
            }

            // A pod stores its occupant in a ThingOwner owned by a ThingComp, so walk
            // ThingOwner -> comp -> parent building instead of only checking ParentHolder.
            IThingHolder holder = pawn.ParentHolder;
            for (int depth = 0; depth < 8 && holder != null; depth++)
            {
                Building building = holder as Building;
                if (building != null)
                {
                    return DescribeBuilding(building);
                }

                ThingComp comp = holder as ThingComp;
                if (comp != null)
                {
                    holder = comp.parent as IThingHolder;
                    continue;
                }

                ThingOwner owner = holder as ThingOwner;
                if (owner != null)
                {
                    holder = owner.Owner;
                    continue;
                }

                break;
            }

            // Some despawned pawns have no usable ParentHolder. Search player buildings
            // and their comps for the pawn so pod/casket labels still work.
            if (map != null)
            {
                List<Building> buildings = map.listerBuildings.allBuildingsColonist;
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building building = buildings[i];
                    if (building == null)
                    {
                        continue;
                    }

                    IThingHolder buildingHolder = building as IThingHolder;
                    if (buildingHolder != null && Holds(buildingHolder.GetDirectlyHeldThings(), pawn))
                    {
                        return DescribeBuilding(building);
                    }

                    List<ThingComp> comps = building.AllComps;
                    for (int j = 0; j < comps.Count; j++)
                    {
                        IThingHolder compHolder = comps[j] as IThingHolder;
                        if (compHolder != null && Holds(compHolder.GetDirectlyHeldThings(), pawn))
                        {
                            return DescribeBuilding(building);
                        }
                    }
                }
            }

            return "Not on the map";
        }

        private static bool Holds(ThingOwner owner, Thing thing)
        {
            if (owner == null)
            {
                return false;
            }
            for (int i = 0; i < owner.Count; i++)
            {
                if (owner[i] == thing)
                {
                    return true;
                }
            }
            return false;
        }

        private static string DescribeBuilding(Building building)
        {
            string label = building != null && building.def != null ? building.def.label : null;
            return string.IsNullOrEmpty(label) ? "In a building" : "In a " + label;
        }

        private static PawnAnalysis AnalyzePawn(Map map, Pawn pawn, List<WeaponInfo> weapons, Dictionary<int, float> powerCache)
        {
            PawnAnalysis analysis = new PawnAnalysis
            {
                Pawn = pawn,
                Current = pawn.equipment == null ? null : pawn.equipment.Primary,
                UnavailableReason = DescribeUnavailable(pawn, map)
            };

            PawnFitContext context = PawnFitContext.Create(pawn);
            analysis.CombatRole = context.CombatRole;
            analysis.PreferredRange = context.PreferredRange;
            analysis.RoleOverridden = context.RoleOverridden;
            analysis.CombatRoleSource = context.CombatRoleSource;
            analysis.HandTypeOverride = ArmoryGameComponent.GetHandTypeOverride(pawn);
            CollectSidearms(analysis, context);

            if (analysis.Current != null)
            {
                analysis.CurrentScore = CombatScoring.Compute(context, analysis.Current, out _);
            }

            analysis.SidearmState = SimpleSidearmsAdapter.GetState(pawn);
            if (pawn.equipment != null && pawn.equipment.bondedWeapon != null &&
                pawn.equipment.Primary == pawn.equipment.bondedWeapon)
            {
                analysis.HandLocked = true;
                analysis.HandLockReason = "bonded persona weapon";
            }
            ResolveRole(analysis, context);
            analysis.HandTypeMismatch = !analysis.HandTypeFromSimpleSidearms &&
                analysis.Current != null && analysis.Current.def != null &&
                (analysis.Current.def.IsRangedWeapon || analysis.Current.def.IsMeleeWeapon) &&
                !analysis.IsHandType(analysis.Current.def);
            if (analysis.SidearmState != null)
            {
                ResolveBestCarriedBackup(analysis, context);
            }

            // Simple Sidearms has an explicit hand role. Use SS's own QuickSwitch choice
            // among carried weapons as the hand baseline; do not assume the best carried
            // weapon by Armory's score, because SS may skip it (manual use, dangerous,
            // EMP, role) and Armory would then block a valid upgrade.
            if (analysis.HandTypeFromSimpleSidearms && analysis.SidearmState != null)
            {
                ResolveBestCarriedHand(analysis, context, powerCache);
            }
            analysis.RangedHand = RangedHandFor(analysis, context);
            analysis.LogicalHand = LogicalHandFor(analysis, context);
            if (analysis.LogicalHand != null)
            {
                analysis.LogicalHandScore = CombatScoring.Compute(
                    context, analysis.LogicalHand, out _);
            }
            if (analysis.SidearmState != null)
            {
                ResolveBestCarriedSidearmTwo(analysis, context);
                ResolveDrawnSidearm(analysis);
            }

            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponInfo info = weapons[i];
                if (info.Thing == analysis.Current)
                {
                    continue;
                }
                if (!analysis.IsHandType(info.Thing.def))
                {
                    // The other type belongs in the Sidearm 1 slot, not the hand ranking.
                    continue;
                }

                string reason;
                bool hard;
                bool eligible;
                if (analysis.Available)
                {
                    eligible = Eligibility.Check(pawn, info.Thing, false, out reason, out hard);
                }
                else
                {
                    // Still rank preferences for reference, but nothing can be equipped
                    // while the pawn is in a pod or otherwise off the map.
                    reason = analysis.UnavailableReason;
                    hard = false;
                    eligible = false;
                }

                Candidate candidate = new Candidate
                {
                    Weapon = info,
                    Score = CombatScoring.Compute(context, info.Thing, out _),
                    Blocker = reason,
                    Hard = hard,
                    Ledger = null
                };

                if (hard)
                {
                    analysis.Incompatible.Add(candidate);
                }
                else
                {
                    analysis.Ranked.Add(candidate);
                }
            }

            analysis.Ranked.Sort((a, b) => b.Score.CompareTo(a.Score));
            analysis.Incompatible.Sort((a, b) => b.Score.CompareTo(a.Score));
            BuildSidearmCandidates(analysis, context, weapons, powerCache);

            if (analysis.Available)
            {
                bool hard;
                analysis.AutomationNote = ArmoryPlanner.GetAutomationNote(
                    pawn, map, ArmoryMod.Settings, Find.TickManager.TicksGame, out hard);
                if (analysis.DrawnSidearm && !string.IsNullOrEmpty(analysis.AutomationNote) &&
                    analysis.AutomationNote.StartsWith("manual"))
                {
                    analysis.AutomationNote = null;
                    hard = false;
                }
                analysis.AutomationBlocked = hard;
            }

            return analysis;
        }

        /// <summary>
        /// Resolves the hand slot type. Simple Sidearms' per-pawn primary mode wins when
        /// it is active and respected; otherwise Armory derives a type from the player's
        /// override, a bonded weapon, a ranged-blocking shield, Brawler, passion, and
        /// finally skill. Ranged is the default when no signal prefers melee.
        /// </summary>
        private static void ResolveRole(PawnAnalysis analysis, PawnFitContext context)
        {
            if (SimpleSidearmsAdapter.Active && SimpleSidearmsAdapter.RespectEnabled)
            {
                analysis.HandTypeFromSimpleSidearms = true;
                if (analysis.SidearmState != null &&
                    analysis.SidearmState.PreferredPrimaryIsRanged.HasValue)
                {
                    SetHandType(analysis, analysis.SidearmState.PreferredPrimaryIsRanged.Value,
                        "Simple Sidearms mode");
                    return;
                }

                if (analysis.Current != null && analysis.Current.def != null)
                {
                    if (analysis.Current.def.IsRangedWeapon)
                    {
                        SetHandType(analysis, true, "from current weapon");
                        return;
                    }
                    if (analysis.Current.def.IsMeleeWeapon)
                    {
                        SetHandType(analysis, false, "from current weapon");
                        return;
                    }
                }

                ResolveAutomaticHandType(analysis, context);
                return;
            }

            analysis.HandTypeFromSimpleSidearms = false;
            ResolveAutomaticHandType(analysis, context);
        }

        private const int MeleeSkillMargin = 3;

        /// <summary>
        /// No-SS hand type. Explicit player choice wins, then hard constraints (bonded
        /// weapon, shield belt), then Brawler, passion, and skill. Passion is deliberately
        /// checked before skill; a clearly better melee skill must beat Shooting by
        /// <see cref="MeleeSkillMargin"/> levels so close skills keep the ranged default.
        /// </summary>
        private static void ResolveAutomaticHandType(PawnAnalysis analysis, PawnFitContext context)
        {
            if (analysis.HandTypeOverride == HandType.Ranged)
            {
                SetHandType(analysis, true, "manual hand type");
                return;
            }
            if (analysis.HandTypeOverride == HandType.Melee)
            {
                SetHandType(analysis, false, "manual hand type");
                return;
            }

            if (analysis.HandLocked && analysis.Current != null && analysis.Current.def != null)
            {
                if (analysis.Current.def.IsRangedWeapon)
                {
                    SetHandType(analysis, true, "bonded weapon");
                    return;
                }
                if (analysis.Current.def.IsMeleeWeapon)
                {
                    SetHandType(analysis, false, "bonded weapon");
                    return;
                }
            }

            if (context.RangedBlockingShield)
            {
                SetHandType(analysis, false, "shield belt");
                return;
            }

            if (context.Brawler)
            {
                SetHandType(analysis, false, "Brawler");
                return;
            }

            if ((int)context.ShootingPassion > (int)context.MeleePassion)
            {
                SetHandType(analysis, true, "shooting passion");
                return;
            }
            if ((int)context.MeleePassion > (int)context.ShootingPassion)
            {
                SetHandType(analysis, false, "melee passion");
                return;
            }

            if (context.Melee >= context.Shooting + MeleeSkillMargin)
            {
                SetHandType(analysis, false, "melee skill");
                return;
            }

            SetHandType(analysis, true, context.Shooting > context.Melee
                ? "shooting skill"
                : "ranged default");
        }

        private static void SetHandType(PawnAnalysis analysis, bool ranged, string source)
        {
            analysis.RoleIsRanged = ranged;
            analysis.RoleSource = source;
        }

        private static void ResolveBestCarriedHand(PawnAnalysis analysis, PawnFitContext context,
            Dictionary<int, float> powerCache)
        {
            bool ranged = analysis.RoleIsRanged.Value;
            ThingWithComps ssChoice = SimpleSidearmsAdapter.GetPreferredCarriedPrimary(analysis.Pawn, ranged);
            if (ssChoice != null)
            {
                if (ssChoice != analysis.Current)
                {
                    ConsiderCarried(analysis, context, ssChoice);
                }
                return;
            }

            if (SimpleSidearmsAdapter.PreferredPrimaryCheckFailed)
            {
                // Reflection failed; fall back to the best weapon already in the pawn's
                // possession so the plan does not stall, but keep the once-only warning.
                ConsiderCarried(analysis, context, analysis.Current);
                for (int i = 0; i < analysis.Sidearms.Count; i++)
                {
                    ConsiderCarried(analysis, context, analysis.Sidearms[i]);
                }
                return;
            }

            // SS would not switch to any carried weapon. The equipped role-type weapon is
            // still the baseline; a carried weapon SS skips (manual use, dangerous, EMP)
            // must not block an upgrade.
            if (analysis.Current != null && analysis.IsHandType(analysis.Current.def))
            {
                ConsiderCarried(analysis, context, analysis.Current);
            }
        }

        private static void ResolveBestCarriedBackup(PawnAnalysis analysis,
            PawnFitContext context)
        {
            for (int i = 0; i < analysis.CarriedWeapons.Count; i++)
            {
                ThingWithComps weapon = analysis.CarriedWeapons[i];
                if (weapon == null || !analysis.IsBackupType(weapon.def))
                {
                    continue;
                }
                string reason;
                bool hard;
                if (!Eligibility.CanUseCarriedWeapon(analysis.Pawn, weapon, out reason, out hard))
                {
                    continue;
                }
                float score = CombatScoring.Compute(context, weapon, out _);
                if (score > analysis.BestCarriedBackupScore)
                {
                    analysis.BestCarriedBackupScore = score;
                    analysis.BestCarriedBackup = weapon;
                }
            }
        }

        private static void ResolveBestCarriedSidearmTwo(PawnAnalysis analysis,
            PawnFitContext context)
        {
            ThingWithComps excluded = analysis.RoleIsRanged == true
                ? analysis.RangedHand
                : analysis.BestCarriedBackup;
            for (int i = 0; i < analysis.CarriedWeapons.Count; i++)
            {
                ThingWithComps weapon = analysis.CarriedWeapons[i];
                if (weapon == null || weapon == excluded ||
                    !analysis.IsSidearmTwoType(weapon.def))
                {
                    continue;
                }
                string reason;
                bool hard;
                if (!Eligibility.CanUseCarriedWeapon(analysis.Pawn, weapon, out reason, out hard))
                {
                    continue;
                }
                float score = CombatScoring.Compute(context, weapon, out _);
                if (score > analysis.BestCarriedSidearmTwoScore)
                {
                    analysis.BestCarriedSidearmTwoScore = score;
                    analysis.BestCarriedSidearmTwo = weapon;
                }
            }
        }

        /// <summary>
        /// The ranged weapon that logically fills the hand slot: the best carried ranged
        /// weapon at the pawn's preferred range. This is deliberately not Simple Sidearms'
        /// current draw, because SS may temporarily draw Sidearm 2 for a close target.
        /// </summary>
        private static ThingWithComps RangedHandFor(PawnAnalysis analysis,
            PawnFitContext context)
        {
            if (analysis.RoleIsRanged != true)
            {
                return null;
            }
            ThingWithComps best = null;
            float bestScore = -1f;
            for (int i = 0; i < analysis.CarriedWeapons.Count; i++)
            {
                ThingWithComps weapon = analysis.CarriedWeapons[i];
                if (weapon == null || weapon.def == null || !weapon.def.IsRangedWeapon)
                {
                    continue;
                }
                float score = CombatScoring.Compute(context, weapon, out _);
                if (best == null || score > bestScore)
                {
                    best = weapon;
                    bestScore = score;
                }
            }
            return best;
        }

        private static ThingWithComps LogicalHandFor(PawnAnalysis analysis,
            PawnFitContext context)
        {
            if (analysis.RoleIsRanged == true)
            {
                return analysis.RangedHand;
            }
            if (analysis.RoleIsRanged == false)
            {
                ThingWithComps best = null;
                float bestScore = -1f;
                for (int i = 0; i < analysis.CarriedWeapons.Count; i++)
                {
                    ThingWithComps weapon = analysis.CarriedWeapons[i];
                    if (weapon == null || weapon.def == null || !weapon.def.IsMeleeWeapon)
                    {
                        continue;
                    }
                    float score = CombatScoring.Compute(context, weapon, out _);
                    if (best == null || score > bestScore)
                    {
                        best = weapon;
                        bestScore = score;
                    }
                }
                return best;
            }
            return null;
        }

        private static void ResolveDrawnSidearm(PawnAnalysis analysis)
        {
            analysis.DrawnSidearm = false;
            analysis.DrawnSidearmName = null;
            if (analysis.Current == null || analysis.Current.def == null ||
                !analysis.RoleIsRanged.HasValue)
            {
                return;
            }

            bool currentRanged = analysis.Current.def.IsRangedWeapon;
            bool currentMelee = analysis.Current.def.IsMeleeWeapon;
            if (!currentRanged && !currentMelee)
            {
                return;
            }

            bool drawn;
            if (analysis.RoleIsRanged.Value)
            {
                drawn = currentMelee;
                if (!drawn && currentRanged && analysis.RangedHand != null &&
                    analysis.RangedHand != analysis.Current)
                {
                    drawn = true;
                }
            }
            else
            {
                drawn = currentRanged;
            }

            if (drawn)
            {
                analysis.DrawnSidearm = true;
                analysis.DrawnSidearmName = analysis.Current.LabelCap;
            }
        }

        private static void BuildSidearmCandidates(PawnAnalysis analysis, PawnFitContext context,
            List<WeaponInfo> weapons, Dictionary<int, float> powerCache)
        {
            if (!analysis.HasRole || analysis.SidearmState == null ||
                !SimpleSidearmsAdapter.CanCheckSidearms || analysis.Pawn == null)
            {
                return;
            }

            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponInfo info = weapons[i];
                if (info.Thing == analysis.Current || !analysis.IsBackupType(info.Thing.def) ||
                    !IsUsableAsBackup(info.Thing))
                {
                    continue;
                }

                string reason;
                bool hard;
                if (analysis.Available)
                {
                    Eligibility.CheckSidearm(analysis.Pawn, info.Thing, false, out reason, out hard);
                }
                else
                {
                    reason = analysis.UnavailableReason;
                    hard = false;
                }
                if (hard)
                {
                    continue;
                }

                bool alreadyCarried = analysis.AlreadyCarries(info.Thing);
                float score = CombatScoring.Compute(context, info.Thing, out _);
                SidearmCandidate candidate = new SidearmCandidate
                {
                    Weapon = info,
                    Slot = WeaponSlot.Sidearm1,
                    Score = score,
                    SlotScore = score,
                    Blocker = alreadyCarried ? Eligibility.AlreadyCarried : reason,
                    AlreadyCarried = alreadyCarried
                };
                if (!alreadyCarried && !candidate.Ready && analysis.Available &&
                    SimpleSidearmsAdapter.CanCheckSidearmType &&
                    SimpleSidearmsAdapter.CanCheckSidearmUse &&
                    SimpleSidearmsAdapter.CanCheckRelativeLimits)
                {
                    // Only promise a drop-first upgrade when the weapon is reachable and
                    // SS's remaining objection is capacity, not the weapon type itself.
                    string worldReason;
                    string ruleReason;
                    bool ruleHard;
                    string typeReason;
                    string useReason;
                    candidate.UpgradeAfterDrop =
                        Eligibility.CheckSidearmWorld(analysis.Pawn, info.Thing, false, out worldReason) &&
                        !Eligibility.CheckSidearmRules(analysis.Pawn, info.Thing, out ruleReason, out ruleHard) &&
                        SimpleSidearmsAdapter.CanUseSidearm(analysis.Pawn, info.Thing, out useReason) &&
                        SimpleSidearmsAdapter.IsValidSidearmForPawn(analysis.Pawn, info.Thing, out typeReason);
                }
                analysis.SidearmCandidates.Add(candidate);
            }

            analysis.SidearmCandidates.Sort(CompareSidearmCandidates);
            BuildSidearmTwoCandidates(analysis, context, weapons);
        }

        private static int CompareSidearmCandidates(SidearmCandidate a, SidearmCandidate b)
        {
            if (a.Ready != b.Ready)
            {
                return a.Ready ? -1 : 1;
            }
            return b.SlotScore.CompareTo(a.SlotScore);
        }

        private static void BuildSidearmTwoCandidates(PawnAnalysis analysis,
            PawnFitContext context, List<WeaponInfo> weapons)
        {
            SidearmCoverageModel coverage = new SidearmCoverageModel();
            coverage.AddBaseline(context, analysis.RangedHand);
            coverage.AddBaseline(context, analysis.BestCarriedBackup);

            SidearmCandidate carriedCandidate = null;
            if (analysis.BestCarriedSidearmTwo != null)
            {
                carriedCandidate = new SidearmCandidate { Slot = WeaponSlot.Sidearm2 };
                coverage.AddCandidate(context, carriedCandidate, analysis.BestCarriedSidearmTwo);
            }

            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponInfo info = weapons[i];
                if (info.Thing == analysis.Current || !analysis.IsSidearmTwoType(info.Thing.def) ||
                    !IsUsableAsBackup(info.Thing) || info.Thing.def.destroyOnDrop ||
                    info.Thing.GetComp<CompBladelinkWeapon>() != null ||
                    analysis.AlreadyCarries(info.Thing) ||
                    Eligibility.IsUnbondedPersona(info.Thing))
                {
                    continue;
                }

                string reason;
                bool hard;
                if (analysis.Available)
                {
                    Eligibility.CheckSidearm(analysis.Pawn, info.Thing, false, out reason, out hard);
                }
                else
                {
                    reason = analysis.UnavailableReason;
                    hard = false;
                }
                if (hard)
                {
                    continue;
                }

                float score = CombatScoring.Compute(context, info.Thing, out _);
                SidearmCandidate candidate = new SidearmCandidate
                {
                    Weapon = info,
                    Slot = WeaponSlot.Sidearm2,
                    Score = score,
                    Blocker = reason
                };
                if (!candidate.Ready && analysis.Available &&
                    SimpleSidearmsAdapter.CanCheckSidearmType &&
                    SimpleSidearmsAdapter.CanCheckSidearmUse &&
                    SimpleSidearmsAdapter.CanCheckRelativeLimits)
                {
                    string worldReason;
                    string ruleReason;
                    bool ruleHard;
                    string typeReason;
                    string useReason;
                    candidate.UpgradeAfterDrop =
                        Eligibility.CheckSidearmWorld(analysis.Pawn, info.Thing, false, out worldReason) &&
                        !Eligibility.CheckSidearmRules(analysis.Pawn, info.Thing, out ruleReason, out ruleHard) &&
                        SimpleSidearmsAdapter.CanUseSidearm(analysis.Pawn, info.Thing, out useReason) &&
                        SimpleSidearmsAdapter.IsValidSidearmForPawn(analysis.Pawn, info.Thing, out typeReason);
                }
                coverage.AddCandidate(context, candidate, info.Thing);
                analysis.SidearmTwoCandidates.Add(candidate);
            }

            coverage.Finish();
            if (carriedCandidate != null)
            {
                analysis.CarriedSidearmTwoCoverage = carriedCandidate.SlotScore;
                analysis.CarriedSidearmTwoGain = carriedCandidate.ComplementGain;
                analysis.CarriedSidearmTwoBand = carriedCandidate.ComplementBand;
                analysis.CarriedSidearmTwoText = carriedCandidate.ComplementText;
            }
            analysis.SidearmTwoCandidates.Sort(CompareSidearmCandidates);
        }

        /// <summary>
        /// SS's QuickSwitch always skips manual-use ranged weapons, so a Molotov or
        /// similar item would sit in the bag unused. Do not offer it as a backup.
        /// </summary>
        private static bool IsUsableAsBackup(ThingWithComps weapon)
        {
            if (weapon == null || weapon.def == null || Eligibility.IsWorkTool(weapon.def))
            {
                return false;
            }
            CompEquippable equippable = weapon.GetComp<CompEquippable>();
            if (equippable == null || equippable.PrimaryVerb == null)
            {
                return false;
            }
            return !weapon.def.IsRangedWeapon ||
                   !equippable.PrimaryVerb.verbProps.onlyManualCast;
        }

        private static void CollectSidearms(PawnAnalysis analysis, PawnFitContext context)
        {
            Pawn pawn = analysis.Pawn;
            if (pawn == null)
            {
                analysis.SidearmText = "none";
                return;
            }

            analysis.CarriedWeapons.Clear();
            if (IsCombatWeapon(analysis.Current))
            {
                analysis.CarriedWeapons.Add(analysis.Current);
            }

            if (pawn.inventory == null || pawn.inventory.innerContainer == null)
            {
                analysis.SidearmText = "none";
                return;
            }

            List<string> labels = new List<string>();
            for (int i = 0; i < pawn.inventory.innerContainer.Count; i++)
            {
                ThingWithComps weapon = pawn.inventory.innerContainer[i] as ThingWithComps;
                if (!IsCombatWeapon(weapon))
                {
                    // Keep the count to combat weapons; tools should not satisfy the
                    // sidearm target or appear as replacement targets.
                    continue;
                }
                if (!analysis.CarriedWeapons.Contains(weapon))
                {
                    analysis.CarriedWeapons.Add(weapon);
                }
                float score = CombatScoring.Compute(context, weapon, out _);
                string unusableReason;
                bool unusableHard;
                bool usable = Eligibility.CanUseCarriedWeapon(pawn, weapon,
                    out unusableReason, out unusableHard);
                analysis.Sidearms.Add(weapon);
                analysis.CarriedSidearms.Add(new CarriedSidearm
                {
                    Thing = weapon,
                    Score = score,
                    Usable = usable,
                    UnusableReason = usable ? null : unusableReason
                });
                labels.Add(string.Format("{0} · score {1:0}", weapon.LabelCap, score));
            }
            analysis.SidearmText = labels.Count == 0 ? "none" : string.Join(", ", labels);
        }

        private static bool IsCombatWeapon(ThingWithComps weapon)
        {
            return weapon != null && weapon.def != null &&
                   WeaponFilter.IsWeaponDef(weapon.def) &&
                   (weapon.def.IsRangedWeapon || weapon.def.IsMeleeWeapon);
        }

        private static void ConsiderCarried(PawnAnalysis analysis, PawnFitContext context,
            ThingWithComps weapon)
        {
            if (weapon == null || !analysis.IsHandType(weapon.def))
            {
                return;
            }
            float score = CombatScoring.Compute(context, weapon, out _);
            if (score > analysis.BestCarriedPrimaryScore)
            {
                analysis.BestCarriedPrimaryScore = score;
                analysis.BestCarriedPrimary = weapon;
            }
        }

        /// <summary>
        /// Demand is an ideal loadout, not a snapshot of the floor: every colonist picks
        /// their best hand and backup weapon from all obtainable defs (craftable, buyable,
        /// or already owned), then the owned copies of that def are subtracted. That turns
        /// the tab into a build/buy/research plan instead of a leftover-stock report.
        /// </summary>
        private static void BuildDemand(ArmorySnapshot snapshot)
        {
            Dictionary<WeaponVariantKey, int> owned = new Dictionary<WeaponVariantKey, int>();
            Dictionary<WeaponVariantKey, int> forbidden = new Dictionary<WeaponVariantKey, int>();
            Dictionary<WeaponVariantKey, ThingWithComps> bestOwned =
                new Dictionary<WeaponVariantKey, ThingWithComps>();

            for (int i = 0; i < snapshot.Pawns.Count; i++)
            {
                PawnAnalysis pawn = snapshot.Pawns[i];
                AddCount(owned, bestOwned, pawn.Current);
                for (int j = 0; j < pawn.Sidearms.Count; j++)
                {
                    AddCount(owned, bestOwned, pawn.Sidearms[j]);
                }
            }

            for (int i = 0; i < snapshot.Weapons.Count; i++)
            {
                WeaponInfo info = snapshot.Weapons[i];
                if (info == null || info.Thing == null)
                {
                    continue;
                }
                AddCount(owned, bestOwned, info.Thing);
                if (info.Thing.IsForbidden(Faction.OfPlayer))
                {
                    AddCount(forbidden, info.Thing);
                }
            }

            // Defs the colony owns belong in the candidate universe even without a recipe
            // (persona weapons, quest rewards). Create them before reading the catalog.
            HashSet<ThingDef> ownedDefs = new HashSet<ThingDef>();
            foreach (WeaponVariantKey key in owned.Keys)
            {
                if (key.Def != null)
                {
                    ownedDefs.Add(key.Def);
                }
            }
            List<ThingDef> ownedDefList = new List<ThingDef>(ownedDefs);
            for (int i = 0; i < ownedDefList.Count; i++)
            {
                WeaponCatalog.GetOrCreate(ownedDefList[i]);
            }
            WeaponCatalog.RefreshAvailability(snapshot.Map);

            HashSet<WeaponVariantKey> ownedKeys = new HashSet<WeaponVariantKey>(owned.Keys);
            List<WeaponSource> universe = WeaponCatalog.All;
            ArmorySettings settings = ArmoryMod.Settings;
            float threshold = settings == null ? 0.1f : settings.upgradeThreshold;
            int minimumAge = settings == null ? 0 : settings.minimumAge;

            // Two option groups per pawn: what can be obtained now, and what research
            // would unlock. Every qualifying variant is listed, so a pawn can appear in
            // several rows; the sections keep immediate crafting ahead of research and
            // trader-only weapons last.
            List<WeaponVariant> craftVariants = new List<WeaponVariant>();
            List<WeaponVariant> researchVariants = new List<WeaponVariant>();
            for (int i = 0; i < universe.Count; i++)
            {
                WeaponSource source = universe[i];
                if (source == null || source.Def == null || source.Probe == null ||
                    source.ProbePower <= 0f || !source.Obtainable)
                {
                    continue;
                }

                // A def the colony already owns is usable supply even when its recipe is
                // still research-locked: the pawn would carry the copy we have, and the
                // plan should say "unforbid/transfer it" rather than "craft a worse knife".
                bool isOwned = ownedDefs.Contains(source.Def);
                bool craft = source.AvailableNow || source.MissingResearch == null || isOwned;
                List<WeaponVariant> variants = WeaponVariants.For(source, snapshot.Map, ownedKeys);
                for (int j = 0; j < variants.Count; j++)
                {
                    WeaponVariant variant = variants[j];
                    if (variant == null || variant.Probe == null || variant.Power <= 0f)
                    {
                        continue;
                    }
                    if (craft)
                    {
                        craftVariants.Add(variant);
                    }
                    else
                    {
                        researchVariants.Add(variant);
                    }
                }
            }

            Dictionary<WeaponVariantKey, DemandRow> rows =
                new Dictionary<WeaponVariantKey, DemandRow>();
            Dictionary<WeaponVariantKey, int> plannedCounts =
                new Dictionary<WeaponVariantKey, int>();
            float variety = settings == null ? 0f : settings.varietyStrength;
            for (int i = 0; i < snapshot.Pawns.Count; i++)
            {
                PawnAnalysis pawn = snapshot.Pawns[i];
                if (!IsDemandEligible(pawn, minimumAge))
                {
                    continue;
                }

                PawnFitContext context = PawnFitContext.Create(pawn.Pawn);

                if (!pawn.HandLocked)
                {
                    AddSlotWants(rows, pawn, context, craftVariants, researchVariants, true,
                        pawn.HandBaselineScore, threshold, LabelOf(pawn.Current),
                        plannedCounts, variety);
                }

                // Backups need SS's per-pawn memory; without it there is no managed
                // Sidearm slot, even though the hand slot is still typed.
                if (!pawn.HasRole || pawn.SidearmState == null)
                {
                    continue;
                }

                // Match the planner: an upgrade replaces the *worst* carried backup, so a
                // pawn with one good and one junk backup still creates demand.
                float backupBaseline;
                ThingWithComps worstBackup;
                TryGetWorstBackup(pawn, out backupBaseline, out worstBackup);
                AddSlotWants(rows, pawn, context, craftVariants, researchVariants, false,
                    backupBaseline, threshold, LabelOf(worstBackup),
                    plannedCounts, variety);
                AddSidearmTwoWants(rows, pawn, context, craftVariants, researchVariants,
                    threshold, LabelOf(pawn.BestCarriedSidearmTwo), plannedCounts);
            }

            // Owned variants that are not already an upgrade row are kept for the optional
            // Owned section. They let the player compare a steel knife with a plasteel
            // knife, or see what a craft row would replace.
            foreach (KeyValuePair<WeaponVariantKey, int> pair in owned)
            {
                if (rows.ContainsKey(pair.Key))
                {
                    continue;
                }

                WeaponSource source;
                if (!WeaponCatalog.TryGet(pair.Key.Def, out source) || source == null)
                {
                    continue;
                }
                List<WeaponVariant> variants = WeaponVariants.For(source, snapshot.Map, ownedKeys);
                WeaponVariant variant = FindVariant(variants, pair.Key.Stuff);
                if (variant == null || variant.Power <= 0f)
                {
                    continue;
                }

                DemandRow ownedRow = CreateDemandRow(variant);
                ThingWithComps actual;
                if (bestOwned.TryGetValue(pair.Key, out actual) && actual != null)
                {
                    ownedRow.Label = actual.LabelCap;
                    ownedRow.MarketValue = actual.MarketValue;
                    ownedRow.Power = WeaponPower.Get(actual, null);
                    ownedRow.Range = WeaponPower.GetRange(actual);
                }
                ownedRow.Section = DemandSection.Owned;
                rows[pair.Key] = ownedRow;
            }

            foreach (KeyValuePair<WeaponVariantKey, DemandRow> pair in rows)
            {
                DemandRow row = pair.Value;
                row.Owned = CountOf(owned, pair.Key);
                row.Forbidden = CountOf(forbidden, pair.Key);

                // Keep the best option even when it is already covered, so the ideal
                // hand/backup is visible and not replaced by a wall of grey alternatives.
                if (row.Need <= 0 && row.BestCount <= 0)
                {
                    if (row.Wanted > 0 || row.Owned <= 0)
                    {
                        continue;
                    }
                    row.Section = DemandSection.Owned;
                }
                else if (row.Section != DemandSection.Owned)
                {
                    WeaponSource source;
                    if (WeaponCatalog.TryGet(row.Def, out source) && source != null)
                    {
                        row.CraftableNow = source.AvailableNow;
                        row.Buyable = source.Buyable;
                        row.BenchLabel = source.BenchLabel;
                        row.CanCraft = row.CraftableNow && row.Need > 0 &&
                                       WeaponCatalog.FindBuiltBench(source, snapshot.Map) != null;
                        row.MissingResearch = source.MissingResearch == null
                            ? null
                            : source.MissingResearch.LabelCap;
                    }

                    if (row.CraftableNow)
                    {
                        row.Section = DemandSection.Craft;
                    }
                    else if (row.MissingResearch != null)
                    {
                        // Research is the next step even when a trader could also supply it;
                        // the Source cell appends "buy" so the row still reads as obtainable.
                        row.Section = DemandSection.Research;
                    }
                    else
                    {
                        row.Section = DemandSection.Buy;
                    }
                }

                snapshot.Demand.Add(row);
            }

            snapshot.Demand.Sort((a, b) =>
            {
                int bySection = a.Section.CompareTo(b.Section);
                if (bySection != 0)
                {
                    return bySection;
                }

                if (a.Section == DemandSection.Owned)
                {
                    int byPower = b.Power.CompareTo(a.Power);
                    if (byPower != 0)
                    {
                        return byPower;
                    }
                    return string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
                }

                // Aggregate expected benefit: range-aware combat score, summed over
                // the colonists who would upgrade. This keeps weak starter weapons
                // below stronger options even when they are somebody's current best.
                int byScore = b.TotalScore.CompareTo(a.TotalScore);
                if (byScore != 0)
                {
                    return byScore;
                }
                int byGain = b.TotalGain.CompareTo(a.TotalGain);
                if (byGain != 0)
                {
                    return byGain;
                }
                int byBest = b.BestCount.CompareTo(a.BestCount);
                if (byBest != 0)
                {
                    return byBest;
                }
                int byNeed = b.Need.CompareTo(a.Need);
                if (byNeed != 0)
                {
                    return byNeed;
                }
                return string.Compare(a.Def.defName, b.Def.defName,
                    StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// Who counts toward the colony's ideal loadout. Transient states (drafted, downed,
        /// asleep, mid-job) do not remove a colonist; permanent inability does.
        /// </summary>
        private static bool IsDemandEligible(PawnAnalysis analysis, int minimumAge)
        {
            if (analysis == null || !analysis.Available || analysis.Pawn == null ||
                analysis.Pawn.Dead)
            {
                return false;
            }

            Pawn pawn = analysis.Pawn;
            if (minimumAge > 0 &&
                (pawn.ageTracker == null || pawn.ageTracker.AgeBiologicalYears < minimumAge))
            {
                return false;
            }
            if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return false;
            }
            if (pawn.health == null ||
                !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                return false;
            }
            return pawn.jobs != null;
        }

        /// <summary>
        /// Adds every obtainable weapon that is an upgrade for this pawn and slot. The
        /// rows are independent options, so one colonist can appear in several of them.
        /// Research options must also beat the best craft-now option by the threshold;
        /// otherwise they would only advertise something worse than what is buildable.
        /// </summary>
        private static void AddSlotWants(Dictionary<WeaponVariantKey, DemandRow> rows,
            PawnAnalysis pawn, PawnFitContext context, List<WeaponVariant> craftSources,
            List<WeaponVariant> researchSources, bool hand, float baseline, float threshold,
            string currentLabel, Dictionary<WeaponVariantKey, int> plannedCounts, float variety)
        {
            float required = baseline * (1f + threshold);
            float bestCraft = 0f;
            float bestResearch = 0f;

            for (int i = 0; i < craftSources.Count; i++)
            {
                float score;
                if (TryScoreCandidate(pawn, context, craftSources[i], hand, required,
                        plannedCounts, variety, out score) &&
                    score > bestCraft)
                {
                    bestCraft = score;
                }
            }

            float researchRequired = bestCraft > 0f ? bestCraft * (1f + threshold) : required;
            for (int i = 0; i < researchSources.Count; i++)
            {
                float score;
                if (TryScoreCandidate(pawn, context, researchSources[i], hand, researchRequired,
                        plannedCounts, variety, out score) &&
                    score > bestResearch)
                {
                    bestResearch = score;
                }
            }

            for (int i = 0; i < craftSources.Count; i++)
            {
                float score;
                if (!TryScoreCandidate(pawn, context, craftSources[i], hand, required,
                        plannedCounts, variety, out score))
                {
                    continue;
                }
                bool isTop = bestCraft > 0f && score >= bestCraft - 0.001f;
                AddWant(rows, pawn, craftSources[i], score,
                    hand ? WeaponSlot.Hand : WeaponSlot.Sidearm1, baseline, currentLabel, isTop,
                    plannedCounts);
            }

            for (int i = 0; i < researchSources.Count; i++)
            {
                float score;
                if (!TryScoreCandidate(pawn, context, researchSources[i], hand, researchRequired,
                        plannedCounts, variety, out score))
                {
                    continue;
                }
                bool isTop = bestResearch > 0f && score >= bestResearch - 0.001f;
                AddWant(rows, pawn, researchSources[i], score,
                    hand ? WeaponSlot.Hand : WeaponSlot.Sidearm1, baseline, currentLabel,
                    isTop, plannedCounts);
            }
        }

        /// <summary>
        /// Adds demand for the optional ranged complement. Uses the same coverage model
        /// as the planner: only improvements in weak Hand + Sidearm 1 bands count.
        /// </summary>
        private static void AddSidearmTwoWants(Dictionary<WeaponVariantKey, DemandRow> rows,
            PawnAnalysis pawn, PawnFitContext context, List<WeaponVariant> craftSources,
            List<WeaponVariant> researchSources, float threshold, string currentLabel,
            Dictionary<WeaponVariantKey, int> plannedCounts)
        {
            SidearmCoverageModel coverage = new SidearmCoverageModel();
            coverage.AddBaseline(context, pawn.RangedHand);
            coverage.AddBaseline(context, pawn.BestCarriedBackup);

            Dictionary<WeaponVariantKey, SidearmCandidate> candidates =
                new Dictionary<WeaponVariantKey, SidearmCandidate>();
            Dictionary<WeaponVariantKey, bool> craftKeys =
                new Dictionary<WeaponVariantKey, bool>();

            for (int pass = 0; pass < 2; pass++)
            {
                List<WeaponVariant> source = pass == 0 ? craftSources : researchSources;
                for (int i = 0; i < source.Count; i++)
                {
                    WeaponVariant variant = source[i];
                    if (!IsSidearmTwoVariant(pawn, variant))
                    {
                        continue;
                    }

                    SidearmCandidate candidate = new SidearmCandidate
                    {
                        Slot = WeaponSlot.Sidearm2
                    };
                    coverage.AddCandidate(context, candidate, variant.Probe);
                    candidates[variant.Key] = candidate;
                    craftKeys[variant.Key] = pass == 0;
                }
            }

            if (candidates.Count == 0)
            {
                return;
            }
            coverage.Finish();

            float bestCraft = 0f;
            float bestResearch = 0f;
            foreach (KeyValuePair<WeaponVariantKey, SidearmCandidate> pair in candidates)
            {
                SidearmCandidate candidate = pair.Value;
                if (!IsUsefulSidearmTwo(candidate, threshold))
                {
                    continue;
                }
                bool craft;
                craftKeys.TryGetValue(pair.Key, out craft);
                if (craft)
                {
                    if (candidate.SlotScore > bestCraft)
                    {
                        bestCraft = candidate.SlotScore;
                    }
                }
                else if (candidate.SlotScore > bestResearch)
                {
                    bestResearch = candidate.SlotScore;
                }
            }

            float researchRequired = bestCraft > 0f
                ? bestCraft * (1f + threshold)
                : 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                List<WeaponVariant> source = pass == 0 ? craftSources : researchSources;
                float best = pass == 0 ? bestCraft : bestResearch;
                for (int i = 0; i < source.Count; i++)
                {
                    WeaponVariant variant = source[i];
                    SidearmCandidate candidate;
                    if (!candidates.TryGetValue(variant.Key, out candidate) ||
                        !IsUsefulSidearmTwo(candidate, threshold))
                    {
                        continue;
                    }
                    if (pass == 1 && researchRequired > 0f &&
                        candidate.SlotScore <= researchRequired)
                    {
                        continue;
                    }

                    RangeBand band = candidate.ComplementBand ?? RangeBand.Close;
                    float baseline = coverage.BaselineBandAverage(band);
                    float score = SidearmCoverageModel.BandAverage(
                        candidate.CoverageProfile, band);
                    if (baseline > 0.01f &&
                        score <= baseline * (1f + threshold))
                    {
                        continue;
                    }

                    bool isTop = best > 0f && candidate.SlotScore >= best - 0.001f;
                    AddWant(rows, pawn, variant, score, WeaponSlot.Sidearm2, baseline,
                        currentLabel, isTop, plannedCounts);
                }
            }
        }

        private static bool IsSidearmTwoVariant(PawnAnalysis pawn, WeaponVariant variant)
        {
            if (variant == null || variant.Def == null || variant.Probe == null ||
                !variant.Def.IsRangedWeapon || variant.Def.destroyOnDrop ||
                variant.Probe.GetComp<CompBladelinkWeapon>() != null ||
                pawn == null || pawn.Pawn == null ||
                pawn.AlreadyCarries(variant.Def) ||
                Eligibility.IsUnbondedPersona(variant.Probe) ||
                !IsUsableAsBackup(variant.Probe))
            {
                return false;
            }

            string reason;
            return SimpleSidearmsAdapter.CanUseSidearm(pawn.Pawn, variant.Probe, out reason) &&
                   SimpleSidearmsAdapter.IsValidSidearmForPawn(pawn.Pawn, variant.Probe,
                       out reason);
        }

        private static bool IsUsefulSidearmTwo(SidearmCandidate candidate, float threshold)
        {
            return candidate != null && candidate.ComplementBand.HasValue &&
                   candidate.SlotScore > 0.001f &&
                   candidate.ComplementGain >= threshold;
        }

        /// <summary>
        /// Scores one candidate for a slot, applying def-level rules and the minimum
        /// improvement. Returns false when the weapon does not belong in that slot or
        /// would not be an upgrade.
        /// </summary>
        private static bool TryScoreCandidate(PawnAnalysis pawn, PawnFitContext context,
            WeaponVariant variant, bool hand, float requiredScore,
            Dictionary<WeaponVariantKey, int> plannedCounts, float variety, out float score)
        {
            score = 0f;
            if (variant == null || variant.Source == null || variant.Probe == null ||
                variant.Power <= 0f || !variant.Source.Obtainable)
            {
                return false;
            }

            ThingDef def = variant.Def;
            if (hand ? !pawn.IsHandType(def) : !pawn.IsBackupType(def))
            {
                return false;
            }

            if (!hand && pawn.AlreadyCarries(def))
            {
                // Do not propose a second copy of a weapon the pawn already carries.
                return false;
            }

            if (Eligibility.IsUnbondedPersona(variant.Probe))
            {
                // A new persona weapon would bond on equip; never put that into an
                // automatic craft/buy plan, for either slot.
                return false;
            }

            if (!hand)
            {
                // Def-level rules only: a hypothetical new weapon has no biocoding,
                // bond or inventory state, and transient slot/capacity rules must not
                // suppress demand.
                string reason;
                if (!IsUsableAsBackup(variant.Probe) ||
                    !SimpleSidearmsAdapter.IsValidSidearmForPawn(pawn.Pawn, variant.Probe,
                        out reason))
                {
                    return false;
                }
            }

            score = CombatScoring.Compute(context, variant.Probe, out _);
            if (variety > 0.001f && plannedCounts != null)
            {
                int count;
                if (plannedCounts.TryGetValue(variant.Key, out count) && count > 0)
                {
                    // Sequential soft cap: after one pawn is planned for a variant, the
                    // next copy is slightly less attractive, and so on. The first
                    // copy is untouched.
                    score /= 1f + variety * 0.15f * count;
                }
            }
            return score > requiredScore;
        }

        private static void AddWant(Dictionary<WeaponVariantKey, DemandRow> rows,
            PawnAnalysis pawn, WeaponVariant variant, float score, WeaponSlot slot,
            float baseline, string currentLabel, bool isTop,
            Dictionary<WeaponVariantKey, int> plannedCounts)
        {
            WeaponVariantKey key = variant.Key;
            DemandRow row;
            if (!rows.TryGetValue(key, out row))
            {
                row = CreateDemandRow(variant);
                rows[key] = row;
            }

            row.TotalScore += score;

            bool hand = slot == WeaponSlot.Hand;
            bool typeSwitch = hand && pawn.HandTypeMismatch;
            bool isNew = baseline <= 0.01f && !typeSwitch;
            float gain = isNew || typeSwitch ? 0f : score / baseline - 1f;

            if (slot == WeaponSlot.Hand)
            {
                row.WantedHand++;
            }
            else if (slot == WeaponSlot.Sidearm1)
            {
                row.WantedBackup++;
            }
            else
            {
                row.WantedSidearmTwo++;
            }

            row.Wants.Add(new DemandWant
            {
                Colonist = pawn.Pawn.LabelShort,
                Slot = slot,
                Hand = hand,
                Gain = gain,
                Score = score,
                IsNew = isNew,
                TypeSwitch = typeSwitch,
                IsTop = isTop,
                CurrentLabel = currentLabel,
                Role = pawn.RoleIsRanged == true
                    ? CombatScoring.Label(pawn.CombatRole)
                    : "Melee",
                PreferredRange = pawn.RoleIsRanged == true ? pawn.PreferredRange : 0
            });

            if (isTop)
            {
                row.BestCount++;
                if (plannedCounts != null)
                {
                    int count;
                    plannedCounts.TryGetValue(key, out count);
                    plannedCounts[key] = count + 1;
                }
            }
            row.KnownCount += isNew || typeSwitch ? 0 : 1;
            row.NewCount += isNew ? 1 : 0;
            row.TypeSwitchCount += typeSwitch ? 1 : 0;
            row.KnownGain += isNew ? 0f : gain;
            row.TotalGain += isNew ? 1f : gain;
        }

        private static DemandRow CreateDemandRow(WeaponVariant variant)
        {
            DemandRow row = new DemandRow
            {
                Def = variant.Def,
                Stuff = variant.Stuff,
                Label = variant.Label,
                Power = variant.Power,
                Range = variant.Range,
                MarketValue = variant.MarketValue,
                CostText = variant.CostText,
                CostTooltip = variant.CostTooltip,
                MaterialsAvailable = variant.MaterialsAvailable,
                CraftableCopies = variant.CraftableCopies
            };
            row.Costs.AddRange(variant.Costs);
            return row;
        }

        private static WeaponVariant FindVariant(List<WeaponVariant> variants, ThingDef stuff)
        {
            if (variants == null)
            {
                return null;
            }
            for (int i = 0; i < variants.Count; i++)
            {
                WeaponVariant variant = variants[i];
                if (variant != null && variant.Stuff == stuff)
                {
                    return variant;
                }
            }
            return null;
        }

        /// <summary>Worst opposite-type backup, matching the planner's replace-worst rule.</summary>
        private static bool TryGetWorstBackup(PawnAnalysis pawn, out float score,
            out ThingWithComps thing)
        {
            score = 0f;
            thing = null;
            bool found = false;
            for (int i = 0; i < pawn.CarriedSidearms.Count; i++)
            {
                CarriedSidearm carried = pawn.CarriedSidearms[i];
                if (!carried.Usable || !pawn.IsBackupType(carried.Thing.def))
                {
                    continue;
                }
                if (!found || carried.Score < score)
                {
                    score = carried.Score;
                    thing = carried.Thing;
                }
                found = true;
            }
            return found;
        }

        private static int CompareColonistOrder(PawnAnalysis a, PawnAnalysis b)
        {
            // displayOrder is the colonist-bar order. Vanilla assigns it when a
            // pawn first appears there, which defaults to join order, and updates
            // it when the player drags portraits. Pawns that have not been
            // processed by the bar yet fall back to join tick.
            int order = DisplayOrderOf(a.Pawn).CompareTo(DisplayOrderOf(b.Pawn));
            if (order != 0)
            {
                return order;
            }

            int joined = JoinTickOf(a.Pawn).CompareTo(JoinTickOf(b.Pawn));
            if (joined != 0)
            {
                return joined;
            }

            return string.Compare(a.Pawn.LabelShort, b.Pawn.LabelShort, StringComparison.OrdinalIgnoreCase);
        }

        private static int DisplayOrderOf(Pawn pawn)
        {
            if (pawn.playerSettings == null || pawn.playerSettings.displayOrder == -9999999)
            {
                return int.MaxValue;
            }
            return pawn.playerSettings.displayOrder;
        }

        private static int JoinTickOf(Pawn pawn)
        {
            return pawn.playerSettings == null ? 0 : pawn.playerSettings.joinTick;
        }

        private static string LabelOf(ThingWithComps thing)
        {
            return thing == null ? "nothing" : thing.LabelCap;
        }

        private static int CountOf(Dictionary<WeaponVariantKey, int> counts,
            WeaponVariantKey key)
        {
            int count;
            return counts.TryGetValue(key, out count) ? count : 0;
        }

        private static void AddCount(Dictionary<WeaponVariantKey, int> counts,
            ThingWithComps thing)
        {
            AddCount(counts, null, thing);
        }

        private static void AddCount(Dictionary<WeaponVariantKey, int> counts,
            Dictionary<WeaponVariantKey, ThingWithComps> bestOwned, ThingWithComps thing)
        {
            if (thing == null || thing.def == null)
            {
                return;
            }
            WeaponVariantKey key = new WeaponVariantKey(thing.def, thing.Stuff);
            int count;
            counts.TryGetValue(key, out count);
            counts[key] = count + 1;

            if (bestOwned != null)
            {
                ThingWithComps current;
                if (!bestOwned.TryGetValue(key, out current) || current == null ||
                    WeaponPower.Get(thing, null) > WeaponPower.Get(current, null))
                {
                    bestOwned[key] = thing;
                }
            }
        }
    }
}
