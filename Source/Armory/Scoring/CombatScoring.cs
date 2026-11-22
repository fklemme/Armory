using RimWorld;
using UnityEngine;
using Verse;

namespace Armory
{
    /// <summary>Ranged combat role, or Auto when the role is derived per pawn.</summary>
    public enum CombatRole
    {
        Auto = 0,
        Cqb = 1,
        Assault = 2,
        Line = 3,
        Marksman = 4
    }

    /// <summary>
    /// Range-aware weapon scoring. Uses the game's own hit-chance functions so skill,
    /// genes, traits, quality and modded weapons all affect the result.
    /// </summary>
    public static class CombatScoring
    {
        private const float SupportBurstThreshold = 6f;
        private const float SupportMultiplier = 0.75f;
        private const float DisplayScale = 10f;
        private const float SigmaCqb = 4f;
        private const float SigmaAssault = 6f;
        private const float SigmaLine = 6f;
        private const float SigmaMarksman = 7f;
        private const float ReachFraction = 0.5f;
        private const int ReachMargin = 8;
        public const int MinDistance = 3;
        public const int MaxDistance = 45;

        /// <summary>
        /// Builds the pawn's distance curve and resolves the automatic or overridden
        /// ranged role. Called once per PawnFitContext.
        /// </summary>
        public static void Prepare(PawnFitContext ctx)
        {
            if (ctx == null || ctx.Pawn == null)
            {
                return;
            }

            ctx.AimCurve = new float[MaxDistance + 1];
            for (int distance = MinDistance; distance <= MaxDistance; distance++)
            {
                ctx.AimCurve[distance] = ShotReport.HitFactorFromShooter(ctx.Pawn, distance);
            }
            for (int distance = 0; distance < MinDistance; distance++)
            {
                ctx.AimCurve[distance] = ctx.AimCurve[MinDistance];
            }

            CombatRole overrideRole = ArmoryGameComponent.GetRoleOverride(ctx.Pawn);
            if (overrideRole != CombatRole.Auto)
            {
                ctx.CombatRole = overrideRole;
                ctx.PreferredRange = RoleCenter(overrideRole);
                ctx.RoleOverridden = true;
                ctx.CombatRoleSource = "manual override";
                return;
            }

            int reach = EffectiveReach(ctx);
            int offset = ArmoryMod.Settings == null ? 0 : ArmoryMod.Settings.engagementOffset;
            int preferred = Mathf.Clamp(reach - ReachMargin + offset, 8, 34);
            ctx.CombatRole = RoleForDistance(preferred);
            ctx.PreferredRange = preferred;
            ctx.RoleOverridden = false;
            ctx.CombatRoleSource = "auto (reach " + reach + "t)";
        }

        /// <summary>
        /// Scores one weapon for one pawn. The score is expected DPS over the pawn's
        /// preferred distance band, multiplied by non-accuracy pawn fit and variety.
        /// </summary>
        public static float Compute(PawnFitContext ctx, ThingWithComps weapon, out string ledger)
        {
            ledger = null;
            if (ctx == null || weapon == null || weapon.def == null)
            {
                return 0f;
            }

            float affinity = Affinity(ctx.Pawn, weapon.def);
            if (!weapon.def.IsRangedWeapon)
            {
                string fitLedger;
                float meleeFit = PawnFit.Compute(ctx, weapon, out fitLedger);
                float meleeScore = WeaponPower.Get(weapon, null) * meleeFit * affinity;
                ledger = fitLedger;
                if (affinity != 1f)
                {
                    ledger += "\n" + string.Format("personal preference x{0:0.00}", affinity);
                }
                return meleeScore;
            }

            WeaponPower.RangedMetrics metrics;
            if (!WeaponPower.TryGetRangedMetrics(weapon, out metrics) || metrics.Range < MinDistance)
            {
                return 0f;
            }

            float sigma = SigmaFor(ctx.CombatRole);
            float rawDps = metrics.RawDps(ctx.Aim);
            float weighted = 0f;
            float weights = 0f;
            for (int distance = MinDistance; distance <= MaxDistance; distance++)
            {
                float weight = Mathf.Exp(
                    -0.5f * Mathf.Pow((distance - ctx.PreferredRange) / sigma, 2f));
                weights += weight;
                if (distance > metrics.Range || distance < metrics.MinRange)
                {
                    continue;
                }

                float weaponHit = metrics.Verb.GetHitChanceFactor(weapon, distance);
                float pawnHit = ctx.AimAt(distance);
                weighted += weight * rawDps * weaponHit * pawnHit;
            }

            float profile = weights > 0.01f ? weighted / weights : 0f;
            string fitText;
            float fit = PawnFit.Compute(ctx, weapon, out fitText);
            float support = metrics.Burst >= SupportBurstThreshold ? SupportMultiplier : 1f;
            float score = profile * (1f + metrics.ArmorPen) * metrics.Situational *
                          fit * support * affinity;

            ledger = string.Format("role {0}, prefers {1}t; profile {2:0.0} dps",
                Label(ctx.CombatRole), ctx.PreferredRange, profile);
            if (!string.IsNullOrEmpty(fitText))
            {
                ledger += "\n" + fitText;
            }
            if (metrics.Burst >= SupportBurstThreshold)
            {
                ledger += string.Format("\nsupport weapon x{0:0.00}", SupportMultiplier);
            }
            if (affinity != 1f)
            {
                ledger += string.Format("\npersonal preference x{0:0.00}", affinity);
            }
            return score * DisplayScale;
        }

