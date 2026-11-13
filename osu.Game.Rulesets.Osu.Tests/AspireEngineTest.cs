// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Beatmaps.Timing;
using osu.Game.IO;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Screens.Edit;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class AspireEngineTest
    {
        [Test]
        public void Test2BEnabledByDefaultWithoutGlobalThemeDependency()
        {
            using var config = new OsuRulesetConfigManager(null, new OsuRuleset().RulesetInfo);
            Assert.That(config.Get<bool>(OsuRulesetSetting.KeepOverlapCirclesVisible), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestSignedTimingRoundTrip(int mode)
        {
            var original = decode("552,-9.88131291682493E-324,-4,2,0,5,1,9\n3552,9.8E+304,4,1,7,60,1,0", mode: mode);
            foreach (var beatmap in new[] { original, decodeEncoded(encode(original)) })
            {
                var points = (LegacyControlPointInfo)beatmap.ControlPointInfo;
                Assert.That(points.TimingPoints.Select(p => p.LegacyBeatLength), Is.EqualTo(new[] { -2 * double.Epsilon, 9.8E+304 }));
                Assert.That(points.TimingPoints.Select(p => p.BeatLength), Is.EqualTo(new[] { 6d, 60000d }));
                Assert.That(points.TimingPoints[0].LegacyTimeSignature, Is.EqualTo(-4));
                Assert.That(points.TimingPoints[0].TimeSignature, Is.EqualTo(TimeSignature.SimpleQuadruple));
                Assert.That(points.TimingPoints[0].OmitFirstBarLine, Is.True);
                Assert.That(points.EffectPointAt(552).KiaiMode, Is.True);
                Assert.That(points.SamplePointAt(552).SampleVolume, Is.EqualTo(5));
                Assert.That(points.DifficultyPointAt(552).SliderVelocity, Is.EqualTo(mode == 0 ? 1 : 10));
                Assert.That(points.EffectPointAt(552).ScrollSpeed, Is.EqualTo(mode is 1 or 3 ? 10 : 1));
            }
        }

        [TestCase(-50, 1000)]
        [TestCase(0, 1000)]
        [TestCase(0.01, 0.005)]
        [TestCase(120000, 60000)]
        public void TestStandardTimingAndNodeSamples(double raw, double effective)
        {
            string timing = FormattableString.Invariant($"0,{raw},4,1,0,100,1,0\n500,-50,4,1,0,100,0,0\n1500,-50,4,2,0,20,0,0");
            var decoded = decode(timing, "256,192,1000,2,0,L|456:192,2,200");
            var slider = (Slider)playable(decoded).HitObjects.Single();
            Assert.That(slider.Velocity, Is.EqualTo(100 / effective).Within(1e-8));
            Assert.That(slider.Duration, Is.EqualTo(4 * effective).Within(1e-8));
            Assert.That(slider.TickDistance, Is.EqualTo(raw > 0 ? 200 : 100).Within(1e-8));
            var repeat = slider.NestedHitObjects.OfType<SliderRepeat>().Single();
            Assert.That(repeat.Samples.Single().Bank, Is.EqualTo(raw == 0.01 ? "normal" : "soft"));
            Assert.That(repeat.Samples.Single().Volume, Is.EqualTo(raw == 0.01 ? 100 : 20));
        }

        [TestCase("5e-324")]
        [TestCase("9.8e304")]
        public void TestExtremeTimingRemainsFinite(string raw)
        {
            var decoded = decode($"0,{raw},4,1,0,100,1,0", "256,192,1000,2,0,L|456:192,2,200");
            var slider = (Slider)playable(decoded).HitObjects.Single();
            Assert.That(decoded.ControlPointInfo.TimingPoints.Single().LegacyBeatLength, Is.EqualTo(double.Parse(raw, CultureInfo.InvariantCulture)));
            Assert.That(double.IsFinite(slider.EndTime), Is.True);
            Assert.That(double.IsFinite(slider.Velocity), Is.True);
            Assert.That(double.IsFinite(slider.TickDistance), Is.True);
            Assert.That(slider.NestedHitObjects.Count, Is.LessThan(10010));
            Assert.That(slider.NestedHitObjects.All(h => double.IsFinite(h.StartTime)), Is.True);
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        public void TestNonFiniteRedlinesRejected(string raw)
        {
            Assert.That(decode($"0,{raw},4,1,0,100,1,0").ControlPointInfo.TimingPoints, Is.Empty);
        }

        [Test]
        public void TestNaNGreenDisablesTicks()
        {
            var slider = (Slider)playable(decode("0,-50,4,1,0,100,1,0\n500,NaN,4,1,0,100,0,0", "256,192,1000,2,0,L|456:192,1,200")).HitObjects.Single();
            Assert.That(slider.GenerateTicks, Is.False);
            Assert.That(slider.NestedHitObjects.OfType<SliderTick>(), Is.Empty);
        }

        [TestCase(DefaultValueHandling.Include)]
        [TestCase(DefaultValueHandling.Ignore)]
        [TestCase(DefaultValueHandling.Populate)]
        [TestCase(DefaultValueHandling.IgnoreAndPopulate)]
        public void TestTimingJsonAndEditLifecycle(DefaultValueHandling defaults)
        {
            var point = decode("0,-50,-4,1,0,100,1,0").ControlPointInfo.TimingPoints.Single();
            var settings = JsonSerializableExtensions.CreateGlobalSettings();
            settings.DefaultValueHandling = defaults;
            var restored = JsonConvert.DeserializeObject<TimingControlPoint>(JsonConvert.SerializeObject(point, settings), settings)!;
            Assert.That(restored, Is.EqualTo(point));
            Assert.That(restored.GetHashCode(), Is.EqualTo(point.GetHashCode()));
            var clone = (TimingControlPoint)point.DeepClone();
            clone.CopyFrom(clone);
            Assert.That(clone, Is.EqualTo(point));
            clone.BeatLengthBindable.TriggerChange();
            clone.TimeSignatureBindable.TriggerChange();
            Assert.That(clone, Is.EqualTo(point));
            int changes = 0;
            clone.Changed += _ => changes++;
            clone.BeatLength = clone.BeatLength;
            clone.TimeSignature = clone.TimeSignature;
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(clone.LegacyBeatLength, Is.Null);
            Assert.That(clone.LegacyTimeSignature, Is.Null);
            clone.CopyFrom(point);
            clone.BeatLengthBindable.Value = 500;
            clone.TimeSignatureBindable.Value = TimeSignature.SimpleTriple;
            Assert.That(clone.LegacyBeatLength, Is.Null);
            Assert.That(clone.LegacyTimeSignature, Is.Null);
            Assert.That(point.LegacyBeatLength, Is.EqualTo(-50));
        }

        [Test]
        public void TestNativeJsonAndDisabledDefaults()
        {
            var point = new TimingControlPoint { BeatLength = 500 };
            var restored = point.Serialize().Deserialize<TimingControlPoint>();
            Assert.That(restored, Is.EqualTo(point));
            Assert.That(restored.LegacyBeatLength, Is.Null);
            Assert.That(restored.LegacyTimeSignature, Is.Null);
            Assert.Throws<InvalidOperationException>(() => TimingControlPoint.DEFAULT.LegacyBeatLength = -50);
            Assert.Throws<InvalidOperationException>(() => TimingControlPoint.DEFAULT.LegacyTimeSignature = -4);
            Assert.Throws<ArgumentOutOfRangeException>(() => point.LegacyBeatLength = double.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => point.LegacyBeatLength = double.PositiveInfinity);
        }

        [Test]
        public void TestEditorUndoRedo()
        {
            using var editor = new EditorBeatmap(playable(decode("0,-50,-4,1,0,100,1,0", "256,192,1000,1,0")));
            using var history = new BeatmapEditorChangeHandler(editor);
            history.SaveState();
            editor.BeginChange();
            var point = editor.ControlPointInfo.TimingPoints.Single();
            point.BeatLength = point.BeatLength;
            point.TimeSignature = point.TimeSignature;
            editor.EndChange();
            Assert.That(point.LegacyBeatLength, Is.Null);
            Assert.That(point.LegacyTimeSignature, Is.Null);
            history.RestoreState(-1);
            Assert.That(editor.ControlPointInfo.TimingPoints.Single().LegacyBeatLength, Is.EqualTo(-50));
            Assert.That(editor.ControlPointInfo.TimingPoints.Single().LegacyTimeSignature, Is.EqualTo(-4));
            history.RestoreState(1);
            Assert.That(editor.ControlPointInfo.TimingPoints.Single().LegacyBeatLength, Is.EqualTo(6));
            Assert.That(editor.ControlPointInfo.TimingPoints.Single().LegacyTimeSignature, Is.Null);
        }

        [TestCase(-2000000000, 2000000000, 0)]
        [TestCase(1000.1, 1840.9, 1)]
        [TestCase(1000.9, 1841.1, 0)]
        [TestCase(-1000.9, -160.1, 1)]
        public void TestWideStacking(double first, double second, int height)
        {
            var map = playable(decode("0,1000,4,1,0,100,1,0", FormattableString.Invariant($"256,192,{first},1,0\n256,192,{second},1,0")));
            Assert.That(((OsuHitObject)map.HitObjects[0]).StackHeight, Is.EqualTo(height));
        }

        [Test]
        public void TestSimultaneousDeclarationOrderSurvivesConversion()
        {
            var map = playable(decode("0,1000,4,1,0,100,1,0", "300,192,1000,1,0\n200,192,500,1,0\n100,192,1000,2,0,L|200:192,1,100\n400,192,1000,1,0"));
            Assert.That(map.HitObjects.Cast<OsuHitObject>().Select(h => h.Position.X), Is.EqualTo(new[] { 200f, 300f, 100f, 400f }));
        }

        [Test]
        public void TestOrderingComparatorIsTotalAndUsesDeclarationTieBreak()
        {
            var later = new OsuVisualOrdering.VisualKey(0, 1000, 1, 2, 1, 0, 1);
            var earlier = later with { Declaration = 1 };
            Assert.That(later.CompareTo(earlier), Is.GreaterThan(0));
            Assert.That(earlier.CompareTo(later), Is.LessThan(0));
            Assert.That(later.CompareTo(later), Is.Zero);
            var values = new[] { later, earlier, later with { Time = 1200 }, later with { Time = 800 }, later with { Phase = 0 }, later with { Group = 1 } };
            foreach (var a in values)
                foreach (var b in values)
                    foreach (var c in values)
                    {
                        if (a.CompareTo(b) <= 0 && b.CompareTo(c) <= 0)
                            Assert.That(a.CompareTo(c), Is.LessThanOrEqualTo(0));
                    }
        }

        [Test]
        public void TestCappedSliderTicksKeepOriginalProgress()
        {
            var events = SliderEventGenerator.Generate(1000, 200000, 1, 25000, 200000, 2).ToArray();
            var forward = events.Where(e => e.Type == SliderEventType.Tick && e.SpanIndex == 0).ToArray();
            var reverse = events.Where(e => e.Type == SliderEventType.Tick && e.SpanIndex == 1).ToArray();
            Assert.That(forward.Select(e => e.PathProgress), Is.EqualTo(new[] { 0.125, 0.25, 0.375 }));
            Assert.That(forward.Select(e => e.Time), Is.EqualTo(new[] { 26000d, 51000d, 76000d }));
            Assert.That(reverse.Select(e => e.PathProgress), Is.EqualTo(new[] { 0.375, 0.25, 0.125 }));
            Assert.That(reverse.Select(e => e.Time), Is.Ordered);
        }

        [Test]
        public void TestTickBudgetZeroDurationAndCancellation()
        {
            Assert.That(SliderEventGenerator.Generate(0, 1000, 0, double.Epsilon, 1000, 2).Count(), Is.LessThanOrEqualTo(10004));
            Assert.That(SliderEventGenerator.Generate(0, 0, 1, 100, 0, 1).All(e => double.IsFinite(e.PathProgress)), Is.True);
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            Assert.Throws<OperationCanceledException>(() => SliderEventGenerator.Generate(0, 1000, 1, 100, 1000, 1, cancel.Token).ToArray());
        }

        [Test]
        public void TestLongLengthIsFiniteAndRoundTripsImplicitLength()
        {
            var explicitMap = decode("0,1000,4,1,0,100,1,0", "256,192,1000,2,0,L|356:192,1,200000");
            Assert.That(((IHasPath)explicitMap.HitObjects.Single()).Path.Distance, Is.EqualTo(200000));
            foreach (string invalid in new[] { "NaN", "Infinity", "-Infinity", "1e300" })
                Assert.That(decode("0,1000,4,1,0,100,1,0", $"256,192,1000,2,0,L|356:192,1,{invalid}").HitObjects, Is.Empty);
            var implicitMap = decode("0,1000,4,1,0,100,1,0", "256,192,1000,2,0,L|356:192,1,0");
            var restored = decodeEncoded(encode(implicitMap));
            Assert.That(((IHasPath)restored.HitObjects.Single()).Path.ExpectedDistance.Value, Is.Null);
        }

        [Test]
        public void TestPathRangeTrimAndInvalidation()
        {
            var path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0), new Vector2(200, 0) }, 200);
            var range = new List<Vector2>();
            path.GetPathToProgress(range, 0.25, 0.75);
            Assert.That(range, Is.EqualTo(new[] { new Vector2(50, 0), new Vector2(100, 0), new Vector2(150, 0) }));
            Assert.That(path.LegacyTrimRemovesAllSegments, Is.False);
            path.ExpectedDistance.Value = 0.00001;
            Assert.That(path.LegacyTrimRemovesAllSegments, Is.True);
            path.ExpectedDistance.Value = 200;
            Assert.That(path.LegacyTrimRemovesAllSegments, Is.False);
            var empty = new SliderPath();
            empty.GetPathToProgress(range, 0, 1);
            Assert.That(range, Is.EqualTo(new[] { Vector2.Zero, Vector2.Zero }));
        }

        [Test]
        public void TestViewportScaleIsBounded()
        {
            Assert.That(PlaySliderBody.GetViewportScale(new Vector2(2000, 500), 1000), Is.EqualTo(new Vector2(0.5f, 1)));
            Assert.That(PlaySliderBody.GetViewportScale(Vector2.Zero, 1000), Is.EqualTo(Vector2.One));
            Assert.That(PlaySliderBody.GetViewportScale(new Vector2(float.NaN, float.PositiveInfinity), 1000), Is.EqualTo(Vector2.One));
        }

        private static IBeatmap playable(Beatmap beatmap) => new TestWorkingBeatmap(beatmap).GetPlayableBeatmap(new OsuRuleset().RulesetInfo);

        private static Beatmap decode(string timing, string objects = "", int mode = 0) => decodeEncoded($"""
            osu file format v14
            [General]
            Mode:{mode}
            [Difficulty]
            ApproachRate:5
            CircleSize:4
            SliderMultiplier:1
            SliderTickRate:1
            [TimingPoints]
            {timing}
            [HitObjects]
            {objects}
            """);

        private static Beatmap decodeEncoded(string content)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            using var reader = new LineBufferedReader(stream);
            return new SourceModeDecoder().Decode(reader);
        }

        // Exercise source-mode decoding without requiring the other ruleset assemblies in this test project.
        private class SourceModeDecoder() : LegacyBeatmapDecoder(14)
        {
            protected override void ParseLine(Beatmap beatmap, Section section, string line, bool isPrimaryStream)
            {
                if (section == Section.General && line.StartsWith("Mode:", StringComparison.Ordinal) && line != "Mode:0")
                {
                    beatmap.BeatmapInfo.Ruleset = new RulesetInfo { OnlineID = int.Parse(line.AsSpan(5), CultureInfo.InvariantCulture) };
                    return;
                }
                base.ParseLine(beatmap, section, line, isPrimaryStream);
            }
        }

        private static string encode(IBeatmap beatmap)
        {
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            new LegacyBeatmapEncoder(beatmap, null, null).Encode(writer);
            return writer.ToString();
        }
    }
}
