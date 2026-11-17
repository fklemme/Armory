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
                    Power = WeaponPower.Get(weapon, powerCache)
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
                    Status = "Unused"
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

            snapshot.Pawns.Sort((a, b) =>
                string.Compare(a.Pawn.LabelShort, b.Pawn.LabelShort, StringComparison.OrdinalIgnoreCase));

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
                    row.Status = "Hand blocked";
                }
                else if (row.BestAnyBackupPawn != null)
                {
                    row.Status = "Backup blocked";
                }
                else
                {
                    row.Status = "No user";
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
            CollectSidearms(analysis, context, powerCache);

            if (analysis.Current != null)
            {
                analysis.CurrentScore = WeaponPower.Get(analysis.Current, powerCache) *
                                        PawnFit.Compute(context, analysis.Current);
            }

            analysis.SidearmState = SimpleSidearmsAdapter.GetState(pawn);
            ResolveRole(analysis, context);
            ResolveBestCarriedBackup(analysis);

            // Simple Sidearms has an explicit hand role. Use SS's own QuickSwitch choice
            // among carried weapons as the hand baseline; do not assume the best carried
            // weapon by Armory's score, because SS may skip it (manual use, dangerous,
            // EMP, role) and Armory would then block a valid upgrade.
            if (analysis.HasRole && SimpleSidearmsAdapter.RespectEnabled)
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
                    Score = info.Power * PawnFit.Compute(context, info.Thing),
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
        /// Resolves the hand slot type. SS's per-pawn primary mode wins; otherwise the
        /// current weapon, then the pawn's better combat skill. When Simple Sidearms is
        /// absent (or the player turned respect off), there is no typed slot model and
        /// every weapon competes for the single vanilla hand slot.
        /// </summary>
        private static void ResolveRole(PawnAnalysis analysis, PawnFitContext context)
        {
            if (!SimpleSidearmsAdapter.Active || !SimpleSidearmsAdapter.RespectEnabled)
            {
                analysis.RoleIsRanged = null;
                analysis.RoleSource = null;
                return;
            }

            if (analysis.SidearmState != null && analysis.SidearmState.PreferredPrimaryIsRanged.HasValue)
            {
                analysis.RoleIsRanged = analysis.SidearmState.PreferredPrimaryIsRanged.Value;
                analysis.RoleSource = "SS mode";
                return;
            }

            if (analysis.Current != null && analysis.Current.def != null)
            {
                if (analysis.Current.def.IsRangedWeapon)
                {
                    analysis.RoleIsRanged = true;
                    analysis.RoleSource = "current weapon";
                    return;
                }
                if (analysis.Current.def.IsMeleeWeapon)
                {
                    analysis.RoleIsRanged = false;
                    analysis.RoleSource = "current weapon";
                    return;
                }
            }

            analysis.RoleIsRanged = context.Shooting >= context.Melee;
            analysis.RoleSource = "combat skill";
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
                    ConsiderCarried(analysis, context, ssChoice, powerCache);
                }
                return;
            }

            if (SimpleSidearmsAdapter.PreferredPrimaryCheckFailed)
            {
                // Reflection failed; fall back to the best weapon already in the pawn's
                // possession so the plan does not stall, but keep the once-only warning.
                ConsiderCarried(analysis, context, analysis.Current, powerCache);
                for (int i = 0; i < analysis.Sidearms.Count; i++)
                {
                    ConsiderCarried(analysis, context, analysis.Sidearms[i], powerCache);
                }
                return;
            }

            // SS would not switch to any carried weapon. The equipped role-type weapon is
            // still the baseline; a carried weapon SS skips (manual use, dangerous, EMP)
            // must not block an upgrade.
            if (analysis.Current != null && analysis.IsHandType(analysis.Current.def))
            {
                ConsiderCarried(analysis, context, analysis.Current, powerCache);
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

                SidearmCandidate candidate = new SidearmCandidate
                {
                    Weapon = info,
                    Score = info.Power * PawnFit.Compute(context, info.Thing),
                    Blocker = reason
                };
                if (!candidate.Ready && analysis.Available &&
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

        private static void CollectSidearms(PawnAnalysis analysis, PawnFitContext context,
            Dictionary<int, float> powerCache)
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
                float score = WeaponPower.Get(weapon, powerCache) * PawnFit.Compute(context, weapon);
                analysis.Sidearms.Add(weapon);
                analysis.CarriedSidearms.Add(new CarriedSidearm
                {
                    Thing = weapon,
                    Score = score
                });
                labels.Add(string.Format("{0} ({1:0})", weapon.LabelCap, score));
            }
            analysis.SidearmText = labels.Count == 0 ? "none" : string.Join(", ", labels);
        }

        private static void ConsiderCarried(PawnAnalysis analysis, PawnFitContext context,
            ThingWithComps weapon, Dictionary<int, float> powerCache)        {
            if (weapon == null || !analysis.IsHandType(weapon.def))
            {
                return;
            }
            float score = WeaponPower.Get(weapon, powerCache) * PawnFit.Compute(context, weapon);
            if (score > analysis.BestCarriedPrimaryScore)
            {
                analysis.BestCarriedPrimaryScore = score;
                analysis.BestCarriedPrimary = weapon;
            }
        }

        private static void BuildDemand(ArmorySnapshot snapshot)
        {
            Dictionary<ThingDef, int> wantedHand = new Dictionary<ThingDef, int>();
            Dictionary<ThingDef, int> wantedBackup = new Dictionary<ThingDef, int>();
            Dictionary<ThingDef, int> blockedHand = new Dictionary<ThingDef, int>();
            Dictionary<ThingDef, int> blockedBackup = new Dictionary<ThingDef, int>();
            Dictionary<ThingDef, int> stock = new Dictionary<ThingDef, int>();
            Dictionary<ThingDef, int> forbidden = new Dictionary<ThingDef, int>();

            for (int i = 0; i < snapshot.Weapons.Count; i++)
            {
                WeaponInfo info = snapshot.Weapons[i];
                ThingDef def = info.Thing.def;
                int count;
                stock.TryGetValue(def, out count);
                stock[def] = count + 1;

                if (info.Thing.IsForbidden(Faction.OfPlayer))
                {
                    forbidden.TryGetValue(def, out count);
                    forbidden[def] = count + 1;
                }
            }

            ArmorySettings settings = ArmoryMod.Settings;
            float threshold = settings == null ? 0.1f : settings.upgradeThreshold;
            bool upgradeBackups = settings == null || settings.replaceWorseSidearms;

            for (int i = 0; i < snapshot.Pawns.Count; i++)
            {
                PawnAnalysis pawn = snapshot.Pawns[i];
                if (!pawn.Available || pawn.AutomationBlocked)
                {
                    continue;
                }

                // Hand slot: the best hand candidate compared with the hand baseline.
                Candidate handTop = pawn.TopReady;
                bool handReady = handTop != null;
                if (handTop == null)
                {
                    handTop = pawn.TopOverall;
                }
                if (handTop != null && handTop.Weapon.Thing != pawn.Current)
                {
                    float baseline = pawn.HandBaselineScore;
                    if (handTop.Score > baseline)
                    {
                        AddCount(handReady ? wantedHand : blockedHand, handTop.Weapon.Thing.def);
                    }
                }

                if (!pawn.HasRole)
                {
                    continue;
                }

                // Backup slot: add the missing opposite type, or upgrade the worst one.
                int backupCount = 0;
                float worstBackup = 0f;
                for (int j = 0; j < pawn.CarriedSidearms.Count; j++)
                {
                    CarriedSidearm carried = pawn.CarriedSidearms[j];
                    if (!pawn.IsBackupType(carried.Thing.def))
                    {
                        continue;
                    }
                    if (backupCount == 0 || carried.Score < worstBackup)
                    {
                        worstBackup = carried.Score;
                    }
                    backupCount++;
                }

                SidearmCandidate bestAddable = null;
                SidearmCandidate bestUpgradeable = null;
                SidearmCandidate bestAny = null;
                for (int j = 0; j < pawn.SidearmCandidates.Count; j++)
                {
                    SidearmCandidate sidearm = pawn.SidearmCandidates[j];
                    if (bestAny == null)
                    {
                        bestAny = sidearm;
                    }
                    if (sidearm.Ready && bestAddable == null)
                    {
                        bestAddable = sidearm;
                    }
                    if ((sidearm.Ready || sidearm.UpgradeAfterDrop) && bestUpgradeable == null)
                    {
                        bestUpgradeable = sidearm;
                    }
                }

                if (backupCount == 0)
                {
                    if (bestAddable != null)
                    {
                        AddCount(wantedBackup, bestAddable.Weapon.Thing.def);
                    }
                    else if (bestAny != null)
                    {
                        AddCount(blockedBackup, bestAny.Weapon.Thing.def);
                    }
                }
                else if (upgradeBackups)
                {
                    if (bestUpgradeable != null &&
                        bestUpgradeable.Score > Mathf.Max(worstBackup, 0.01f) * (1f + threshold))
                    {
                        AddCount(wantedBackup, bestUpgradeable.Weapon.Thing.def);
                    }
                    else if (bestAny != null && bestAny.Score > worstBackup)
                    {
                        AddCount(blockedBackup, bestAny.Weapon.Thing.def);
                    }
                }
            }

            HashSet<ThingDef> defs = new HashSet<ThingDef>(wantedHand.Keys);
            defs.UnionWith(wantedBackup.Keys);
            defs.UnionWith(blockedHand.Keys);
            defs.UnionWith(blockedBackup.Keys);
            defs.UnionWith(stock.Keys);

            foreach (ThingDef def in defs)
            {
                int wantHand;
                int wantBackup;
                int blockHand;
                int blockBackup;
                int stockCount;
                int forbiddenCount;
                wantedHand.TryGetValue(def, out wantHand);
                wantedBackup.TryGetValue(def, out wantBackup);
                blockedHand.TryGetValue(def, out blockHand);
                blockedBackup.TryGetValue(def, out blockBackup);
                stock.TryGetValue(def, out stockCount);
                forbidden.TryGetValue(def, out forbiddenCount);
                if (wantHand + wantBackup + blockHand + blockBackup <= 0)
                {
                    continue;
                }
                snapshot.Demand.Add(new DemandRow
                {
                    Def = def,
                    Label = def.LabelCap,
                    WantedHand = wantHand,
                    WantedBackup = wantBackup,
                    BlockedHand = blockHand,
                    BlockedBackup = blockBackup,
                    Stock = stockCount,
                    Forbidden = forbiddenCount
                });
            }

            snapshot.Demand.Sort((a, b) =>
            {
                int byShortage = b.Shortage.CompareTo(a.Shortage);
                if (byShortage != 0)
                {
                    return byShortage;
                }
                int byBlocked = b.Blocked.CompareTo(a.Blocked);
                if (byBlocked != 0)
                {
                    return byBlocked;
                }
                int byForbidden = b.Forbidden.CompareTo(a.Forbidden);
                if (byForbidden != 0)
                {
                    return byForbidden;
                }
                int byWanted = b.Wanted.CompareTo(a.Wanted);
                if (byWanted != 0)
                {
                    return byWanted;
                }
                return string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
            });
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
