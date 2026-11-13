// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class StableUIStrings
    {
        public static LocalisableString AllMods => new TranslatableString(getKey(@"all_mods"), @"All mods / customise");

        public static LocalisableString LegacyBeatmapOptionsPrompt => new TranslatableString(getKey(@"legacy_beatmap_options_prompt"), @"What do you want to do with this beatmap?");

        public static LocalisableString LegacyBeatmapOptionsRemoveFromUnplayed => new TranslatableString(getKey(@"legacy_beatmap_options_remove_from_unplayed"), @"Remove from Unplayed");

        public static LocalisableString LegacyBeatmapOptionsClearLocalScores => new TranslatableString(getKey(@"legacy_beatmap_options_clear_local_scores"), @"Clear local scores");

        public static LocalisableString LegacyModsDescription => new TranslatableString(getKey(@"legacy_mods_description"), @"Mods provide different ways to enjoy gameplay. Some have an effect on the score you can achieve during ranked play. Others are just for fun.");

        public static LocalisableString DifficultyReduction => new TranslatableString(getKey(@"difficulty_reduction"), @"Difficulty Reduction");

        public static LocalisableString DifficultyIncrease => new TranslatableString(getKey(@"difficulty_increase"), @"Difficulty Increase");

        public static LocalisableString SpecialMods => new TranslatableString(getKey(@"special_mods"), @"Special");

        public static LocalisableString ResetAllMods => new TranslatableString(getKey(@"reset_all_mods"), @"1. Reset all mods");

        public static LocalisableString CloseDialog => new TranslatableString(getKey(@"close_dialog"), @"2. Close");

        public static LocalisableString LegacyScoreMultiplier(string multiplier) => new TranslatableString(getKey(@"legacy_score_multiplier"), @"Score Multiplier: {0}", multiplier);

        private static string getKey(string key) => $@"osu.Game.Resources.Localisation.UserInterface:{key}";
    }
}
