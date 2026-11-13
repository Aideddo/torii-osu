// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Net;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Mapperatorinator;
using osu.Game.Online.API;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay;

namespace osu.Game.Tests.Configuration
{
    [TestFixture]
    public class ToriiEnglishUITest
    {
        [TestCase(0, "osu", "torii")]
        [TestCase(1, "taiko", "torii drums")]
        [TestCase(2, "fruits", "torii fruits")]
        [TestCase(3, "mania", "torii keys")]
        public void TestModeLabelsPreserveInternalIdentity(int mode, string shortName, string displayName)
        {
            Ruleset ruleset = mode switch
            {
                0 => new OsuRuleset(),
                1 => new TaikoRuleset(),
                2 => new CatchRuleset(),
                _ => new ManiaRuleset(),
            };

            Assert.That(ruleset.Description, Is.EqualTo(displayName));
            Assert.That(ruleset.RulesetInfo.Name, Is.EqualTo(displayName));
            Assert.That(ruleset.ShortName, Is.EqualTo(shortName));
            Assert.That(ruleset.RulesetInfo.OnlineID, Is.EqualTo(mode));
            Assert.That(((ILegacyRuleset)ruleset).LegacyID, Is.EqualTo(mode));

            var existingInfo = new RulesetInfo(shortName, "old display name", ruleset.RulesetInfo.InstantiationInfo, mode) { Available = true };
            Assert.That(existingInfo.Equals(ruleset.RulesetInfo), Is.True);
            Assert.That(existingInfo.CreateInstance().Description, Is.EqualTo(displayName));

            var generatorMode = (MapperatorinatorGamemode)mode;
            var label = typeof(MapperatorinatorGamemode).GetField(generatorMode.ToString())!.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()!;
            Assert.That(label.Description, Is.EqualTo(displayName));
        }

        [Test]
        public void TestCreditsIncludeOriginalContributors()
        {
            using var dialog = new SettingsFooter.CreditsDialog();
            Assert.That(dialog.HeaderText.ToString(), Is.EqualTo("With thanks"));
            string credits = dialog.BodyText.ToString();
            Assert.That(credits, Does.Contain("GooGuTeam"));
            Assert.That(credits, Does.Contain("shigetiro"));
            Assert.That(credits, Does.Contain("telecomadm1145"));
            Assert.That(credits, Does.Contain("ppy"));
        }

        [Test]
        public void TestStableRestartDialogIsEnglish()
        {
            using var dialog = new StableUIRestartDialog(null);
            Assert.That(dialog.HeaderText.ToString(), Is.EqualTo("Restart to apply changes"));
            Assert.That(dialog.BodyText.ToString(), Is.EqualTo("Your Stable UI settings have been saved. Restart the game to apply the new interface."));
        }

        [TestCase(HttpStatusCode.Conflict, "You have already chosen your star rating for this season.")]
        [TestCase(HttpStatusCode.UnprocessableEntity, "Choose at least 4.50 stars based on your top plays.")]
        [TestCase(HttpStatusCode.Unauthorized, "Please sign in again to save your star rating.")]
        [TestCase(HttpStatusCode.InternalServerError, "Could not save your star rating. Please try again.")]
        public void TestRankedPickDoesNotDisplayRawServerText(HttpStatusCode status, string expected)
        {
            var error = new APIException("Ya elegiste tu star rating comodo esta season.", null, status);
            Assert.That(ComfortPickPanel.GetSaveErrorMessage(error, 4.5), Is.EqualTo(expected));
        }

        [Test]
        public void TestRankedPickUnknownErrorUsesEnglishFallback()
            => Assert.That(ComfortPickPanel.GetSaveErrorMessage(new InvalidOperationException("No se pudo guardar"), 4.5),
                Is.EqualTo("Could not save your star rating. Please try again."));
    }
}
