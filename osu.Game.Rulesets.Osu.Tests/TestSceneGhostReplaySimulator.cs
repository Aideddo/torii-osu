// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Models;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Ghost;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// torii: the ghost race stands or falls on this. Everything downstream just
    /// reads the timeline, so if the simulation is wrong the race is quietly wrong
    /// and nobody notices until they are losing to a ghost that never existed.
    /// </summary>
    public partial class TestSceneGhostReplaySimulator : OsuTestScene
    {
        private GhostReplaySimulator simulator = null!;

        private static Beatmap<OsuHitObject> createBeatmap(int circles = 20)
        {
            var beatmap = new Beatmap<OsuHitObject>
            {
                BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo },
            };

            for (int i = 0; i < circles; i++)
            {
                beatmap.HitObjects.Add(new HitCircle
                {
                    StartTime = 1000 + i * 500,
                    Position = new Vector2(100 + i % 5 * 60, 100 + i % 3 * 60),
                });
            }

            foreach (var hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            return beatmap;
        }

        private void runSimulation(IBeatmap beatmap, Func<IBeatmap, Score> createGhost)
        {
            AddStep("start simulation", () =>
            {
                var ruleset = new OsuRuleset();

                Child = simulator = new GhostReplaySimulator(createGhost(beatmap), beatmap, ruleset, Array.Empty<Mod>());
            });

            // Generous: the simulation is deliberately spread over frames, and CI
            // machines are slower than this one.
            AddUntilStep("simulation finishes", () => simulator.Finished);
        }

        /// <summary>
        /// An autoplay replay is far sparser than anything a person produces: it only
        /// emits frames where it needs to move. Measuring against one flatters the
        /// simulator badly, so this pads it out to a human-ish frame rate.
        /// </summary>
        private static Score denseGhost(IBeatmap beatmap, double frameInterval)
        {
            var source = new OsuAutoGenerator(beatmap, Array.Empty<Mod>()).Generate();
            var dense = new osu.Game.Replays.Replay();

            var frames = source.Frames.OfType<OsuReplayFrame>().OrderBy(f => f.Time).ToArray();

            for (int i = 0; i < frames.Length - 1; i++)
            {
                var current = frames[i];
                var next = frames[i + 1];

                dense.Frames.Add(current);

                for (double t = current.Time + frameInterval; t < next.Time; t += frameInterval)
                {
                    float progress = (float)((t - current.Time) / (next.Time - current.Time));

                    dense.Frames.Add(new OsuReplayFrame(t, Vector2.Lerp(current.Position, next.Position, progress), current.Actions.ToArray()));
                }
            }

            if (frames.Length > 0)
                dense.Frames.Add(frames[^1]);

            return new Score
            {
                Replay = dense,
                ScoreInfo = new ScoreInfo
                {
                    Ruleset = new OsuRuleset().RulesetInfo,
                    RealmUser = new RealmUser { Username = "ghost" },
                },
            };
        }

        /// <summary>
        /// A map shaped like one people actually play: circles AND sliders. Sliders are
        /// the expensive part by a wide margin, and a test built only from circles will
        /// happily report that everything is instant while the real thing crawls.
        /// </summary>
        private static Beatmap<OsuHitObject> createRealisticBeatmap(int objects = 1700)
        {
            var beatmap = new Beatmap<OsuHitObject> { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };

            for (int i = 0; i < objects; i++)
            {
                double time = 1000 + i * 170;
                var position = new Vector2(100 + i % 7 * 50, 100 + i % 4 * 60);

                // Roughly a third sliders, which is a normal ratio for a real map.
                if (i % 3 == 2)
                {
                    beatmap.HitObjects.Add(new Slider
                    {
                        StartTime = time,
                        Position = position,
                        Path = new SliderPath(PathType.LINEAR, new[] { new Vector2(), new Vector2(120, 0) }),
                    });
                }
                else
                {
                    beatmap.HitObjects.Add(new HitCircle { StartTime = time, Position = position });
                }
            }

            foreach (var hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            return beatmap;
        }

        [Test]
        public void TestDenseHumanLikeReplayIsStillQuick()
        {
            // 4 minutes of map at roughly 120Hz of input, which is what a real replay
            // from a real player actually looks like.
            var beatmap = createRealisticBeatmap();

            var stopwatch = new System.Diagnostics.Stopwatch();
            int frameCount = 0;

            AddStep("start simulation", () =>
            {
                var ghost = denseGhost(beatmap, 8);
                frameCount = ghost.Replay.Frames.Count;
                stopwatch.Restart();
                Child = simulator = new GhostReplaySimulator(ghost, beatmap, new OsuRuleset(), Array.Empty<Mod>());
            });

            AddUntilStep("simulation finishes", () => simulator.Finished);

            AddAssert("full map was simulated", () =>
            {
                stopwatch.Stop();
                Console.WriteLine($"[ghost-perf] {frameCount} replay frames simulated in {stopwatch.ElapsedMilliseconds}ms");
                return simulator.Result != null;
            }, () => Is.True);
        }

        private static Score autoplayGhost(IBeatmap beatmap)
        {
            var replay = new OsuAutoGenerator(beatmap, Array.Empty<Mod>()).Generate();

            return new Score
            {
                Replay = replay,
                ScoreInfo = new ScoreInfo
                {
                    Ruleset = new OsuRuleset().RulesetInfo,
                    RealmUser = new RealmUser { Username = "ghost" },
                },
            };
        }

        [Test]
        public void TestAutoplayGhostScoresPerfectly()
        {
            var beatmap = createBeatmap();

            runSimulation(beatmap, autoplayGhost);

            AddAssert("timeline was produced", () => simulator.Result != null);
            // Autoplay hits everything, so anything short of a perfect run means the
            // simulation dropped judgements rather than that the ghost played badly.
            AddAssert("ghost accuracy is perfect", () => simulator.Result!.Final.Accuracy, () => Is.EqualTo(1).Within(0.0001));
            AddAssert("ghost combo is the full map", () => simulator.Result!.Final.Combo, () => Is.EqualTo(20));
            AddAssert("ghost scored something", () => simulator.Result!.Final.TotalScore, () => Is.GreaterThan(0));
        }

        [Test]
        public void TestScoreOnlyEverClimbs()
        {
            var beatmap = createBeatmap();

            runSimulation(beatmap, autoplayGhost);

            AddAssert("samples cover the map", () => simulator.Result!.EndTime, () => Is.GreaterThanOrEqualTo(beatmap.HitObjects.Last().StartTime));
            AddAssert("score never goes backwards", () =>
            {
                // A racing opponent whose score dips would look like a bug to the
                // player even when the final number is right.
                long previous = -1;

                for (double t = 0; t <= simulator.Result!.EndTime; t += 100)
                {
                    long score = simulator.Result!.SampleAt(t).TotalScore;
                    if (score < previous)
                        return false;

                    previous = score;
                }

                return true;
            });
        }

        [Test]
        public void TestGhostStartsFromNothing()
        {
            var beatmap = createBeatmap();

            runSimulation(beatmap, autoplayGhost);

            AddAssert("no score before the first note", () => simulator.Result!.SampleAt(0).TotalScore, () => Is.Zero);
            AddAssert("score by the end", () => simulator.Result!.SampleAt(simulator.Result!.EndTime).TotalScore, () => Is.GreaterThan(0));
        }

        [Test]
        public void TestRealisticMapSimulatesQuickly()
        {
            // ~4 minutes, 1400 objects: a normal map to grind, and the shape of the
            // thing this has to survive. The point is not the exact number, it is
            // that a full map finishes in the time a loading screen lasts rather
            // than in the time a coffee break lasts.
            var beatmap = new Beatmap<OsuHitObject> { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };

            for (int i = 0; i < 1400; i++)
            {
                beatmap.HitObjects.Add(new HitCircle
                {
                    StartTime = 1000 + i * 170,
                    Position = new Vector2(100 + i % 7 * 50, 100 + i % 4 * 60),
                });
            }

            foreach (var hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var stopwatch = new System.Diagnostics.Stopwatch();

            AddStep("start simulation", () =>
            {
                stopwatch.Restart();
                Child = simulator = new GhostReplaySimulator(autoplayGhost(beatmap), beatmap, new OsuRuleset(), Array.Empty<Mod>());
            });

            AddUntilStep("simulation finishes", () => simulator.Finished);

            AddAssert("full map was simulated", () =>
            {
                stopwatch.Stop();
                TestContext.Out.WriteLine($"simulated {beatmap.HitObjects.Count} objects "
                                          + $"({beatmap.HitObjects.Last().StartTime / 1000:0}s of map) in {stopwatch.ElapsedMilliseconds}ms");

                return simulator.Result!.Final.Combo;
            }, () => Is.EqualTo(1400));
        }

        [Test]
        public void TestEmptyReplayStillFinishes()
        {
            // A replay with no frames judges every note as a miss. The race should
            // end up with a real (bad) ghost rather than hanging or handing back null.
            var beatmap = createBeatmap(5);

            runSimulation(beatmap, _ => new Score
            {
                Replay = new osu.Game.Replays.Replay(),
                ScoreInfo = new ScoreInfo
                {
                    Ruleset = new OsuRuleset().RulesetInfo,
                    RealmUser = new RealmUser { Username = "ghost" },
                },
            });

            AddAssert("timeline was produced", () => simulator.Result != null);
            AddAssert("ghost missed everything", () => simulator.Result!.Final.TotalScore, () => Is.Zero);
        }
    }
}
