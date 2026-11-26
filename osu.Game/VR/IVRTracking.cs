// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using Newtonsoft.Json;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: where the VR headset, controllers and game panel are. Cached by VR builds only.
    /// </summary>
    public interface IVRTracking
    {
        /// <summary>
        /// The name of the VR runtime, e.g. "Oculus" or "SteamVR/OpenXR".
        /// </summary>
        string RuntimeName { get; }

        /// <summary>
        /// Tracking as of the latest VR frame.
        /// </summary>
        VRTrackingState GetState();
    }

    /// <summary>
    /// Torii: one VR frame's tracking. All poses are in the same space: fixed to the room, and replaced when the user recentres.
    /// </summary>
    public struct VRTrackingState
    {
        /// <summary>
        /// Counts VR frames: unchanged since the last read means there is nothing new.
        /// </summary>
        public long Frame;

        /// <summary>
        /// The headset (it looks along -Z), or null while it isn't tracked.
        /// </summary>
        public VRPose? Head;

        public VRHandTracking Left;
        public VRHandTracking Right;

        /// <summary>
        /// The controller driving the pointer: <see cref="IVRControllers.LEFT"/> or <see cref="IVRControllers.RIGHT"/>.
        /// </summary>
        public int ActiveHand;

        public VRPanelGeometry Panel;
    }

    public struct VRHandTracking
    {
        /// <summary>
        /// Where the controller is held (OpenXR's grip pose), or null while it isn't tracked.
        /// </summary>
        public VRPose? Grip;

        /// <summary>
        /// The pointer ray, which starts at this pose's position and runs along its -Z: out of the front of the controller, or out of the
        /// tip of the pen shown in its place. Null while the controller isn't tracked.
        /// </summary>
        public VRPose? Pointer;

        /// <summary>
        /// How far the trigger is pulled, 0 to 1.
        /// </summary>
        public float Trigger;

        /// <summary>
        /// How far the grip button is squeezed, 0 to 1.
        /// </summary>
        public float Squeeze;
    }

    /// <summary>
    /// Torii: where the panel showing the game is, and its shape: a section of a vertical cylinder, or a flat rectangle.
    /// </summary>
    /// <param name="Pose">The cylinder's axis point, normally near the viewer: the panel's middle is straight ahead of it along -Z, and +Y runs
    /// along the axis.</param>
    /// <param name="Radius">How far the panel is from <paramref name="Pose"/>, in metres.</param>
    /// <param name="HorizontalAngle">The horizontal angle the panel covers as seen from <paramref name="Pose"/>, in radians.</param>
    /// <param name="Aspect">The panel's width divided by its height, as shown.</param>
    /// <param name="Curved">Whether the panel is a cylinder section. A flat panel is as wide as <paramref name="HorizontalAngle"/> makes it at
    /// <paramref name="Radius"/>.</param>
    public readonly record struct VRPanelGeometry(
        [property: JsonProperty("pose")] VRPose Pose,
        [property: JsonProperty("radius")] float Radius,
        [property: JsonProperty("horizontal_angle")] float HorizontalAngle,
        [property: JsonProperty("aspect")] float Aspect,
        [property: JsonProperty("curved")] bool Curved)
    {
        /// <summary>
        /// The panel's width in metres, along its surface.
        /// </summary>
        [JsonIgnore]
        public float Width => Curved ? Radius * HorizontalAngle : 2 * Radius * MathF.Tan(HorizontalAngle / 2);

        /// <summary>
        /// The middle of the panel's surface, facing as <see cref="Pose"/> does.
        /// </summary>
        [JsonIgnore]
        public VRPose SurfaceCentre => Pose.Apply(new VRPose(new Vector3(0, 0, -Radius), Quaternion.Identity));

        /// <summary>
        /// The panel's height in metres.
        /// </summary>
        [JsonIgnore]
        public float Height => Aspect > 0 ? Width / Aspect : 0;

        /// <summary>
        /// This panel, shown at another width for its height: wider or narrower about its middle, at the same distance.
        /// </summary>
        /// <remarks>
        /// For an image of another shape, laid out about its middle at the same scale (as osu! lays the playfield out): what is in
        /// the middle of the image stays where it was.
        /// </remarks>
        public VRPanelGeometry WithAspect(float aspect)
        {
            if (aspect <= 0 || Aspect <= 0 || Radius <= 0)
                return this;

            float width = Height * aspect;

            // No more than most of the way round.
            float angle = Curved ? MathF.Min(width / Radius, 1.8f * MathF.PI) : MathF.Min(2 * MathF.Atan(width / (2 * Radius)), 0.95f * MathF.PI);

            return this with { HorizontalAngle = angle, Aspect = aspect };
        }

        /// <summary>
        /// Takes a pose placed around <paramref name="other"/> to the matching place around this panel: as far from the middle of the
        /// panel, in proportion to its size. What pointed at the other panel points at about the same part of this one (exactly so if
        /// the two have the same shape).
        /// </summary>
        public VRPose MapFrom(VRPanelGeometry other, VRPose pose)
        {
            var relative = pose.RelativeTo(other.SurfaceCentre);
            float scale = other.Width > 0 ? Width / other.Width : 1;

            return SurfaceCentre.Apply(relative with { Position = relative.Position * scale });
        }
    }
}
