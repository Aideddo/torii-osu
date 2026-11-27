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
        public void TestFooterProfileHasBoundedHitArea()
        {
            using var card = new osu.Game.Skinning.Select.LegacyFooterUser();
            Assert.That(card.AutoSizeAxes, Is.EqualTo(osu.Framework.Graphics.Axes.None));
            Assert.That(card.Size, Is.EqualTo(new osuTK.Vector2(340, 86)));
            Assert.That(card.Masking, Is.True);
        }

        [Test]
        public void TestSongSelectDecorationDoesNotReceiveInput()
        {
            using var decoration = new osu.Game.Screens.Select.LegacyTopDecoration();
            Assert.That(decoration.PropagatePositionalInputSubTree, Is.False);
            Assert.That(decoration.PropagateNonPositionalInputSubTree, Is.False);
        }

        [TestCase(false, true, false)]
        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        [TestCase(false, false, false)]
        public void TestClassicEntranceOnlyAnimatesNewLocalPlay(bool localPlay, bool autoShow, bool expected)
        {
            using var screen = new osu.Game.Screens.Ranking.SoloResultsScreen(new osu.Game.Scoring.ScoreInfo()) { IsLocalPlay = localPlay };
            Assert.That(osu.Game.Screens.Ranking.LegacyRankingOverlay.ShouldAnimateEntrance(screen, autoShow), Is.EqualTo(expected));
        }

        [TestCase("Skins.Legacy.SLATE-CREDITS.txt")]
        [TestCase("Skins.Legacy.SLATE-ASSETS.json")]
        [TestCase("Skins.Legacy.pause-continue@2x.png")]
        [TestCase("Skins.Legacy.pause-retry@2x.png")]
        [TestCase("Skins.Legacy.pause-back@2x.png")]
        [TestCase("Skins.Legacy.ranking-panel@2x.png")]
        [TestCase("Skins.Legacy.selection-mods@2x.png")]
        [TestCase("Skins.Legacy.selection-options@2x.png")]
        [TestCase("Skins.Legacy.selection-random@2x.png")]
        [TestCase("Skins.Legacy.menu-back@2x.png")]
        [TestCase("Fonts.MapleMono.MapleMono-Regular.bin")]
        [TestCase("Fonts.MapleMono.MapleMono-Regular_0.png")]
        [TestCase("Fonts.MapleMono.LICENSE.txt")]
        [TestCase("Skins.Legacy.menuhit.mp3")]
        [TestCase("Skins.Legacy.back-button-layer@2x.png")]
        [TestCase("Skins.Legacy.menu-button-background.png")]
        [TestCase("Skins.Legacy.menu-button-background@2x.png")]
        [TestCase("Skins.Legacy.PROJECT-MINIMALIST.txt")]
        public void TestExpandedClassicAssetsAreEmbedded(string suffix)
        {
            var assembly = osu.Game.Resources.OsuResources.ResourceAssembly;
            string resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith(suffix, System.StringComparison.Ordinal));
            using var stream = assembly.GetManifestResourceStream(resource);
            Assert.That(stream, Is.Not.Null);
            Assert.That(stream!.Length, Is.GreaterThan(0));
        }

        [Test]
        public void TestExpandedComponentsOnlyApplyAfterRestartWithClassicTheme()
        {
            using var storage = new TemporaryNativeStorage("classic-expanded");
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(ToriiStableUI.MainMenu(config), Is.False);
                Assert.That(ToriiStableUI.Settings(config), Is.False);
                Assert.That(ToriiStableUI.GameplayScreens(config), Is.False);
                Assert.That(ToriiStableUI.GameplayLeaderboard(config), Is.False);
                config.SetValue(OsuSetting.UITheme, UIThemeOption.StableUI);
                config.Save();
                Assert.That(ToriiStableUI.MainMenu(config), Is.False);
            }

            using var restarted = new OsuConfigManager(storage);
            Assert.That(ToriiStableUI.MainMenu(restarted), Is.False);
            Assert.That(ToriiStableUI.Settings(restarted), Is.True);
            Assert.That(ToriiStableUI.FpsCounter(restarted), Is.True);
            Assert.That(ToriiStableUI.GameplayScreens(restarted), Is.True);
            Assert.That(ToriiStableUI.GameplayLeaderboard(restarted), Is.True);
        }

        [Test]
        public void TestWithdrawnReleaseNoticeDoesNotHideClassicAnnouncement()
        {
            using var storage = new TemporaryNativeStorage("classic-announcement");
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.ToriiStableV2PromoShown, true);
            Assert.That(config.Get<bool>(OsuSetting.ToriiClassicThemePromoShown), Is.False);
        }

        [Test]
        public void TestClassicMainMenuStaysModernEvenWithSavedPreference()
        {
            using var storage = new TemporaryNativeStorage("classic-modern-home");
            using (var config = new OsuConfigManager(storage))
            {
                config.SetValue(OsuSetting.UITheme, UIThemeOption.StableUI);
                config.SetValue(OsuSetting.ToriiStableUIMainMenu, true);
                config.Save();
            }

            using var restarted = new OsuConfigManager(storage);
            Assert.That(ToriiStableUI.MainMenu(restarted), Is.False);
            Assert.That(ToriiStableUI.GameplayLeaderboard(restarted), Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        [NonParallelizable]
        public void TestDefaultLeaderboardMatchesClassicOption(bool enabled)
        {
            bool previous = osu.Game.Screens.Play.HUD.LegacyGameplayLeaderboard.UseInLegacySkins;
            try
            {
                osu.Game.Screens.Play.HUD.LegacyGameplayLeaderboard.UseInLegacySkins = enabled;
                using var leaderboard = osu.Game.Screens.Play.HUD.LegacyGameplayLeaderboard.CreateDefault();
                Assert.That(leaderboard.GetType(), Is.EqualTo(enabled
                    ? typeof(osu.Game.Screens.Play.HUD.LegacyGameplayLeaderboard)
                    : typeof(osu.Game.Screens.Play.HUD.DrawableGameplayLeaderboard)));
            }
            finally
            {
                osu.Game.Screens.Play.HUD.LegacyGameplayLeaderboard.UseInLegacySkins = previous;
            }
        }

        [TestCase("hit300", "300")]
        [TestCase("hit100", "100")]
        [TestCase("hit50", "50")]
        [TestCase("hit0", "Miss")]
        [TestCase("mania-hit300g", "MAX")]
        public void TestBundledResultsHaveVisibleJudgementLabels(string asset, string label)
            => Assert.That(osu.Game.Screens.Ranking.LegacyRankingPanel.HitLabel(asset), Is.EqualTo(label));

        [Test]
        public void TestAllerCompatibilityUsesOpenFontWithoutBundlingAller()
        {
            Assert.That(osu.Game.Graphics.OsuFont.GetFamilyString(osu.Game.Graphics.Typeface.Aller), Is.EqualTo("Inter"));
            Assert.That(osu.Game.Resources.OsuResources.ResourceAssembly.GetManifestResourceNames()
                .Any(name => name.Contains("Fonts.Aller.")), Is.False);
        }

        [Test]
        public void TestMissingResultsButtonTextureStillHasClickableContent()
        {
            using var button = new osu.Game.Screens.Ranking.LegacyRankingPanel.LegacyPanelButton(null, null, "Retry");
            Assert.That(button.AutoSizeAxes, Is.EqualTo(osu.Framework.Graphics.Axes.None));
            Assert.That(button.Size, Is.EqualTo(new osuTK.Vector2(230, 64)));
            Assert.That(button.Children.OfType<osu.Game.Graphics.Sprites.OsuSpriteText>().Single().Text.ToString(), Is.EqualTo("Retry"));
        }

        [Test]
        public void TestSavedHudGetsExactlyOneClassicLeaderboard()
        {
            using var layout = new osu.Framework.Graphics.Containers.Container();
            layout.Add(new osu.Game.Screens.Play.HUD.DrawableGameplayLeaderboard());
            osu.Game.Skinning.SkinnableContainer.ApplyClassicLeaderboard(layout);
            osu.Game.Skinning.SkinnableContainer.ApplyClassicLeaderboard(layout);
            Assert.That(layout.Children.OfType<osu.Game.Screens.Play.HUD.DrawableGameplayLeaderboard>(), Is.Empty);
            var classic = layout.Children.OfType<osu.Game.Screens.Play.HUD.LegacyGameplayLeaderboard>().Single();
            Assert.That(classic.Position, Is.EqualTo(new osuTK.Vector2(5.704895f, 45.772842f)));
            Assert.That(classic.UsesFixedAnchor, Is.False);
            Assert.That(classic.Size, Is.EqualTo(new osuTK.Vector2(144, 345.6f)));
        }

        [Test]
        public void TestRulesetHudDoesNotDuplicateGlobalClassicLeaderboard()
        {
            using var layout = new osu.Framework.Graphics.Containers.Container();
            layout.Add(new osu.Game.Screens.Play.HUD.DrawableGameplayLeaderboard());
            layout.Add(new osu.Game.Screens.Play.HUD.LegacyGameplayLeaderboard());
            osu.Game.Skinning.SkinnableContainer.ApplyClassicLeaderboard(layout, false);
            Assert.That(layout.Children, Is.Empty);
        }

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
            Assert.That(description!.Description, Is.EqualTo("Torii Classic"));
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
                Assert.That(ToriiStableUI.SongSelect(config), Is.False);
                Assert.That(config.Get<bool>(OsuSetting.ToriiStableUISongSelect), Is.True);
                Assert.That(config.Get<bool>(OsuSetting.ToriiStableUIResults), Is.False);
                Assert.That(config.Get<bool>(OsuSetting.ToriiStableUIFonts), Is.True);
                config.SetValue(OsuSetting.ToriiStableUISongSelect, false);
                config.Save();
            }

            using var restarted = new OsuConfigManager(storage);
            Assert.That(restarted.Get<bool>(OsuSetting.ToriiStableUISongSelect), Is.False);
            Assert.That(ToriiStableUI.SongSelect(restarted), Is.False);
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
