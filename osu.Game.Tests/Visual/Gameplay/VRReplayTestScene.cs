// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Numerics;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;
using osu.Game.Tests.Resources;
using osu.Game.VR;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// Torii: a player as in VR, as far as the game can tell: tracking and controllers of its own making, and scores, replays and
    /// what's saved along with them kept in the test's own storage.
    /// </summary>
    public abstract partial class VRReplayTestScene : PlayerTestScene
    {
        protected TestTracking Tracking { get; private set; } = null!;

        protected ReplayExtrasStore Store { get; private set; } = null!;

        /// <summary>
        /// The score of the play, as it's in the database.
        /// </summary>
        protected ScoreInfo SavedScore => Realm.Run(r => r.Find<ScoreInfo>(Player.Score.ScoreInfo.ID)!.Detach());

        private BeatmapManager beatmaps = null!;
        private BeatmapSetInfo? importedSet;

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio)
        {
            RulesetStore rulesets;

            Dependencies.Cache(rulesets = new RealmRulesetStore(Realm));
            Dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default));
            Dependencies.Cache(new ScoreManager(rulesets, () => beatmaps, LocalStorage, Realm, API));
            Dependencies.Cache(Realm);

            Dependencies.CacheAs<IVRTracking>(Tracking = new TestTracking());
            Dependencies.CacheAs<IVRControllers>(new TestControllers());
            Dependencies.Cache(Store = new ReplayExtrasStore(LocalStorage));
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("import beatmap", () =>
            {
                beatmaps.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                importedSet = beatmaps.GetAllUsableBeatmapSets().First();
            });
        }

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => beatmaps.GetWorkingBeatmap(importedSet?.Beatmaps.First()).Beatmap;

        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new TestPlayer(false);

        protected override bool HasCustomSteps => true;

        protected override bool AllowFail => false;

        /// <summary>
        /// Plays for a while, skips to the end, and waits for the score and what was recorded along with it to be saved.
        /// </summary>
        protected void PlayThrough()
        {
            long framesAtStart = 0;

            CreateTest();

            AddUntilStep("wait for track to start running", () => Beatmap.Value.Track.IsRunning);
            AddStep("note tracking frame", () => framesAtStart = Tracking.Frame);
            AddUntilStep("play for a while", () => Tracking.Frame > framesAtStart + 50);

            AddStep("seek to completion", () => Player.GameplayClockContainer.Seek(Player.DrawableRuleset.Objects.Last().GetEndTime()));
            AddUntilStep("score in database", () => Realm.Run(r => r.Find<ScoreInfo>(Player.Score.ScoreInfo.ID) != null));
            AddUntilStep("extras saved", () => Store.Exists(SavedScore.Hash));
        }

        /// <summary>
        /// Every read is a new frame: the head stays put, the left hand moves steadily to the right, the right hand isn't tracked.
        /// </summary>
        protected class TestTracking : IVRTracking
        {
            public static readonly VRPanelGeometry PANEL = new VRPanelGeometry(new VRPose(new Vector3(0, 1.5f, 0), Quaternion.Identity), 1.5f, 1, 16f / 9, true);

            public long Frame { get; private set; }

            public string RuntimeName => "Test runtime";

            public VRTrackingState GetState()
            {
                Frame++;

                var grip = new VRPose(new Vector3(Frame * 0.001f, 1.2f, -0.4f), Quaternion.Identity);

                return new VRTrackingState
                {
                    Frame = Frame,
                    Head = new VRPose(new Vector3(0, 1.6f, 0), Quaternion.Identity),
                    Left = new VRHandTracking
                    {
                        Grip = grip,
                        Pointer = grip.Apply(new VRPose(new Vector3(0, 0, -0.05f), Quaternion.Identity)),
                        Trigger = 0.5f,
                    },
                    Panel = PANEL,
                };
            }
        }

        protected class TestControllers : IVRControllers
        {
            private double leftDistance;

            public int ActiveHand => IVRControllers.LEFT;

            // No trigger is ever pulled: the left controller is only moved about (a centimetre each time it's asked).
            public bool IsTriggerDown(int hand) => false;

            public bool SuppressPointerButtons { get; set; }

            public void Vibrate(int hand, float amplitude, double duration, float frequency = 0)
            {
            }

            public double GetDistanceMoved(int hand) => hand == IVRControllers.LEFT ? leftDistance += 0.01 : 0;
        }
    }
}
