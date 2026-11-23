// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;

namespace osu.Game.Tests.Configuration
{
    [TestFixture]
    public class ToriiStableUITestSafety
    {
        [TestCase(UIThemeOption.LiquidGlass)]
        [TestCase(UIThemeOption.Torii)]
        [TestCase(UIThemeOption.GrayscaleByFsyori)]
        [TestCase(UIThemeOption.Midnight)]
        [TestCase(UIThemeOption.MidnightCrimson)]
        [TestCase(UIThemeOption.MidnightCerulean)]
        public void TestOtherThemesIgnoreSavedClassicFlags(UIThemeOption theme)
        {
            using var storage = new TemporaryNativeStorage("classic-isolation");
            using (var writer = new StreamWriter(storage.GetStream(OsuConfigManager.TORII_CONFIG_FILENAME, FileAccess.Write, FileMode.Create)))
                writer.WriteLine($"UITheme = {theme}\nToriiLegacyFooterUseSkin = True\nToriiStableResults = True\nToriiStableModSelect = True\nToriiLegacySongSelectFooter = True\nToriiLegacyFont = True\nToriiLegacyOptions = True\nToriiLegacyFpsCounter = True\nToriiLegacyGameplayScreens = True\nToriiLegacyGameplayLeaderboard = True");
            using var config = new OsuConfigManager(storage);
            Assert.That(config.Get<UIThemeOption>(OsuSetting.UITheme), Is.EqualTo(theme));
            Assert.That(ToriiStableUI.SongSelect(config), Is.False);
            Assert.That(ToriiStableUI.Results(config), Is.False);
            Assert.That(ToriiStableUI.Mods(config), Is.False);
            Assert.That(ToriiStableUI.Footer(config), Is.False);
            Assert.That(ToriiStableUI.Fonts(config), Is.False);
            Assert.That(ToriiStableUI.Settings(config), Is.False);
            Assert.That(ToriiStableUI.FpsCounter(config), Is.False);
            Assert.That(ToriiStableUI.GameplayScreens(config), Is.False);
            Assert.That(ToriiStableUI.GameplayLeaderboard(config), Is.False);
        }

        [Test]
        public void TestGuestConfigurationSurvivesMigration()
        {
            using var storage = new TemporaryNativeStorage("guest-config-preserved");
            using (var writer = new StreamWriter(storage.GetStream("game.ini", FileAccess.Write, FileMode.Create)))
                writer.WriteLine("UITheme = Torii\nToriiAutoHideToolbar = True");
            using (var writer = new StreamWriter(storage.GetStream(OsuConfigManager.TORII_CONFIG_FILENAME, FileAccess.Write, FileMode.Create)))
                writer.WriteLine("UITheme = Midnight\nToriiAutoHideToolbar = False");
            using var config = new OsuConfigManager(storage);
            Assert.That(config.Get<UIThemeOption>(OsuSetting.UITheme), Is.EqualTo(UIThemeOption.Midnight));
            Assert.That(config.Get<bool>(OsuSetting.ToriiAutoHideToolbar), Is.False);
        }
    }
}
