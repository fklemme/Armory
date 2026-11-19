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
        private const int FailedDropMemoryTicks = 60000;

        private sealed class Claim
        {
            public int PawnId;
            public int ExpiresTick;
        }

        private static readonly Dictionary<int, Claim> claims = new Dictionary<int, Claim>();
        private static readonly Dictionary<int, int> lastSwap = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> lastSidearm = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> lastKnownWeapon = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> lastIssuedWeapon = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> manualUntil = new Dictionary<int, int>();
        private static readonly Dictionary<long, int> failedDropUntil = new Dictionary<long, int>();
        private static int lastPruneTick = -1;
        private static JobDef equipSecondaryDef;
        private static bool equipSecondaryResolved;

        public static void Reset()
        {
            claims.Clear();
            lastSwap.Clear();
            lastSidearm.Clear();
            lastKnownWeapon.Clear();
            lastIssuedWeapon.Clear();
            manualUntil.Clear();
            failedDropUntil.Clear();
            lastPruneTick = -1;
            equipSecondaryDef = null;
            equipSecondaryResolved = false;
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
            Dictionary<int, bool> queuedPrimaryRanged = new Dictionary<int, bool>();
            Dictionary<ThingDef, int> plannedHandDefs = new Dictionary<ThingDef, int>();
            float variety = settings.varietyStrength;

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
                if (analysis.HandLocked)
                {
                    // The bonded persona weapon is in hand. Do not replace it; the
                    // separate backup pass can still maintain the off-hand weapon.
                    continue;
                }

                float baselineScore = analysis.HandBaselineScore;
                float minimumScore = baselineScore > 0.01f
                    ? baselineScore * (1f + settings.upgradeThreshold)
                    : 0.01f;

                List<Candidate> choices = new List<Candidate>(analysis.Ranked);
                choices.Sort((a, b) =>
                    EffectiveHandScore(b, plannedHandDefs, variety).CompareTo(
                        EffectiveHandScore(a, plannedHandDefs, variety)));

                for (int c = 0; c < choices.Count; c++)
                {
                    Candidate candidate = choices[c];
                    if (!candidate.Ready)
                    {
                        continue;
                    }
                    float effectiveScore = EffectiveHandScore(candidate, plannedHandDefs, variety);
                    if (effectiveScore <= minimumScore)
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
                    queuedPrimaryRanged[pawn.thingIDNumber] = weapon.def.IsRangedWeapon;
                    claims[weapon.thingIDNumber] = new Claim
                    {
                        PawnId = pawn.thingIDNumber,
                        ExpiresTick = now + 900
                    };

                    string detail;
                    if (baselineScore <= 0.01f)
                    {
                        if (analysis.Current == null || !analysis.HasRole)
                        {
                            detail = "No hand weapon equipped";
                        }
                        else
                        {
                            // The pawn holds the other type (SS has not switched yet).
                            detail = analysis.RoleIsRanged.Value
                                ? "No ranged hand weapon equipped"
                                : "No melee hand weapon equipped";
                        }
                    }
                    else
                    {
                        string baselineLabel = analysis.Current == null
                            ? "unarmed"
                            : analysis.Current.LabelCap;
                        bool carried = false;
                        if (analysis.BestCarriedPrimary != null &&
                            analysis.BestCarriedPrimary != analysis.Current &&
                            analysis.BestCarriedPrimaryScore >= analysis.HandBaselineScore)
                        {
                            baselineLabel = analysis.BestCarriedPrimary.LabelCap;
                            carried = true;
                        }
                        detail = string.Format("{0}% better than {1}{2}",
                            Mathf.RoundToInt((candidate.Score / baselineScore - 1f) * 100f),
                            carried ? "carried " : "equipped ",
                            baselineLabel);
                    }
                    ArmoryGameComponent.Record(now, pawn, weapon, DecisionKind.EquipHand, detail);
                    int assignedCount;
                    plannedHandDefs.TryGetValue(weapon.def, out assignedCount);
                    plannedHandDefs[weapon.def] = assignedCount + 1;

                    if (settings.debugLogging)
                    {
                        Log.Message(string.Format("[Armory] queued {0} for {1} ({2})",
                            weapon.LabelCap, pawn.LabelShort, detail));
                    }
                    break;
                }
            }

            if (settings.manageSidearms && SimpleSidearmsAdapter.CanCheckSidearms)
            {
                RunSidearms(map, snapshot, settings, now, queuedPrimaryRanged);
            }
        }

        /// <summary>
        /// Maintains the opposite-type backup slot. A ranged pawn gets one melee backup and
        /// a melee pawn gets one ranged backup. An upgrade replaces only the worst carried
        /// weapon of that same type; Armory never drops one type to add the other.
        /// </summary>
        private static void RunSidearms(Map map, ArmorySnapshot snapshot, ArmorySettings settings, int now,
            Dictionary<int, bool> queuedPrimaryRanged)
        {
            JobDef jobDef = GetEquipSecondaryJobDef();
            if (jobDef == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Pawns.Count; i++)
            {
                PawnAnalysis analysis = snapshot.Pawns[i];
                Pawn pawn = analysis.Pawn;

                if (!CanManage(pawn, map, settings) || !analysis.HasRole ||
                    analysis.SidearmState == null)
                {
                    continue;
                }
                if (settings.respectSimpleSidearms && analysis.SidearmState.AutomationVeto)
                {
                    continue;
                }
                if (now - LastSidearm(pawn, now) < settings.swapCooldownTicks)
                {
                    continue;
                }

                bool roleRanged = analysis.RoleIsRanged.Value;
                if (analysis.Current == null && !queuedPrimaryRanged.ContainsKey(pawn.thingIDNumber))
                {
                    // Backups complement a hand weapon; do not use them to arm an unarmed pawn.
                    continue;
                }
                // Wait until SS has actually switched the pawn to the role type. Adding the
                // other type while the wrong one is equipped invites SS to promote it.
                if (analysis.Current != null && analysis.Current.def != null)
                {
                    bool currentRanged = analysis.Current.def.IsRangedWeapon;
                    if (currentRanged != roleRanged)
                    {
                        continue;
                    }
                }

                int backupCount = 0;
                CarriedSidearm worst = null;
                for (int j = 0; j < analysis.CarriedSidearms.Count; j++)
                {
                    CarriedSidearm carried = analysis.CarriedSidearms[j];
                    if (!analysis.IsBackupType(carried.Thing.def))
                    {
                        continue;
                    }
                    backupCount++;
                    if (!IsDroppableBackup(carried.Thing))
                    {
                        continue;
                    }
                    if (worst == null || carried.Score < worst.Score)
                    {
                        worst = carried;
                    }
                }

                if (backupCount > 0 && (!settings.replaceWorseSidearms ||
                                        !SimpleSidearmsAdapter.CanDropSidearms || worst == null))
                {
                    continue;
                }

                float minimumScore = worst == null
                    ? 0f
                    : Mathf.Max(worst.Score, 0.01f) * (1f + settings.upgradeThreshold);

                for (int c = 0; c < analysis.SidearmCandidates.Count; c++)
                {
                    SidearmCandidate chosen = analysis.SidearmCandidates[c];
                    if (chosen.Score <= minimumScore)
                    {
                        break; // sorted by score
                    }
                    if (chosen.AlreadyCarried)
                    {
                        // The pawn already has this weapon def in the hand or bag. Adding
                        // another gladius/axe is not an upgrade, even if this instance
                        // happens to have a slightly better score.
                        continue;
                    }

                    ThingWithComps weapon = chosen.Weapon.Thing;
                    if (weapon == null || !weapon.Spawned || weapon.Map != map)
                    {
                        continue;
                    }

                    Claim claim;
                    if (claims.TryGetValue(weapon.thingIDNumber, out claim) &&
                        claim.PawnId != pawn.thingIDNumber && claim.ExpiresTick > now)
                    {
                        continue;
                    }

                    string worldReason;
                    if (!Eligibility.CheckSidearmWorld(pawn, weapon, true, out worldReason))
                    {
                        continue;
                    }

                    string rulesReason;
                    bool hard;
                    bool rulesOk = Eligibility.CheckSidearmRules(pawn, weapon, out rulesReason, out hard);
                    if (!rulesOk && worst == null)
                    {
                        continue;
                    }

                    bool dropFirst = false;
                    if (!rulesOk)
                    {
                        // At SS's slot or free-space cap, dropping the old backup is the only
                        // way to fit a better one. Verify the weapon itself is legal first so
                        // the pawn is not left without a backup for a weapon SS rejects anyway.
                        if (!SimpleSidearmsAdapter.CanCheckSidearmType ||
                            !SimpleSidearmsAdapter.CanCheckSidearmUse ||
                            !SimpleSidearmsAdapter.CanCheckRelativeLimits)
                        {
                            continue;
                        }
                        string typeReason;
                        string useReason;
                        if (!SimpleSidearmsAdapter.IsValidSidearmForPawn(pawn, weapon, out typeReason) ||
                            !SimpleSidearmsAdapter.CanUseSidearm(pawn, weapon, out useReason))
                        {
                            continue;
                        }
                        dropFirst = true;
                    }

                    float gain = worst == null
                        ? 0f
                        : (chosen.Score / Mathf.Max(worst.Score, 0.01f) - 1f) * 100f;

                    long dropKey = 0L;
                    if (dropFirst)
                    {
                        dropKey = FailedDropKey(pawn, weapon);
                        int failedUntil;
                        if (failedDropUntil.TryGetValue(dropKey, out failedUntil) &&
                            failedUntil > now)
                        {
                            // This exact drop-first upgrade already failed; do not drop the
                            // old backup again until the memory expires or SS has room now.
                            continue;
                        }

                        if (!DropBackup(pawn, worst, now))
                        {
                            failedDropUntil[dropKey] = now + FailedDropMemoryTicks;
                            continue;
                        }

                        string postReason;
                        bool postHard;
                        if (!Eligibility.CheckSidearmRules(pawn, weapon, out postReason, out postHard))
                        {
                            failedDropUntil[dropKey] = now + FailedDropMemoryTicks;
                            RestoreBackup(pawn, worst, jobDef, now, false);
                            ArmoryGameComponent.Record(now, pawn, weapon, DecisionKind.SkipUpgrade,
                                "Simple Sidearms refused it: " + postReason +
                                "; the old backup was restored");
                            break;
                        }
                    }

                    Job job = JobMaker.MakeJob(jobDef, weapon);
                    if (!pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, true))
                    {
                        if (settings.debugLogging)
                        {
                            Log.Warning("[Armory] " + pawn.LabelShort +
                                        " rejected an EquipSecondary job for " + weapon.LabelCap);
                        }
                        if (dropFirst)
                        {
                            failedDropUntil[dropKey] = now + FailedDropMemoryTicks;
                            RestoreBackup(pawn, worst, jobDef, now, true);
                        }
                        continue;
                    }

                    lastSidearm[pawn.thingIDNumber] = now;
                    claims[weapon.thingIDNumber] = new Claim
                    {
                        PawnId = pawn.thingIDNumber,
                        ExpiresTick = now + 900
                    };

                    string slot = roleRanged ? "melee" : "ranged";
                    string detail;
                    if (worst == null)
                    {
                        detail = "No " + slot + " backup carried";
                    }
                    else
                    {
                        detail = string.Format("{0}% better than carried {1}{2}",
                            Mathf.RoundToInt(gain),
                            worst.Thing.LabelCap,
                            dropFirst ? " (old one dropped first)" : string.Empty);
                    }
                    ArmoryGameComponent.Record(now, pawn, weapon, DecisionKind.EquipBackup, detail);

                    if (settings.debugLogging)
                    {
                        Log.Message(string.Format("[Armory] queued backup {0} for {1} ({2})",
                            weapon.LabelCap, pawn.LabelShort, detail));
                    }

                    if (!dropFirst && worst != null)
                    {
                        if (DropBackup(pawn, worst, now))
                        {
                            ArmoryGameComponent.Record(now, pawn, worst.Thing,
                                DecisionKind.DropBackup, "Replaced by " + weapon.LabelCap);
                        }
                    }
                    break;
                }
            }
        }

        private static float EffectiveHandScore(Candidate candidate,
            Dictionary<ThingDef, int> plannedDefs, float variety)
        {
            if (candidate == null || candidate.Weapon == null || candidate.Weapon.Thing == null)
            {
                return 0f;
            }

            float score = candidate.Score;
            if (variety > 0.001f && plannedDefs != null)
            {
                int count;
                if (plannedDefs.TryGetValue(candidate.Weapon.Thing.def, out count) && count > 0)
                {
                    score /= 1f + variety * 0.15f * count;
                }
            }
            return score;
        }

        private static bool IsDroppableBackup(ThingWithComps thing)
        {
            return thing != null && !thing.Destroyed && thing.def != null &&
                   (thing.def.IsRangedWeapon || thing.def.IsMeleeWeapon) &&
                   !thing.def.destroyOnDrop && thing.GetComp<CompBladelinkWeapon>() == null;
        }

        private static bool DropBackup(Pawn pawn, CarriedSidearm backup, int now)
        {
            if (backup == null || backup.Thing == null)
            {
                return false;
            }
            if (pawn.inventory == null || pawn.inventory.innerContainer == null ||
                !pawn.inventory.innerContainer.Contains(backup.Thing))
            {
                return false;
            }
            bool dropped = SimpleSidearmsAdapter.DropSidearm(pawn, backup.Thing, true, true);
            if (!dropped)
            {
                ArmoryGameComponent.Record(now, pawn, backup.Thing, DecisionKind.DropFailed,
                    "Simple Sidearms drop unavailable");
            }
            return dropped;
        }

        private static void RestoreBackup(Pawn pawn, CarriedSidearm backup, JobDef jobDef, int now,
            bool record)
        {
            if (backup == null || backup.Thing == null || backup.Thing.Destroyed ||
                !backup.Thing.Spawned)
            {
                return;
            }
            Job job = JobMaker.MakeJob(jobDef, backup.Thing);
            if (pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, true))
            {
                lastSidearm[pawn.thingIDNumber] = now;
                claims[backup.Thing.thingIDNumber] = new Claim
                {
                    PawnId = pawn.thingIDNumber,
                    ExpiresTick = now + 900
                };
                if (record)
                {
                    ArmoryGameComponent.Record(now, pawn, backup.Thing, DecisionKind.RestoreBackup,
                        "Upgrade job could not be queued");
                }
            }
            else
            {
                Log.Warning("[Armory] " + pawn.LabelShort + " could not restore dropped backup " +
                            backup.Thing.LabelCap);
            }
        }

        private static long FailedDropKey(Pawn pawn, ThingWithComps weapon)
        {
            return ((long)pawn.thingIDNumber << 32) ^ (uint)weapon.thingIDNumber;
        }

        private static JobDef GetEquipSecondaryJobDef()
        {
            if (equipSecondaryResolved)
            {
                return equipSecondaryDef;
            }
            equipSecondaryResolved = true;
            equipSecondaryDef = DefDatabase<JobDef>.GetNamedSilentFail("EquipSecondary");
            if (equipSecondaryDef == null && ArmoryMod.Settings != null && ArmoryMod.Settings.debugLogging)
            {
                Log.Warning("[Armory] Simple Sidearms EquipSecondary job def not found; sidearm management disabled.");
            }
            return equipSecondaryDef;
        }

        private static bool CanManage(Pawn pawn, Map map, ArmorySettings settings)
        {
            return GetHardBlock(pawn, map, settings) == null;
        }

        /// <summary>
        /// Hard gates that prevent any automation for this pawn, with a readable reason.
        /// Kept separate from cooldowns/pauses so demand can ignore hard-blocked pawns.
        /// </summary>
        public static string GetHardBlock(Pawn pawn, Map map, ArmorySettings settings)
        {
            if (settings == null)
            {
                return "settings not loaded";
            }
            if (pawn == null || !pawn.Spawned || pawn.Map != map)
            {
                return "not on the map";
            }
            if (!pawn.IsColonist || pawn.IsSlave || pawn.IsPrisoner)
            {
                return "not a free colonist";
            }
            if (pawn.Dead)
            {
                return "dead";
            }
            if (pawn.Downed)
            {
                return "downed";
            }
            if (pawn.InMentalState)
            {
                return "in a mental state";
            }
            if (pawn.ageTracker == null ||
                pawn.ageTracker.AgeBiologicalYears < settings.minimumAge)
            {
                return string.Format("under minimum age ({0})", settings.minimumAge);
            }
            if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return "incapable of violence";
            }
            if (pawn.health == null ||
                !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                return "cannot manipulate";
            }
            if (pawn.Drafted && !settings.equipDrafted)
            {
                return "drafted (equip drafted is off)";
            }
            if (pawn.jobs == null)
            {
                return "no job tracker";
            }
            if (pawn.jobs.curJob != null && pawn.jobs.curJob.def == JobDefOf.Equip)
            {
                return "already equipping";
            }
            if (SimpleSidearmsAdapter.IsSidearmJobRunning(pawn))
            {
                return "Simple Sidearms sidearm job running";
            }
            if (pawn.equipment == null)
            {
                return "no equipment tracker";
            }
            return null;
        }

        /// <summary>
        /// Read-only automation explanation for the UI: a hard gate, the manual-change
        /// pause, or the swap cooldown. Never mutates planner state.
        /// </summary>
        public static string GetAutomationNote(Pawn pawn, Map map, ArmorySettings settings,
            int now, out bool hard)
        {
            hard = false;
            string block = GetHardBlock(pawn, map, settings);
            if (block != null)
            {
                hard = true;
                return block;
            }

            if (settings.respectManualEquip)
            {
                int until;
                if (manualUntil.TryGetValue(pawn.thingIDNumber, out until) && now < until)
                {
                    int seconds = Mathf.Max(1, (until - now + 59) / 60);
                    return string.Format("manual change {0}s", seconds);
                }
            }

            int last = LastSwap(pawn, now);
            int elapsed = now - last;
            if (elapsed < settings.swapCooldownTicks)
            {
                int seconds = Mathf.Max(1, (settings.swapCooldownTicks - elapsed + 59) / 60);
                return string.Format("swap cooldown {0}s", seconds);
            }
            return null;
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

        private static int LastSidearm(Pawn pawn, int now)
        {
            int tick;
            if (!lastSidearm.TryGetValue(pawn.thingIDNumber, out tick))
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
            foreach (KeyValuePair<int, int> pair in lastSidearm)
            {
                if (now < pair.Value || now - pair.Value > 30000)
                {
                    remove.Add(pair.Key);
                }
            }
            for (int i = 0; i < remove.Count; i++)
            {
                lastSidearm.Remove(remove[i]);
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

            List<long> removeLong = new List<long>();
            foreach (KeyValuePair<long, int> pair in failedDropUntil)
            {
                if (pair.Value <= now || now < pair.Value - 100000)
                {
                    removeLong.Add(pair.Key);
                }
            }
            for (int i = 0; i < removeLong.Count; i++)
            {
                failedDropUntil.Remove(removeLong[i]);
            }
        }
    }
}
