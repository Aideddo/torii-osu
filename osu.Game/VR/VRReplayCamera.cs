// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: where a replay shown as a scene (see <see cref="VRReplayScene"/>) is watched from.
    /// </summary>
    public enum VRReplayCamera
    {
        /// <summary>
        /// Wherever it's flown to.
        /// </summary>
        [Description("Free")]
        Free,

        /// <summary>
        /// Behind the player's right shoulder, going where they go.
        /// </summary>
        [Description("Over the shoulder")]
        OverTheShoulder,

        /// <summary>
        /// Through the player's eyes.
        /// </summary>
        [Description("Player's view")]
        FollowHead,
    }
}
