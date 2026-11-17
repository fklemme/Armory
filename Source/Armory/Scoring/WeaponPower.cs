using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Armory
{
    /// <summary>
    /// Pawn-independent weapon strength, derived from vanilla stats so modded weapons
    /// (including Combat Extended) are scored by their own numbers rather than a table.
    /// </summary>
    public static class WeaponPower
    {
        private static StatDef meleeArmorPenStat;
        private static bool meleeArmorPenResolved;
        private const float DisplayScale = 10f;

        public static float Get(ThingWithComps weapon, Dictionary<int, float> cache)
        {
            if (weapon == null)
            {
                return 0f;
            }
            float power;
            if (cache != null && cache.TryGetValue(weapon.thingIDNumber, out power))
            {
                return power;
            }
            power = Compute(weapon);
            if (cache != null)
            {
                cache[weapon.thingIDNumber] = power;
            }
            return power;
        }

        private static float Compute(ThingWithComps weapon)
        {
            ThingDef def = weapon.def;
            if (def == null)
            {
                return 0f;
            }
            StatRequest request = StatRequest.For(weapon);

            if (def.IsMeleeWeapon)
            {
                float meleeDps = StatDefOf.MeleeWeapon_AverageDPS.Worker.GetValue(request, true);
                float meleeAp = MeleeArmorPenetration(request);
                return meleeDps * (1f + meleeAp) * DisplayScale;
            }

            if (!def.IsRangedWeapon)
            {
                return 0f;
            }

            VerbProperties verb = PrimaryVerb(def, weapon);
            if (verb == null || verb.defaultProjectile == null || verb.defaultProjectile.projectile == null)
            {
                return 0f;
            }

            ProjectileProperties projectile = verb.defaultProjectile.projectile;
            float damage = projectile.GetDamageAmount(weapon, null);
            float burst = Mathf.Max(1, verb.burstShotCount);
            float gap = Mathf.Max(0, verb.ticksBetweenBurstShots) / 60f;
            float warmup = Mathf.Max(0f, verb.warmupTime) *
                           StatDefOf.RangedWeapon_WarmupMultiplier.Worker.GetValue(request, true);
            float cooldown = StatDefOf.RangedWeapon_Cooldown.Worker.GetValue(request, true);
            float cycle = Mathf.Max(0.1f, warmup + (burst - 1f) * gap + cooldown);
            float dps = damage * burst / cycle;

            float accuracy = 0.2f * StatValue(request, StatDefOf.AccuracyShort)
                             + 0.3f * StatValue(request, StatDefOf.AccuracyMedium)
                             + 0.5f * StatValue(request, StatDefOf.AccuracyLong);
            if (accuracy <= 0f)
            {
                accuracy = 0.5f;
            }

            float armorPen = projectile.GetArmorPenetration(null, null) *
                             StatValue(request, StatDefOf.RangedWeapon_ArmorPenetrationMultiplier);

            return dps * accuracy * (1f + armorPen) * SituationalMultiplier(projectile) * DisplayScale;
        }

        /// <summary>
        /// Grenades, rockets, incendiaries, EMP and smoke are useful, but they should not
        /// outrank a normal weapon for the primary slot just because their raw damage is high.
        /// </summary>
        private static float SituationalMultiplier(ProjectileProperties projectile)
        {
            if (projectile == null)
            {
                return 1f;
            }

            bool situational = projectile.explosionRadius > 0f;
            DamageDef damage = projectile.damageDef;
            if (!situational && damage != null)
            {
                situational = damage.isExplosive ||
                              !damage.harmsHealth ||
                              damage == DamageDefOf.EMP ||
                              damage == DamageDefOf.Stun ||
                              damage == DamageDefOf.Smoke ||
                              damage == DamageDefOf.Extinguish;
            }
            return situational ? 0.35f : 1f;
        }

        public static VerbProperties PrimaryVerb(ThingDef def, ThingWithComps weapon)
        {
            if (def == null || def.Verbs == null || def.Verbs.Count == 0)
            {
                return null;
            }
            for (int i = 0; i < def.Verbs.Count; i++)
            {
                VerbProperties verb = def.Verbs[i];
                if (verb != null && verb.isPrimary)
                {
                    return verb;
                }
            }
            for (int i = 0; i < def.Verbs.Count; i++)
            {
                VerbProperties verb = def.Verbs[i];
                if (verb != null && verb.defaultProjectile != null && verb.defaultProjectile.projectile != null)
                {
                    return verb;
                }
            }
            return def.Verbs[0];
        }

        private static float StatValue(StatRequest request, StatDef stat)
        {
            return stat == null ? 1f : stat.Worker.GetValue(request, true);
        }

        private static float MeleeArmorPenetration(StatRequest request)
        {
            if (!meleeArmorPenResolved)
            {
                meleeArmorPenStat = DefDatabase<StatDef>.GetNamedSilentFail("MeleeWeapon_AverageArmorPenetration");
                meleeArmorPenResolved = true;
            }
            return meleeArmorPenStat == null ? 0f : meleeArmorPenStat.Worker.GetValue(request, true);
        }
    }
}
