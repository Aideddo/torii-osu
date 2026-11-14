// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: switching a desktop game in and out of VR mode (presenting through a headset's runtime instead of the window).
    /// Only available where the mode can be switched: not on a standalone headset, which is always in VR.
    /// </summary>
    /// <remarks>
    /// VR mode is chosen when the game starts, so switching restarts the game.
    /// </remarks>
    public interface IVRModeControl
    {
        /// <summary>
        /// Whether the game is running in VR mode.
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// Whether VR mode can be enabled right now (the headset's runtime, SteamVR, is running). Updated by <see cref="Refresh"/>.
        /// </summary>
        IBindable<bool> Available { get; }

        /// <summary>
        /// Whether to offer VR mode once the game has started (it wasn't just left, or declined by starting the game without it).
        /// </summary>
        bool OfferOnStartup { get; }

        /// <summary>
        /// Why the game is back out of VR mode, to tell the user after it restarted, or null.
        /// </summary>
        string? StartupMessage { get; }

        /// <summary>
        /// Checks again whether VR mode is available.
        /// </summary>
        void Refresh();

        /// <summary>
        /// Restarts the game in VR mode.
        /// </summary>
        void Enable();

        /// <summary>
        /// Restarts the game out of VR mode.
        /// </summary>
        void Disable();
    }
}
