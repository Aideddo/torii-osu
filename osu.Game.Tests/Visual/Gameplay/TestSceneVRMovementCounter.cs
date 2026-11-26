// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.VR;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// Torii: the VR controller movement counter and the session statistics behind it.
    /// </summary>
    public partial class TestSceneVRMovementCounter : OsuTestScene
    {
        private TestControllers controllers = null!;
        private GameplayClockContainer gameplayClock = null!;
        private TestMovementTracker? tracker;
        private VRMovementCounter counter = null!;

        private VRSessionStore store => new VRSessionStore(LocalStorage);

        [SetUpSteps]
        public void SetUpSteps()
        {
            // The tracker of the previous test may still be saving: it's done once it's disposed of.
            AddStep("remove previous tracker", Clear);
            AddUntilStep("previous tracker disposed", () => tracker == null || tracker.Disposed);
            AddStep("only previous sessions", () => store.Save(new[]
            {
                new VRSession(Guid.NewGuid(), DateTimeOffset.Now.AddDays(-1))
                {
                    LeftDistance = 400,
                    RightDistance = 800,
                },
                // Earlier today, whatever the time is now.
                new VRSession(Guid.NewGuid(), new DateTimeOffset(DateTime.Now.Date))
                {
                    LeftDistance = 20,
                    RightDistance = 30,
                },
            }));
            AddStep("create counter", () => create());
        }

        private void create(double controllerStart = 0)
        {
            controllers = new TestControllers();
            controllers.Distance[IVRControllers.LEFT] = controllers.Distance[IVRControllers.RIGHT] = controllerStart;

            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(Storage), LocalStorage),
                    (typeof(IVRControllers), controllers),
                    (typeof(IGameplayClock), gameplayClock = new GameplayClockContainer(new TrackVirtual(60000), false, false)),
                    (typeof(VRMovementTracker), tracker = new TestMovementTracker { SaveInterval = 100 }),
                },
                Children = new Drawable[]
                {
                    gameplayClock,
                    tracker,
                },
            };

            // The counter resolves the tracker, which resolves the dependencies above.
            LoadComponent(tracker);
            ((DependencyProvidingContainer)Child).Add(counter = new VRMovementCounter
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Scale = new osuTK.Vector2(4),
            });

            gameplayClock.Start();
        }

        [Test]
        public void TestPeriods()
        {
            AddStep("move left 1.5 m", () => controllers.Distance[IVRControllers.LEFT] += 1.5);
            AddStep("move right 2 m", () => controllers.Distance[IVRControllers.RIGHT] += 2);
            assertDisplayed("3.5m");

            AddStep("pause", () => gameplayClock.Stop());
            AddStep("move right 10 m", () => controllers.Distance[IVRControllers.RIGHT] += 10);
            assertDisplayed("3.5m");

            AddStep("show session", () => counter.Period.Value = VRMovementCounter.MovementPeriod.Session);
            assertDisplayed("13.5m");

            AddStep("show today", () => counter.Period.Value = VRMovementCounter.MovementPeriod.Today);
            assertDisplayed("63.5m");

            AddStep("show all time", () => counter.Period.Value = VRMovementCounter.MovementPeriod.AllTime);
            assertDisplayed("1.26km");

            AddStep("resume", () => gameplayClock.Start());
            AddStep("show beatmap", () => counter.Period.Value = VRMovementCounter.MovementPeriod.Beatmap);
            AddStep("move left 1 m", () => controllers.Distance[IVRControllers.LEFT] += 1);
            assertDisplayed("4.5m");

            AddToggleStep("toggle label", v => counter.ShowLabel.Value = v);

            foreach (var alignment in Enum.GetValues<VRMovementCounter.TextAlignment>())
                AddStep($"align {alignment}", () => counter.Alignment.Value = alignment);

            AddStep("show all time, aligned right", () =>
            {
                counter.Period.Value = VRMovementCounter.MovementPeriod.AllTime;
                counter.Alignment.Value = VRMovementCounter.TextAlignment.Right;
            });
            AddAssert("label and distance share their right edge", () =>
            {
                var texts = counter.ChildrenOfType<SpriteText>().ToArray();
                float labelRight = texts[0].ScreenSpaceDrawQuad.TopRight.X;
                float distanceRight = texts.Skip(1).Max(t => t.ScreenSpaceDrawQuad.TopRight.X);
                return Math.Abs(labelRight - distanceRight) < 1;
            });
        }

        [Test]
        public void TestSessionIsPersisted()
        {
            AddStep("move left 1.5 m", () => controllers.Distance[IVRControllers.LEFT] += 1.5);
            AddStep("move right 2 m", () => controllers.Distance[IVRControllers.RIGHT] += 2);

            AddUntilStep("session saved", () =>
            {
                var sessions = store.Load().OrderBy(s => s.StartTime).ToList();
                return sessions.Count == 3 && sessions[2].LeftDistance == 1.5 && sessions[2].RightDistance == 2;
            });

            AddStep("move right 1 m", () => controllers.Distance[IVRControllers.RIGHT] += 1);
            AddUntilStep("same session updated", () =>
            {
                var sessions = store.Load();
                return sessions.Count == 3 && sessions.Sum(s => s.RightDistance) == 833;
            });

            // The controllers' own counters start anywhere (the game may have been running for a while): only movement since counts.
            AddStep("start a new session", () => create(controllerStart: 50));
            AddStep("show all time", () => counter.Period.Value = VRMovementCounter.MovementPeriod.AllTime);
            assertDisplayed("1.25km");
            AddStep("show today", () => counter.Period.Value = VRMovementCounter.MovementPeriod.Today);
            assertDisplayed("54.5m");
            AddStep("show session", () => counter.Period.Value = VRMovementCounter.MovementPeriod.Session);
            assertDisplayed("0.0m");
        }

        private void assertDisplayed(string expected) => AddUntilStep($"shows {expected}", () => string.Concat(counter.ChildrenOfType<SpriteText>().Skip(1).Select(t => t.Text.ToString())), () => Is.EqualTo(expected));

        private partial class TestMovementTracker : VRMovementTracker
        {
            public bool Disposed => IsDisposed;
        }

        private class TestControllers : IVRControllers
        {
            public readonly double[] Distance = new double[2];

            public int ActiveHand => IVRControllers.RIGHT;

            public bool IsTriggerDown(int hand) => false;

            public bool SuppressPointerButtons { get; set; }

            public void Vibrate(int hand, float amplitude, double duration, float frequency = 0)
            {
            }

            public double GetDistanceMoved(int hand) => Distance[hand];
        }
    }
}
