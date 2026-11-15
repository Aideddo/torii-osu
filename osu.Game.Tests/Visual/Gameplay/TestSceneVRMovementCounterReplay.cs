// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.VR;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// Torii: while a replay is watched, the VR controller movement counter shows how far the replay's player had moved theirs for the
    /// beatmap, and how far the viewer moves their own for the other periods.
    /// </summary>
    public partial class TestSceneVRMovementCounterReplay : VRReplayTestScene
    {
        private const string replay_hash = "feed0003";

        private VRMovementTracker tracker = null!;
        private VRMovementCounter counter = null!;
        private VRReplayExtras extras = null!;

        private string? replayHash;

        [BackgroundDependencyLoader]
        private void load()
        {
            // The viewer's own movement: this test's controllers are moved about all the time.
            Dependencies.Cache(tracker = new VRMovementTracker());

            Add(new DependencyProvidingContainer
            {
                CachedDependencies = new (Type, object)[] { (typeof(Storage), LocalStorage) },
                Child = tracker,
            });
        }

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => replayHash == null ? base.CreatePlayer(ruleset) : new SavedReplayPlayer(replayHash);

        [SetUp]
        public void SetUp() => replayHash = null;

        [Test]
        public void TestReplayShowsItsPlayersMovement()
        {
            AddStep("save extras for the replay", () =>
            {
                // The player moved their left hand at 20 cm a second, from 100 s before the first object on.
                extras = new VRReplayExtras { LeftDistance = 240 };

                for (int i = 0; i <= 12000; i++)
                {
                    extras.Frames.Add(new VRReplayFrame
                    {
                        Time = -100000 + i * 100,
                        Left = new VRReplayHand { Grip = new VRPose(new Vector3(i * 0.01f, 1.2f, -0.4f), Quaternion.Identity) },
                        ActiveHand = IVRControllers.LEFT,
                    });
                }

                VRReplayExtrasEncoder.Save(Store, replay_hash, extras);
            });

            AddStep("watch the replay", () => replayHash = replay_hash);
            createTestWithCounter();

            AddUntilStep("shows the player's movement", () => counter.Current.Value, () => Is.GreaterThan(19));
            assertShowsReplayDistance();

            AddStep("seek ahead", () => Player.GameplayClockContainer.Seek(3000));
            AddUntilStep("shows the movement up to there", () => counter.Current.Value, () => Is.GreaterThan(20.55));
            assertShowsReplayDistance();

            AddStep("seek back", () => Player.GameplayClockContainer.Seek(1000));
            AddUntilStep("shows the movement up to there", () => counter.Current.Value, () => Is.LessThan(20.5));
            assertShowsReplayDistance();

            AddStep("show session", () => counter.Period.Value = VRMovementCounter.MovementPeriod.Session);
            // (This test's controllers move whenever they're asked, so the two are never quite the same.)
            AddUntilStep("shows the viewer's movement", () => counter.Current.Value, () => Is.EqualTo(tracker.SessionDistance).Within(1));
        }

        [Test]
        public void TestReplayWithoutExtrasShowsNothing()
        {
            AddStep("watch a replay", () => replayHash = "feed0004");
            createTestWithCounter();

            AddWaitStep("watch for a while", 10);
            AddAssert("viewer moved", () => tracker.SessionDistance, () => Is.GreaterThan(0));
            AddAssert("nothing shown for the beatmap", () => counter.Current.Value, () => Is.Zero);
        }

        [Test]
        public void TestLivePlayShowsOwnMovement()
        {
            createTestWithCounter();

            AddUntilStep("shows movement", () => counter.Current.Value, () => Is.GreaterThan(0));
        }

        private void createTestWithCounter()
        {
            CreateTest();

            AddStep("add counter", () => Player.HUDOverlay.Add(counter = new VRMovementCounter
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Scale = new osuTK.Vector2(4),
            }));
            AddUntilStep("wait for counter", () => counter.IsLoaded);
        }

        // (Once the replay has caught up after a seek: the counter is a frame behind the clock at most.)
        private void assertShowsReplayDistance() => AddUntilStep("matches the replay at the time watched",
            () => counter.Current.Value, () => Is.EqualTo(extras.GetDistanceAt(Player.DrawableRuleset.FrameStableClock.CurrentTime)).Within(0.05));

        /// <summary>
        /// Plays the autoplay replay as if it were a saved one.
        /// </summary>
        private partial class SavedReplayPlayer : TestPlayer
        {
            private readonly string hash;

            public SavedReplayPlayer(string hash)
                : base(false)
            {
                this.hash = hash;
            }

            protected override void PrepareReplay()
            {
                var score = ((ICreateReplayData)new OsuModAutoplay()).CreateScoreFromReplayData(GameplayState.Beatmap, Mods.Value);
                score.ScoreInfo.Hash = hash;
                DrawableRuleset?.SetReplayScore(score);
            }
        }
    }
}
