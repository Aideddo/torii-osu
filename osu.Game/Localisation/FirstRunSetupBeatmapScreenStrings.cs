// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class FirstRunSetupBeatmapScreenStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.FirstRunSetupBeatmapScreen";

        /// <summary>
        /// "Obtaining Beatmaps"
        /// </summary>
        public static LocalisableString Header => new TranslatableString(getKey(@"header"), @"Obtaining Beatmaps");

        /// <summary>
        /// "&quot;Beatmaps&quot; are playable levels. Torii does not come with any beatmaps pre-loaded."
        /// </summary>
        public static LocalisableString Description => new TranslatableString(getKey(@"description"), @"""Beatmaps"" are playable levels. Torii does not come with any beatmaps pre-loaded.");

        /// <summary>
        /// "Drag and drop .osz beatmap files into the game window, import your collection on the next step, or use the main menu &quot;browse&quot; button at any time."
        /// </summary>
        public static LocalisableString ObtainMoreBeatmaps => new TranslatableString(getKey(@"obtain_more_beatmaps"), @"Drag and drop .osz beatmap files into the game window, import your collection on the next step, or use the main menu ""browse"" button at any time.");

        /// <summary>
        /// "You currently have {0} beatmap loaded!|You currently have {0} beatmaps loaded!"
        /// </summary>
        public static LocalisableString CurrentlyLoadedBeatmaps(int beatmaps) => new PluralisableString(new TranslatableString(getKey(@"currently_loaded_beatmaps"), @"You currently have {0} beatmap loaded!|You currently have {0} beatmaps loaded!", beatmaps), beatmaps, '|');

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
