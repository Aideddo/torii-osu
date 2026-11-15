// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Numerics;
using Newtonsoft.Json;
using osu.Framework.Platform.OpenXR;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: what a play in VR saves in addition to its replay, so that watching the replay can show what the player did: where their
    /// head and controllers were throughout, where the game panel was, and how far the controllers were moved.
    /// Kept as the <see cref="Scoring.ReplayExtrasType.VRTracking"/> extras of the replay (see <see cref="Scoring.ReplayExtrasStore"/>),
    /// in the format of <see cref="VRReplayExtrasEncoder"/>.
    /// </summary>
    /// <remarks>
    /// All poses are in one space, which is fixed to the player's room: only how they lie relative to each other (and to the panels in
    /// <see cref="Panels"/>) means anything.
    /// </remarks>
    public class VRReplayExtras
    {
        /// <summary>
        /// The hash of the replay these belong to (<see cref="Scoring.ScoreInfo.Hash"/>).
        /// </summary>
        [JsonProperty("score_hash")]
        public string ScoreHash { get; set; } = string.Empty;

        [JsonProperty("recorded_at")]
        public DateTimeOffset RecordedAt { get; set; }

        /// <summary>
        /// The VR runtime the play was made on, e.g. "Oculus" or "SteamVR/OpenXR".
        /// </summary>
        [JsonProperty("runtime")]
        public string Runtime { get; set; } = string.Empty;

        /// <summary>
        /// What the player held.
        /// </summary>
        [JsonProperty("controller_model")]
        public VRControllerModel ControllerModel { get; set; }

        /// <summary>
        /// With <see cref="VRControllerModel.Pen"/>: how far the pen was tilted down from the controller's pointing direction, in degrees
        /// (<see cref="Configuration.OsuSetting.VRPenTilt"/>).
        /// </summary>
        /// <remarks>
        /// The pen settings are what the player had set. Where the pen actually was follows from <see cref="LeftPointer"/> and
        /// <see cref="RightPointer"/>, which include them.
        /// </remarks>
        [JsonProperty("pen_tilt")]
        public float PenTilt { get; set; }

        /// <summary>
        /// How far forward of the controller the pen's tip was, in centimetres (<see cref="Configuration.OsuSetting.VRPenOffsetForward"/>).
        /// </summary>
        [JsonProperty("pen_offset_forward")]
        public float PenOffsetForward { get; set; }

        /// <summary>
        /// How far above the controller the pen's tip was, in centimetres (<see cref="Configuration.OsuSetting.VRPenOffsetUp"/>).
        /// </summary>
        [JsonProperty("pen_offset_up")]
        public float PenOffsetUp { get; set; }

        /// <summary>
        /// How far the left controller was moved while the play was running (not paused), in metres.
        /// </summary>
        [JsonProperty("left_distance")]
        public double LeftDistance { get; set; }

        /// <summary>
        /// How far the right controller was moved while the play was running (not paused), in metres.
        /// </summary>
        [JsonProperty("right_distance")]
        public double RightDistance { get; set; }

        /// <summary>
        /// Where the left controller's pointer ray starts, relative to its grip pose (<see cref="VRReplayHand.Grip"/>): the ray runs along
        /// -Z of this. Null if the controller was never tracked.
        /// </summary>
        [JsonProperty("left_pointer")]
        public VRPose? LeftPointer { get; set; }

        /// <summary>
        /// Where the right controller's pointer ray starts, relative to its grip pose. See <see cref="LeftPointer"/>.
        /// </summary>
        [JsonProperty("right_pointer")]
        public VRPose? RightPointer { get; set; }

        /// <summary>
        /// Where the game panel was: at the start, and whenever it changed after (the player can grab it, or recentre). In order of time.
        /// </summary>
        [JsonProperty("panels")]
        public List<VRReplayPanel> Panels { get; set; } = new List<VRReplayPanel>();

        /// <summary>
        /// The resolution the times of <see cref="Frames"/> are saved at, in milliseconds. Set when saved (see
        /// <see cref="VRReplayExtrasEncoder"/>); likewise the two below.
        /// </summary>
        [JsonProperty("time_unit")]
        public double TimeUnit { get; set; }

        /// <summary>
        /// The resolution positions are saved at, in metres.
        /// </summary>
        [JsonProperty("position_unit")]
        public float PositionUnit { get; set; }

        /// <summary>
        /// The resolution rotations are saved at (each component of the quaternion).
        /// </summary>
        [JsonProperty("rotation_unit")]
        public float RotationUnit { get; set; }

        /// <summary>
        /// Head and controller poses over the play, in order of time. Saved apart from the rest, in a compact form.
        /// </summary>
        [JsonIgnore]
        public List<VRReplayFrame> Frames { get; set; } = new List<VRReplayFrame>();

        /// <summary>
        /// Frames further apart than this (in milliseconds) aren't blended: tracking was lost or the play was held up in between.
        /// </summary>
        private const double max_blend_gap = 250;

        /// <summary>
        /// Where the head and controllers were at <paramref name="time"/>, between the frames around it.
        /// </summary>
        /// <returns>Whether <paramref name="time"/> is within the recording.</returns>
        public bool TryGetFrameAt(double time, out VRReplayFrame frame)
        {
            frame = default;

            if (Frames.Count == 0 || time < Frames[0].Time || time > Frames[^1].Time)
                return false;

            int low = frameIndexAt(time);

            var before = Frames[low];
            frame = before;

            if (low == Frames.Count - 1)
                return true;

            var after = Frames[low + 1];
            double gap = after.Time - before.Time;

            if (gap <= 0 || gap > max_blend_gap)
                return true;

            float amount = (float)((time - before.Time) / gap);

            frame.Time = time;
            frame.Head = blend(before.Head, after.Head, amount);
            frame.Left = blend(before.Left, after.Left, amount);
            frame.Right = blend(before.Right, after.Right, amount);
            return true;
        }

        /// <summary>
        /// The last frame at or before <paramref name="time"/>, which must be within the recording.
        /// </summary>
        private int frameIndexAt(double time)
        {
            int low = 0, high = Frames.Count - 1;

            while (low < high)
            {
                int mid = (low + high + 1) / 2;

                if (Frames[mid].Time <= time)
                    low = mid;
                else
                    high = mid - 1;
            }

            return low;
        }

        // Per frame, whether each hand had been left alone long enough to be hidden (left, right interleaved). Worked out on first use.
        private bool[]? idleHands;

        /// <summary>
        /// Whether the controller of <paramref name="hand"/> had been left alone for a while at <paramref name="time"/>, so it's hidden as
        /// the player's own would have been (see <see cref="ControllerIdleTracker"/>).
        /// </summary>
        /// <param name="hand"><see cref="IVRControllers.LEFT"/> or <see cref="IVRControllers.RIGHT"/>.</param>
        /// <param name="time">The gameplay time, in milliseconds.</param>
        public bool IsHandIdleAt(int hand, double time)
        {
            if (Frames.Count == 0 || time < Frames[0].Time || time > Frames[^1].Time)
                return false;

            idleHands ??= computeIdleHands();
            return idleHands[frameIndexAt(time) * 2 + hand];
        }

        private bool[] computeIdleHands()
        {
            var tracker = new ControllerIdleTracker();
            bool[] idle = new bool[Frames.Count * 2];

            for (int i = 0; i < Frames.Count; i++)
            {
                var frame = Frames[i];

                update(IVRControllers.LEFT, frame.Left);
                update(IVRControllers.RIGHT, frame.Right);

                void update(int hand, in VRReplayHand h)
                {
                    var grip = h.Grip ?? VRPose.IDENTITY;
                    bool touched = ControllerIdleTracker.IsPulled(h.Trigger) || ControllerIdleTracker.IsPulled(h.Squeeze);

                    tracker.Update(hand, h.Grip != null, grip.Position, grip.Rotation, touched, frame.ActiveHand, frame.Time / 1000);
                    idle[i * 2 + hand] = tracker.IsIdle(hand);
                }
            }

            return idle;
        }

        private static VRReplayHand blend(in VRReplayHand a, in VRReplayHand b, float amount) => new VRReplayHand
        {
            Grip = blend(a.Grip, b.Grip, amount),
            Trigger = a.Trigger + (b.Trigger - a.Trigger) * amount,
            Squeeze = a.Squeeze + (b.Squeeze - a.Squeeze) * amount,
        };

        // Tracked on one side only: stays where it was last (or isn't there yet).
        private static VRPose? blend(VRPose? a, VRPose? b, float amount) => a is VRPose from && b is VRPose to ? VRPose.Lerp(from, to, amount) : a;

        /// <summary>
        /// Where the player was over the play: the average of where their head was, level, facing the way they did on average.
        /// What was around them (the panel, their hands) can be placed around someone else from this.
        /// </summary>
        /// <returns>The pose, or null if the head was never tracked.</returns>
        public VRPose? GetPlayerPose()
        {
            var position = Vector3.Zero;
            var forward = Vector3.Zero;
            int count = 0;

            foreach (var frame in Frames)
            {
                if (frame.Head is not VRPose head)
                    continue;

                position += head.Position;
                forward += Vector3.Transform(-Vector3.UnitZ, head.Rotation);
                count++;
            }

            if (count == 0)
                return null;

            forward.Y = 0;

            // Looked every way equally: any way round will do.
            if (forward.LengthSquared() < 1e-6f)
                forward = -Vector3.UnitZ;

            return new VRPose(position / count, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(-forward.X, -forward.Z)));
        }

        /// <summary>
        /// Where the game panel was at <paramref name="time"/>, or null if that was never recorded.
        /// </summary>
        public VRPanelGeometry? GetPanelAt(double time)
        {
            VRPanelGeometry? panel = Panels.Count > 0 ? Panels[0].Geometry : null;

            foreach (var p in Panels)
            {
                if (p.Time > time)
                    break;

                panel = p.Geometry;
            }

            return panel;
        }
    }

    /// <summary>
    /// Torii: where the game panel was from a point of a VR play on.
    /// </summary>
    public class VRReplayPanel
    {
        /// <summary>
        /// The gameplay time this applies from, in milliseconds.
        /// </summary>
        [JsonProperty("time")]
        public double Time { get; set; }

        [JsonProperty("geometry")]
        public VRPanelGeometry Geometry { get; set; }
    }

    /// <summary>
    /// Torii: where the player's head and controllers were at one moment of a VR play.
    /// </summary>
    public struct VRReplayFrame
    {
        /// <summary>
        /// Gameplay time in milliseconds, as for the frames of the replay itself.
        /// </summary>
        public double Time;

        /// <summary>
        /// The headset (it looks along -Z), or null if it wasn't tracked.
        /// </summary>
        public VRPose? Head;

        public VRReplayHand Left;
        public VRReplayHand Right;

        /// <summary>
        /// The controller driving the cursor: <see cref="IVRControllers.LEFT"/> or <see cref="IVRControllers.RIGHT"/>.
        /// </summary>
        public int ActiveHand;
    }

    public struct VRReplayHand
    {
        /// <summary>
        /// Where the controller was held (OpenXR's grip pose), or null if it wasn't tracked.
        /// </summary>
        public VRPose? Grip;

        /// <summary>
        /// How far the trigger was pulled, 0 to 1.
        /// </summary>
        public float Trigger;

        /// <summary>
        /// How far the grip button was squeezed, 0 to 1.
        /// </summary>
        public float Squeeze;
    }
}
