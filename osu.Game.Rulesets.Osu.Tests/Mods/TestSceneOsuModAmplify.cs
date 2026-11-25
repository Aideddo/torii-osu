// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    public partial class TestSceneOsuModAmplify : OsuModTestScene
    {
        [TestCase(0.7)]
        [TestCase(1.5)]
        [TestCase(2.0)]
        public void TestDefaultBeatmap(double multiplier) => CreateModTest(new ModTestData
        {
            Mod = new OsuModAmplify
            {
                SpacingMultiplier = { Value = multiplier }
            },
            Autoplay = true,
            PassCondition = () => true
        });

        [Test]
        public void TestSliderAndStreamBeatmap() => CreateModTest(new ModTestData
        {
            Mod = new OsuModAmplify
            {
                SpacingMultiplier = { Value = 2.0 },
                SpaceStreams = { Value = true },
                RotateSliders = { Value = true },
            },
            CreateBeatmap = createMixedBeatmap,
            Autoplay = true,
            PassCondition = () => true
        });

        private static OsuBeatmap createMixedBeatmap()
        {
            var controlPoints = new ControlPointInfo();
            controlPoints.Add(0, new TimingControlPoint { BeatLength = 400 });

            var beatmap = new OsuBeatmap
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Difficulty = new BeatmapDifficulty
                    {
                        CircleSize = 4f,
                        ApproachRate = 9f,
                        SliderMultiplier = 1.6,
                    }
                },
                ControlPointInfo = controlPoints,
            };

            double t = 0;

            for (int i = 0; i < 8; i++)
            {
                beatmap.HitObjects.Add(new HitCircle
                {
                    StartTime = t,
                    Position = new Vector2(180 + (i % 2) * 140, 140 + (i % 3) * 50),
                    NewCombo = i == 0,
                });
                t += 200;
            }

            beatmap.HitObjects.Add(new Slider
            {
                StartTime = t,
                Position = new Vector2(320, 190),
                Path = new SliderPath(PathType.PERFECT_CURVE, new[] { Vector2.Zero, new Vector2(55, -35), new Vector2(110, 0) }),
                NewCombo = true,
            });
            t += 600;

            beatmap.HitObjects.AddRange(Enumerable.Range(0, 12).Select(i => new HitCircle
            {
                StartTime = t + i * 100,
                Position = new Vector2(140 + i * 20, 260 + (i % 2) * 12),
                NewCombo = i == 0,
            }));

            return beatmap;
        }
    }
}
