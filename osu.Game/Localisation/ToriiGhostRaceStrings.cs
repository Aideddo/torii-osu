// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ToriiGhostRaceStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ToriiGhostRace";

        /// <summary>
        /// "Race"
        /// </summary>
        public static LocalisableString Race => new TranslatableString(getKey(@"race"), @"Race");

        /// <summary>
        /// "Race (your mods)"
        /// </summary>
        public static LocalisableString RaceWithYourMods => new TranslatableString(getKey(@"race_with_your_mods"), @"Race (your mods)");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
