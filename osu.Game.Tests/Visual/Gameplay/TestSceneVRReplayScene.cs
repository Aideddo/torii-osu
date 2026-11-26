// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Framework.Platform.OpenXR;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.PlayerSettings;
using osu.Game.Tests.Resources;
using osu.Game.VR;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// Torii: outside VR, a replay of a play made in VR can be watched as the scene it was.
    /// </summary>
    public partial class TestSceneVRReplayScene : PlayerTestScene
    {
        private const string hash = "feed0003";

        // The player's panel, and where they stood before it.
        private static readonly VRPanelGeometry panel = new VRPanelGeometry(new VRPose(new Vector3(0.3f, 1.5f, -0.2f), Quaternion.CreateFromYawPitchRoll(0.3f, 0, 0)), 1.4f, 1.1f, 16f / 9, true);
        private static readonly VRPose player = new VRPose(new Vector3(0.3f, 1.55f, -0.1f), Quaternion.CreateFromYawPitchRoll(0.3f, 0, 0));

        private BeatmapManager beatmaps = null!;
        private BeatmapSetInfo? importedSet;
        private ReplayExtrasStore store = null!;

        /// <summary>
        /// Whether the replay has extras saved for it.
        /// </summary>
        private bool withExtras = true;

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio)
        {
            Dependencies.Cache(new RealmRulesetStore(Realm));
            Dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default));
            Dependencies.Cache(Realm);
            Dependencies.Cache(store = new ReplayExtrasStore(LocalStorage));
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("import beatmap", () =>
            {
                beatmaps.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                importedSet = beatmaps.GetAllUsableBeatmapSets().First();
            });
            AddStep("start flat", () => LocalConfig.SetValue(OsuSetting.VRReplaySceneView, false));
            // (Seen from outside: through the player's eyes there's no headset to see.)
            AddStep("free camera", () => LocalConfig.SetValue(OsuSetting.VRReplaySceneCamera, VRReplayCamera.Free));
        }

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => beatmaps.GetWorkingBeatmap(importedSet?.Beatmaps.First()).Beatmap;

        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new VRReplayPlayer(withExtras ? store : null);

        protected override bool HasCustomSteps => true;

        [SetUp]
        public void SetUp() => withExtras = true;

        [Test]
        public void TestScene()
        {
            CreateTest();

            AddUntilStep("scene offered", () => scene, () => Is.Not.Null);
            AddAssert("replay shown as usual", () => Player.GameplayClockContainer.Parent, () => Is.SameAs(Player));

            AddStep("add its settings", () =>
            {
                var settings = scene!.CreateSettings();
                scene.Overlay.Add(settings);
                viewSetting = settings.ChildrenOfType<PlayerCheckbox>().Single();
            });
            AddUntilStep("settings loaded", () => viewSetting.IsLoaded);

            AddStep("show the scene", () => viewSetting.Current.Value = true);
            AddAssert("showing", () => scene!.Showing.Value);
            // (The replay's own bar is with the replay, on the panel.)
            AddUntilStep("with a bar to seek with in front", () => scene!.ChildrenOfType<SongProgress>().Single(p => !view!.ChildrenOfType<SongProgress>().Contains(p)).Alpha,
                () => Is.GreaterThan(0));
            AddAssert("replay shown in the scene", () => Player.GameplayClockContainer.Parent, () => Is.SameAs(view));
            AddUntilStep("player's hands in the scene", () => view!.Hands[IVRControllers.RIGHT].GripValid && view.Hands[IVRControllers.RIGHT].AimValid);
            AddAssert("a headset where their head was", () => view!.ShowHead && view.HeadModel != null);
            AddAssert("on their panel", () => view!.Panel.Radius, () => Is.EqualTo(panel.Radius));
            AddAssert("panel as tall as theirs", () => view!.Panel.Height, () => Is.EqualTo(panel.Height).Within(0.001f));

            double time = 0;
            AddStep("note the time", () => time = Player.GameplayClockContainer.CurrentTime);
            AddUntilStep("replay carries on", () => Player.GameplayClockContainer.CurrentTime, () => Is.GreaterThan(time + 500));

            AddStep("back to flat", () => viewSetting.Current.Value = false);
            AddAssert("replay shown as usual", () => Player.GameplayClockContainer.Parent, () => Is.SameAs(Player));
            AddStep("note the time", () => time = Player.GameplayClockContainer.CurrentTime);
            AddUntilStep("replay carries on", () => Player.GameplayClockContainer.CurrentTime, () => Is.GreaterThan(time + 500));

            AddStep("show the scene again", () => viewSetting.Current.Value = true);
            AddAssert("choice kept", () => LocalConfig.Get<bool>(OsuSetting.VRReplaySceneView));
        }

        [Test]
        public void TestShownFromTheStartUnlessSwitchedOff()
        {
            AddStep("setting as it comes", () => LocalConfig.GetBindable<bool>(OsuSetting.VRReplaySceneView).SetDefault());
            CreateTest();

            AddUntilStep("scene offered", () => scene, () => Is.Not.Null);
            AddAssert("replay shown in the scene", () => Player.GameplayClockContainer.Parent, () => Is.SameAs(view));
        }

        [Test]
        public void TestNotOfferedWithoutExtras()
        {
            AddStep("no extras", () => withExtras = false);
            CreateTest();

            AddUntilStep("replay playing", () => Player.GameplayClockContainer.CurrentTime, () => Is.GreaterThan(0));
            AddAssert("no scene", () => scene, () => Is.Null);
        }

        // The setting as the replay's settings show it (a plain player has no settings to show it in).
        private PlayerCheckbox viewSetting = null!;

        private VRReplayScene? scene => Player.ChildrenOfType<VRReplayScene>().SingleOrDefault();

        private SceneViewContainer? view => scene?.ChildrenOfType<SceneViewContainer>().SingleOrDefault();

        /// <summary>
        /// Plays the autoplay replay as if it were a saved one of a play in VR, with the player's right hand pointing at the cursor
        /// throughout.
        /// </summary>
        private partial class VRReplayPlayer : TestPlayer
        {
            private readonly ReplayExtrasStore? store;

            public VRReplayPlayer(ReplayExtrasStore? store)
                : base(false)
            {
                this.store = store;
            }

            protected override Score CreateScore(IBeatmap beatmap)
            {
                var score = ((ICreateReplayData)new OsuModAutoplay()).CreateScoreFromReplayData(beatmap, Mods.Value);

                if (store != null)
                {
                    score.ScoreInfo.Hash = hash;
                    VRReplayExtrasEncoder.Save(store, hash, createExtras(score));
                }

                return score;
            }

            protected override void PrepareReplay() => DrawableRuleset?.SetReplayScore(Score);

            private static VRReplayExtras createExtras(Score score)
            {
                var extras = new VRReplayExtras
                {
                    LeftPointer = new VRPose(new Vector3(0, 0.02f, -0.05f), Quaternion.CreateFromYawPitchRoll(0, -0.9f, 0)),
                    RightPointer = new VRPose(new Vector3(0, 0.02f, -0.05f), Quaternion.CreateFromYawPitchRoll(0, -0.9f, 0)),
                };

                extras.Panels.Add(new VRReplayPanel { Time = -100000, Geometry = panel });

                var frames = score.Replay.Frames.Cast<OsuReplayFrame>().ToList();
                int index = 0;

                for (double time = frames[0].Time - 3000; time <= frames[^1].Time + 3000; time += 1000.0 / 72)
                {
                    while (index < frames.Count - 2 && frames[index + 1].Time <= time)
                        index++;

                    var from = frames[index];
                    var to = frames[Math.Min(index + 1, frames.Count - 1)];
                    float amount = to.Time > from.Time ? (float)Math.Clamp((time - from.Time) / (to.Time - from.Time), 0, 1) : 0;
                    var cursor = from.Position + (to.Position - from.Position) * amount;

                    // The playfield is laid out about the middle of the panel, 80% of its height.
                    float scale = panel.Height * 0.8f / 384;
                    var surface = panel.Pose.Apply(new VRPose(new Vector3((cursor.X - 256) * scale, (192 - cursor.Y) * scale - panel.Height * 0.02f, -panel.Radius), Quaternion.Identity)).Position;
                    float t = (float)(time / 1000);

                    extras.Frames.Add(new VRReplayFrame
                    {
                        Time = time,
                        Head = player.Apply(new VRPose(new Vector3(MathF.Sin(t) * 0.02f, 0, 0), Quaternion.CreateFromYawPitchRoll(MathF.Sin(t * 0.7f) * 0.1f, -0.05f, 0))),
                        Left = createHand(player.Apply(new VRPose(new Vector3(-0.2f, -0.35f, -0.3f), Quaternion.Identity)).Position, panel.SurfaceCentre.Position, extras.LeftPointer.Value, 0),
                        Right = createHand(player.Apply(new VRPose(new Vector3(0.2f, -0.35f + MathF.Sin(t * 2) * 0.02f, -0.3f), Quaternion.Identity)).Position, surface, extras.RightPointer.Value,
                            from.Actions.Count > 0 ? 1 : 0),
                        ActiveHand = IVRControllers.RIGHT,
                    });
                }

                return extras;
            }

            /// <summary>
            /// A hand whose pointer starts at <paramref name="origin"/> and points at <paramref name="target"/>.
            /// </summary>
            private static VRReplayHand createHand(Vector3 origin, Vector3 target, VRPose pointer, float trigger)
            {
                var direction = Vector3.Normalize(target - origin);
                var aim = new VRPose(origin, Quaternion.CreateFromYawPitchRoll(MathF.Atan2(-direction.X, -direction.Z), MathF.Asin(direction.Y), 0));

                // The grip is where it has to be for the pointer to be there.
                var gripRotation = Quaternion.Normalize(aim.Rotation * Quaternion.Inverse(pointer.Rotation));
                var grip = new VRPose(aim.Position - Vector3.Transform(pointer.Position, gripRotation), gripRotation);

                return new VRReplayHand { Grip = grip, Trigger = trigger };
            }
        }
    }
}
