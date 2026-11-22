using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Armory
{
    /// <summary>Rough engagement band used to explain Sidearm 2 coverage.</summary>
    public enum RangeBand
    {
        Close = 0,
        Short = 1,
        Medium = 2,
        Long = 3
    }

    /// <summary>
    /// Builds the Hand + Sidearm 1 range profile and scores Sidearm 2 candidates by
    /// how much they improve the weakest bands. The scoring deliberately ignores
    /// improvements in bands the current loadout already covers well, so a second
    /// sniper does not win just because it is a better sniper.
    /// </summary>
    public sealed class SidearmCoverageModel
    {
        private readonly float[] baseline = new float[CombatScoring.MaxDistance + 1];
        private readonly float[] reference = new float[CombatScoring.MaxDistance + 1];
        private readonly List<SidearmCandidate> candidates = new List<SidearmCandidate>();
        private readonly float[] scratch = new float[CombatScoring.MaxDistance + 1];

        public void AddBaseline(PawnFitContext context, ThingWithComps weapon)
        {
            if (!CombatScoring.TryFillRangedProfile(context, weapon, scratch))
            {
                return;
            }
            for (int distance = 0; distance < baseline.Length; distance++)
            {
                if (scratch[distance] > baseline[distance])
                {
                    baseline[distance] = scratch[distance];
                }
            }
        }

        public void AddCandidate(PawnFitContext context, SidearmCandidate candidate,
            ThingWithComps weapon)
        {
            if (candidate == null || weapon == null)
            {
                return;
            }
            if (!CombatScoring.TryFillRangedProfile(context, weapon, scratch))
            {
                candidate.SlotScore = 0f;
                return;
            }

            candidate.CoverageProfile = new float[scratch.Length];
            for (int distance = 0; distance < scratch.Length; distance++)
            {
                float value = scratch[distance];
                candidate.CoverageProfile[distance] = value;
                if (value > reference[distance])
                {
                    reference[distance] = value;
                }
            }
            candidates.Add(candidate);
        }

        public float BaselineBandAverage(RangeBand band)
        {
            return BandAverage(baseline, band);
        }

        public float ReferenceBandAverage(RangeBand band)
        {
            return BandAverage(reference, band);
        }

        public void Finish()
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                SidearmCandidate candidate = candidates[i];
                float total = 0f;
                float bestGain = 0f;
                RangeBand bestBand = RangeBand.Close;
                bool foundBand = false;

                for (int bandValue = (int)RangeBand.Close;
                    bandValue <= (int)RangeBand.Long; bandValue++)
                {
                    RangeBand band = (RangeBand)bandValue;
                    float currentBand = BandAverage(baseline, band);
                    float referenceBand = BandAverage(reference, band);
                    float candidateBand = BandAverage(candidate.CoverageProfile, band);
                    float deficit = Mathf.Max(0f, referenceBand - currentBand);
                    float improvement = Mathf.Max(0f, candidateBand - currentBand);
                    float closed = Mathf.Min(deficit, improvement);
                    float gapRatio = deficit / Mathf.Max(referenceBand, 1f);
                    float closureRatio = deficit > 0.01f ? closed / deficit : 0f;
                    total += gapRatio * closureRatio;

                    float gain = improvement / Mathf.Max(currentBand, 1f);
                    if (gain > bestGain || !foundBand)
                    {
                        bestGain = gain;
                        bestBand = band;
                        foundBand = true;
                    }
                }

                // Scale the 0..4 band ratio into a display-friendly score range. The
                // planner only compares candidates by relative value, so scale is free.
                candidate.SlotScore = total * 100f;
                candidate.ComplementGain = bestGain;
                if (bestGain > 0.01f)
                {
                    candidate.ComplementBand = bestBand;
                    candidate.ComplementText = "covers " +
                        BandLabel(bestBand).ToLowerInvariant() + " range";
                }
                else
                {
                    candidate.ComplementBand = null;
                    candidate.ComplementText = "no useful gap";
                }
            }
        }

        public static float BandAverage(float[] profile, RangeBand band)
        {
            if (profile == null)
            {
                return 0f;
            }
            int start;
            int end;
            BandBounds(band, out start, out end);
            float total = 0f;
            int count = 0;
            for (int distance = start; distance <= end; distance++)
            {
                if (distance < 0 || distance >= profile.Length)
                {
                    continue;
                }
                total += profile[distance];
                count++;
            }
            return count == 0 ? 0f : total / count;
        }

        public static void BandBounds(RangeBand band, out int start, out int end)
        {
            switch (band)
            {
                case RangeBand.Close:
                    start = 3;
                    end = 12;
                    break;
                case RangeBand.Short:
                    start = 13;
                    end = 22;
                    break;
                case RangeBand.Medium:
                    start = 23;
                    end = 30;
                    break;
                default:
                    start = 31;
                    end = 40;
                    break;
            }
        }

        public static string BandLabel(RangeBand band)
        {
            switch (band)
            {
                case RangeBand.Close:
                    return "Close";
                case RangeBand.Short:
                    return "Short";
                case RangeBand.Medium:
                    return "Medium";
                default:
                    return "Long";
            }
        }

        public static string BandRangeText(RangeBand band)
        {
            int start;
            int end;
            BandBounds(band, out start, out end);
            return start + "-" + end;
        }
    }
}
