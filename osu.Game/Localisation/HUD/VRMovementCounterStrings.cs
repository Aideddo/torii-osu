// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation.HUD
{
    public static class VRMovementCounterStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.HUD.VRMovementCounter";

        /// <summary>
        /// "Distance to show"
        /// </summary>
        public static LocalisableString Period => new TranslatableString(getKey(@"period"), @"Distance to show");

        /// <summary>
        /// "How far the VR controllers were moved: during this play of the beatmap, since the game was started, today, or ever."
        /// </summary>
        public static LocalisableString PeriodDescription => new TranslatableString(getKey(@"period_description"), @"How far the VR controllers were moved: during this play of the beatmap, since the game was started, today, or ever.");

        /// <summary>
        /// "Current beatmap"
        /// </summary>
        public static LocalisableString PeriodBeatmap => new TranslatableString(getKey(@"period_beatmap"), @"Current beatmap");

        /// <summary>
        /// "Game session"
        /// </summary>
        public static LocalisableString PeriodSession => new TranslatableString(getKey(@"period_session"), @"Game session");

        /// <summary>
        /// "All time"
        /// </summary>
        public static LocalisableString PeriodAllTime => new TranslatableString(getKey(@"period_all_time"), @"All time");

        /// <summary>
        /// "Today"
        /// </summary>
        public static LocalisableString PeriodToday => new TranslatableString(getKey(@"period_today"), @"Today");

        /// <summary>
        /// "Text alignment"
        /// </summary>
        public static LocalisableString Alignment => new TranslatableString(getKey(@"alignment"), @"Text alignment");

        /// <summary>
        /// "Which side the distance and its label line up on."
        /// </summary>
        public static LocalisableString AlignmentDescription => new TranslatableString(getKey(@"alignment_description"), @"Which side the distance and its label line up on.");

        /// <summary>
        /// "Left"
        /// </summary>
        public static LocalisableString AlignmentLeft => new TranslatableString(getKey(@"alignment_left"), @"Left");

        /// <summary>
        /// "Centre"
        /// </summary>
        public static LocalisableString AlignmentCentre => new TranslatableString(getKey(@"alignment_centre"), @"Centre");

        /// <summary>
        /// "Right"
        /// </summary>
        public static LocalisableString AlignmentRight => new TranslatableString(getKey(@"alignment_right"), @"Right");

        /// <summary>
        /// "Show label"
        /// </summary>
        public static LocalisableString ShowLabel => new TranslatableString(getKey(@"show_label"), @"Show label");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
