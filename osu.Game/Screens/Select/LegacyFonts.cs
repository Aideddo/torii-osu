// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Shared typography for the classic UI. Inter is used for the classic font option.
    /// </summary>
    public static class LegacyFonts
    {
        /// <summary>
        /// Set from the classic theme's startup snapshot.
        /// </summary>
        public static bool UseClassicFont { get; set; }

        public static FontUsage Get(float size, FontWeight weight = FontWeight.Regular)
            => UseClassicFont
                ? OsuFont.GetFont(Typeface.Inter, size: size, weight: weight)
                : OsuFont.GetFont(size: size, weight: weight);
    }
}
