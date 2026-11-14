// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: while a replay is watched in VR, shows the hands of its player where they were (if the play was in VR and saved them: see
    /// <see cref="VRReplayExtras"/>), and has the viewer hold plain controllers so the two aren't mistaken for each other.
    /// </summary>
    /// <remarks>
    /// For the hands to point at the panel just as they did, the viewer's panel is given the size and distance of the player's for the
    /// time being, and put where the player had theirs: in the same place as seen from the viewer as it was from the player.
    /// The hands are placed around the panel as they were around the player's, so they follow it when the viewer moves it.
    /// </remarks>
    public partial class VRReplayGhostHands : Component
    {
        [Resolved]
        private IVRScene scene { get; set; } = null!;

        [Resolved]
        private IVRTracking tracking { get; set; } = null!;

        [Resolved]
        private ReplayExtrasStore store { get; set; } = null!;

        [Resolved]
        private DrawableRuleset drawableRuleset { get; set; } = null!;

        private VRReplayExtras? extras;
        private bool loadStarted;

        // Where the player was (null if their head was never tracked), and where the viewer was when the replay's panel was first placed:
        // what was around the one is put around the other.
        private VRPose? playerPose;
        private VRPose? viewerPose;

        // The player's panel last placed for the viewer, and where it was put.
        private VRPanelGeometry? placedPanel;
        private VRPanelGeometry placement;

        protected override void Update()
        {
            base.Update();

            if (!drawableRuleset.HasReplayLoaded.Value)
                return;

            scene.UseDefaultControllers();

            if (!loadStarted)
            {
                loadStarted = true;
                load(drawableRuleset.ReplayScore?.ScoreInfo.Hash);
            }

            // The time the replay's cursor is shown for.
            double time = drawableRuleset.FrameStableClock.CurrentTime;

            if (extras == null || extras.GetPanelAt(time) is not VRPanelGeometry recordedPanel)
                return;

            var state = tracking.GetState();

            if (recordedPanel != placedPanel)
            {
                placedPanel = recordedPanel;
                placement = place(recordedPanel, state);
            }

            scene.UsePanel(placement);

            if (!extras.TryGetFrameAt(time, out var frame))
                return;

            // Where the panel actually is: about to be where it was put, or where the viewer has moved it since.
            var panel = state.Panel;

            scene.ShowGhostHands(
                createHand(frame.Left, extras.LeftPointer, recordedPanel, panel),
                createHand(frame.Right, extras.RightPointer, recordedPanel, panel),
                frame.ActiveHand,
                extras.ControllerModel);
        }

        private void load(string? scoreHash) => VRReplayExtrasEncoder.LoadAsync(store, scoreHash, Scheduler, loaded =>
        {
            extras = loaded;
            playerPose = loaded.GetPlayerPose();

            Logger.Log($"Replay has VR extras: showing its player's hands ({loaded.Frames.Count} frames, held {loaded.ControllerModel}).");
        });

        /// <summary>
        /// Where the player's panel goes for the viewer.
        /// </summary>
        private VRPanelGeometry place(VRPanelGeometry recordedPanel, in VRTrackingState state)
        {
            // The viewer as they are when the replay starts: the panel doesn't follow them around after.
            viewerPose ??= state.Head?.Levelled;

            if (playerPose is VRPose player && viewerPose is VRPose viewer)
                return recordedPanel with { Pose = viewer.Apply(recordedPanel.Pose.RelativeTo(player)) };

            // Nothing to go by for where it was: where the viewer's is, at the player's size and distance.
            return recordedPanel with { Pose = state.Panel.Pose };
        }

        private static VRGhostHand? createHand(in VRReplayHand hand, VRPose? pointer, VRPanelGeometry recordedPanel, VRPanelGeometry panel)
        {
            if (hand.Grip is not VRPose recordedGrip)
                return null;

            var grip = panel.MapFrom(recordedPanel, recordedGrip);

            // (The pointer is fixed to the controller: it isn't moved further off by a larger panel.)
            return VRGhostHand.From(hand, grip, pointer);
        }
    }
}
