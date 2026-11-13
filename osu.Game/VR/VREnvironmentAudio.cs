// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: derives the music data driving the VR environment (spectrum bands, beat pulse, kiai) from the current track and beatmap,
    /// and passes it to <see cref="Output"/> every update.
    /// </summary>
    public partial class VREnvironmentAudio : Component
    {
        public const int BAND_COUNT = 32;

        /// <summary>
        /// Receives per-band amplitudes (0..1, low to high frequencies), the beat pulse (1 on the beat, decaying to 0) and kiai.
        /// </summary>
        public Action<float[], float, bool>? Output { get; set; }

        [Resolved]
        private IBeatSyncProvider beatSync { get; set; } = null!;

        private readonly float[] bands = new float[BAND_COUNT];
        private readonly int[] bandEdges = new int[BAND_COUNT + 1];

        public VREnvironmentAudio()
        {
            // Log-spaced bands over the 256 FFT bins (bin 0 is skipped: DC).
            for (int i = 0; i <= BAND_COUNT; i++)
                bandEdges[i] = Math.Clamp((int)MathF.Round(MathF.Pow(256, (float)i / BAND_COUNT)), 1, 256);

            for (int i = 1; i <= BAND_COUNT; i++)
                bandEdges[i] = Math.Max(bandEdges[i], bandEdges[i - 1] + 1);
        }

        protected override void Update()
        {
            base.Update();

            if (Output == null)
                return;

            updateBands();

            float pulse = 0;
            bool kiai = false;

            if (beatSync.ControlPoints is var controlPoints && controlPoints != null && beatSync.Clock.IsRunning)
            {
                double time = beatSync.Clock.CurrentTime;
                var timingPoint = controlPoints.TimingPointAt(time);
                double beatLength = timingPoint.BeatLength;

                if (beatLength > 0)
                {
                    double phase = ((time - timingPoint.Time) % beatLength + beatLength) % beatLength / beatLength;
                    pulse = MathF.Pow(1 - (float)phase, 3);
                }

                kiai = controlPoints.EffectPointAt(time).KiaiMode;
            }

            Output(bands, pulse, kiai);
        }

        private void updateBands()
        {
            var spectrum = beatSync.CurrentAmplitudes.FrequencyAmplitudes.Span;
            float decay = MathF.Pow(0.02f, (float)Time.Elapsed / 1000); // falls back smoothly rather than flickering

            for (int i = 0; i < BAND_COUNT; i++)
            {
                float peak = 0;

                for (int bin = bandEdges[i]; bin < bandEdges[i + 1] && bin < spectrum.Length; bin++)
                    peak = Math.Max(peak, spectrum[bin]);

                // Higher frequencies carry less energy; tilt them up so the whole floor moves.
                float value = Math.Clamp(peak * (2 + i * 0.25f), 0, 1);
                bands[i] = Math.Max(value, bands[i] * decay);
            }
        }
    }
}
