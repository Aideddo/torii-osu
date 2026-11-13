// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;

namespace osu.Game.Tests.Configuration
{
    [TestFixture]
    public class ToriiStableUITest
    {
        [Test]
        public void TestClassicThemeIsSecondWithoutChangingEnumValues()
        {
            var themes = UIThemeDropdownAndRestart.ThemeOrder;
            Assert.That(themes.Take(2), Is.EqualTo(new[] { UIThemeOption.LiquidGlass, UIThemeOption.StableUI }));
            Assert.That(themes.Distinct().Count(), Is.EqualTo(System.Enum.GetValues<UIThemeOption>().Length));
            Assert.That((int)UIThemeOption.Torii, Is.EqualTo(1));
            Assert.That((int)UIThemeOption.StableUI, Is.EqualTo(6));
        }

        [Test]
        public void TestThemeDisplayNamePreservesSavedIdentifier()
        {
            var theme = UIThemeOption.StableUI;
            var description = typeof(UIThemeOption).GetField(theme.ToString())!.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>();
            Assert.That(description!.Description, Is.EqualTo("Torii Classic (Stable)"));
            Assert.That(theme.ToString(), Is.EqualTo("StableUI"));
        }

        [Test]
        public void TestSavedChangesOnlyApplyAfterRestart()
        {
            using var storage = new TemporaryNativeStorage("stable-ui-restart");
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(ToriiStableUI.SongSelect(config), Is.False);
                config.SetValue(OsuSetting.UITheme, UIThemeOption.StableUI);
                config.Save();
                Assert.That(ToriiStableUI.SongSelect(config), Is.False);
            }

            using (var restarted = new OsuConfigManager(storage))
            {
                Assert.That(ToriiStableUI.SongSelect(restarted), Is.True);
                Assert.That(ToriiStableUI.Mods(restarted), Is.True);
                restarted.SetValue(OsuSetting.ToriiStableUISongSelect, false);
                restarted.Save();
                Assert.That(ToriiStableUI.SongSelect(restarted), Is.True);
            }

            using var nextRestart = new OsuConfigManager(storage);
            Assert.That(ToriiStableUI.SongSelect(nextRestart), Is.False);
            Assert.That(ToriiStableUI.Footer(nextRestart), Is.True);
        }

        [Test]
        public void TestLegacyPreferencesMigrateOnceWithoutChangingTheme()
        {
            using var storage = new TemporaryNativeStorage("stable-ui-migration");
            using (var writer = new StreamWriter(storage.GetStream(OsuConfigManager.TORII_CONFIG_FILENAME, FileAccess.Write, FileMode.Create)))
                writer.WriteLine("UITheme = Midnight\nToriiLegacyFooterUseSkin = True\nToriiStableResults = False\nToriiLegacyFont = True");

            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<UIThemeOption>(OsuSetting.UITheme), Is.EqualTo(UIThemeOption.Midnight));
                Assert.That(ToriiStableUI.SongSelect(config), Is.True);
                Assert.That(config.Get<bool>(OsuSetting.ToriiStableUISongSelect), Is.True);
                Assert.That(config.Get<bool>(OsuSetting.ToriiStableUIResults), Is.False);
                Assert.That(config.Get<bool>(OsuSetting.ToriiStableUIFonts), Is.True);
                config.SetValue(OsuSetting.ToriiStableUISongSelect, false);
                config.Save();
            }

            using var restarted = new OsuConfigManager(storage);
            Assert.That(restarted.Get<bool>(OsuSetting.ToriiStableUISongSelect), Is.False);
            Assert.That(ToriiStableUI.SongSelect(restarted), Is.True);
        }

        [Test]
        public void TestComponentsAreIndependent()
        {
            using var storage = new TemporaryNativeStorage("stable-ui-components");
            using (var writer = new StreamWriter(storage.GetStream(OsuConfigManager.TORII_CONFIG_FILENAME, FileAccess.Write, FileMode.Create)))
                writer.WriteLine("UITheme = StableUI\nToriiStableUISongSelect = True\nToriiStableUIResults = False\nToriiStableUIFooter = False\nToriiStableUIMods = True\nToriiStableUIFonts = False");

            using var config = new OsuConfigManager(storage);
            Assert.That(ToriiStableUI.SongSelect(config), Is.True);
            Assert.That(ToriiStableUI.Results(config), Is.False);
            Assert.That(ToriiStableUI.Footer(config), Is.False);
            Assert.That(ToriiStableUI.Mods(config), Is.True);
            Assert.That(ToriiStableUI.Fonts(config), Is.False);
        }
    }
}
