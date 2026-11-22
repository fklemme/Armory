using System.Collections.Generic;
using Armory.Compatibility;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Armory
{
    /// <summary>
    /// Issues equip jobs for the best ready weapon per pawn. Enabled by default, rate-limited, and
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
                if (analysis.DrawnSidearm && analysis.CombatSidearmDraw)
                {
                    // Do not let Simple Sidearms' combat draw register as a manual weapon
                    // change, and leave the draw alone so a hand swap cannot drop it.
                    // Outside combat the left-over sidearm is restored below.
                    lastKnownWeapon[pawn.thingIDNumber] = CurrentWeaponId(pawn);
                    manualUntil.Remove(pawn.thingIDNumber);
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
                    // separate sidearm pass can still maintain the off-hand weapon.
                    continue;
                }
                if (analysis.DrawnSidearm && settings.manageSidearms &&
                    TryRestoreHandFromSidearm(analysis, now))
                {
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
                        if (analysis.Current == null)
                        {
                            detail = "No hand weapon equipped";
                        }
                        else if (analysis.HandTypeMismatch && analysis.RoleIsRanged.HasValue)
                        {
                            string from = analysis.Current.def != null && analysis.Current.def.IsMeleeWeapon
                                ? "melee"
                                : "ranged";
                            string to = analysis.RoleIsRanged.Value ? "ranged" : "melee";
                            detail = "Switching hand type from " + from + " to " + to;
                        }
                        else if (analysis.HasRole)
                        {
                            // The pawn holds the other type (SS has not switched yet).
                            detail = analysis.RoleIsRanged.Value
                                ? "No ranged hand weapon equipped"
                                : "No melee hand weapon equipped";
                        }
                        else
                        {
                            detail = "No hand weapon equipped";
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
        /// Reconciles the carried Sidearm 1 and Sidearm 2 slots. Extras are dropped,
        /// missing slots are filled, and worse sidearms are replaced only after the
        /// replacement passes Simple Sidearms' rules. One action per pawn per pass keeps
        /// the state machine simple and lets the next scan verify the result.
        /// </summary>
        private static void RunSidearms(Map map, ArmorySnapshot snapshot, ArmorySettings settings,
            int now, Dictionary<int, bool> queuedPrimaryRanged)
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
                if (analysis.DrawnSidearm)
                {
                    // A sidearm is in hand. Let the hand pass restore it outside combat;
                    // do not clear a manual pause there. Only a combat draw is SS-owned.
                    if (analysis.CombatSidearmDraw)
                    {
                        manualUntil.Remove(pawn.thingIDNumber);
                    }
                    continue;
                }
                if (now - LastSidearm(pawn, now) < settings.swapCooldownTicks)
                {
                    continue;
                }

                bool roleRanged = analysis.RoleIsRanged.Value;
                if (analysis.Current == null && !queuedPrimaryRanged.ContainsKey(pawn.thingIDNumber))
                {
                    // Sidearms complement a hand weapon; do not use them to arm an
                    // unarmed pawn.
                    continue;
                }
                if (analysis.Current != null && analysis.Current.def != null)
                {
                    bool currentRanged = analysis.Current.def.IsRangedWeapon;
                    if (currentRanged != roleRanged)
                    {
                        // Wait until SS has actually switched the pawn to the role type.
                        continue;
                    }
                }
                if (analysis.Current == null)
                {
                    continue;
                }

                ThingWithComps unusable = FindUnusableSidearm(analysis, roleRanged);
                if (unusable != null)
                {
                    if (TryDropSidearmThing(pawn, unusable, now, DecisionKind.DropExtraSidearm,
                            "Unusable sidearm"))
                    {
                        continue;
                    }
                }

                int meleeCount = analysis.CountCarried(false);
                int rangedCount = analysis.CountCarried(true);
                int extraOpposite = roleRanged ? Mathf.Max(0, meleeCount - 1) : 0;
                int extraRanged = Mathf.Max(0, rangedCount - 2);

                if (extraOpposite > 0)
                {
                    ThingWithComps extra = FindWorstExtra(analysis, true, roleRanged);
                    if (extra != null &&
                        TryDropSidearmThing(pawn, extra, now, DecisionKind.DropExtraSidearm,
                            "Extra melee sidearm"))
                    {
                        continue;
                    }
                }
                if (extraRanged > 0)
                {
                    ThingWithComps extra = FindWorstExtra(analysis, false, roleRanged);
                    if (extra != null &&
                        TryDropSidearmThing(pawn, extra, now, DecisionKind.DropExtraSidearm,
                            "Extra ranged sidearm"))
                    {
                        continue;
                    }
                }

                bool hasSidearmOne = analysis.BestCarriedBackup != null;
                bool hasSidearmTwo = analysis.BestCarriedSidearmTwo != null;

                if (!hasSidearmOne)
                {
                    SidearmCandidate add = FindSidearmOneAdd(analysis, now);
                    if (add != null)
                    {
                        EquipNewSidearm(pawn, add, now, jobDef, WeaponSlot.Sidearm1);
                    }
                    continue;
                }

                if (settings.replaceWorseSidearms && SimpleSidearmsAdapter.CanDropSidearms)
                {
                    SidearmCandidate upgrade = FindSidearmOneUpgrade(analysis, settings, now);
                    if (upgrade != null &&
                        UpgradeSidearm(pawn, analysis, upgrade, analysis.BestCarriedBackup,
                            now, jobDef, WeaponSlot.Sidearm1))
                    {
                        continue;
                    }
                }

                if (hasSidearmTwo &&
                    !analysis.IsCarriedSidearmTwoUseful(settings.upgradeThreshold))
                {
                    // The hand or Sidearm 1 changed since this weapon was added and it no
                    // longer closes a range gap. Try a useful replacement before dropping,
                    // then remove the stale weapon so it stops occupying the SS slot.
                    if (settings.replaceWorseSidearms && SimpleSidearmsAdapter.CanDropSidearms)
                    {
                        SidearmCandidate upgrade = FindSidearmTwoUpgrade(analysis, settings, now);
                        if (upgrade != null &&
                            UpgradeSidearm(pawn, analysis, upgrade, analysis.BestCarriedSidearmTwo,
                                now, jobDef, WeaponSlot.Sidearm2))
                        {
                            continue;
                        }
                    }
                    if (analysis.BestCarriedSidearmTwo != analysis.Current &&
                        TryDropSidearmThing(pawn, analysis.BestCarriedSidearmTwo, now,
                            DecisionKind.DropExtraSidearm,
                            "Sidearm 2 no longer fills a range gap"))
                    {
                        continue;
                    }
                }

                if (!hasSidearmTwo)
                {
                    SidearmCandidate add = FindSidearmTwoAdd(analysis, settings, now);
                    if (add != null)
                    {
                        EquipNewSidearm(pawn, add, now, jobDef, WeaponSlot.Sidearm2);
                    }
                    continue;
                }

                if (settings.replaceWorseSidearms && SimpleSidearmsAdapter.CanDropSidearms)
                {
                    SidearmCandidate upgrade = FindSidearmTwoUpgrade(analysis, settings, now);
                    if (upgrade != null &&
                        UpgradeSidearm(pawn, analysis, upgrade, analysis.BestCarriedSidearmTwo,
                            now, jobDef, WeaponSlot.Sidearm2))
                    {
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// Simple Sidearms can leave a sidearm equipped after combat. Put the carried hand
        /// weapon back through SS's own swap helper instead of waiting forever for a stow
        /// that only happens during combat. The current sidearm moves to the inventory and is
        /// reconciled on the next planner pass.
        /// </summary>
        private static bool TryRestoreHandFromSidearm(PawnAnalysis analysis, int now)
        {
            Pawn pawn = analysis.Pawn;
            if (pawn == null || pawn.Drafted || pawn.equipment == null ||
                !SimpleSidearmsAdapter.CanEquipFromInventory)
            {
                return false;
            }

            ThingWithComps hand = analysis.BestCarriedPrimary;
            if (hand == null || hand == pawn.equipment.Primary)
            {
                hand = analysis.LogicalHand;
            }
            if (hand == null || hand == pawn.equipment.Primary || hand.Destroyed ||
                hand.def == null || !analysis.IsHandType(hand.def))
            {
                return false;
            }

            bool handRemembered = SimpleSidearmsAdapter.IsRemembered(pawn, hand);
            bool currentRemembered = SimpleSidearmsAdapter.IsRemembered(pawn, analysis.Current);
            ArmorySettings settings = ArmoryMod.Settings;
            float threshold = settings == null ? 0.1f : settings.upgradeThreshold;
            bool currentNoGapSidearmTwo =
                analysis.BestCarriedSidearmTwo == analysis.Current &&
                !analysis.IsCarriedSidearmTwoUseful(threshold);
            if (!handRemembered || (currentRemembered && !currentNoGapSidearmTwo))
            {
                // A remembered sidearm in hand is normally Simple Sidearms' business.
                // The exception is a remembered Sidearm 2 that no longer fills a gap:
                // restore the hand and let the next pass drop it. An unremembered planned
                // hand would be swapped straight back, so never use one here.
                return false;
            }

            string reason;
            bool hard;
            if (!Eligibility.CanUseCarriedWeapon(pawn, hand, out reason, out hard) ||
                !Eligibility.Check(pawn, hand, false, out reason, out hard))
            {
                if (ArmoryMod.Settings != null && ArmoryMod.Settings.debugLogging &&
                    !string.IsNullOrEmpty(reason))
                {
                    Log.Message("[Armory] cannot restore hand for " + pawn.LabelShort +
                                ": " + reason);
                }
                return false;
            }

            if (!SimpleSidearmsAdapter.EquipFromInventory(pawn, hand))
            {
                return false;
            }

            int pawnId = pawn.thingIDNumber;
            lastSwap[pawnId] = now;
            lastKnownWeapon[pawnId] = hand.thingIDNumber;
            lastIssuedWeapon[pawnId] = hand.thingIDNumber;
            manualUntil.Remove(pawnId);
            ArmoryGameComponent.Record(now, pawn, hand, DecisionKind.EquipHand,
                "Restoring hand after a sidearm");

            if (ArmoryMod.Settings != null && ArmoryMod.Settings.debugLogging)
            {
                Log.Message(string.Format("[Armory] restored {0} as {1}'s hand after a sidearm",
                    hand.LabelCap, pawn.LabelShort));
            }
            return true;
        }

        private static int CurrentWeaponId(Pawn pawn)
        {
            return pawn != null && pawn.equipment != null && pawn.equipment.Primary != null
                ? pawn.equipment.Primary.thingIDNumber
                : 0;
        }

        private static ThingWithComps FindUnusableSidearm(PawnAnalysis analysis, bool roleRanged)
        {
            for (int i = 0; i < analysis.CarriedWeapons.Count; i++)
            {
                ThingWithComps weapon = analysis.CarriedWeapons[i];
                if (weapon == null || weapon == analysis.Current || weapon.def == null)
                {
                    continue;
                }
                if (!IsSidearmSlotType(weapon, roleRanged))
                {
                    continue;
                }
                string reason;
                bool hard;
                if (Eligibility.CanUseCarriedWeapon(analysis.Pawn, weapon, out reason, out hard))
                {
                    continue;
                }
                if (IsDroppableBackup(weapon))
                {
                    return weapon;
                }
            }
            return null;
        }

        private static bool IsSidearmSlotType(ThingWithComps weapon, bool roleRanged)
        {
            if (weapon == null || weapon.def == null)
            {
                return false;
            }
            return roleRanged
                ? weapon.def.IsMeleeWeapon || weapon.def.IsRangedWeapon
                : weapon.def.IsRangedWeapon;
        }

        private static ThingWithComps FindWorstExtra(PawnAnalysis analysis, bool melee,
            bool roleRanged)
        {
            PawnFitContext context = PawnFitContext.Create(analysis.Pawn);
            ThingWithComps worst = null;
            float worstScore = float.MaxValue;
            for (int i = 0; i < analysis.CarriedWeapons.Count; i++)
            {
                ThingWithComps weapon = analysis.CarriedWeapons[i];
                if (weapon == null || weapon == analysis.Current || weapon.def == null)
                {
                    continue;
                }
                bool typeMatch = melee ? weapon.def.IsMeleeWeapon : weapon.def.IsRangedWeapon;
                if (!typeMatch || !IsSidearmSlotType(weapon, roleRanged))
                {
                    continue;
                }
                if (weapon == analysis.BestCarriedBackup ||
                    weapon == analysis.BestCarriedSidearmTwo)
                {
                    continue;
                }
                if (!IsDroppableBackup(weapon))
                {
                    continue;
                }
                int failedUntil;
                if (failedDropUntil.TryGetValue(FailedDropKey(analysis.Pawn, weapon),
                        out failedUntil) && failedUntil > Find.TickManager.TicksGame)
                {
                    continue;
                }
                float score = CombatScoring.Compute(context, weapon, out _);
                if (worst == null || score < worstScore)
                {
                    worst = weapon;
                    worstScore = score;
                }
            }
            return worst;
        }

        private static SidearmCandidate FindSidearmOneAdd(PawnAnalysis analysis, int now)
        {
            for (int i = 0; i < analysis.SidearmCandidates.Count; i++)
            {
                SidearmCandidate candidate = analysis.SidearmCandidates[i];
                if (candidate.AlreadyCarried || !candidate.Ready)
                {
                    continue;
                }
                string reason;
                if (CanQueueSidearm(analysis.Pawn, candidate.Weapon.Thing, now, out reason))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static SidearmCandidate FindSidearmOneUpgrade(PawnAnalysis analysis,
            ArmorySettings settings, int now)
        {
            if (analysis.BestCarriedBackup == null ||
                !IsDroppableBackup(analysis.BestCarriedBackup))
            {
                return null;
            }
            float current = Mathf.Max(analysis.BestCarriedBackupScore, 0.01f);
            float minimum = current * (1f + settings.upgradeThreshold);
            for (int i = 0; i < analysis.SidearmCandidates.Count; i++)
            {
                SidearmCandidate candidate = analysis.SidearmCandidates[i];
                if (candidate.AlreadyCarried || candidate.SlotScore <= minimum)
                {
                    continue;
                }
                if (!candidate.Ready && !candidate.UpgradeAfterDrop)
                {
                    continue;
                }
                string reason;
                if (CanReplaceWithSidearm(analysis.Pawn, candidate.Weapon.Thing, now, out reason))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static SidearmCandidate FindSidearmTwoAdd(PawnAnalysis analysis,
            ArmorySettings settings, int now)
        {
            float threshold = settings.upgradeThreshold;
            for (int i = 0; i < analysis.SidearmTwoCandidates.Count; i++)
            {
                SidearmCandidate candidate = analysis.SidearmTwoCandidates[i];
                if (candidate.AlreadyCarried || !candidate.Ready ||
                    !candidate.ComplementBand.HasValue ||
                    candidate.ComplementGain < threshold || candidate.SlotScore <= 0.001f)
                {
                    continue;
                }
                string reason;
                if (CanQueueSidearm(analysis.Pawn, candidate.Weapon.Thing, now, out reason))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static SidearmCandidate FindSidearmTwoUpgrade(PawnAnalysis analysis,
            ArmorySettings settings, int now)
        {
            if (analysis.BestCarriedSidearmTwo == null ||
                !IsDroppableBackup(analysis.BestCarriedSidearmTwo))
            {
                return null;
            }
            float threshold = settings.upgradeThreshold;
            float current = Mathf.Max(analysis.CarriedSidearmTwoCoverage, 0.01f);
            float minimum = current * (1f + threshold);
            for (int i = 0; i < analysis.SidearmTwoCandidates.Count; i++)
            {
                SidearmCandidate candidate = analysis.SidearmTwoCandidates[i];
                if (candidate.AlreadyCarried || candidate.SlotScore <= minimum ||
                    !candidate.ComplementBand.HasValue ||
                    candidate.ComplementGain < threshold)
                {
                    continue;
                }
                if (!candidate.Ready && !candidate.UpgradeAfterDrop)
                {
                    continue;
                }
                string reason;
                if (CanReplaceWithSidearm(analysis.Pawn, candidate.Weapon.Thing, now, out reason))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static bool CanQueueSidearm(Pawn pawn, ThingWithComps weapon, int now,
            out string reason)
        {
            reason = null;
            if (weapon == null || !weapon.Spawned || weapon.Map != pawn.Map)
            {
                reason = "not on the map";
                return false;
            }
            if (Eligibility.IsUnbondedPersona(weapon))
            {
                reason = "persona: bond manually";
                return false;
            }
            Claim claim;
            if (claims.TryGetValue(weapon.thingIDNumber, out claim) &&
                claim.PawnId != pawn.thingIDNumber && claim.ExpiresTick > now)
            {
                reason = "claimed by another colonist";
                return false;
            }
            bool hard;
            return Eligibility.CheckSidearm(pawn, weapon, true, out reason, out hard);
        }

        private static bool CanReplaceWithSidearm(Pawn pawn, ThingWithComps weapon,
            int now, out string reason)
        {
            reason = null;
            if (weapon == null || !weapon.Spawned || weapon.Map != pawn.Map)
            {
                reason = "not on the map";
                return false;
            }
            if (Eligibility.IsUnbondedPersona(weapon))
            {
                reason = "persona: bond manually";
                return false;
            }
            Claim claim;
            if (claims.TryGetValue(weapon.thingIDNumber, out claim) &&
                claim.PawnId != pawn.thingIDNumber && claim.ExpiresTick > now)
            {
                reason = "claimed by another colonist";
                return false;
            }
            if (!SimpleSidearmsAdapter.CanCheckSidearmType ||
                !SimpleSidearmsAdapter.CanCheckSidearmUse ||
                !SimpleSidearmsAdapter.CanCheckRelativeLimits)
            {
                reason = "Simple Sidearms checks unavailable";
                return false;
            }
            string worldReason;
            if (!Eligibility.CheckSidearmWorld(pawn, weapon, false, out worldReason))
            {
                reason = worldReason;
                return false;
            }
            string typeReason = null;
            string useReason = null;
            if (!SimpleSidearmsAdapter.IsValidSidearmForPawn(pawn, weapon, out typeReason) ||
                !SimpleSidearmsAdapter.CanUseSidearm(pawn, weapon, out useReason))
            {
                reason = !string.IsNullOrEmpty(typeReason) ? typeReason : useReason;
                return false;
            }
            return true;
        }

        private static bool EquipNewSidearm(Pawn pawn, SidearmCandidate candidate, int now,
            JobDef jobDef, WeaponSlot slot)
        {
            ThingWithComps weapon = candidate.Weapon.Thing;
            string reason;
            if (!CanQueueSidearm(pawn, weapon, now, out reason))
            {
                return false;
            }

            Job job = JobMaker.MakeJob(jobDef, weapon);
            if (!pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, true))
            {
                Log.Warning("[Armory] " + pawn.LabelShort +
                            " rejected an EquipSecondary job for " + weapon.LabelCap);
                return false;
            }

            lastSidearm[pawn.thingIDNumber] = now;
            claims[weapon.thingIDNumber] = new Claim
            {
                PawnId = pawn.thingIDNumber,
                ExpiresTick = now + 900
            };

            string detail = slot == WeaponSlot.Sidearm2
                ? candidate.ComplementText + " (+" +
                  Mathf.RoundToInt(Mathf.Min(candidate.ComplementGain, 9.99f) * 100f) + "%)"
                : "No opposite-type sidearm carried";
            ArmoryGameComponent.Record(now, pawn, weapon,
                slot == WeaponSlot.Sidearm2
                    ? DecisionKind.EquipSidearmTwo
                    : DecisionKind.EquipBackup,
                detail);

            if (ArmoryMod.Settings != null && ArmoryMod.Settings.debugLogging)
            {
                Log.Message(string.Format("[Armory] queued {0} {1} for {2} ({3})",
                    slot == WeaponSlot.Sidearm2 ? "Sidearm 2" : "Sidearm 1",
                    weapon.LabelCap, pawn.LabelShort, detail));
            }
            return true;
        }

        private static bool UpgradeSidearm(Pawn pawn, PawnAnalysis analysis,
            SidearmCandidate candidate, ThingWithComps current, int now, JobDef jobDef,
            WeaponSlot slot)
        {
            ThingWithComps weapon = candidate.Weapon.Thing;
            if (current == null || weapon == null)
            {
                return false;
            }
            string reason;
            if (!CanReplaceWithSidearm(pawn, weapon, now, out reason))
            {
                return false;
            }

            long dropKey = FailedDropKey(pawn, weapon);
            int failedUntil;
            if (failedDropUntil.TryGetValue(dropKey, out failedUntil) && failedUntil > now)
            {
                return false;
            }

            if (!TryDropSidearmThing(pawn, current, now, DecisionKind.DropBackup,
                    "Replaced by " + weapon.LabelCap))
            {
                failedDropUntil[dropKey] = now + FailedDropMemoryTicks;
                return false;
            }

            string postReason;
            bool postHard;
            if (!Eligibility.CheckSidearmRules(pawn, weapon, out postReason, out postHard))
            {
                failedDropUntil[dropKey] = now + FailedDropMemoryTicks;
                RestoreBackup(pawn, new CarriedSidearm { Thing = current, Score = 0f },
                    jobDef, now, false);
                ArmoryGameComponent.Record(now, pawn, weapon, DecisionKind.SkipUpgrade,
                    "Simple Sidearms refused it after drop: " + postReason);
                return false;
            }

            Job job = JobMaker.MakeJob(jobDef, weapon);
            if (!pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc, true))
            {
                failedDropUntil[dropKey] = now + FailedDropMemoryTicks;
                RestoreBackup(pawn, new CarriedSidearm { Thing = current, Score = 0f },
                    jobDef, now, true);
                return false;
            }

            lastSidearm[pawn.thingIDNumber] = now;
            claims[weapon.thingIDNumber] = new Claim
            {
                PawnId = pawn.thingIDNumber,
                ExpiresTick = now + 900
            };

            float gain = slot == WeaponSlot.Sidearm2
                ? candidate.ComplementGain
                : (candidate.SlotScore / Mathf.Max(
                    slot == WeaponSlot.Sidearm1 ? analysis.BestCarriedBackupScore : 1f,
                    0.01f) - 1f);
            string detail = slot == WeaponSlot.Sidearm2
                ? candidate.ComplementText + " (+" +
                  Mathf.RoundToInt(Mathf.Min(gain, 9.99f) * 100f) + "%)"
                : Mathf.RoundToInt(gain * 100f) + "% better than carried " + current.LabelCap;

            ArmoryGameComponent.Record(now, pawn, weapon,
                slot == WeaponSlot.Sidearm2
                    ? DecisionKind.EquipSidearmTwo
                    : DecisionKind.EquipBackup,
                detail);
            return true;
        }

        private static bool TryDropSidearmThing(Pawn pawn, ThingWithComps weapon, int now,
            DecisionKind kind, string reason)
        {
            if (weapon == null || pawn == null || pawn.inventory == null ||
                pawn.inventory.innerContainer == null ||
                !pawn.inventory.innerContainer.Contains(weapon))
            {
                return false;
            }
            if (pawn.IsQuestLodger())
            {
                ArmoryGameComponent.Record(now, pawn, weapon, DecisionKind.DropFailed,
                    "Quest lodger weapon cannot be dropped");
                return false;
            }
            if (!IsDroppableBackup(weapon))
            {
                return false;
            }
            if (!SimpleSidearmsAdapter.CanDropSidearms)
            {
                ArmoryGameComponent.Record(now, pawn, weapon, DecisionKind.DropFailed,
                    "Simple Sidearms drop unavailable");
                return false;
            }

            long key = FailedDropKey(pawn, weapon);
            int until;
            if (failedDropUntil.TryGetValue(key, out until) && until > now)
            {
                return false;
            }

            bool dropped = SimpleSidearmsAdapter.DropSidearm(pawn, weapon, true, true);
            if (!dropped || pawn.inventory.innerContainer.Contains(weapon))
            {
                failedDropUntil[key] = now + FailedDropMemoryTicks;
                ArmoryGameComponent.Record(now, pawn, weapon, DecisionKind.DropFailed,
                    "Simple Sidearms did not drop it");
                return false;
            }

            claims.Remove(weapon.thingIDNumber);
            ArmoryGameComponent.Record(now, pawn, weapon, kind, reason);
            return true;
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

        /// <summary>
        /// Live duplicate check used just before ordering a pickup. The snapshot can lag one
        /// scan interval, and a queued job may already target the same def. Ordinary weapons
        /// compare by def; persona and unique weapons compare by instance.
        /// </summary>
        private static bool AlreadyCarriesLive(Pawn pawn, ThingWithComps candidate)
        {
            if (pawn == null || candidate == null || candidate.def == null)
            {
                return false;
            }

            bool unique = candidate.GetComp<CompUniqueWeapon>() != null ||
                          candidate.GetComp<CompBladelinkWeapon>() != null;
            if (pawn.equipment != null &&
                IsSameWeapon(pawn.equipment.Primary, candidate, unique))
            {
                return true;
            }
            if (pawn.inventory != null && pawn.inventory.innerContainer != null)
            {
                for (int i = 0; i < pawn.inventory.innerContainer.Count; i++)
                {
                    ThingWithComps carried = pawn.inventory.innerContainer[i] as ThingWithComps;
                    if (IsSameWeapon(carried, candidate, unique))
                    {
                        return true;
                    }
                }
            }
            if (pawn.jobs == null)
            {
                return false;
            }
            if (JobTargetsWeapon(pawn.jobs.curJob, candidate, unique))
            {
                return true;
            }
            if (pawn.jobs.jobQueue != null)
            {
                for (int i = 0; i < pawn.jobs.jobQueue.Count; i++)
                {
                    QueuedJob queued = pawn.jobs.jobQueue[i];
                    if (queued != null && JobTargetsWeapon(queued.job, candidate, unique))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsSameWeapon(ThingWithComps carried, ThingWithComps candidate,
            bool unique)
        {
            if (carried == null || carried.def == null)
            {
                return false;
            }
            return unique ? carried == candidate : carried.def == candidate.def;
        }

        private static bool JobTargetsWeapon(Job job, ThingWithComps candidate, bool unique)
        {
            return job != null &&
                   IsSameWeapon(job.targetA.Thing as ThingWithComps, candidate, unique);
        }

        private static bool HasCurrentOrQueuedJob(Pawn pawn, JobDef def)
        {
            if (pawn == null || pawn.jobs == null || def == null)
            {
                return false;
            }
            if (pawn.jobs.curJob != null && pawn.jobs.curJob.def == def)
            {
                return true;
            }
            if (pawn.jobs.jobQueue != null)
            {
                for (int i = 0; i < pawn.jobs.jobQueue.Count; i++)
                {
                    QueuedJob queued = pawn.jobs.jobQueue[i];
                    if (queued != null && queued.job != null && queued.job.def == def)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsDroppableBackup(ThingWithComps thing)
        {
            return thing != null && !thing.Destroyed && thing.def != null &&
                   (thing.def.IsRangedWeapon || thing.def.IsMeleeWeapon) &&
                   !thing.def.destroyOnDrop && thing.GetComp<CompBladelinkWeapon>() == null &&
                   !Eligibility.IsWorkTool(thing.def);
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
                Log.Warning("[Armory] " + pawn.LabelShort + " could not restore dropped sidearm " +
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
                return "under minimum age";
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
                return "drafted";
            }
            if (pawn.jobs == null)
            {
                return "no job tracker";
            }
            if (HasCurrentOrQueuedJob(pawn, JobDefOf.Equip))
            {
                return "already equipping";
            }
            if (SimpleSidearmsAdapter.IsSidearmJobRunning(pawn))
            {
                return "sidearm job";
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
                    return string.Format("manual {0}s", seconds);
                }
            }

            int last = LastSwap(pawn, now);
            int elapsed = now - last;
            if (elapsed < settings.swapCooldownTicks)
            {
                int seconds = Mathf.Max(1, (settings.swapCooldownTicks - elapsed + 59) / 60);
                return string.Format("cooldown {0}s", seconds);
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
            int currentId = CurrentWeaponId(pawn);

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
                    ArmoryDiagnostics.Write("Manual", string.Format(
                        "pawn={0}; paused {1} ticks after a weapon change",
                        pawn.LabelShort, settings.manualCooldownTicks));
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
