// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Online
{
    /// <summary>
    /// Lightweight, safe provider for client environment and peripheral metadata
    /// (tablet model, active area in mm, monitor refresh rate, audio offset).
    /// Used by anti-cheat and multi-account correlation services.
    /// </summary>
    public static class EnvironmentTelemetry
    {
        /// <summary>
        /// Delegate registered by the game host to provide live hardware/peripherals state.
        /// </summary>
        public static Func<string?>? Provider { get; set; }

        /// <summary>
        /// Returns the formatted environment telemetry string, or null if uninitialized/unavailable.
        /// Guaranteed not to throw exceptions.
        /// </summary>
        public static string? Get()
        {
            try
            {
                return Provider?.Invoke();
            }
            catch
            {
                return null;
            }
        }
    }
}
