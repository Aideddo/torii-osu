// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play.Ghost
{
    /// <summary>
    /// torii: what a past score looked like at every moment of the map, so a new
    /// play can be raced against it.
    /// </summary>
    /// <remarks>
    /// A stored score only knows its final numbers. To race one you need to know
    /// where it stood at 40 seconds in, which means replaying it, which is what
    /// <see cref="GhostReplaySimulator"/> does. This is the result: a handful of
    /// samples per second of map, cheap to hold and cheap to look up.
    /// </remarks>
    public class GhostTimeline
    {
        /// <summary>
        /// The score this was built from. Only the identity (who set it, what mods)
        /// is meaningful here: the numbers below come from re-simulating, not from
        /// the stored totals, so that both racers are scored by the same rules.
        /// </summary>
        public ScoreInfo Score { get; }

        /// <summary>Map time of the last sample, i.e. where the ghost stops moving.</summary>
        public double EndTime => times.Length == 0 ? 0 : times[^1];

        public GhostSample Final => samples.Length == 0 ? default : samples[^1];

        private readonly double[] times;
        private readonly GhostSample[] samples;

        public GhostTimeline(ScoreInfo score, IEnumerable<GhostSample> samples)
        {
            Score = score;

            // Sorting rather than trusting the caller: a simulator that stutters
            // could hand these over slightly out of order, and a non-monotonic
            // array would break the binary search below in a way that is very
            // annoying to track down later.
            this.samples = samples.OrderBy(s => s.Time).ToArray();
            times = this.samples.Select(s => s.Time).ToArray();
        }

        /// <summary>
        /// Where the ghost stood at <paramref name="time"/>, interpolated between samples.
        /// </summary>
        public GhostSample SampleAt(double time)
        {
            if (samples.Length == 0)
                return default;

            int i = Array.BinarySearch(times, time);

            // exact hit on a sample.
            if (i >= 0)
                return samples[i];

            i = ~i;

            // before the first sample the ghost has not started, after the last it is done.
            if (i == 0)
                return new GhostSample(time, 0, 1, 0);
            if (i >= samples.Length)
                return samples[^1] with { Time = time };

            var previous = samples[i - 1];
            var next = samples[i];

            double span = next.Time - previous.Time;
            double t = span <= 0 ? 0 : (time - previous.Time) / span;

            return new GhostSample(
                time,
                // Score and accuracy get interpolated so the ghost's numbers glide
                // instead of ticking once per sample. Both only ever move a little
                // between samples, so the lie is small and the read is much calmer.
                (long)Math.Round(previous.TotalScore + (next.TotalScore - previous.TotalScore) * t),
                previous.Accuracy + (next.Accuracy - previous.Accuracy) * t,
                // Combo is a count, not a quantity: showing 143.6 would be nonsense,
                // so it holds the last known value until the next sample.
                previous.Combo);
        }
    }

    /// <summary>
    /// One reading of a ghost's progress.
    /// </summary>
    public readonly record struct GhostSample(double Time, long TotalScore, double Accuracy, int Combo);
}
