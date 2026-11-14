// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Numerics;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.VR;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// Torii: in VR, a replay of a play made in VR shows its player's hands, on a panel placed as theirs was.
    /// </summary>
    public partial class TestSceneVRReplayGhostHands : VRReplayTestScene
    {
        private readonly TestScene scene = new TestScene();

        /// <summary>
        /// When set, the player shows a replay with this hash.
        /// </summary>
        private string? replayHash;

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.CacheAs<IVRScene>(scene);
        }

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => replayHash == null ? base.CreatePlayer(ruleset) : new SavedReplayPlayer(replayHash);

        [SetUp]
        public void SetUp()
        {
            replayHash = null;
            scene.Reset();
        }

        [Test]
        public void TestNothingShownForALivePlay()
        {
            PlayThrough();

            AddAssert("no hands shown", () => scene.GhostRequests, () => Is.Zero);
            AddAssert("controllers as the user has them", () => scene.DefaultControllerRequests, () => Is.Zero);
            AddAssert("panel where the user has it", () => scene.Panel, () => Is.Null);
        }

        // The player's panel: a metre to the right of the viewer's, larger and further away.
        private static readonly VRPanelGeometry recorded_panel = new VRPanelGeometry(new VRPose(new Vector3(1, 1.5f, 0), Quaternion.Identity), 2, 1.2f, 16f / 9, true);

        private static readonly VRPose recorded_grip = new VRPose(new Vector3(1.1f, 1.2f, -0.4f), Quaternion.Identity);

        [Test]
        public void TestReplayShowsRecordedHands()
        {
            const string hash = "feed0001";

            AddStep("save extras for the replay", () =>
            {
                // The player's panel was a metre to the right of where the viewer's is.
                var extras = new VRReplayExtras
                {
                    ControllerModel = VRControllerModel.Pen,
                    LeftPointer = new VRPose(new Vector3(0, 0, -0.05f), Quaternion.Identity),
                };

                extras.Panels.Add(new VRReplayPanel { Time = -100000, Geometry = recorded_panel });

                for (int i = 0; i < 2; i++)
                {
                    extras.Frames.Add(new VRReplayFrame
                    {
                        Time = i == 0 ? -100000 : 1000000,
                        // Level with the middle of their panel's curve, half a metre behind it.
                        Head = new VRPose(new Vector3(1, 1.5f, 0.5f), Quaternion.Identity),
                        Left = new VRReplayHand { Grip = recorded_grip, Trigger = 0.5f },
                        ActiveHand = IVRControllers.LEFT,
                    });
                }

                // (Frames this far apart aren't blended.)
                VRReplayExtrasEncoder.Save(Store, hash, extras);
            });

            AddStep("watch the replay", () => replayHash = hash);
            CreateTest();

            AddUntilStep("player's hands shown", () => scene.Left, () => Is.Not.Null);
            // (The panel of this test's tracking stays where it is: the hand is placed around that.)
            AddAssert("left hand around the viewer's panel", () => Vector3.Distance(scene.Left!.Value.Grip.Position, TestTracking.PANEL.MapFrom(recorded_panel, recorded_grip).Position),
                () => Is.LessThan(0.001f));
            AddAssert("pointer ahead of the hand", () => scene.Left!.Value.Pointer.Position.Z - scene.Left!.Value.Grip.Position.Z, () => Is.EqualTo(-0.05f).Within(0.001f));
            AddAssert("trigger shown", () => scene.Left!.Value.Trigger, () => Is.EqualTo(0.5f).Within(0.01f));
            AddAssert("no right hand", () => scene.Right, () => Is.Null);
            AddAssert("held pens", () => scene.Model, () => Is.EqualTo(VRControllerModel.Pen));
            AddAssert("viewer holds plain controllers", () => scene.DefaultControllerRequests, () => Is.GreaterThan(0));

            // The player's panel was 0.5 m ahead of their head and level with it: so it is for the viewer (head at 1.6 m).
            AddAssert("panel placed as the player had it", () => Vector3.Distance(scene.Panel!.Value.Pose.Position, new Vector3(0, 1.6f, -0.5f)), () => Is.LessThan(0.001f));
            AddAssert("panel has the player's size", () => scene.Panel!.Value.HorizontalAngle, () => Is.EqualTo(1.2f));
            AddAssert("panel has the player's distance", () => scene.Panel!.Value.Radius, () => Is.EqualTo(2));
        }

        [Test]
        public void TestReplayWithoutExtras()
        {
            AddStep("watch a replay", () => replayHash = "feed0002");
            CreateTest();

            AddUntilStep("viewer holds plain controllers", () => scene.DefaultControllerRequests, () => Is.GreaterThan(10));
            AddAssert("no hands shown", () => scene.GhostRequests, () => Is.Zero);
        }

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

        private class TestScene : IVRScene
        {
            public VRGhostHand? Left { get; private set; }
            public VRGhostHand? Right { get; private set; }
            public VRControllerModel Model { get; private set; }
            public int GhostRequests { get; private set; }
            public int DefaultControllerRequests { get; private set; }

            public void ShowGhostHands(VRGhostHand? left, VRGhostHand? right, int activeHand, VRControllerModel model)
            {
                Left = left;
                Right = right;
                Model = model;
                GhostRequests++;
            }

            public VRPanelGeometry? Panel { get; private set; }

            public void UsePanel(VRPanelGeometry panel) => Panel = panel;

            public void UseDefaultControllers() => DefaultControllerRequests++;

            public void Reset()
            {
                Left = Right = null;
                Panel = null;
                GhostRequests = DefaultControllerRequests = 0;
            }
        }
    }
}
