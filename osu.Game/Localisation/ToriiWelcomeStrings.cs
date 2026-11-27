// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ToriiWelcomeStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.ToriiWelcome";

        public static LocalisableString WelcomeTitle => new TranslatableString(getKey(@"welcome_title"), @"Welcome to Torii!");

        public static LocalisableString WelcomeDescription => new TranslatableString(getKey(@"welcome_description"), @"A quick look at what's unique to Torii.");

        public static LocalisableString ClickToResumeWelcome => new TranslatableString(getKey(@"click_to_resume_welcome"), @"Click to resume the Torii welcome guide at any point");

        // ── paso 1: que es torii ──

        public static LocalisableString IntroTitle => new TranslatableString(getKey(@"intro_title"), @"Torii");

        public static LocalisableString IntroDescription => new TranslatableString(getKey(@"intro_description"), @"Torii is an independent rhythm game community and server with its own client and extras built on top.

Everything you play here stays here. Your scores, pp, and leaderboard spots live on Torii, completely separate from any other server.");

        public static LocalisableString IntroPointsTitle => new TranslatableString(getKey(@"intro_points_title"), @"Points and cosmetics");

        public static LocalisableString IntroPointsDescription => new TranslatableString(getKey(@"intro_points_description"), @"Playing earns Torii points. Spend them in the store on cursor trails, name colours and auras that show up next to your name everywhere.");

        public static LocalisableString IntroRenderTitle => new TranslatableString(getKey(@"intro_render_title"), @"Replay rendering");

        public static LocalisableString IntroRenderDescription => new TranslatableString(getKey(@"intro_render_description"), @"Turn any replay into a video straight from the results screen. No external tools, no setup.");

        public static LocalisableString IntroNotesTitle => new TranslatableString(getKey(@"intro_notes_title"), @"Score notes");

        public static LocalisableString IntroNotesDescription => new TranslatableString(getKey(@"intro_notes_description"), @"Leave a note on your own scores, text or an image, so you remember how a play actually went.");

        // ── paso 2: atajos ──

        public static LocalisableString ShortcutsTitle => new TranslatableString(getKey(@"shortcuts_title"), @"Shortcuts");

        public static LocalisableString ShortcutsDescription => new TranslatableString(getKey(@"shortcuts_description"), @"Resize your gameplay cursor anytime without opening settings.");

        public static LocalisableString ShortcutsIncreaseCursorSize => new TranslatableString(getKey(@"shortcuts_increase_cursor_size"), @"Bigger cursor");

        public static LocalisableString ShortcutsDecreaseCursorSize => new TranslatableString(getKey(@"shortcuts_decrease_cursor_size"), @"Smaller cursor");

        public static LocalisableString ShortcutsAnywhere => new TranslatableString(getKey(@"shortcuts_anywhere"), @"This works anywhere, not just in gameplay: the main menu, song select, the results screen, mid-map. A preview pops up while you adjust so you can see the size you're landing on before you commit to it.");

        public static LocalisableString ShortcutsWheelHint => new TranslatableString(getKey(@"shortcuts_wheel_hint"), @"Prefer the mouse? Ctrl + Shift + scroll wheel does exactly the same thing.");

        public static LocalisableString ShortcutsMenuCursorHint => new TranslatableString(getKey(@"shortcuts_menu_cursor_hint"), @"The size below is the one the shortcut moves. It also drives the menu cursor when you pick a menu style that scales with it.");

        // ── paso 3: themes ──

        public static LocalisableString ThemesTitle => new TranslatableString(getKey(@"themes_title"), @"Themes");

        public static LocalisableString ThemesDescription => new TranslatableString(getKey(@"themes_description"), @"Torii ships its own interface themes. ""Torii"" is the default: translucent glass panels you can see the game through, with soft rounded corners. ""Torii Legacy"" is the classic look if you'd rather keep that. There's also a desaturated monochrome theme and the Midnight family in three hues.

You can change this whenever you want from Settings, this is just the fastest place to try one.");

        public static LocalisableString ThemesRestartNote => new TranslatableString(getKey(@"themes_restart_note"), @"Picking a different theme closes the game, because the palette is baked in when the interface is built. Open it again and you'll come straight back to this guide.");

        public static LocalisableString ThemesHueDescription => new TranslatableString(getKey(@"themes_hue_description"), @"Want your own colour instead? Tint the interface to whatever hue you like.");

        // ── paso 4: opciones ──

        public static LocalisableString OptionsTitle => new TranslatableString(getKey(@"options_title"), @"Options");

        public static LocalisableString OptionsDescription => new TranslatableString(getKey(@"options_description"), @"A handful of Torii-only options worth deciding on now. Nothing here is permanent, they all live in Settings > Torii afterwards.");

        public static LocalisableString OptionsConfirmDangerousButtons => new TranslatableString(getKey(@"options_confirm_dangerous_buttons"), @"Confirm Retry/Quit on long attempts");

        public static LocalisableString OptionsConfirmDangerousButtonsHint => new TranslatableString(getKey(@"options_confirm_dangerous_buttons_hint"), @"After about a minute of playing, Retry and Quit on the pause and fail screens ask for a second click. Saves the run you were about to throw away by misclicking.");

        public static LocalisableString OptionsAutoHideToolbar => new TranslatableString(getKey(@"options_auto_hide_toolbar"), @"Auto-hide the toolbar");

        public static LocalisableString OptionsAutoHideToolbarHint => new TranslatableString(getKey(@"options_auto_hide_toolbar_hint"), @"Keeps the top bar hidden and only reveals it when you push the cursor to the very top of the screen, then it tucks itself away again.");

        public static LocalisableString OptionsServerPulse => new TranslatableString(getKey(@"options_server_pulse"), @"Show server pulse on toolbar");

        public static LocalisableString OptionsServerPulseHint => new TranslatableString(getKey(@"options_server_pulse_hint"), @"Shows how many people are currently playing on Torii in the top toolbar.");

        public static LocalisableString OptionsPerformanceCounter => new TranslatableString(getKey(@"options_performance_counter"), @"Show performance counter");

        public static LocalisableString OptionsPerformanceCounterHint => new TranslatableString(getKey(@"options_performance_counter_hint"), @"Shows a compact FPS and frame-time counter in the corner.");

        public static LocalisableString OptionsUserAuras => new TranslatableString(getKey(@"options_user_auras"), @"Show user auras");

        public static LocalisableString OptionsUserAurasHint => new TranslatableString(getKey(@"options_user_auras_hint"), @"Renders the particle effect behind usernames that have one, everywhere they appear. Turn it off if you'd rather save the GPU work.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
