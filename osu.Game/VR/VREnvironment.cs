// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: what surrounds the game panel in VR.
    /// </summary>
    public enum VREnvironment
    {
        /// <summary>
        /// The background floating far behind the panel, in a soft glow of its dominant colour above a grid floor.
        /// </summary>
        [Description("Ambient void")]
        Void,

        /// <summary>
        /// The real room around you (camera passthrough). Storyboards and backgrounds are hidden.
        /// </summary>
        [Description("Passthrough (your room)")]
        Passthrough,

        /// <summary>
        /// A dark room with the background on a far wall and a music-reactive floor.
        /// </summary>
        [Description("Custom field")]
        Field,
    }
}
