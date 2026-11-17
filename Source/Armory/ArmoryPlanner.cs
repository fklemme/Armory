using System.Collections.Generic;
using Armory.Compatibility;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Armory
{
    /// <summary>
    /// Issues equip jobs for the best ready weapon per pawn. Opt-in, rate-limited, and
    /// deliberately conservative: no unforbidding, no breaking persona bonds, no stealing.
    /// </summary>
    public static class ArmoryPlanner
    {
        private sealed class Claim
        {
            public int PawnId;
            public int ExpiresTick;
        }

        private static readonly Dictionary<int, Claim> claims = new Dictionary<int, Claim>();
        private static readonly Dictionary<int, int> lastSwap = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> lastKnownWeapon = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> lastIssuedWeapon = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> manualUntil = new Dictionary<int, int>();
        private static int lastPruneTick = -1;

        public static void Reset()
        {
            claims.Clear();
            lastSwap.Clear();
            lastKnownWeapon.Clear();
            lastIssuedWeapon.Clear();
            manualUntil.Clear();
            lastPruneTick = -1;
        }

        public static void Run(Map map)
        {
            ArmorySettings settings = ArmoryMod.Settings;
            if (settings == null || !settings.autoEquipEnabled || !ArmoryMod.AutomationActive)
            {
                return;
            }
            if (map == null)
            {
                return;
            }

            ArmoryMapComponent component = map.GetComponent<ArmoryMapComponent>();
            if (component == null)
            {
                return;
            }
            ArmorySnapshot snapshot = component.GetSnapshot(false);
            if (snapshot == null)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            Prune(now);

            List<PawnAnalysis> order = new List<PawnAnalysis>(snapshot.Pawns);
            order.Sort((a, b) => b.TopReadyDeltaPct.CompareTo(a.TopReadyDeltaPct));

            for (int i = 0; i < order.Count; i++)
            {
                PawnAnalysis analysis = order[i];
                Pawn pawn = analysis.Pawn;

                if (!CanManage(pawn, map, settings))
                {
                    continue;
                }
                if (settings.respectSimpleSidearms && analysis.SidearmState != null &&
                    analysis.SidearmState.AutomationVeto)
                {
                    // Simple Sidearms forced weapon/unarmed: that is the player's decision.
                    continue;
                }
                if (settings.respectManualEquip && IsManualOverride(pawn, settings, now))
                {
                    continue;
                }
                if (now - LastSwap(pawn, now) < settings.swapCooldownTicks)
                {
                    continue;
                }

                float currentScore = analysis.CurrentScore;
                float baselineScore = Mathf.Max(currentScore, analysis.BestCarriedPrimaryScore);
                float minimumScore = baselineScore > 0.01f
                    ? baselineScore * (1f + settings.upgradeThreshold)
                    : 0.01f;

                for (int c = 0; c < analysis.Ranked.Count; c++)
                {
                    Candidate candidate = analysis.Ranked[c];
                    if (!candidate.Ready)
                    {
                        continue;
                    }
                    if (candidate.Score <= minimumScore)
                    {
                        break;
                    }

                    ThingWithComps weapon = candidate.Weapon.Thing;
                    if (weapon == null || !weapon.Spawned || weapon.Map != map)
                    {
                        continue;
                    }

                    Claim claim;
                    if (claims.TryGetValue(weapon.thingIDNumber, out claim) &&
                        claim.PawnId != pawn.thingIDNumber &&
                        claim.ExpiresTick > now)
                    {
                        continue;
                    }

                    string reason;
                    bool hard;
                    if (!Eligibility.Check(pawn, weapon, true, out reason, out hard))
                    {
                        continue;
                    }

                    Job job = JobMaker.MakeJob(JobDefOf.Equip, weapon);
                    if (!pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, true))
                    {
                        if (settings.debugLogging)
                        {
                            Log.Warning("[Armory] " + pawn.LabelShort + " rejected an Equip job for " + weapon.LabelCap);
                        }
                        continue;
                    }

                    lastSwap[pawn.thingIDNumber] = now;
                    lastIssuedWeapon[pawn.thingIDNumber] = weapon.thingIDNumber;
                    claims[weapon.thingIDNumber] = new Claim
                    {
                        PawnId = pawn.thingIDNumber,
                        ExpiresTick = now + 900
                    };

                    string detail;
                    if (baselineScore <= 0.01f)
                    {
                        detail = "unarmed";
                    }
                    else
                    {
                        string baselineLabel = analysis.Current == null
                            ? "unarmed"
                            : analysis.Current.LabelCap;
                        if (analysis.BestCarriedPrimary != null &&
                            analysis.BestCarriedPrimaryScore > currentScore)
                        {
                            baselineLabel = analysis.BestCarriedPrimary.LabelCap + " (carried)";
                        }
                        detail = string.Format("+{0}% over {1}",
                            Mathf.RoundToInt((candidate.Score / baselineScore - 1f) * 100f),
                            baselineLabel);
                    }
                    ArmoryGameComponent.Record(now, pawn, weapon, "Queued", detail);

                    if (settings.debugLogging)
                    {
                        Log.Message(string.Format("[Armory] queued {0} for {1} ({2})",
                            weapon.LabelCap, pawn.LabelShort, detail));
                    }
                    break;
                }
            }
        }

        private static bool CanManage(Pawn pawn, Map map, ArmorySettings settings)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map != map)
            {
                return false;
            }
            if (!pawn.IsColonist || pawn.IsSlave || pawn.IsPrisoner)
            {
                return false;
            }
            if (pawn.Dead || pawn.Downed || pawn.InMentalState)
            {
                return false;
            }
            if (pawn.ageTracker == null || pawn.ageTracker.AgeBiologicalYears < settings.minimumAge)
            {
                return false;
            }
            if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return false;
            }
            if (pawn.health == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                return false;
            }
            if (pawn.Drafted && !settings.equipDrafted)
            {
                return false;
            }
            if (pawn.jobs == null)
            {
                return false;
            }
            if (pawn.jobs.curJob != null && pawn.jobs.curJob.def == JobDefOf.Equip)
            {
                return false;
            }
            if (SimpleSidearmsAdapter.IsSidearmJobRunning(pawn))
            {
                return false;
            }
            if (pawn.equipment == null || pawn.equipment.bondedWeapon != null)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// If the current weapon changed and it was not the weapon we queued, the player (or
        /// another mod) intervened. Back off for a while instead of fighting the choice.
        /// </summary>
        private static bool IsManualOverride(Pawn pawn, ArmorySettings settings, int now)
        {
            int pawnId = pawn.thingIDNumber;
            int currentId = pawn.equipment != null && pawn.equipment.Primary != null
                ? pawn.equipment.Primary.thingIDNumber
                : 0;

            int knownId;
            if (!lastKnownWeapon.TryGetValue(pawnId, out knownId))
            {
                lastKnownWeapon[pawnId] = currentId;
                return false;
            }

            if (currentId != knownId)
            {
                lastKnownWeapon[pawnId] = currentId;
                int issuedId;
                bool ours = lastIssuedWeapon.TryGetValue(pawnId, out issuedId) && issuedId == currentId;
                if (ours)
                {
                    manualUntil.Remove(pawnId);
                }
                else
                {
                    manualUntil[pawnId] = now + settings.manualCooldownTicks;
                    if (settings.debugLogging)
                    {
                        Log.Message(string.Format("[Armory] manual weapon change for {0}; pausing {1} ticks",
                            pawn.LabelShort, settings.manualCooldownTicks));
                    }
                }
            }

            int until;
            return manualUntil.TryGetValue(pawnId, out until) && now < until;
        }

        private static int LastSwap(Pawn pawn, int now)
        {
            int tick;
            if (!lastSwap.TryGetValue(pawn.thingIDNumber, out tick))
            {
                return -999999;
            }
            return now < tick ? -999999 : tick;
        }

        private static void Prune(int now)
        {
            if (lastPruneTick >= 0 && now - lastPruneTick < 2000)
            {
                return;
            }
            lastPruneTick = now;

            List<int> remove = new List<int>();
            foreach (KeyValuePair<int, Claim> pair in claims)
            {
                if (pair.Value.ExpiresTick <= now || now < pair.Value.ExpiresTick - 100000)
                {
                    remove.Add(pair.Key);
                }
            }
            for (int i = 0; i < remove.Count; i++)
            {
                claims.Remove(remove[i]);
            }

            remove.Clear();
            foreach (KeyValuePair<int, int> pair in lastSwap)
            {
                if (now < pair.Value || now - pair.Value > 30000)
                {
                    remove.Add(pair.Key);
                }
            }
            for (int i = 0; i < remove.Count; i++)
            {
                lastSwap.Remove(remove[i]);
            }

            remove.Clear();
            foreach (KeyValuePair<int, int> pair in manualUntil)
            {
                if (pair.Value <= now || now < pair.Value - 100000)
                {
                    remove.Add(pair.Key);
                }
            }
            for (int i = 0; i < remove.Count; i++)
            {
                manualUntil.Remove(remove[i]);
            }
        }
    }
}
