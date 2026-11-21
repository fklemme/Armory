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

        /// <summary>Ranged numbers shared by the display power and the range profile.</summary>
        public sealed class RangedMetrics
        {
            public VerbProperties Verb;
            public float Damage;
            public int Burst;
            public float Gap;
            public float Warmup;
            public float Cooldown;
            public float Range;
            public float MinRange;
            public float ArmorPen;
            public float Situational;

            public float RawDps(float aimFactor)
            {
                float warmup = Warmup * Mathf.Max(0.1f, aimFactor);
                float cycle = Mathf.Max(0.1f, warmup + (Burst - 1f) * Gap + Cooldown);
                return Damage * Burst / cycle;
            }
        }

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

        /// <summary>
        /// Pawn-independent numbers shared by the Weapons comparison table. The generic
        /// DPSA uses the four standard QM-style range bands and counts an unreachable
        /// selected band as zero; it is deliberately not the per-colonist Armory score.
        /// </summary>
        public sealed class ComparisonStats
        {
            public bool IsRanged;
            public float RawDps;
            public float Dpsa;
            public float ArmorPen;
            public float AccuracyTouch;
            public float AccuracyShort;
            public float AccuracyMedium;
            public float AccuracyLong;
            public float AccuracyAverage;
            public float Range;
            public float MinRange;
            public float Mass;
            public int Burst;
            public float Warmup;
            public float Cooldown;

            public bool HasAccuracy
            {
                get { return IsRanged; }
            }

            public string AccuracyText
            {
                get
                {
                    if (!IsRanged)
                    {
                        return "-";
                    }
                    return (AccuracyTouch * 100f).ToString("0") + "/" +
                           (AccuracyShort * 100f).ToString("0") + "/" +
                           (AccuracyMedium * 100f).ToString("0") + "/" +
                           (AccuracyLong * 100f).ToString("0");
                }
            }
        }

        public static ComparisonStats GetComparison(ThingWithComps weapon)
        {
            ComparisonStats stats = new ComparisonStats();
            if (weapon == null || weapon.def == null)
            {
                return stats;
            }

            StatRequest request = StatRequest.For(weapon);
            stats.Mass = StatValue(request, StatDefOf.Mass);
            if (weapon.def.IsMeleeWeapon)
            {
                stats.RawDps = StatValue(request, StatDefOf.MeleeWeapon_AverageDPS);
                stats.Dpsa = stats.RawDps;
                stats.ArmorPen = MeleeArmorPenetration(request);
                return stats;
            }

            if (!weapon.def.IsRangedWeapon)
            {
                return stats;
            }

            RangedMetrics metrics;
            if (!TryGetRangedMetrics(weapon, out metrics))
            {
                return stats;
            }

            stats.IsRanged = true;
            stats.RawDps = metrics.RawDps(1f);
            stats.ArmorPen = metrics.ArmorPen;
            stats.Range = metrics.Range;
            stats.MinRange = metrics.MinRange;
            stats.Burst = metrics.Burst;
            stats.Warmup = metrics.Warmup;
            stats.Cooldown = metrics.Cooldown;
            stats.AccuracyTouch = StatValue(request, StatDefOf.AccuracyTouch);
            stats.AccuracyShort = StatValue(request, StatDefOf.AccuracyShort);
            stats.AccuracyMedium = StatValue(request, StatDefOf.AccuracyMedium);
            stats.AccuracyLong = StatValue(request, StatDefOf.AccuracyLong);

            float accuracy = 0f;
            if (stats.MinRange <= 3f && stats.Range >= 3f)
            {
                accuracy += stats.AccuracyTouch;
            }
            if (stats.MinRange <= 12f && stats.Range >= 12f)
            {
                accuracy += stats.AccuracyShort;
            }
            if (stats.MinRange <= 25f && stats.Range >= 25f)
            {
                accuracy += stats.AccuracyMedium;
            }
            if (stats.MinRange <= 40f && stats.Range >= 40f)
            {
                accuracy += stats.AccuracyLong;
            }
            stats.AccuracyAverage = accuracy / 4f;
            stats.Dpsa = stats.RawDps * stats.AccuracyAverage;
            return stats;
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

            RangedMetrics metrics;
            if (!TryGetRangedMetrics(weapon, out metrics))
            {
                return 0f;
            }

            // Display-only blend, kept for continuity with earlier versions. The
            // range-aware combat score deliberately does not use this value.
            float accuracy = 0.2f * StatValue(request, StatDefOf.AccuracyShort)
                             + 0.3f * StatValue(request, StatDefOf.AccuracyMedium)
                             + 0.5f * StatValue(request, StatDefOf.AccuracyLong);
            if (accuracy <= 0f)
            {
                accuracy = 0.5f;
            }

            return metrics.RawDps(1f) * accuracy * (1f + metrics.ArmorPen) *
                   metrics.Situational * DisplayScale;
        }

        /// <summary>Fills the ranged metrics used by both display and range scoring.</summary>
        public static bool TryGetRangedMetrics(ThingWithComps weapon, out RangedMetrics metrics)
        {
            metrics = null;
            ThingDef def = weapon == null ? null : weapon.def;
            if (def == null || !def.IsRangedWeapon)
            {
                return false;
            }

            VerbProperties verb = PrimaryVerb(def, weapon);
            if (verb == null || verb.defaultProjectile == null ||
                verb.defaultProjectile.projectile == null)
            {
                return false;
            }

            ProjectileProperties projectile = verb.defaultProjectile.projectile;
            StatRequest request = StatRequest.For(weapon);
            metrics = new RangedMetrics
            {
                Verb = verb,
                Damage = projectile.GetDamageAmount(weapon, null),
                Burst = Mathf.Max(1, verb.burstShotCount),
                Gap = Mathf.Max(0, verb.ticksBetweenBurstShots) / 60f,
                Warmup = Mathf.Max(0f, verb.warmupTime) *
                         StatDefOf.RangedWeapon_WarmupMultiplier.Worker.GetValue(request, true),
                Cooldown = StatDefOf.RangedWeapon_Cooldown.Worker.GetValue(request, true),
                Range = verb.range,
                MinRange = verb.minRange,
                ArmorPen = projectile.GetArmorPenetration(null, null) *
                           StatValue(request, StatDefOf.RangedWeapon_ArmorPenetrationMultiplier),
                Situational = SituationalMultiplier(projectile)
            };
            return true;
        }

        /// <summary>Maximum range for display; zero for melee or invalid ranged weapons.</summary>
        public static float GetRange(ThingWithComps weapon)
        {
            RangedMetrics metrics;
            return TryGetRangedMetrics(weapon, out metrics) ? metrics.Range : 0f;
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
