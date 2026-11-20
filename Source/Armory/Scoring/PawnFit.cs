using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Armory
{
    /// <summary>Pawn stats that do not depend on the weapon, gathered once per pawn.</summary>
    public sealed class PawnFitContext
    {
        public Pawn Pawn;
        public int Shooting;
        public int Melee;
        public bool Brawler;
        public bool HasIdeo;
        public float Aim;

        /// <summary>Passion for Shooting, used to choose the no-SS hand type before skill.</summary>
        public Passion ShootingPassion;

        /// <summary>Passion for Melee, used to choose the no-SS hand type before skill.</summary>
        public Passion MeleePassion;

        /// <summary>True when worn apparel blocks ranged fire (vanilla or modded shield belt).</summary>
        public bool RangedBlockingShield;

        /// <summary>Resolved ranged combat role, either automatic or a player override.</summary>
        public CombatRole CombatRole;

        /// <summary>Preferred engagement distance in tiles for this pawn's ranged hand.</summary>
        public int PreferredRange;

        /// <summary>True when the player fixed the role instead of deriving it from accuracy.</summary>
        public bool RoleOverridden;

        /// <summary>Human-readable origin of <see cref="CombatRole"/>.</summary>
        public string CombatRoleSource;

        /// <summary>
        /// Pawn-only hit factor by distance, including the pawn's per-tile shooting
        /// accuracy and its range-specific accuracy factors. Built once per pawn.
        /// </summary>
        public float[] AimCurve;

        public static PawnFitContext Create(Pawn pawn)
        {
            PawnFitContext context = new PawnFitContext
            {
                Pawn = pawn,
                Shooting = SkillLevel(pawn, SkillDefOf.Shooting),
                Melee = SkillLevel(pawn, SkillDefOf.Melee),
                ShootingPassion = SkillPassion(pawn, SkillDefOf.Shooting),
                MeleePassion = SkillPassion(pawn, SkillDefOf.Melee),
                Brawler = HasBrawler(pawn),
                RangedBlockingShield = WearsRangedBlockingShield(pawn),
                HasIdeo = ModsConfig.IdeologyActive && pawn.Ideo != null,
                Aim = StatExtension.GetStatValue(pawn, StatDefOf.AimingDelayFactor, true, 2500)
            };
            CombatScoring.Prepare(context);
            return context;
        }

        public float AimAt(int distance)
        {
            if (AimCurve != null && distance >= 0 && distance < AimCurve.Length)
            {
                return AimCurve[distance];
            }
            return ShotReport.HitFactorFromShooter(Pawn, distance);
        }

        private static int SkillLevel(Pawn pawn, SkillDef skill)
        {
            if (pawn == null || pawn.skills == null)
            {
                return 0;
            }
            SkillRecord record = pawn.skills.GetSkill(skill);
            return record == null ? 0 : record.Level;
        }

        private static Passion SkillPassion(Pawn pawn, SkillDef skill)
        {
            if (pawn == null || pawn.skills == null)
            {
                return Passion.None;
            }
            SkillRecord record = pawn.skills.GetSkill(skill);
            return record == null ? Passion.None : record.passion;
        }

        private static bool HasBrawler(Pawn pawn)
        {
            return pawn != null && pawn.story != null && pawn.story.traits != null &&
                   pawn.story.traits.HasTrait(TraitDefOf.Brawler);
        }

        private static bool WearsRangedBlockingShield(Pawn pawn)
        {
            if (pawn == null || pawn.apparel == null || pawn.apparel.WornApparel == null)
            {
                return false;
            }
            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                Apparel apparel = worn[i];
                if (apparel == null || apparel.def == null)
                {
                    continue;
                }
                CompProperties_Shield shield = apparel.def.GetCompProperties<CompProperties_Shield>();
                if (shield != null && shield.blocksRangedWeapons)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// How well a specific weapon suits a specific pawn, excluding ranged accuracy and
    /// aiming speed. Ranged accuracy is handled by <see cref="CombatScoring"/> so the
    /// game's distance curve is applied exactly once.
    /// </summary>
    public static class PawnFit
    {
        /// <summary>Bulk path: no string allocations.</summary>
        public static float Compute(PawnFitContext ctx, ThingWithComps weapon)
        {
            return ComputeInternal(ctx, weapon, null);
        }

        /// <summary>Detail path: records each multiplier for the ledger.</summary>
        public static float Compute(PawnFitContext ctx, ThingWithComps weapon, out string ledger)
        {
            List<string> parts = new List<string>(6);
            float fit = ComputeInternal(ctx, weapon, parts);
            ledger = string.Join("\n", parts);
            return fit;
        }

        private static float ComputeInternal(PawnFitContext ctx, ThingWithComps weapon, List<string> parts)
        {
            float fit = 1f;

            bool ranged = weapon.def.IsRangedWeapon;
            bool melee = weapon.def.IsMeleeWeapon;
            int relevant = ranged ? ctx.Shooting : ctx.Melee;

            float skill = 1f + relevant * 0.02f;
            fit *= skill;
            Add(parts, string.Format("skill {0}: x{1:0.00}", relevant, skill));

            if (ranged && ctx.Melee > ctx.Shooting)
            {
                fit *= 0.75f;
                Add(parts, "shooting below melee: x0.75");
            }
            else if (melee && ctx.Shooting > ctx.Melee)
            {
                fit *= 0.75f;
                Add(parts, "melee below shooting: x0.75");
            }

            if (ctx.Brawler)
            {
                if (ranged)
                {
                    fit *= 0.75f;
                    Add(parts, "brawler with ranged: x0.75");
                }
                else
                {
                    fit *= 1.15f;
                    Add(parts, "brawler with melee: x1.15");
                }
            }

            if (melee && ctx.Pawn != null)
            {
                float hit = Mathf.Clamp(
                    StatExtension.GetStatValue(ctx.Pawn, StatDefOf.MeleeHitChance, true, 2500), 0.4f, 1f);
                fit *= hit;
                Add(parts, string.Format("melee hit chance: x{0:0.00}", hit));
            }

            if (ctx.HasIdeo)
            {
                IdeoWeaponDisposition disposition = ctx.Pawn.Ideo.GetDispositionForWeapon(weapon.def);
                if (disposition == IdeoWeaponDisposition.Noble)
                {
                    fit *= 1.10f;
                    Add(parts, "ideoligion noble weapon: x1.10");
                }
                else if (disposition == IdeoWeaponDisposition.Despised)
                {
                    fit *= 0.85f;
                    Add(parts, "ideoligion despised weapon: x0.85");
                }
            }

            float weight = ranged
                ? (ArmoryMod.Settings == null ? 1f : ArmoryMod.Settings.rangedWeight)
                : (ArmoryMod.Settings == null ? 1f : ArmoryMod.Settings.meleeWeight);
            if (Mathf.Abs(weight - 1f) > 0.001f)
            {
                fit *= weight;
                Add(parts, string.Format("{0} preference: x{1:0.00}", ranged ? "ranged" : "melee", weight));
            }

            return fit;
        }

        private static void Add(List<string> parts, string text)
        {
            if (parts != null)
            {
                parts.Add(text);
            }
        }
    }
}
