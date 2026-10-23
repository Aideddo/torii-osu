// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuPerformanceCalculator : PerformanceCalculator
    {
        public const double PERFORMANCE_BASE_MULTIPLIER = 1.12;
        public const double PERFORMANCE_NORM_EXPONENT = 1.1;

        private bool usingClassicSliderAccuracy;
        private bool usingScoreV2;

        private double accuracy;
        private int scoreMaxCombo;
        private int countGreat;
        private int countOk;
        private int countMeh;
        private int countMiss;

        private int countSliderTickMiss;
        private int countSliderEndsDropped;
        private double effectiveMissCount;

        private double clockRate;
        private double greatHitWindow;
        private double okHitWindow;
        private double mehHitWindow;

        private double overallDifficulty;
        private double approachRate;
        private double drainRate;

        private double? speedDeviation;

        private double aimEstimatedSliderBreaks;
        private double speedEstimatedSliderBreaks;

        public static double DifficultyToPerformance(double difficulty) => 4.0 * Math.Pow(difficulty, 3.0);

        public OsuPerformanceCalculator()
            : base(new OsuRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var osuAttributes = (OsuDifficultyAttributes)attributes;

            usingClassicSliderAccuracy = score.Mods.OfType<OsuModClassic>().Any(m => m.NoSliderHeadAccuracy.Value);
            usingScoreV2 = score.Mods.Any(m => m is ModScoreV2);

            accuracy = score.Accuracy;
            scoreMaxCombo = score.MaxCombo;
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);
            countSliderEndsDropped = osuAttributes.SliderCount - score.Statistics.GetValueOrDefault(HitResult.SliderTailHit);
            countSliderTickMiss = score.Statistics.GetValueOrDefault(HitResult.LargeTickMiss);
            effectiveMissCount = countMiss;

            var difficulty = score.BeatmapInfo!.Difficulty.Clone();

            score.Mods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(difficulty));

            clockRate = ModUtils.CalculateRateWithMods(score.Mods);

            HitWindows hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(difficulty.OverallDifficulty);

            greatHitWindow = hitWindows.WindowFor(HitResult.Great) / clockRate;
            okHitWindow = hitWindows.WindowFor(HitResult.Ok) / clockRate;
            mehHitWindow = hitWindows.WindowFor(HitResult.Meh) / clockRate;

            approachRate = OsuDifficultyCalculator.CalculateRateAdjustedApproachRate(difficulty.ApproachRate, clockRate);
            overallDifficulty = OsuDifficultyCalculator.CalculateRateAdjustedOverallDifficulty(difficulty.OverallDifficulty, clockRate);
            drainRate = difficulty.DrainRate;

            double comboBasedEstimatedMissCount = calculateComboBasedEstimatedMissCount(osuAttributes);
            double? scoreBasedEstimatedMissCount = null;

            if (usingClassicSliderAccuracy && !usingScoreV2 && score.LegacyTotalScore != null)
            {
                var legacyScoreMissCalculator = new OsuLegacyScoreMissCalculator(score, osuAttributes);
                scoreBasedEstimatedMissCount = legacyScoreMissCalculator.Calculate();

                effectiveMissCount = scoreBasedEstimatedMissCount.Value;
            }
            else
            {
                effectiveMissCount = comboBasedEstimatedMissCount;
            }

            effectiveMissCount = Math.Max(countMiss, effectiveMissCount);
            effectiveMissCount = Math.Min(totalHits, effectiveMissCount);

            if (effectiveMissCount > 0)
            {
                aimEstimatedSliderBreaks = calculateEstimatedSliderBreaks(osuAttributes.AimTopWeightedSliderFactor, osuAttributes);
                speedEstimatedSliderBreaks = calculateEstimatedSliderBreaks(osuAttributes.SpeedTopWeightedSliderFactor, osuAttributes);
            }

            double multiplier = PERFORMANCE_BASE_MULTIPLIER;

            if (score.Mods.Any(m => m is OsuModNoFail))
                multiplier *= Math.Max(0.90, 1.0 - 0.02 * effectiveMissCount);

            if (score.Mods.Any(m => m is OsuModSpunOut) && totalHits > 0)
                multiplier *= 1.0 - Math.Pow((double)osuAttributes.SpinnerCount / totalHits, 0.85);

            // --- RELAX OD & MISS PENALTY REWORK BLOCK ---
            // Adjusts virtual miss weights for Ok/Meh hits under Relax mod.
            // Below OD 10: Gradually increases penalties to prevent low-OD exploit abuse.
            // Above OD 10: Rapidly scales down the penalty to protect high-OD maps (e.g., OD 11.1), smoothly reaching 0 at OD 13.33.
            if (score.Mods.Any(h => h is OsuModRelax))
            {
                double safeOD = Math.Max(0.0, overallDifficulty);
                double penaltyScale;

                if (safeOD <= 10.0)
                {
                    double odDiff = 10.0 - safeOD;
                    penaltyScale = 1.0 + (odDiff * 0.2) + Math.Pow(odDiff, 1.3) * 0.1;
                }
                else
                {
                    double odExcess = safeOD - 10.0;
                    double progress = Math.Clamp(odExcess / 3.33, 0.0, 1.0);

                    penaltyScale = 1.0 * Math.Max(0.0, 1.0 - Math.Pow(progress, 0.25));
                }

                double okMultiplier = 0.75 * penaltyScale;
                double mehMultiplier = 1.0 * penaltyScale;

                // As we're adding Oks and Mehs to an approximated number of combo breaks the result can be higher than total hits in specific scenarios (which breaks some calculations) so we need to clamp it.
                effectiveMissCount = Math.Min(effectiveMissCount + countOk * okMultiplier + countMeh * mehMultiplier, totalHits);
            }


            speedDeviation = calculateSpeedDeviation(osuAttributes);

            double aimValue = computeAimValue(score, osuAttributes);
            double speedValue = computeSpeedValue(score, osuAttributes);
            double accuracyValue = computeAccuracyValue(score, osuAttributes);

            double readingValue = computeReadingValue(osuAttributes);
            double flashlightValue = computeFlashlightValue(score, osuAttributes);
            double cognitionValue = OsuDifficultyCalculator.SumCognitionDifficulty(readingValue, flashlightValue);

            double totalValue = DifficultyCalculationUtils.Norm(PERFORMANCE_NORM_EXPONENT, aimValue, speedValue, accuracyValue, cognitionValue) * multiplier;

            return new OsuPerformanceAttributes
            {
                Aim = aimValue,
                Speed = speedValue,
                Accuracy = accuracyValue,
                Flashlight = flashlightValue,
                Reading = readingValue,
                EffectiveMissCount = effectiveMissCount,
                ComboBasedEstimatedMissCount = comboBasedEstimatedMissCount,
                ScoreBasedEstimatedMissCount = scoreBasedEstimatedMissCount,
                AimEstimatedSliderBreaks = aimEstimatedSliderBreaks,
                SpeedEstimatedSliderBreaks = speedEstimatedSliderBreaks,
                SpeedDeviation = speedDeviation,
                Total = totalValue
            };
        }

        private double computeAimValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModAutopilot))
                return 0.0;

            double aimDifficulty = attributes.AimDifficulty;

            if (attributes.SliderCount > 0 && attributes.AimDifficultSliderCount > 0)
            {
                double estimateImproperlyFollowedDifficultSliders;

                if (usingClassicSliderAccuracy)
                {
                    int maximumPossibleDroppedSliders = totalImperfectHits;
                    estimateImproperlyFollowedDifficultSliders = Math.Clamp(Math.Min(maximumPossibleDroppedSliders, attributes.MaxCombo - scoreMaxCombo), 0, attributes.AimDifficultSliderCount);
                }
                else
                {
                    estimateImproperlyFollowedDifficultSliders = Math.Clamp(countSliderEndsDropped + countSliderTickMiss, 0, attributes.AimDifficultSliderCount);
                }

                double sliderNerfFactor = (1 - attributes.SliderFactor) * Math.Pow(1 - estimateImproperlyFollowedDifficultSliders / attributes.AimDifficultSliderCount, 3) + attributes.SliderFactor;
                aimDifficulty *= sliderNerfFactor;
            }

            double aimValue = DifficultyToPerformance(aimDifficulty);

            double lengthBonus = 0.95 + 0.35 * Math.Min(1.0, totalHits / 2000.0) +
                                  (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0.0);
            aimValue *= lengthBonus;

            if (effectiveMissCount > 0)
            {
                double relevantMissCount = Math.Min(effectiveMissCount + aimEstimatedSliderBreaks, totalImperfectHits + countSliderTickMiss);

                aimValue *= calculateMissPenalty(relevantMissCount, attributes.AimDifficultStrainCount);
            }

            if (score.Mods.Any(m => m is OsuModBlinds))
                aimValue *= 1.3 + (totalHits * (0.0016 / (1 + 2 * effectiveMissCount)) * Math.Pow(accuracy, 16)) * (1 - 0.003 * drainRate * drainRate);
            else if (score.Mods.Any(m => m is OsuModTraceable))
            {
                aimValue *= 1.0 + calculateTraceableBonus(attributes.SliderFactor);
            }

            aimValue *= accuracy;

            // Applies an additional accuracy-based aim nerf for high-CS maps under Relax mod, active exclusively at OD <= 10.
            if (score.Mods.Any(m => m is OsuModRelax))
            {
                var difficulty = score.BeatmapInfo!.Difficulty.Clone();
                score.Mods.OfType<IApplicableToDifficulty>().ForEach(m => m.ApplyToDifficulty(difficulty));
                double circleSize = difficulty.CircleSize;

                double relaxNerfMultiplier = 1.0;
                double safeOD = Math.Max(0.0, overallDifficulty);

                if (circleSize > 4.5 && safeOD <= 10.0)
                {
                    double baseExponent = 3.0 + (circleSize * 1.7);
                    double baseNerf = Math.Pow(accuracy, baseExponent);

                    double odDeficit = 10.0 - safeOD;
                    double harshness = 2.5 + (odDeficit * 2.0);
                    double harshNerf = Math.Pow(baseNerf, harshness);
                    double absoluteAnnihilation = Math.Pow(0.4, odDeficit);

                    relaxNerfMultiplier *= Math.Max(0.000000001, harshNerf * absoluteAnnihilation);
                }

                aimValue *= relaxNerfMultiplier;
            }

            return aimValue;
        }

        private double computeSpeedValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModRelax) || speedDeviation == null)
                return 0.0;

            double speedValue = HarmonicSkill.DifficultyToPerformance(attributes.SpeedDifficulty);

            if (effectiveMissCount > 0)
            {
                double relevantMissCount = Math.Min(effectiveMissCount + speedEstimatedSliderBreaks, totalImperfectHits + countSliderTickMiss);

                speedValue *= calculateMissPenalty(relevantMissCount, attributes.SpeedDifficultStrainCount);
            }

            if (score.Mods.Any(m => m is OsuModBlinds))
            {
                speedValue *= 1.12;
            }

            double speedHighDeviationMultiplier = calculateSpeedHighDeviationNerf(attributes);
            speedValue *= speedHighDeviationMultiplier;

            double effectiveHitWindow = 20 * Math.Pow(4 / attributes.SpeedDifficulty, 0.35);
            double effectiveAccuracy = DifficultyCalculationUtils.Erf(effectiveHitWindow / (double)speedDeviation);

            speedValue *= Math.Pow(effectiveAccuracy, 2);

            return speedValue;
        }

        private double computeAccuracyValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (score.Mods.Any(h => h is OsuModRelax))
                return 0.0;

            double betterAccuracyPercentage;
            int amountHitObjectsWithAccuracy = attributes.HitCircleCount;
            if (!usingClassicSliderAccuracy || usingScoreV2)
                amountHitObjectsWithAccuracy += attributes.SliderCount;

            if (amountHitObjectsWithAccuracy > 0)
                betterAccuracyPercentage = ((countGreat - Math.Max(totalHits - amountHitObjectsWithAccuracy, 0)) * 6 + countOk * 2 + countMeh) / (double)(amountHitObjectsWithAccuracy * 6);
            else
                betterAccuracyPercentage = 0;

            if (betterAccuracyPercentage < 0)
                betterAccuracyPercentage = 0;

            double accuracyValue = Math.Pow(1.52163, overallDifficulty) * Math.Pow(betterAccuracyPercentage, 24) * 2.83;

            accuracyValue *= amountHitObjectsWithAccuracy < 1000
                ? Math.Pow(amountHitObjectsWithAccuracy / 1000.0, 0.3)
                : Math.Pow(amountHitObjectsWithAccuracy / 1000.0, 0.1);

            if (score.Mods.Any(m => m is OsuModBlinds))
                accuracyValue *= 1.14;
            else if (score.Mods.Any(m => m is OsuModTraceable))
            {
                accuracyValue *= 1 + 0.08 * DifficultyCalculationUtils.ReverseLerp(approachRate, 11.5, 10);
            }

            return accuracyValue;
        }

        private double computeFlashlightValue(ScoreInfo score, OsuDifficultyAttributes attributes)
        {
            if (!score.Mods.Any(h => h is OsuModFlashlight))
                return 0.0;

            double flashlightValue = Flashlight.DifficultyToPerformance(attributes.FlashlightDifficulty.GetValueOrDefault());

            if (effectiveMissCount > 0)
                flashlightValue *= 0.97 * Math.Pow(1 - Math.Pow(effectiveMissCount / totalHits, 0.775), Math.Pow(effectiveMissCount, .875));

            flashlightValue *= getComboScalingFactor(attributes);
            flashlightValue *= 0.5 + accuracy / 2.0;

            return flashlightValue;
        }

        private double computeReadingValue(OsuDifficultyAttributes attributes)
        {
            double readingValue = HarmonicSkill.DifficultyToPerformance(attributes.ReadingDifficulty);

            if (effectiveMissCount > 0)
                readingValue *= calculateMissPenalty(effectiveMissCount + aimEstimatedSliderBreaks, attributes.ReadingDifficultNoteCount);

            readingValue *= Math.Pow(accuracy, 3);

            return readingValue;
        }

        private double calculateComboBasedEstimatedMissCount(OsuDifficultyAttributes attributes)
        {
            if (attributes.SliderCount <= 0)
                return countMiss;

            double missCount = countMiss;

            if (usingClassicSliderAccuracy)
            {
                double likelyMissedSliderendPortion = 0.04 + 0.06 * Math.Pow(Math.Min(attributes.AimTopWeightedSliderFactor, 1), 2);
                double fullComboThreshold = attributes.MaxCombo - Math.Min(4 + likelyMissedSliderendPortion * attributes.SliderCount, attributes.SliderCount);

                if (scoreMaxCombo < fullComboThreshold)
                    missCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);

                missCount = Math.Min(missCount, totalImperfectHits);

                int maxPossibleSliderBreaks = Math.Min(attributes.SliderCount, (attributes.MaxCombo - scoreMaxCombo) / 2);
                double sliderBreaks = missCount - countMiss;

                if (sliderBreaks > maxPossibleSliderBreaks)
                    missCount = countMiss + maxPossibleSliderBreaks;
            }
            else
            {
                double fullComboThreshold = attributes.MaxCombo - countSliderEndsDropped;

                if (scoreMaxCombo < fullComboThreshold)
                    missCount = fullComboThreshold / Math.Max(1.0, scoreMaxCombo);

                missCount = Math.Min(missCount, countSliderTickMiss + countMiss);
            }

            return missCount;
        }

        private double calculateEstimatedSliderBreaks(double topWeightedSliderFactor, OsuDifficultyAttributes attributes)
        {
            int nonMissMistakes = countOk + countMeh;

            if (!usingClassicSliderAccuracy || nonMissMistakes == 0)
                return 0;

            double missedComboPercent = 1.0 - (double)scoreMaxCombo / attributes.MaxCombo;
            double estimatedSliderBreaks = Math.Min(nonMissMistakes, effectiveMissCount * topWeightedSliderFactor);

            double nonMissMistakeAdjustment = (nonMissMistakes - estimatedSliderBreaks + 4.5) / (nonMissMistakes + 4);
            estimatedSliderBreaks *= DifficultyCalculationUtils.Smoothstep(effectiveMissCount, 1, 2);

            return estimatedSliderBreaks * nonMissMistakeAdjustment * DifficultyCalculationUtils.Logistic(missedComboPercent, 0.33, 15);
        }

        private double? calculateSpeedDeviation(OsuDifficultyAttributes attributes)
        {
            if (totalSuccessfulHits == 0)
                return null;

            double speedNoteCount = attributes.SpeedNoteCount;
            speedNoteCount += (totalHits - attributes.SpeedNoteCount) * 0.1;

            double relevantCountMiss = Math.Min(countMiss, speedNoteCount);
            double relevantCountMeh = Math.Min(countMeh, speedNoteCount - relevantCountMiss);
            double relevantCountOk = Math.Min(countOk, speedNoteCount - relevantCountMiss - relevantCountMeh);
            double relevantCountGreat = Math.Max(0, speedNoteCount - relevantCountMiss - relevantCountMeh - relevantCountOk);

            return calculateDeviation(relevantCountGreat, relevantCountOk, relevantCountMeh);
        }

        private double? calculateDeviation(double relevantCountGreat, double relevantCountOk, double relevantCountMeh)
        {
            if (relevantCountGreat + relevantCountOk + relevantCountMeh <= 0)
                return null;

            double n = Math.Max(1, relevantCountGreat + relevantCountOk);
            double p = relevantCountGreat / n;

            const double z = 2.32634787404;

            double pLowerBound = Math.Min(p, (n * p + z * z / 2) / (n + z * z) - z / (n + z * z) * Math.Sqrt(n * p * (1 - p) + z * z / 4));

            double deviation;

            if (pLowerBound > 0.01)
            {
                deviation = greatHitWindow / (Math.Sqrt(2) * DifficultyCalculationUtils.ErfInv(pLowerBound));

                double okHitWindowTailAmount = Math.Sqrt(2 / Math.PI) * okHitWindow * Math.Exp(-0.5 * Math.Pow(okHitWindow / deviation, 2))
                                             / (deviation * DifficultyCalculationUtils.Erf(okHitWindow / (Math.Sqrt(2) * deviation)));

                deviation *= Math.Sqrt(1 - okHitWindowTailAmount);
            }
            else
            {
                deviation = okHitWindow / Math.Sqrt(3);
            }

            double mehVariance = (mehHitWindow * mehHitWindow + okHitWindow * mehHitWindow + okHitWindow * okHitWindow) / 3;

            deviation = Math.Sqrt(((relevantCountGreat + relevantCountOk) * Math.Pow(deviation, 2) + relevantCountMeh * mehVariance) / (relevantCountGreat + relevantCountOk + relevantCountMeh));

            return deviation;
        }

        private double calculateSpeedHighDeviationNerf(OsuDifficultyAttributes attributes)
        {
            if (speedDeviation == null)
                return 0;

            double speedValue = HarmonicSkill.DifficultyToPerformance(attributes.SpeedDifficulty);

            double excessSpeedDifficultyCutoff = 100 + 220 * Math.Pow(22 / speedDeviation.Value, 6.5);

            if (speedValue <= excessSpeedDifficultyCutoff)
                return 1.0;

            const double scale = 50;
            double adjustedSpeedValue = scale * (Math.Log((speedValue - excessSpeedDifficultyCutoff) / scale + 1) + excessSpeedDifficultyCutoff / scale);

            double lerp = 1 - DifficultyCalculationUtils.ReverseLerp(speedDeviation.Value, 22.0, 27.0);
            adjustedSpeedValue = double.Lerp(adjustedSpeedValue, speedValue, lerp);

            return adjustedSpeedValue / speedValue;
        }

        private double calculateTraceableBonus(double sliderFactor = 1)
        {
            double highApproachRateSliderVisibilityFactor = 0.5 + (Math.Pow(sliderFactor, 6) / 2);
            double lowApproachRateSliderVisibilityFactor = Math.Pow(sliderFactor, 6);

            double traceableBonus = 0.0275;
            traceableBonus += 0.025 * (12.0 - Math.Max(approachRate, 7)) * highApproachRateSliderVisibilityFactor;

            if (approachRate < 7)
                traceableBonus += 0.025 * (7.0 - Math.Max(approachRate, 0)) * lowApproachRateSliderVisibilityFactor;

            if (approachRate < 0)
                traceableBonus += 0.025 * (1 - Math.Pow(1.5, approachRate)) * lowApproachRateSliderVisibilityFactor;

            return traceableBonus;
        }

        private double calculateMissPenalty(double missCount, double difficultStrainCount) => 0.93 / (missCount / (4 * Math.Log(difficultStrainCount)) + 1);
        private double getComboScalingFactor(OsuDifficultyAttributes attributes) => attributes.MaxCombo <= 0 ? 1.0 : Math.Min(Math.Pow(scoreMaxCombo, 0.8) / Math.Pow(attributes.MaxCombo, 0.8), 1.0);

        private int totalHits => countGreat + countOk + countMeh + countMiss;
        private int totalSuccessfulHits => countGreat + countOk + countMeh;
        private int totalImperfectHits => countOk + countMeh + countMiss;
    }
}
