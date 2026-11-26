// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.VR
{
    /// <summary>
    /// Torii: names of the external render targets (compositor layers) the game draws into in VR.
    /// See <see cref="osu.Framework.Platform.GameHost.GetExternalRenderTarget"/>.
    /// </summary>
    public static class VRLayers
    {
        /// <summary>
        /// Beatmap / menu backgrounds, video and storyboard, placed far behind the game panel.
        /// </summary>
        public const string BACKGROUND = "background";
    }
}
