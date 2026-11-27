// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class FirstRunSetupOverlayStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.FirstRunSetupOverlay";

        /// <summary>
        /// "Get started"
        /// </summary>
        public static LocalisableString GetStarted => new TranslatableString(getKey(@"get_started"), @"Get started");

        /// <summary>
        /// "Click to resume first-run setup at any point"
        /// </summary>
        public static LocalisableString ClickToResumeFirstRunSetupAtAnyPoint =>
            new TranslatableString(getKey(@"click_to_resume_first_run_setup_at_any_point"), @"Click to resume first-run setup at any point");

        /// <summary>
        /// "First-run setup"
        /// </summary>
        public static LocalisableString FirstRunSetupTitle => new TranslatableString(getKey(@"first_run_setup_title"), @"First-run setup");

        /// <summary>
        /// "Set up Torii to suit you"
        /// </summary>
        public static LocalisableString FirstRunSetupDescription => new TranslatableString(getKey(@"first_run_setup_description"), @"Set up Torii to suit you");

        /// <summary>
        /// "Welcome"
        /// </summary>
        public static LocalisableString WelcomeTitle => new TranslatableString(getKey(@"welcome_title"), @"Welcome");

        /// <summary>
        /// "Welcome to the Torii setup guide!
        ///
        /// Torii is an independent community rhythm game project and is not affiliated with any other rhythm game or its creators.
        ///
        /// This guide will help you get the important choices out of the way to ensure a great first experience."
        /// </summary>
        public static LocalisableString WelcomeDescription => new TranslatableString(getKey(@"welcome_description"), @"Welcome to the Torii setup guide!

Torii is an independent community rhythm game project and is not affiliated with any other rhythm game or its creators.

This guide will help you get the important choices out of the way to ensure a great first experience.");

        /// <summary>
        /// "The size of the Torii user interface can be adjusted to your liking."
        /// </summary>
        public static LocalisableString UIScaleDescription => new TranslatableString(getKey(@"ui_scale_description"), @"The size of the Torii user interface can be adjusted to your liking.");

        /// <summary>
        /// "Behaviour"
        /// </summary>
        public static LocalisableString Behaviour => new TranslatableString(getKey(@"behaviour"), @"Behaviour");

        /// <summary>
        /// "Torii ships with curated defaults tuned for the best gameplay and reading experience (primarily for standard torii mode, though many also apply across other modes)."
        /// </summary>
        public static LocalisableString BehaviourDescription => new TranslatableString(getKey(@"behaviour_description"),
            @"Torii ships with curated defaults tuned for the best gameplay and reading experience (primarily for standard torii mode, though many also apply across other modes).");

        /// <summary>
        /// "Torii Recommended"
        /// </summary>
        public static LocalisableString ToriiRecommendedButton => new TranslatableString(getKey(@"torii_recommended_button"), @"Torii Recommended");

        /// <summary>
        /// "You already have the Torii Recommended settings active by default."
        /// </summary>
        public static LocalisableString ToriiRecommendedAlreadyActive => new TranslatableString(getKey(@"torii_recommended_already_active"), @"You already have the Torii Recommended settings active by default.");

        /// <summary>
        /// "Torii Recommended settings applied."
        /// </summary>
        public static LocalisableString ToriiRecommendedApplied => new TranslatableString(getKey(@"torii_recommended_applied"), @"Torii Recommended settings applied.");

        /// <summary>
        /// "New defaults"
        /// </summary>
        public static LocalisableString NewDefaults => new TranslatableString(getKey(@"new_defaults"), @"New defaults");

        /// <summary>
        /// "Classic defaults"
        /// </summary>
        public static LocalisableString ClassicDefaults => new TranslatableString(getKey(@"classic_defaults"), @"Classic defaults");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
