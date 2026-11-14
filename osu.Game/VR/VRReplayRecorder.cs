// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: records the <see cref="VRReplayExtras"/> of a play in VR: one frame for every VR frame while gameplay is running.
    /// Nothing is recorded while a replay is being watched.
    /// </summary>
    public partial class VRReplayRecorder : Component
    {
        [Resolved]
        private IVRTracking tracking { get; set; } = null!;

        [Resolved]
        private IVRControllers? controllers { get; set; }

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        [Resolved]
        private DrawableRuleset? drawableRuleset { get; set; }

        [Resolved]
        private ReplayExtrasStore? store { get; set; }

        private readonly VRReplayExtras extras = new VRReplayExtras { RecordedAt = DateTimeOffset.Now };

        private readonly double[] lastDistanceReading = new double[2];

        private long lastTrackingFrame;
        private double lastTime = double.NegativeInfinity;
        private VRPanelGeometry? lastPanel;
        private bool stopped;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            extras.Runtime = tracking.RuntimeName;
            extras.ControllerModel = config.Get<VRControllerModel>(OsuSetting.VRControllerModel);
            extras.PenTilt = config.Get<float>(OsuSetting.VRPenTilt);
            extras.PenOffsetForward = config.Get<float>(OsuSetting.VRPenOffsetForward);
            extras.PenOffsetUp = config.Get<float>(OsuSetting.VRPenOffsetUp);
            extras.Frames.Capacity = 8192;

            for (int hand = 0; hand < 2; hand++)
                lastDistanceReading[hand] = controllers?.GetDistanceMoved(hand) ?? 0;
        }

        protected override void Update()
        {
            base.Update();

            if (stopped)
                return;

            bool recording = gameplayClock.IsRunning && drawableRuleset?.HasReplayLoaded.Value != true;

            // Read even while not recording, so that movement during a pause isn't counted once resumed.
            double left = updateDistance(IVRControllers.LEFT);
            double right = updateDistance(IVRControllers.RIGHT);

            if (!recording)
                return;

            extras.LeftDistance += left;
            extras.RightDistance += right;

            var state = tracking.GetState();

            // The game can update more often than the headset tracks.
            if (state.Frame == lastTrackingFrame)
                return;

            lastTrackingFrame = state.Frame;

            // Frames are in order of time, like the replay's.
            double time = gameplayClock.CurrentTime;
            if (time <= lastTime)
                return;

            lastTime = time;

            if (state.Panel != lastPanel)
            {
                lastPanel = state.Panel;
                extras.Panels.Add(new VRReplayPanel { Time = time, Geometry = state.Panel });
            }

            // The pointer is fixed to the controller, so it's kept once per hand rather than in every frame.
            extras.LeftPointer ??= getPointer(state.Left);
            extras.RightPointer ??= getPointer(state.Right);

            extras.Frames.Add(new VRReplayFrame
            {
                Time = time,
                Head = state.Head,
                Left = new VRReplayHand { Grip = state.Left.Grip, Trigger = state.Left.Trigger, Squeeze = state.Left.Squeeze },
                Right = new VRReplayHand { Grip = state.Right.Grip, Trigger = state.Right.Trigger, Squeeze = state.Right.Squeeze },
                ActiveHand = state.ActiveHand,
            });
        }

        private double updateDistance(int hand)
        {
            if (controllers == null)
                return 0;

            double reading = controllers.GetDistanceMoved(hand);
            double moved = reading - lastDistanceReading[hand];
            lastDistanceReading[hand] = reading;
            return Math.Max(moved, 0);
        }

        private static VRPose? getPointer(in VRHandTracking hand)
            => hand.Grip is VRPose grip && hand.Pointer is VRPose pointer ? pointer.RelativeTo(grip) : null;

        /// <summary>
        /// Ends the recording: what was recorded no longer changes.
        /// </summary>
        public void Stop() => stopped = true;

        /// <summary>
        /// Saves what was recorded as extras of <paramref name="score"/>'s replay. To be called after <see cref="Stop"/>, once the
        /// score has been imported (which gives the replay its hash). Not bound to the update thread.
        /// </summary>
        public void Save(ScoreInfo score)
        {
            string hash = score.Hash;

            // Nothing recorded (e.g. a replay was watched); or no replay to save it for (the play wasn't imported, or the ruleset's
            // replays aren't saved).
            if (extras.Frames.Count == 0 || store == null || string.IsNullOrEmpty(hash))
                return;

            try
            {
                VRReplayExtrasEncoder.Save(store, hash, extras);
                Logger.Log($@"Saved VR replay extras for {hash}: {extras.Frames.Count} frames, controllers moved {extras.LeftDistance + extras.RightDistance:N1} m.");
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to save VR replay extras.");
            }
        }
    }
}
