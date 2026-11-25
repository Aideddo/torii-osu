// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Tests.Beatmaps;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [TestFixture]
    public class OsuModAmplifyTest
    {
        private static readonly OsuRuleset ruleset = new OsuRuleset();

        private static OsuBeatmap createBeatmap(IEnumerable<OsuHitObject> objects, double beatLength = 500, float cs = 4f, params (double time, bool kiai)[] kiaiPoints)
        {
            var controlPoints = new ControlPointInfo();
            controlPoints.Add(0, new TimingControlPoint { BeatLength = beatLength });

            foreach (var (time, kiai) in kiaiPoints)
                controlPoints.Add(time, new EffectControlPoint { KiaiMode = kiai });

            var beatmap = new OsuBeatmap
            {
                BeatmapInfo =
                {
                    Ruleset = ruleset.RulesetInfo,
                    Difficulty = new BeatmapDifficulty
                    {
                        CircleSize = cs,
                        ApproachRate = 9f,
                        OverallDifficulty = 8f,
                        DrainRate = 5f,
                        SliderMultiplier = 1.4,
                        SliderTickRate = 1,
                    }
                },
                StackLeniency = 0.7f,
                ControlPointInfo = controlPoints,
            };

            beatmap.HitObjects.AddRange(objects);

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            OsuBeatmapProcessor.ApplyStacking(beatmap);
            return beatmap;
        }

        private static HitCircle circle(float x, float y, double time) => new HitCircle
        {
            Position = new Vector2(x, y),
            StartTime = time,
        };

        private static Slider straightSlider(float x, float y, double time, Vector2 offset, int repeats = 0) => new Slider
        {
            Position = new Vector2(x, y),
            StartTime = time,
            RepeatCount = repeats,
            Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, offset }),
        };

        private static Slider curvedSlider(float x, float y, double time, Vector2 mid, Vector2 end, int repeats = 0) => new Slider
        {
            Position = new Vector2(x, y),
            StartTime = time,
            RepeatCount = repeats,
            Path = new SliderPath(PathType.PERFECT_CURVE, new[] { Vector2.Zero, mid, end }),
        };

        private static Spinner spinner(double start, double end) => new Spinner
        {
            Position = OsuPlayfield.BASE_SIZE / 2,
            StartTime = start,
            EndTime = end,
        };

        private static bool isInsidePlayfield(Vector2 p, float margin = 0f)
            => p.X >= margin - 0.5f && p.X <= OsuPlayfield.BASE_SIZE.X - margin + 0.5f
               && p.Y >= margin - 0.5f && p.Y <= OsuPlayfield.BASE_SIZE.Y - margin + 0.5f;

        [Test]
        public void TestIdentityAtFactorOne()
        {
            var beatmap = createBeatmap(new OsuHitObject[]
            {
                circle(120, 120, 0),
                straightSlider(220, 160, 250, new Vector2(80, 20)),
                curvedSlider(340, 200, 750, new Vector2(40, 40), new Vector2(80, 0), repeats: 1),
                spinner(1500, 2000),
                circle(260, 260, 2500),
            });

            var origPositions = beatmap.HitObjects.Select(o => o.Position).ToArray();
            var origEnds = beatmap.HitObjects.Select(o => o.EndPosition).ToArray();

            var mod = new OsuModAmplify { SpacingMultiplier = { Value = 1.0 } };
            mod.ApplyToBeatmap(beatmap);

            for (int i = 0; i < beatmap.HitObjects.Count; i++)
            {
                Assert.That(beatmap.HitObjects[i].Position, Is.EqualTo(origPositions[i]));
                Assert.That(beatmap.HitObjects[i].EndPosition, Is.EqualTo(origEnds[i]));
            }
        }

        [Test]
        public void TestSmallJumpsScaleAndBigJumpsSaturate()
        {
            var smallObjects = Enumerable.Range(0, 14)
                                         .Select(i => circle(i % 2 == 0 ? 226 : 286, 192, i * 250))
                                         .ToArray();
            var smallMap = createBeatmap(smallObjects);

            new OsuModAmplify { SpacingMultiplier = { Value = 2.0 } }.ApplyToBeatmap(smallMap);

            for (int i = 1; i < smallMap.HitObjects.Count; i++)
            {
                float dist = Vector2.Distance(smallMap.HitObjects[i].Position, smallMap.HitObjects[i - 1].Position);
                Assert.That(dist, Is.InRange(115f, 121f), $"small jump {i}");
                Assert.That(isInsidePlayfield(smallMap.HitObjects[i].Position, 12f), Is.True);
            }

            var bigObjects = Enumerable.Range(0, 10)
                                       .Select(i => circle(i % 2 == 0 ? 106 : 406, 192, i * 250))
                                       .ToArray();
            var bigMap = createBeatmap(bigObjects);

            new OsuModAmplify { SpacingMultiplier = { Value = 2.0 } }.ApplyToBeatmap(bigMap);

            for (int i = 1; i < bigMap.HitObjects.Count; i++)
            {
                float dist = Vector2.Distance(bigMap.HitObjects[i].Position, bigMap.HitObjects[i - 1].Position);
                Assert.That(dist, Is.InRange(430f, 455f), $"big jump {i} should saturate smoothly");
                Assert.That(isInsidePlayfield(bigMap.HitObjects[i].Position, 12f), Is.True);
            }
        }

        [Test]
        public void TestCompactingShrinksSmoothlyWithoutCollapsingStacks()
        {
            var objects = Enumerable.Range(0, 12)
                                    .Select(i => circle(i % 2 == 0 ? 176 : 336, 192, i * 250))
                                    .ToArray();
            var beatmap = createBeatmap(objects);

            new OsuModAmplify { SpacingMultiplier = { Value = 0.6 } }.ApplyToBeatmap(beatmap);

            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                float dist = Vector2.Distance(beatmap.HitObjects[i].Position, beatmap.HitObjects[i - 1].Position);
                Assert.That(dist, Is.InRange(92f, 100f), $"compacted jump {i}");
                Assert.That(dist, Is.GreaterThan(OsuBeatmapProcessor.STACK_DISTANCE));
            }
        }

        [Test]
        public void TestCircleSizeAwareMarginKeepsObjectsOffPlayfieldEdge()
        {
            var objects = Enumerable.Range(0, 10)
                                    .Select(i => circle(i % 2 == 0 ? 80 : 432, i % 2 == 0 ? 60 : 324, i * 250))
                                    .ToArray();
            var beatmap = createBeatmap(objects, cs: 4f);

            new OsuModAmplify { SpacingMultiplier = { Value = 3.0 } }.ApplyToBeatmap(beatmap);

            // CS4 radius is ~36.4px -> comfortable margin ~15.3px, far cleaner than 1px clipping.
            foreach (var obj in beatmap.HitObjects)
                Assert.That(isInsidePlayfield(obj.Position, 14f), Is.True, $"position {obj.Position} clipped too close to playfield edge");
        }

        [Test]
        public void TestLongStreamAndDeathstreamStaySmoothWithoutCuts()
        {
            // 48-note 1/4 stream along diagonal at 180 BPM.
            double beatLength = 60000.0 / 180.0;
            var stream = Enumerable.Range(0, 48)
                                   .Select(i => circle(70 + 7 * i, 70 + 5 * i, i * beatLength / 4))
                                   .ToArray();
            var beatmap = createBeatmap(stream, beatLength: beatLength);

            new OsuModAmplify { SpacingMultiplier = { Value = 2.0 }, SpaceStreams = { Value = true } }.ApplyToBeatmap(beatmap);

            var gaps = new double[stream.Length - 1];
            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                Assert.That(isInsidePlayfield(beatmap.HitObjects[i].Position, 10f), Is.True);
                gaps[i - 1] = Vector2.Distance(beatmap.HitObjects[i].Position, beatmap.HitObjects[i - 1].Position);
            }

            double median = gaps.OrderBy(g => g).ElementAt(gaps.Length / 2);
            double originalSpacing = Math.Sqrt(7 * 7 + 5 * 5);

            Assert.That(median, Is.GreaterThan(1.3 * originalSpacing), "stream should grow noticeably");
            Assert.That(gaps.Max() / gaps.Min(), Is.LessThan(1.25), "stream should remain uniform with no sudden cuts");
        }

        [Test]
        public void TestEdgeStuckStreamTurnsIntoPlayfield()
        {
            double beatLength = 60000.0 / 180.0;
            // Anchor a jump at top-left, then run an 8-note stream tightly along the bottom edge.
            var objects = new List<OsuHitObject>
            {
                circle(60, 60, 0),
                circle(180, 362, 500),
            };

            for (int i = 1; i < 8; i++)
                objects.Add(circle(180 + 30 * i, 362, 500 + i * beatLength / 4));

            var beatmap = createBeatmap(objects, beatLength: beatLength);
            new OsuModAmplify { SpacingMultiplier = { Value = 2.0 }, SpaceStreams = { Value = true } }.ApplyToBeatmap(beatmap);

            double avgStreamGap = 0;
            for (int i = 2; i < beatmap.HitObjects.Count; i++)
            {
                Assert.That(isInsidePlayfield(beatmap.HitObjects[i].Position, 10f), Is.True);
                avgStreamGap += Vector2.Distance(beatmap.HitObjects[i].Position, beatmap.HitObjects[i - 1].Position);
            }

            avgStreamGap /= beatmap.HitObjects.Count - 2;
            Assert.That(avgStreamGap, Is.GreaterThan(42.0), "edge-stuck stream should turn into playfield to achieve spacing");
        }

        [Test]
        public void TestSpaceStreamsOffKeepsStreamsRigid()
        {
            var objects = new OsuHitObject[]
            {
                circle(100, 192, 0),
                circle(200, 192, 500),
                circle(220, 200, 625),
                circle(240, 192, 750),
                circle(260, 200, 875),
                circle(360, 192, 1375),
            };
            var beatmap = createBeatmap(objects, beatLength: 500);
            float origStreamStep = Vector2.Distance(objects[2].Position, objects[1].Position);

            new OsuModAmplify { SpacingMultiplier = { Value = 2.0 }, SpaceStreams = { Value = false } }.ApplyToBeatmap(beatmap);

            Assert.That(Vector2.Distance(beatmap.HitObjects[1].Position, beatmap.HitObjects[0].Position), Is.GreaterThan(150f));
            for (int i = 2; i <= 4; i++)
            {
                Assert.That(Vector2.Distance(beatmap.HitObjects[i].Position, beatmap.HitObjects[i - 1].Position),
                    Is.EqualTo(origStreamStep).Within(0.25f), $"stream step {i} should stay rigid");
            }
        }

        [Test]
        public void TestSlidersPreserveVelocityAndUpdateNestedObjects()
        {
            var objects = new OsuHitObject[]
            {
                circle(100, 192, 0),
                curvedSlider(200, 160, 300, new Vector2(45, -35), new Vector2(90, 0), repeats: 1),
                straightSlider(360, 260, 1000, new Vector2(100, 20), repeats: 0),
                circle(440, 120, 1600),
            };
            var beatmap = createBeatmap(objects);

            var sliders = beatmap.HitObjects.OfType<Slider>().ToArray();
            var origVelocities = sliders.Select(s => s.Velocity).ToArray();
            var origDurations = sliders.Select(s => s.Duration).ToArray();
            var origDistances = sliders.Select(s => s.Path.Distance).ToArray();
            var origNestedCounts = sliders.Select(s => s.NestedHitObjects.Count).ToArray();

            new OsuModAmplify { SpacingMultiplier = { Value = 2.0 }, RotateSliders = { Value = true } }.ApplyToBeatmap(beatmap);

            for (int sIdx = 0; sIdx < sliders.Length; sIdx++)
            {
                var slider = sliders[sIdx];
                Assert.That(slider.Velocity, Is.EqualTo(origVelocities[sIdx]).Within(1e-6), "slider velocity must not change");
                Assert.That(slider.Duration, Is.EqualTo(origDurations[sIdx]).Within(1e-6), "slider duration must not change");
                Assert.That(slider.Path.Distance, Is.EqualTo(origDistances[sIdx]).Within(1e-3), "slider path length must not change");
                Assert.That(slider.NestedHitObjects.Count, Is.EqualTo(origNestedCounts[sIdx]));
                Assert.That(slider.HeadCircle.Position, Is.EqualTo(slider.Position));
                Assert.That(slider.TailCircle.Position, Is.EqualTo(slider.EndPosition));

                foreach (var tick in slider.NestedHitObjects.OfType<SliderTick>())
                    Assert.That(Vector2.Distance(tick.Position, slider.Position + slider.Path.PositionAt(tick.PathProgress)), Is.LessThan(0.05f));

                foreach (var repeat in slider.NestedHitObjects.OfType<SliderRepeat>())
                    Assert.That(Vector2.Distance(repeat.Position, slider.Position + slider.Path.PositionAt(repeat.PathProgress)), Is.LessThan(0.05f));

                foreach (var pt in slider.Path.CalculatedPath)
                    Assert.That(isInsidePlayfield(slider.Position + pt, 10f), Is.True, $"slider {sIdx} point {slider.Position + pt} left playfield");
            }
        }

        [Test]
        public void TestWallBlockedSliderRotatesOnlyWhenEnabled()
        {
            OsuBeatmap makeMap() => createBeatmap(new OsuHitObject[]
            {
                circle(180, 192, 0),
                straightSlider(340, 192, 250, new Vector2(140, 0)),
                circle(340, 320, 750),
                circle(160, 192, 1000),
            });

            var mapWithRot = makeMap();
            var mapNoRot = makeMap();

            var origControlOffset = ((Slider)mapNoRot.HitObjects[1]).Path.ControlPoints[1].Position;

            new OsuModAmplify { SpacingMultiplier = { Value = 2.0 }, RotateSliders = { Value = false } }.ApplyToBeatmap(mapNoRot);
            new OsuModAmplify { SpacingMultiplier = { Value = 2.0 }, RotateSliders = { Value = true } }.ApplyToBeatmap(mapWithRot);

            var sliderNoRot = (Slider)mapNoRot.HitObjects[1];
            var sliderWithRot = (Slider)mapWithRot.HitObjects[1];

            Assert.That(sliderNoRot.Path.ControlPoints[1].Position, Is.EqualTo(origControlOffset), "RotateSliders=false must not rotate sliders");
            Assert.That(sliderWithRot.Path.Distance, Is.EqualTo(140.0).Within(0.05));

            foreach (var pt in sliderWithRot.Path.CalculatedPath)
                Assert.That(isInsidePlayfield(sliderWithRot.Position + pt, 10f), Is.True);
        }

        [Test]
        public void TestStacksStayStackedAndRestacked()
        {
            var objects = new OsuHitObject[]
            {
                circle(200, 192, 0),
                circle(200, 192, 125),
                circle(200, 192, 250),
                circle(300, 192, 500),
                circle(300, 192, 625),
            };
            var beatmap = createBeatmap(objects);

            new OsuModAmplify { SpacingMultiplier = { Value = 2.0 } }.ApplyToBeatmap(beatmap);

            Assert.That(Vector2.Distance(beatmap.HitObjects[1].Position, beatmap.HitObjects[0].Position), Is.LessThan(0.5f));
            Assert.That(Vector2.Distance(beatmap.HitObjects[2].Position, beatmap.HitObjects[1].Position), Is.LessThan(0.5f));
            Assert.That(Vector2.Distance(beatmap.HitObjects[4].Position, beatmap.HitObjects[3].Position), Is.LessThan(0.5f));
            Assert.That(beatmap.HitObjects[0].StackHeight, Is.GreaterThan(0), "stack heights should remain computed after ApplyStacking");
            Assert.That(Vector2.Distance(beatmap.HitObjects[3].Position, beatmap.HitObjects[2].Position), Is.GreaterThan(150f));
        }

        [Test]
        public void TestKiaiOnlyLeavesNonKiaiUntouched()
        {
            var objects = Enumerable.Range(0, 12)
                                    .Select(i => circle(100 + 30 * i, i % 2 == 0 ? 150 : 230, i * 250))
                                    .ToArray();
            var beatmap = createBeatmap(objects, beatLength: 500, cs: 4f, (0, false), (1000, true), (2250, false));
            var origPos = beatmap.HitObjects.Select(o => o.Position).ToArray();

            new OsuModAmplify { SpacingMultiplier = { Value = 2.0 }, KiaiOnly = { Value = true } }.ApplyToBeatmap(beatmap);

            for (int i = 0; i < beatmap.HitObjects.Count; i++)
            {
                bool inKiai = beatmap.ControlPointInfo.EffectPointAt(beatmap.HitObjects[i].StartTime).KiaiMode;
                if (!inKiai)
                    Assert.That(beatmap.HitObjects[i].Position, Is.EqualTo(origPos[i]), $"non-kiai object {i} must not move");
            }

            // Inside kiai (indices 4..8), jumps must have grown.
            float origJump = Vector2.Distance(origPos[6], origPos[5]);
            float newJump = Vector2.Distance(beatmap.HitObjects[6].Position, beatmap.HitObjects[5].Position);
            Assert.That(newJump, Is.GreaterThan(origJump * 1.4f));
        }

        [Test]
        public void TestAccentAnglesSharpensTurnsWithoutCreatingOverlaps()
        {
            var objects = Enumerable.Range(0, 12)
                                    .Select(i => circle(60 + 35 * i, i % 2 == 0 ? 150 : 220, i * 250))
                                    .ToArray();

            var plainMap = createBeatmap(objects.Select(o => circle(o.X, o.Y, o.StartTime)));
            var accentedMap = createBeatmap(objects.Select(o => circle(o.X, o.Y, o.StartTime)));

            new OsuModAmplify { SpacingMultiplier = { Value = 1.5 }, AccentAngles = { Value = false } }.ApplyToBeatmap(plainMap);
            new OsuModAmplify { SpacingMultiplier = { Value = 1.5 }, AccentAngles = { Value = true } }.ApplyToBeatmap(accentedMap);

            double avgTurn(OsuBeatmap map)
            {
                double sum = 0;
                int cnt = 0;
                for (int i = 2; i < map.HitObjects.Count; i++)
                {
                    Vector2 a = map.HitObjects[i - 1].Position - map.HitObjects[i - 2].Position;
                    Vector2 b = map.HitObjects[i].Position - map.HitObjects[i - 1].Position;
                    sum += Math.Abs(Math.Atan2(a.X * b.Y - a.Y * b.X, a.X * b.X + a.Y * b.Y)) * 180.0 / Math.PI;
                    cnt++;
                }

                return sum / cnt;
            }

            Assert.That(avgTurn(accentedMap), Is.GreaterThan(avgTurn(plainMap) + 4.0), "accented angles should sharpen jump turns");

            for (int i = 2; i < accentedMap.HitObjects.Count; i++)
            {
                for (int j = Math.Max(0, i - 6); j <= i - 2; j++)
                    Assert.That(Vector2.Distance(accentedMap.HitObjects[i].Position, accentedMap.HitObjects[j].Position), Is.GreaterThan(28f));
            }
        }

        [Test]
        public void TestDeterminismMonotonicStarRatingAndModCompatibility()
        {
            OsuBeatmap makeBaseMap()
            {
                var objects = new List<OsuHitObject>();
                for (int i = 0; i < 40; i++)
                    objects.Add(circle(140 + (i % 4) * 60, i % 2 == 0 ? 140 : 244, i * 200));

                return createBeatmap(objects, beatLength: 400);
            }

            double[] factors = { 1.0, 1.5, 2.0, 2.5 };
            double[] stars = new double[factors.Length];

            for (int f = 0; f < factors.Length; f++)
            {
                var map1 = makeBaseMap();
                var map2 = makeBaseMap();
                var mod1 = new OsuModAmplify { SpacingMultiplier = { Value = factors[f] } };
                var mod2 = new OsuModAmplify { SpacingMultiplier = { Value = factors[f] } };

                double sr1 = new OsuDifficultyCalculator(ruleset.RulesetInfo, new TestWorkingBeatmap(map1)).Calculate(new Mod[] { mod1 }).StarRating;
                double sr2 = new OsuDifficultyCalculator(ruleset.RulesetInfo, new TestWorkingBeatmap(map2)).Calculate(new Mod[] { mod2 }).StarRating;

                Assert.That(sr1, Is.EqualTo(sr2), $"determinism check failed at {factors[f]}x");

                for (int i = 0; i < map1.HitObjects.Count; i++)
                    Assert.That(map1.HitObjects[i].Position, Is.EqualTo(map2.HitObjects[i].Position), $"object {i} position must be deterministic");

                stars[f] = sr1;
            }

            for (int f = 1; f < factors.Length; f++)
                Assert.That(stars[f], Is.GreaterThanOrEqualTo(stars[f - 1] - 1e-6), $"SR should not drop when spacing increases ({factors[f - 1]}x -> {factors[f]}x)");

            Assert.That(stars[^1], Is.GreaterThan(stars[0] + 0.3), "amplified beatmap should have noticeably higher star rating");

            // Verify compatibility with HR, DT, RX, MR, DA.
            var combinedMods = new Mod[]
            {
                new OsuModAmplify { SpacingMultiplier = { Value = 1.75 } },
                new OsuModHardRock(),
                new OsuModDoubleTime(),
                new OsuModRelax(),
                new OsuModMirror(),
            };

            Assert.That(ModUtils.CheckCompatibleSet(combinedMods), Is.True);
            var combinedAttr = new OsuDifficultyCalculator(ruleset.RulesetInfo, new TestWorkingBeatmap(makeBaseMap())).Calculate(combinedMods);
            Assert.That(combinedAttr.StarRating, Is.GreaterThan(0));
        }

        [Test]
        public void TestMarathonPerformanceIsFast()
        {
            // 2000-object beatmap with 10 phrases of jumps, streams, and sliders separated by breaks.
            var objects = new List<OsuHitObject>(2000);
            double t = 0;

            for (int phrase = 0; phrase < 10; phrase++)
            {
                for (int i = 0; i < 160; i++)
                {
                    if (i % 20 == 10)
                    {
                        objects.Add(straightSlider(160 + (i % 5) * 40, 140 + (i % 3) * 40, t, new Vector2(60, 20)));
                        t += 250;
                    }
                    else
                    {
                        objects.Add(circle(140 + (i % 6) * 40, 120 + (i % 4) * 45, t));
                        t += (i % 32) < 16 ? 125 : 250;
                    }
                }

                // 40-note stream
                for (int i = 0; i < 40; i++)
                {
                    objects.Add(circle(100 + i * 8, 220 + MathF.Sin(i * 0.25f) * 35f, t));
                    t += 125;
                }

                // 5-beat break
                t += 2500;
            }

            var beatmap = createBeatmap(objects, beatLength: 500);

            // Warm up JIT once on a tiny map.
            new OsuModAmplify { SpacingMultiplier = { Value = 1.5 } }.ApplyToBeatmap(createBeatmap(new[] { circle(100, 100, 0), circle(200, 200, 250) }));

            var sw = Stopwatch.StartNew();
            new OsuModAmplify { SpacingMultiplier = { Value = 2.0 }, SpaceStreams = { Value = true }, RotateSliders = { Value = true } }.ApplyToBeatmap(beatmap);
            sw.Stop();

            TestContext.Out.WriteLine($"2000-object marathon solve time: {sw.ElapsedMilliseconds} ms");
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(500), "phrase-partitioned ADMM solver should stay fast for song-select SR calculation");

            foreach (var obj in beatmap.HitObjects)
                Assert.That(isInsidePlayfield(obj.Position, 8f), Is.True);
        }
    }
}
