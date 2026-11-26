// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Performance
{
    /// <summary>
    /// Torii: whether the interface gives up some of its less consequential effects where they cost the most frame time, for devices
    /// without much to spare (on by default on mobile and standalone headsets; a setting elsewhere). Also implied by
    /// <see cref="PotatoMode"/>, which goes a lot further (storyboards, backgrounds, cosmetics).
    /// </summary>
    /// <remarks>
    /// What changes:
    /// <list type="bullet">
    /// <item>Beatmap covers on cards appear at once, without fading in (a card is cached in a frame buffer, which a fade redraws every frame).</item>
    /// <item>Song select panels: no frosted glass, no beat-synced pulse on the selection, still triangles. Panels can then be cached as well.</item>
    /// </list>
    /// Like <see cref="PotatoMode"/>, read once at startup: what's affected is decided as it's built.
    /// </remarks>
    public static class LightweightInterface
    {
        private static bool enabled;

        public static bool Active => enabled || PotatoMode.Active;

        /// <summary>
        /// Sets the flag from config, before the interface is built.
        /// </summary>
        public static void SetFromConfig(bool enabled) => LightweightInterface.enabled = enabled;
    }
}
