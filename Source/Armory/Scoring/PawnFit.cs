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
        public float Accuracy;
        public float Aim;

        public static PawnFitContext Create(Pawn pawn)
        {
            PawnFitContext context = new PawnFitContext
            {
                Pawn = pawn,
                Shooting = SkillLevel(pawn, SkillDefOf.Shooting),
                Melee = SkillLevel(pawn, SkillDefOf.Melee),
                Brawler = HasBrawler(pawn),
                HasIdeo = ModsConfig.IdeologyActive && pawn.Ideo != null,
                Accuracy = StatExtension.GetStatValue(pawn, StatDefOf.ShootingAccuracyPawn, true, 2500),
                Aim = StatExtension.GetStatValue(pawn, StatDefOf.AimingDelayFactor, true, 2500)
            };
            return context;
        }

        private static int SkillLevel(Pawn pawn, SkillDef skill)
        {
            if (pawn.skills == null)
            {
                return 0;
            }
            SkillRecord record = pawn.skills.GetSkill(skill);
            return record == null ? 0 : record.Level;
        }

        private static bool HasBrawler(Pawn pawn)
        {
            return pawn.story != null && pawn.story.traits != null && pawn.story.traits.HasTrait(TraitDefOf.Brawler);
        }
    }

    /// <summary>How well a specific weapon suits a specific pawn, with an optional readable ledger.</summary>
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

            if (ranged)
            {
                float accuracy = Mathf.Clamp(ctx.Accuracy, 0.6f, 1.4f);
                float aim = Mathf.Clamp(1f / Mathf.Max(0.1f, ctx.Aim), 0.7f, 1.3f);
                fit *= accuracy * aim;
                Add(parts, string.Format("shooting accuracy: x{0:0.00}", accuracy));
                Add(parts, string.Format("aim speed: x{0:0.00}", aim));
            }
            else if (melee && ctx.Pawn != null)
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
