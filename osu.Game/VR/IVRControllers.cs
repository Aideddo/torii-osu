// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: per-controller state of VR motion controllers. Cached by VR builds only.
    /// </summary>
    /// <remarks>
    /// By default both triggers act as the left mouse button for the controller that pressed last, which suits menus.
    /// Gameplay can take over the triggers through <see cref="SuppressPointerButtons"/> and <see cref="IsTriggerDown"/>.
    /// </remarks>
    public interface IVRControllers
    {
        public const int LEFT = 0;
        public const int RIGHT = 1;

        /// <summary>
        /// The controller currently driving the pointer.
        /// </summary>
        int ActiveHand { get; }

        /// <summary>
        /// Whether the trigger of <paramref name="hand"/> (<see cref="LEFT"/> or <see cref="RIGHT"/>) is held.
        /// </summary>
        bool IsTriggerDown(int hand);

        /// <summary>
        /// When set, triggers stop pressing the mouse button. The pointer still follows the controller that pressed last.
        /// This is a per-frame request: it is cleared after every input frame and must be set again on every update it should apply to.
        /// </summary>
        bool SuppressPointerButtons { get; set; }

        /// <summary>
        /// Vibrates the controller of <paramref name="hand"/>. A new pulse replaces one still playing.
        /// </summary>
        /// <param name="hand"><see cref="LEFT"/> or <see cref="RIGHT"/>.</param>
        /// <param name="amplitude">Strength, 0 to 1.</param>
        /// <param name="duration">Duration in milliseconds.</param>
        /// <param name="frequency">Frequency in Hz, or 0 for the controller's default.</param>
        void Vibrate(int hand, float amplitude, double duration, float frequency = 0);

        /// <summary>
        /// How far the controller of <paramref name="hand"/> has physically moved since startup, in metres. Only ever increases.
        /// </summary>
        /// <param name="hand"><see cref="LEFT"/> or <see cref="RIGHT"/>.</param>
        double GetDistanceMoved(int hand);
    }
}
