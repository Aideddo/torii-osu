// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.CompilerServices;

namespace osu.Game.Configuration
{
    public static class ToriiStableUI
    {
        private static readonly ConditionalWeakTable<OsuConfigManager, Snapshot> snapshots = new ConditionalWeakTable<OsuConfigManager, Snapshot>();

        internal static void Capture(OsuConfigManager config) => snapshots.Add(config, new Snapshot(config));

        public static bool SongSelect(OsuConfigManager config) => snapshots.GetValue(config, c => new Snapshot(c)).SongSelect;
        public static bool Results(OsuConfigManager config) => snapshots.GetValue(config, c => new Snapshot(c)).Results;
        public static bool Mods(OsuConfigManager config) => snapshots.GetValue(config, c => new Snapshot(c)).Mods;
        public static bool Footer(OsuConfigManager config) => snapshots.GetValue(config, c => new Snapshot(c)).Footer;
        public static bool Fonts(OsuConfigManager config) => snapshots.GetValue(config, c => new Snapshot(c)).Fonts;
        public static bool MainMenu(OsuConfigManager config) => snapshots.GetValue(config, c => new Snapshot(c)).MainMenu;
        public static bool Settings(OsuConfigManager config) => snapshots.GetValue(config, c => new Snapshot(c)).Settings;
        public static bool FpsCounter(OsuConfigManager config) => snapshots.GetValue(config, c => new Snapshot(c)).FpsCounter;
        public static bool GameplayScreens(OsuConfigManager config) => snapshots.GetValue(config, c => new Snapshot(c)).GameplayScreens;
        public static bool GameplayLeaderboard(OsuConfigManager config) => snapshots.GetValue(config, c => new Snapshot(c)).GameplayLeaderboard;

        // guardar para el proximo arranque no cambia la jerarquia que ya esta cargada.
        private sealed class Snapshot
        {
            public readonly bool SongSelect;
            public readonly bool Results;
            public readonly bool Mods;
            public readonly bool Footer;
            public readonly bool Fonts;
            public readonly bool MainMenu;
            public readonly bool Settings;
            public readonly bool FpsCounter;
            public readonly bool GameplayScreens;
            public readonly bool GameplayLeaderboard;

            public Snapshot(OsuConfigManager config)
            {
                bool stable = config.Get<UIThemeOption>(OsuSetting.UITheme) == UIThemeOption.StableUI;
                SongSelect = stable && config.Get<bool>(OsuSetting.ToriiStableUISongSelect);
                Results = stable && config.Get<bool>(OsuSetting.ToriiStableUIResults);
                Mods = stable && config.Get<bool>(OsuSetting.ToriiStableUIMods);
                Footer = stable && config.Get<bool>(OsuSetting.ToriiStableUIFooter);
                Fonts = stable && config.Get<bool>(OsuSetting.ToriiStableUIFonts);
                // hasta tener un set completo de assets, la home sigue siendo la moderna.
                MainMenu = false;
                Settings = stable && config.Get<bool>(OsuSetting.ToriiStableUISettings);
                FpsCounter = stable && config.Get<bool>(OsuSetting.ToriiStableUIFpsCounter);
                GameplayScreens = stable && config.Get<bool>(OsuSetting.ToriiStableUIGameplayScreens);
                GameplayLeaderboard = stable && config.Get<bool>(OsuSetting.ToriiStableUIGameplayLeaderboard);
            }
        }
    }
}
