// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Scoring
{
    /// <summary>
    /// Torii: a kind of data a replay's extras file can carry (see <see cref="ReplayExtrasFile"/>). Saved in files: values never change
    /// or get reused.
    /// </summary>
    public enum ReplayExtrasType
    {
        /// <summary>
        /// Where the player's head, controllers and game panel were over a play in VR.
        /// </summary>
        VRTracking = 1,
    }
}
