using System;
using System.Collections.Generic;
using RimWorld;
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

                    if (candidate.Score > row.BestAnyScore)
                    {
                        row.BestAnyScore = candidate.Score;
                        row.BestAnyPawn = analysis;
                    }
                    if (candidate.Ready && candidate.Score > row.BestScore)
                    {
                        row.BestScore = candidate.Score;
                        row.BestPawn = analysis;
                    }
                }
            }

            snapshot.Pawns.Sort((a, b) =>
                string.Compare(a.Pawn.LabelShort, b.Pawn.LabelShort, StringComparison.OrdinalIgnoreCase));

            for (int i = 0; i < snapshot.Weapons.Count; i++)
            {
                WeaponAnalysis row = rows[snapshot.Weapons[i]];
                row.PawnCandidates.Sort((a, b) => b.Score.CompareTo(a.Score));
                row.Status = row.BestPawn != null
                    ? "Wanted"
                    : row.BestAnyPawn != null ? "Only blocked" : "No user";
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
            if (weapon == null || !weapon.Spawned || weapon.def == null)
            {
                return false;
            }
            ThingDef def = weapon.def;
            if (def.category != ThingCategory.Item || def.IsStuff || def.IsDrug || def.IsIngestible ||
                def.IsMedicine || def.IsApparel || def.IsShell)
            {
                return false;
            }
            return def.IsRangedWeapon || def.IsMeleeWeapon || def.Verbs != null && def.Verbs.Count > 0;
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

            if (analysis.Current != null)
            {
                analysis.CurrentScore = WeaponPower.Get(analysis.Current, powerCache) *
                                        PawnFit.Compute(context, analysis.Current);
            }

            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponInfo info = weapons[i];
                if (info.Thing == analysis.Current)
                {
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
            return analysis;
        }

        private static void BuildDemand(ArmorySnapshot snapshot)
        {
            Dictionary<ThingDef, int> wanted = new Dictionary<ThingDef, int>();
            Dictionary<ThingDef, int> blockedWishes = new Dictionary<ThingDef, int>();
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

            for (int i = 0; i < snapshot.Pawns.Count; i++)
            {
                PawnAnalysis pawn = snapshot.Pawns[i];
                if (!pawn.Available)
                {
                    continue;
                }
                Candidate top = pawn.TopReady;
                if (top == null)
                {
                    top = pawn.TopOverall;
                    if (top == null || pawn.CurrentScore >= top.Score)
                    {
                        continue;
                    }
                    ThingDef blockedDef = top.Weapon.Thing.def;
                    int blockedCount;
                    blockedWishes.TryGetValue(blockedDef, out blockedCount);
                    blockedWishes[blockedDef] = blockedCount + 1;
                    continue;
                }
                if (pawn.Current != null && pawn.Current == top.Weapon.Thing)
                {
                    continue;
                }
                if (pawn.CurrentScore >= top.Score)
                {
                    continue;
                }
                ThingDef def = top.Weapon.Thing.def;
                int count;
                wanted.TryGetValue(def, out count);
                wanted[def] = count + 1;
            }

            HashSet<ThingDef> defs = new HashSet<ThingDef>(wanted.Keys);
            defs.UnionWith(blockedWishes.Keys);
            defs.UnionWith(stock.Keys);

            foreach (ThingDef def in defs)
            {
                int wantCount;
                int blockedCount;
                int stockCount;
                int forbiddenCount;
                wanted.TryGetValue(def, out wantCount);
                blockedWishes.TryGetValue(def, out blockedCount);
                stock.TryGetValue(def, out stockCount);
                forbidden.TryGetValue(def, out forbiddenCount);
                if (wantCount <= 0 && blockedCount <= 0)
                {
                    continue;
                }
                snapshot.Demand.Add(new DemandRow
                {
                    Def = def,
                    Label = def.LabelCap,
                    Wanted = wantCount,
                    Blocked = blockedCount,
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
                return byWanted != 0
                    ? byWanted
                    : string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
            });
        }
    }
}
