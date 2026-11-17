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

            // Simple Sidearms has the final say on this pawn's primary type and forced states.
            // These are player intents, not permanent restrictions, so they stay soft blockers.
            string sidearmReason;
            if (SimpleSidearmsAdapter.TryGetPrimaryBlocker(pawn, weapon, out sidearmReason))
            {
                reason = sidearmReason;
                return false;
            }

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
