// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: process-wide information about whether the game is running as an immersive VR application (Quest build).
    /// </summary>
    /// <remarks>
    /// This is static (like <see cref="osu.Framework.RuntimeInfo.IsMobile"/>) because it must be readable while config defaults are
    /// initialised, before dependency injection is available.
    /// It must be set by the platform entry point before the game instance is constructed, and never changes afterwards.
    /// </remarks>
    public static class VRRuntime
    {
        private static bool isVR;
        private static bool locked;

        /// <summary>
        /// Whether the game is running as an immersive VR application.
        /// </summary>
        /// <remarks>
        /// When <see langword="true"/>, mobile-specific behaviour (touch-first defaults, orientation handling, phone UI scaling)
        /// should be bypassed even though <see cref="osu.Framework.RuntimeInfo.IsMobile"/> is also <see langword="true"/>.
        /// </remarks>
        public static bool IsVR
        {
            get
            {
                locked = true;
                return isVR;
            }
        }

        /// <summary>
        /// Marks this process as running in immersive VR. Must be called before the game is constructed.
        /// </summary>
        /// <exception cref="InvalidOperationException">If <see cref="IsVR"/> has already been read.</exception>
        public static void EnableVR()
        {
            if (locked && !isVR)
                throw new InvalidOperationException($"{nameof(VRRuntime)}.{nameof(EnableVR)} must be called before {nameof(IsVR)} is first read.");

            isVR = true;
        }

        /// <summary>
        /// Whether phone/tablet-specific behaviour should apply. <see langword="false"/> on desktop and in VR.
        /// </summary>
        public static bool IsMobileNonVR => osu.Framework.RuntimeInfo.IsMobile && !IsVR;
    }
}
