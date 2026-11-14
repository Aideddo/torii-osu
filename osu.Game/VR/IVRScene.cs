// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: what is drawn around the game panel in VR, beyond the user's settings. Cached by VR builds only.
    /// </summary>
    /// <remarks>
    /// Everything here is a per-frame request: it applies until the next update frame, and must be made again on every update it should
    /// keep applying to. That way a screen which stops updating (e.g. gameplay left behind the results) never leaves anything behind.
    /// </remarks>
    public interface IVRScene
    {
        /// <summary>
        /// Draws a second pair of hands: those of the player of the replay being watched.
        /// </summary>
        /// <param name="left">The left hand, in the space of <see cref="IVRTracking"/>; null to draw none.</param>
        /// <param name="right">The right hand.</param>
        /// <param name="activeHand">The hand driving the replay's cursor: <see cref="IVRControllers.LEFT"/> or <see cref="IVRControllers.RIGHT"/>.</param>
        /// <param name="model">What the hands hold.</param>
        void ShowGhostHands(VRGhostHand? left, VRGhostHand? right, int activeHand, VRControllerModel model);

        /// <summary>
        /// Puts the game panel somewhere else than the user's settings have it, at another size and distance, without changing those
        /// settings: it goes back to them once this is no longer requested.
        /// </summary>
        /// <remarks>
        /// The panel is moved when <paramref name="panel"/> differs from the last request. In between the user can still grab and move it.
        /// Whether the panel is curved isn't up to the game.
        /// </remarks>
        /// <param name="panel">Where the panel goes, in the space of <see cref="IVRTracking"/>.</param>
        void UsePanel(VRPanelGeometry panel);

        /// <summary>
        /// Has the user hold plain controllers pointing straight ahead, whatever their settings (<see cref="VRControllerModel"/>, pen tilt
        /// and offsets): what they hold shouldn't be mistaken for what the player of a replay held.
        /// </summary>
        void UseDefaultControllers();
    }

    /// <summary>
    /// Torii: one hand of <see cref="IVRScene.ShowGhostHands"/>.
    /// </summary>
    /// <param name="Grip">Where the controller is held.</param>
    /// <param name="Pointer">Where the pointer ray starts (it runs along -Z): a pen is drawn with its tip here.</param>
    /// <param name="Trigger">How far the trigger is pulled, 0 to 1.</param>
    /// <param name="Squeeze">How far the grip button is squeezed, 0 to 1.</param>
    public readonly record struct VRGhostHand(VRPose Grip, VRPose Pointer, float Trigger, float Squeeze)
    {
        /// <summary>
        /// A hand as it was recorded, held at <paramref name="grip"/>.
        /// </summary>
        /// <param name="hand">The recorded hand.</param>
        /// <param name="grip">Where its controller is held: where it was recorded, or where that is taken to.</param>
        /// <param name="pointer">Where the pointer ray starts relative to the grip (it's fixed to the controller), if known.</param>
        public static VRGhostHand From(in VRReplayHand hand, VRPose grip, VRPose? pointer)
            => new VRGhostHand(grip, grip.Apply(pointer ?? VRPose.IDENTITY), hand.Trigger, hand.Squeeze);
    }
}