        /// <summary>
        /// Fills the per-distance expected-DPS profile used by Sidearm 2 coverage.
        /// Returns false for melee or unusable ranged weapons; the destination is zeroed
        /// first so callers can treat a false result as "no ranged coverage".
        /// </summary>
        public static bool TryFillRangedProfile(PawnFitContext ctx, ThingWithComps weapon,
            float[] destination)
        {
            if (destination == null)
            {
                return false;
            }
            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = 0f;
            }
            if (ctx == null || ctx.Pawn == null || weapon == null || weapon.def == null ||
                !weapon.def.IsRangedWeapon)
            {
                return false;
            }

            WeaponPower.RangedMetrics metrics;
            if (!WeaponPower.TryGetRangedMetrics(weapon, out metrics) ||
                metrics.Range < MinDistance)
            {
                return false;
            }

            float rawDps = metrics.RawDps(ctx.Aim);
            string fitText;
            float fit = PawnFit.Compute(ctx, weapon, out fitText);
            float support = metrics.Burst >= SupportBurstThreshold ? SupportMultiplier : 1f;
            float affinity = Affinity(ctx.Pawn, weapon.def);
            float factor = (1f + metrics.ArmorPen) * metrics.Situational * fit *
                           support * affinity;

            for (int distance = MinDistance; distance <= MaxDistance; distance++)
            {
                if (distance > metrics.Range || distance < metrics.MinRange)
                {
                    continue;
                }
                destination[distance] = rawDps *
                                        metrics.Verb.GetHitChanceFactor(weapon, distance) *
                                        ctx.AimAt(distance) * factor;
            }
            return true;
        }

        /// <summary>Farthest distance where the pawn still keeps half its 10-tile aim.</summary>
        public static int EffectiveReach(PawnFitContext ctx)
        {
            if (ctx == null || ctx.Pawn == null || ctx.AimCurve == null)
            {
                return 10;
            }

            float baseAim = ctx.AimAt(10);
            if (baseAim <= 0.021f)
            {
                // The game clamps hit chance to 2.01%, which would otherwise make a
                // blind pawn look effective at every distance.
                return 10;
            }
            float threshold = baseAim * ReachFraction;
            int reach = 10;
            for (int distance = 10; distance <= MaxDistance; distance++)
            {
                if (ctx.AimAt(distance) >= threshold)
                {
                    reach = distance;
                }
            }
            return reach;
        }

        public static CombatRole RoleForDistance(int distance)
        {
            if (distance <= 12)
            {
                return CombatRole.Cqb;
            }
            if (distance <= 22)
            {
                return CombatRole.Assault;
            }
            if (distance <= 30)
            {
                return CombatRole.Line;
            }
            return CombatRole.Marksman;
        }

        public static int RoleCenter(CombatRole role)
        {
            switch (role)
            {
                case CombatRole.Cqb:
                    return 10;
                case CombatRole.Assault:
                    return 17;
                case CombatRole.Line:
                    return 25;
                case CombatRole.Marksman:
                    return 34;
                default:
                    return 20;
            }
        }

        private static float SigmaFor(CombatRole role)
        {
            switch (role)
            {
                case CombatRole.Cqb:
                    return SigmaCqb;
                case CombatRole.Assault:
                    return SigmaAssault;
                case CombatRole.Line:
                    return SigmaLine;
                case CombatRole.Marksman:
                    return SigmaMarksman;
                default:
                    return SigmaAssault;
            }
        }

        public static string Label(CombatRole role)
        {
            switch (role)
            {
                case CombatRole.Cqb:
                    return "CQB";
                case CombatRole.Assault:
                    return "Assault";
                case CombatRole.Line:
                    return "Line";
                case CombatRole.Marksman:
                    return "Marksman";
                default:
                    return "Auto";
            }
        }

        /// <summary>
        /// Small deterministic preference so equal pawns do not always choose identical
        /// weapons. The same pawn and def always produce the same multiplier.
        /// </summary>
        public static float Affinity(Pawn pawn, ThingDef def)
        {
            if (pawn == null || def == null || ArmoryMod.Settings == null)
            {
                return 1f;
            }

            float strength = ArmoryMod.Settings.varietyStrength;
            if (strength <= 0.001f)
            {
                return 1f;
            }

            uint hash = 2166136261u;
            string name = def.defName ?? string.Empty;
            for (int i = 0; i < name.Length; i++)
            {
                hash ^= name[i];
                hash *= 16777619u;
            }
            hash ^= (uint)pawn.thingIDNumber;
            hash *= 16777619u;

            float value = (hash % 2001u) / 1000f - 1f;
            return 1f + strength * 0.10f * value;
        }
    }
}
