using Armory.Compatibility;
using RimWorld;
using Verse;
using Verse.AI;

namespace Armory
{
    /// <summary>Blockers are reported as reasons, never folded into the score.</summary>
    public static class Eligibility
    {
        public const string Forbidden = "Forbidden";
        public const string OutsideArea = "Outside area";
        public const string Unreachable = "Unreachable";
        public const string Reserved = "Reserved";
        public const string AlreadyCarried = "Already carried";

        /// <param name="deep">
        /// Adds reachability and reservation checks. These are relatively expensive, so bulk
        /// ranking uses the cheap gates and the detail pane asks for the deep answer.
        /// </param>
        /// <param name="hard">
        /// True when the blocker is permanent (biocoded, bonded, role). Soft blockers such as
        /// forbidden, allowed area, reachability, and reservations can be fixed by the player.
        /// </param>
        public static bool Check(Pawn pawn, ThingWithComps weapon, bool deep, out string reason, out bool hard)
        {
            reason = null;
            hard = false;

            if (!CheckEquippable(pawn, weapon, out reason, out hard))
            {
                return false;
            }

            // Simple Sidearms has the final say on this pawn's primary type and forced states.
            // These are player intents, not permanent restrictions, so they stay soft blockers.
            string sidearmReason;
            if (SimpleSidearmsAdapter.TryGetPrimaryBlocker(pawn, weapon, out sidearmReason))
            {
                reason = sidearmReason;
                return false;
            }

            return CheckWorld(pawn, weapon, deep, out reason);
        }

        /// <summary>
        /// Sidearm variant: Simple Sidearms' slot/mass/selection rules are checked, but its
        /// primary-mode and forced-primary blockers do not apply. Hard failures (biocoded,
        /// bonded elsewhere, role) mean the weapon is not a sidearm candidate at all.
        /// </summary>
        public static bool CheckSidearm(Pawn pawn, ThingWithComps weapon, bool deep, out string reason, out bool hard)
        {
            reason = null;
            hard = false;

            if (!CheckSidearmRules(pawn, weapon, out reason, out hard))
            {
                return false;
            }

            return CheckWorld(pawn, weapon, deep, out reason);
        }

        /// <summary>
        /// Equippability plus Simple Sidearms' allowance check. Excludes reachability and
        /// reservations, so the planner can tell "SS has no room right now" apart from
        /// "the pawn cannot walk to it".
        /// </summary>
        public static bool CheckSidearmRules(Pawn pawn, ThingWithComps weapon, out string reason, out bool hard)
        {
            reason = null;
            hard = false;

            if (!CheckEquippable(pawn, weapon, out reason, out hard))
            {
                return false;
            }

            string sidearmReason;
            if (!SimpleSidearmsAdapter.CanPickupSidearm(pawn, weapon, out sidearmReason))
            {
                // SS reports terse localized keys ("Slots full", "No free space"); rewrite
                // the capacity failures so the UI says which limit was hit and why.
                reason = SimpleSidearmsAdapter.ExplainPickupFailure(pawn, weapon, sidearmReason);
                return false;
            }

            return true;
        }

        /// <summary>World-only sidearm gate: forbidden, allowed area, reachability, reservation.</summary>
        public static bool CheckSidearmWorld(Pawn pawn, ThingWithComps weapon, bool deep, out string reason)
        {
            return CheckWorld(pawn, weapon, deep, out reason);
        }

        private static bool CheckEquippable(Pawn pawn, ThingWithComps weapon, out string reason, out bool hard)
        {
            reason = null;
            hard = false;

            if (weapon == null || weapon.Destroyed || weapon.def == null)
            {
                reason = "Destroyed";
                hard = true;
                return false;
            }

            string equipReason;
            if (!EquipmentUtility.CanEquip(weapon, pawn, out equipReason, true))
            {
                reason = string.IsNullOrEmpty(equipReason) ? "Cannot equip" : equipReason;
                hard = true;
                return false;
            }

            return true;
        }

        private static bool CheckWorld(Pawn pawn, ThingWithComps weapon, bool deep, out string reason)
        {
            reason = null;

            if (ForbidUtility.IsForbidden(weapon, pawn))
            {
                reason = Forbidden;
                return false;
            }

            if (weapon.Spawned)
            {
                Area allowed = pawn.playerSettings == null
                    ? null
                    : pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap;
                if (allowed != null && !allowed[weapon.Position])
                {
                    reason = OutsideArea;
                    return false;
                }

                if (deep)
                {
                    if (!pawn.CanReach(weapon, PathEndMode.ClosestTouch, Danger.Deadly))
                    {
                        reason = Unreachable;
                        return false;
                    }
                    if (pawn.Map != null &&
                        !pawn.Map.reservationManager.CanReserve(pawn, weapon, 1, -1, null, false))
                    {
                        reason = Reserved;
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
