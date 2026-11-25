// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Utils;

namespace osu.Game.Rulesets.Osu.Mods
{
    /// <summary>
    /// Torii-exclusive conversion mod that scales spacing between hitobjects while keeping rhythm, pattern shapes,
    /// slider velocities, and circle-size-aware playfield boundaries intact.
    /// </summary>
    public class OsuModAmplify : Mod, IApplicableToBeatmap, IToriiExclusiveMod
    {
        public override string Name => "Amplify";
        public override string Acronym => "AM";
        public override IconUsage? Icon => OsuIcon.ModAmplify;
        public override ModType Type => ModType.Conversion;
        public override LocalisableString Description => "Stretch or compact spacing between objects while keeping patterns playable.";

        public override bool Ranked => true;

        public override string ExtendedIconInformation => string.Create(CultureInfo.InvariantCulture, $"{SpacingMultiplier.Value:0.##}x");

        public override Type[] IncompatibleMods => new[]
        {
            typeof(OsuModRandom),
            typeof(OsuModTargetPractice),
        };

        [SettingSource("Spacing multiplier", "Target multiplier for distance between consecutive objects.", SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> SpacingMultiplier { get; } = new BindableDouble(1.5)
        {
            MinValue = 0.5,
            MaxValue = 3.0,
            Precision = 0.05,
        };

        [SettingSource("Space streams", "Also adjust spacing inside streams. Off keeps streams at their original spacing.")]
        public BindableBool SpaceStreams { get; } = new BindableBool(true);

        [SettingSource("Rotate sliders to fit", "Allow sliders to rotate smoothly around their head when needed to avoid playfield edges or awkward flow. Slider velocity is unchanged.")]
        public BindableBool RotateSliders { get; } = new BindableBool(true);

        [SettingSource("Accent jump angles", "Make turns between consecutive jumps sharper without creating overlaps.")]
        public BindableBool AccentAngles { get; } = new BindableBool();

        [SettingSource("Kiai only", "Only adjust objects inside kiai sections.")]
        public BindableBool KiaiOnly { get; } = new BindableBool();

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Spacing", string.Create(CultureInfo.InvariantCulture, $"{SpacingMultiplier.Value:0.##}x"));

                if (!SpaceStreams.IsDefault)
                    yield return ("Space streams", SpaceStreams.Value ? "On" : "Off");

                if (!RotateSliders.IsDefault)
                    yield return ("Rotate sliders", RotateSliders.Value ? "On" : "Off");

                if (!AccentAngles.IsDefault)
                    yield return ("Accent angles", AccentAngles.Value ? "On" : "Off");

                if (!KiaiOnly.IsDefault)
                    yield return ("Kiai only", KiaiOnly.Value ? "On" : "Off");
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            if (beatmap is not OsuBeatmap osuBeatmap || osuBeatmap.HitObjects.Count < 2)
                return;

            var options = new AmplifyOptions(
                SpacingMultiplier.Value,
                SpaceStreams.Value,
                RotateSliders.Value,
                AccentAngles.Value,
                KiaiOnly.Value);

            var placements = OsuSpacingAmplifier.Amplify(osuBeatmap.HitObjects, osuBeatmap.ControlPointInfo, options);

            for (int i = 0; i < osuBeatmap.HitObjects.Count; i++)
            {
                var hitObject = osuBeatmap.HitObjects[i];
                var placement = placements[i];

                if (hitObject is Spinner)
                    continue;

                if (hitObject is Slider slider && MathF.Abs(placement.Rotation) > 1e-5f)
                    rotateSliderExact(slider, placement.Rotation);

                if (hitObject.Position != placement.Position)
                    hitObject.Position = placement.Position;
            }

            // Recompute stacking after repositioning so stacks and slider-end offsets stay consistent.
            OsuBeatmapProcessor.ApplyStacking(osuBeatmap);
        }

        private static void rotateSliderExact(Slider slider, float radians)
        {
            double lockedDistance = slider.Path.ExpectedDistance.Value ?? slider.Path.Distance;
            PathControlPoint[] controlPoints = slider.Path.ControlPoints
                                                     .Select(p => new PathControlPoint(OsuSpacingAmplifier.Rotate(p.Position, radians), p.Type))
                                                     .ToArray();

            slider.Path = new SliderPath(controlPoints, lockedDistance);
        }
    }
}
