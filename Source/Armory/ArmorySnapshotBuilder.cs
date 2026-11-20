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
                    Range = WeaponPower.GetRange(weapon)
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
                    row.Status = "Backup";
                }
                else if (row.BestAnyHandPawn != null)
                {
                    row.Status = "Hand (blocked)";
                }
                else if (row.BestAnyBackupPawn != null)
                {
                    row.Status = "Backup (blocked)";
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
                ResolveBestCarriedBackup(analysis);
            }

            // Simple Sidearms has an explicit hand role. Use SS's own QuickSwitch choice
            // among carried weapons as the hand baseline; do not assume the best carried
            // weapon by Armory's score, because SS may skip it (manual use, dangerous,
            // EMP, role) and Armory would then block a valid upgrade.
            if (analysis.HandTypeFromSimpleSidearms && analysis.SidearmState != null)
            {
                ResolveBestCarriedHand(analysis, context, powerCache);
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
                    // The other type belongs in the backup slot, not the hand ranking.
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

        private static void ResolveBestCarriedBackup(PawnAnalysis analysis)
        {
            for (int i = 0; i < analysis.CarriedSidearms.Count; i++)
            {
                CarriedSidearm carried = analysis.CarriedSidearms[i];
                if (!analysis.IsBackupType(carried.Thing.def))
                {
                    continue;
                }
                if (carried.Score > analysis.BestCarriedBackupScore)
                {
                    analysis.BestCarriedBackupScore = carried.Score;
                    analysis.BestCarriedBackup = carried.Thing;
                }
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

                bool alreadyCarried = analysis.AlreadyCarries(info.Thing.def);
                SidearmCandidate candidate = new SidearmCandidate
                {
                    Weapon = info,
                    Score = CombatScoring.Compute(context, info.Thing, out _),
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

            analysis.SidearmCandidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        }

        /// <summary>
        /// SS's QuickSwitch always skips manual-use ranged weapons, so a Molotov or
        /// similar item would sit in the bag unused. Do not offer it as a backup.
        /// </summary>
        private static bool IsUsableAsBackup(ThingWithComps weapon)
        {
            if (weapon == null || weapon.def == null)
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
            if (pawn == null || pawn.inventory == null || pawn.inventory.innerContainer == null)
            {
                analysis.SidearmText = "none";
                return;
            }

            List<string> labels = new List<string>();
            for (int i = 0; i < pawn.inventory.innerContainer.Count; i++)
            {
                ThingWithComps weapon = pawn.inventory.innerContainer[i] as ThingWithComps;
                if (weapon == null || !WeaponFilter.IsWeaponDef(weapon.def))
                {
                    continue;
                }
                if (!weapon.def.IsRangedWeapon && !weapon.def.IsMeleeWeapon)
                {
                    // Keep the count to combat weapons; tools should not satisfy the
                    // backup target or appear as replacement targets.
                    continue;
                }
                float score = CombatScoring.Compute(context, weapon, out _);
                analysis.Sidearms.Add(weapon);
                analysis.CarriedSidearms.Add(new CarriedSidearm
                {
                    Thing = weapon,
                    Score = score
                });
                labels.Add(string.Format("{0} · score {1:0}", weapon.LabelCap, score));
            }
            analysis.SidearmText = labels.Count == 0 ? "none" : string.Join(", ", labels);
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
            Dictionary<ThingDef, int> owned = new Dictionary<ThingDef, int>();
            Dictionary<ThingDef, int> forbidden = new Dictionary<ThingDef, int>();

            for (int i = 0; i < snapshot.Pawns.Count; i++)
            {
                PawnAnalysis pawn = snapshot.Pawns[i];
                AddCount(owned, DefOf(pawn.Current));
                for (int j = 0; j < pawn.Sidearms.Count; j++)
                {
                    AddCount(owned, DefOf(pawn.Sidearms[j]));
                }
            }

            for (int i = 0; i < snapshot.Weapons.Count; i++)
            {
                WeaponInfo info = snapshot.Weapons[i];
                if (info == null || info.Thing == null)
                {
                    continue;
                }
                AddCount(owned, info.Thing.def);
                if (info.Thing.IsForbidden(Faction.OfPlayer))
                {
                    AddCount(forbidden, info.Thing.def);
                }
            }

            // Defs the colony owns belong in the candidate universe even without a recipe
            // (persona weapons, quest rewards). Create them before reading the catalog.
            List<ThingDef> ownedDefs = new List<ThingDef>(owned.Keys);
            for (int i = 0; i < ownedDefs.Count; i++)
            {
                WeaponCatalog.GetOrCreate(ownedDefs[i]);
            }
            WeaponCatalog.RefreshAvailability(snapshot.Map);

            List<WeaponSource> universe = WeaponCatalog.All;
            ArmorySettings settings = ArmoryMod.Settings;
            float threshold = settings == null ? 0.1f : settings.upgradeThreshold;
            int minimumAge = settings == null ? 0 : settings.minimumAge;

            // Two option groups per pawn: what can be obtained now, and what research
            // would unlock. Every qualifying def is listed, so a pawn can appear in
            // several rows; the sections keep immediate crafting ahead of research and
            // trader-only weapons last.
            List<WeaponSource> craftSources = new List<WeaponSource>();
            List<WeaponSource> researchSources = new List<WeaponSource>();
            for (int i = 0; i < universe.Count; i++)
            {
                WeaponSource source = universe[i];
                if (source == null || source.Probe == null || source.ProbePower <= 0f ||
                    !source.Obtainable)
                {
                    continue;
                }

                // A def the colony already owns is usable supply even when its recipe is
                // still research-locked: the pawn would carry the copy we have, and the
                // plan should say "unforbid/transfer it" rather than "craft a worse knife".
                bool isOwned = owned.ContainsKey(source.Def);
                if (source.AvailableNow || source.MissingResearch == null || isOwned)
                {
                    craftSources.Add(source);
                }
                else
                {
                    researchSources.Add(source);
                }
            }

            Dictionary<ThingDef, DemandRow> rows = new Dictionary<ThingDef, DemandRow>();
            Dictionary<ThingDef, int> plannedCounts = new Dictionary<ThingDef, int>();
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
                    AddSlotWants(rows, pawn, context, craftSources, researchSources, true,
                        pawn.HandBaselineScore, threshold, LabelOf(pawn.Current),
                        plannedCounts, variety);
                }

                // Backups need SS's per-pawn memory; without it Armory has no typed slot.
                if (!pawn.HasRole || pawn.SidearmState == null)
                {
                    continue;
                }

                // Match the planner: an upgrade replaces the *worst* carried backup, so a
                // pawn with one good and one junk backup still creates demand.
                float backupBaseline;
                ThingWithComps worstBackup;
                TryGetWorstBackup(pawn, out backupBaseline, out worstBackup);
                AddSlotWants(rows, pawn, context, craftSources, researchSources, false,
                    backupBaseline, threshold, LabelOf(worstBackup),
                    plannedCounts, variety);
            }

            foreach (KeyValuePair<ThingDef, DemandRow> pair in rows)
            {
                DemandRow row = pair.Value;
                row.Owned = CountOf(owned, row.Def);
                row.Forbidden = CountOf(forbidden, row.Def);

                // Keep the best option even when it is already covered, so the ideal
                // hand/backup is visible and not replaced by a wall of grey alternatives.
                if (row.Need <= 0 && row.BestCount <= 0)
                {
                    continue;
                }

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
                else if (row.Buyable)
                {
                    row.Section = DemandSection.Buy;
                }
                else
                {
                    // Unreachable: picks come from Obtainable sources only.
                    row.Section = DemandSection.Buy;
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
        private static void AddSlotWants(Dictionary<ThingDef, DemandRow> rows,
            PawnAnalysis pawn, PawnFitContext context, List<WeaponSource> craftSources,
            List<WeaponSource> researchSources, bool hand, float baseline, float threshold,
            string currentLabel, Dictionary<ThingDef, int> plannedCounts, float variety)
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
                AddWant(rows, pawn, craftSources[i], score, hand, baseline, currentLabel, isTop,
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
                AddWant(rows, pawn, researchSources[i], score, hand, baseline, currentLabel,
                    isTop, plannedCounts);
            }
        }

        /// <summary>
        /// Scores one candidate for a slot, applying def-level rules and the minimum
        /// improvement. Returns false when the weapon does not belong in that slot or
        /// would not be an upgrade.
        /// </summary>
        private static bool TryScoreCandidate(PawnAnalysis pawn, PawnFitContext context,
            WeaponSource source, bool hand, float requiredScore,
            Dictionary<ThingDef, int> plannedCounts, float variety, out float score)
        {
            score = 0f;
            if (source == null || source.Probe == null || source.ProbePower <= 0f ||
                !source.Obtainable)
            {
                return false;
            }

            ThingDef def = source.Def;
            if (hand ? !pawn.IsHandType(def) : !pawn.IsBackupType(def))
            {
                return false;
            }

            if (!hand && pawn.AlreadyCarries(def))
            {
                // Do not propose a second copy of a weapon the pawn already carries.
                return false;
            }

            if (Eligibility.IsUnbondedPersona(source.Probe))
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
                if (!IsUsableAsBackup(source.Probe) ||
                    !SimpleSidearmsAdapter.IsValidSidearmForPawn(pawn.Pawn, source.Probe,
                        out reason))
                {
                    return false;
                }
            }

            score = CombatScoring.Compute(context, source.Probe, out _);
            if (variety > 0.001f && plannedCounts != null)
            {
                int count;
                if (plannedCounts.TryGetValue(def, out count) && count > 0)
                {
                    // Sequential soft cap: after one pawn is planned for a def, the
                    // next copy is slightly less attractive, and so on. The first
                    // copy is untouched.
                    score /= 1f + variety * 0.15f * count;
                }
            }
            return score > requiredScore;
        }

        private static void AddWant(Dictionary<ThingDef, DemandRow> rows, PawnAnalysis pawn,
            WeaponSource source, float score, bool hand, float baseline, string currentLabel,
            bool isTop, Dictionary<ThingDef, int> plannedCounts)
        {
            DemandRow row;
            if (!rows.TryGetValue(source.Def, out row))
            {
                row = new DemandRow
                {
                    Def = source.Def,
                    Label = source.Def.LabelCap,
                    Power = source.ProbePower,
                    Range = WeaponPower.GetRange(source.Probe)
                };
                rows[source.Def] = row;
            }

            row.TotalScore += score;

            bool typeSwitch = hand && pawn.HandTypeMismatch;
            bool isNew = baseline <= 0.01f && !typeSwitch;
            float gain = isNew || typeSwitch ? 0f : score / baseline - 1f;

            if (hand)
            {
                row.WantedHand++;
            }
            else
            {
                row.WantedBackup++;
            }

            row.Wants.Add(new DemandWant
            {
                Colonist = pawn.Pawn.LabelShort,
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
                    plannedCounts.TryGetValue(source.Def, out count);
                    plannedCounts[source.Def] = count + 1;
                }
            }
            row.KnownCount += isNew || typeSwitch ? 0 : 1;
            row.NewCount += isNew ? 1 : 0;
            row.TypeSwitchCount += typeSwitch ? 1 : 0;
            row.KnownGain += isNew ? 0f : gain;
            row.TotalGain += isNew ? 1f : gain;
        }

        private static ThingDef DefOf(ThingWithComps thing)
        {
            return thing == null ? null : thing.def;
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
                if (!pawn.IsBackupType(carried.Thing.def))
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

        private static int CountOf(Dictionary<ThingDef, int> counts, ThingDef def)
        {
            int count;
            return def != null && counts.TryGetValue(def, out count) ? count : 0;
        }

        private static void AddCount(Dictionary<ThingDef, int> counts, ThingDef def)
        {
            if (def == null)
            {
                return;
            }
            int count;
            counts.TryGetValue(def, out count);
            counts[def] = count + 1;
        }
    }
}
