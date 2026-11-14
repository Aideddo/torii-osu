// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Numerics;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Platform.OpenXR;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.PlayerSettings;
using osu.Game.VR;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// Torii: what a replay shown as a scene (see <see cref="VRReplayScene"/>) is controlled with.
    /// </summary>
    public partial class TestSceneVRReplaySceneControls : RateAdjustedBeatmapTestScene
    {
        private const string hash = "feed0004";

        private ReplayExtrasStore store = null!;
        private VRReplayPlayer player = null!;
        private OsuConfigManager config = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            this.config = config;
            Dependencies.Cache(store = new ReplayExtrasStore(LocalStorage));
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("setting as it comes", () => config.GetBindable<bool>(OsuSetting.VRReplaySceneView).SetDefault());
            AddStep("settings shown", () => config.SetValue(OsuSetting.ReplaySettingsOverlay, true));
            AddStep("create player", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(new OsuRuleset().RulesetInfo);
                SelectedMods.Value = new[] { new OsuRuleset().GetAutoplayMod()! };
                player = new VRReplayPlayer(store);
            });
            AddStep("load player", () => LoadScreen(player));
            AddUntilStep("player loaded", () => player.IsLoaded && player.IsCurrentScreen());
            AddUntilStep("replay shown in the scene", () => player.GameplayClockContainer.Parent, () => Is.SameAs(view));
        }

        [Test]
        public void TestSettingsStayFlat()
        {
            AddAssert("settings not in the scene", () => view.ChildrenOfType<ReplaySettingsOverlay>().Any(), () => Is.False);
            AddAssert("playback settings among them", () => scene.ChildrenOfType<PlaybackSettings>().Count(), () => Is.EqualTo(1));

            AddStep("switch to flat in the settings", () => viewSetting.Current.Value = false);
            AddAssert("replay shown as usual", () => player.GameplayClockContainer.Parent, () => Is.SameAs(player));
            AddAssert("choice kept", () => config.Get<bool>(OsuSetting.VRReplaySceneView), () => Is.False);

            AddStep("and back", () => viewSetting.Current.Value = true);
            AddAssert("replay shown in the scene", () => player.GameplayClockContainer.Parent, () => Is.SameAs(view));
        }

        [Test]
        public void TestCameraKeys()
        {
            Vector3 start = default;

            AddUntilStep("replay running", () => player.GameplayClockContainer.IsRunning);
            AddStep("hold right mouse over the scene", () =>
            {
                InputManager.MoveMouseTo(player.ScreenSpaceDrawQuad.Centre);
                InputManager.PressButton(MouseButton.Right);
            });
            AddStep("note the camera", () => start = view.CameraPosition);

            AddStep("hold space", () => InputManager.PressKey(Key.Space));
            AddUntilStep("camera rises", () => view.CameraPosition.Y, () => Is.GreaterThan(start.Y + 0.05f));
            AddAssert("straight up", () => new Vector2(view.CameraPosition.X - start.X, view.CameraPosition.Z - start.Z).Length(), () => Is.LessThan(0.001f));
            AddAssert("replay not paused by it", () => player.GameplayClockContainer.IsRunning);
            AddStep("release space", () => InputManager.ReleaseKey(Key.Space));

            AddStep("note the camera", () => start = view.CameraPosition);
            AddStep("hold shift", () => InputManager.PressKey(Key.ShiftLeft));
            AddUntilStep("camera sinks", () => view.CameraPosition.Y, () => Is.LessThan(start.Y - 0.05f));
            AddStep("release shift", () => InputManager.ReleaseKey(Key.ShiftLeft));

            AddStep("note the camera", () => start = view.CameraPosition);
            AddStep("hold W", () => InputManager.PressKey(Key.W));
            AddUntilStep("camera moves", () => new Vector2(view.CameraPosition.X - start.X, view.CameraPosition.Z - start.Z).Length(), () => Is.GreaterThan(0.05f));
            // (It looks down at the panel from where it starts: forwards is still along the floor.)
            AddAssert("at the same height", () => view.CameraPosition.Y, () => Is.EqualTo(start.Y).Within(0.001f));
            AddStep("release all", () =>
            {
                InputManager.ReleaseKey(Key.W);
                InputManager.ReleaseButton(MouseButton.Right);
            });

            AddStep("space without the mouse held", () => InputManager.Key(Key.Space));
            AddUntilStep("pauses the replay as ever", () => !player.GameplayClockContainer.IsRunning);
        }

        private VRReplayScene scene => player.ChildrenOfType<VRReplayScene>().Single();

        private SceneViewContainer view => scene.ChildrenOfType<SceneViewContainer>().Single();

        private PlayerCheckbox viewSetting => scene.ChildrenOfType<ReplaySettingsOverlay>().Single().ChildrenOfType<PlayerCheckbox>().Single(c => c.LabelText == @"3D view");

        /// <summary>
        /// Plays the autoplay replay as a saved one of a play in VR.
        /// </summary>
        private partial class VRReplayPlayer : ReplayPlayer
        {
            public new GameplayClockContainer GameplayClockContainer => base.GameplayClockContainer;

            public VRReplayPlayer(ReplayExtrasStore store)
                : base((beatmap, mods) =>
                {
                    var score = mods.OfType<ModAutoplay>().First().CreateScoreFromReplayData(beatmap, mods);
                    var panel = new VRPanelGeometry(new VRPose(new Vector3(0, 1.5f, 0), Quaternion.Identity), 1.4f, 1.1f, 16f / 9, true);
                    var head = new VRPose(new Vector3(0, 1.6f, 0.1f), Quaternion.Identity);

                    var extras = new VRReplayExtras();
                    extras.Panels.Add(new VRReplayPanel { Time = -100000, Geometry = panel });

                    for (double time = -3000; time <= 60000; time += 100)
                        extras.Frames.Add(new VRReplayFrame { Time = time, Head = head, ActiveHand = IVRControllers.RIGHT });

                    score.ScoreInfo.Hash = hash;
                    VRReplayExtrasEncoder.Save(store, hash, extras);
                    return score;
                }, new PlayerConfiguration { AllowPause = false, ShowResults = false })
            {
            }
        }
    }
}
