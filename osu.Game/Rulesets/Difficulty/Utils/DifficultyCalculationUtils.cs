// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;

namespace osu.Game.Rulesets.Difficulty.Utils
{
    [Obsolete("Use DiffUtils instead.")]
    public static class DifficultyCalculationUtils
    {
        public const double SQRT2 = DiffUtils.SQRT2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double BPMToMilliseconds(double bpm, int delimiter = 4) => DiffUtils.BPMToMilliseconds(bpm, delimiter);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double MillisecondsToBPM(double ms, int delimiter = 4) => DiffUtils.MillisecondsToBPM(ms, delimiter);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Logistic(double x, double midpointOffset, double multiplier, double maxValue = 1) => DiffUtils.Logistic(x, midpointOffset, multiplier, maxValue);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Logistic(double exponent, double maxValue = 1) => DiffUtils.Logistic(exponent, maxValue);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Norm(double p, params double[] values) => DiffUtils.Norm(p, values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double BellCurve(double x, double mean, double width, double multiplier = 1.0) => DiffUtils.BellCurve(x, mean, width, multiplier);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SmoothstepBellCurve(double x, double mean, double width) => DiffUtils.SmoothstepBellCurve(x, mean, width);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SmoothstepBellCurve(double x) => DiffUtils.SmoothstepBellCurve(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Smoothstep(double x, double start, double end) => DiffUtils.Smoothstep(x, start, end);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Smootherstep(double x, double start, double end) => DiffUtils.Smootherstep(x, start, end);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ReverseLerp(double x, double start, double end) => DiffUtils.ReverseLerp(x, start, end);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Erf(double x) => DiffUtils.Erf(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Erfc(double x) => DiffUtils.Erfc(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ErfInv(double x) => DiffUtils.ErfInv(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Pow(double x, double exponent) => DiffUtils.Pow(x, exponent);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Pow(double x, int exponent) => DiffUtils.Pow(x, exponent);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ErfcInv(double x) => DiffUtils.ErfcInv(x);
    }
}
