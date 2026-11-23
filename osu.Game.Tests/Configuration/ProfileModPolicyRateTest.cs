// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.Configuration
{
    [TestFixture]
    public class ProfileModPolicyRateTest
    {
        [TestCase("DT", 1.2)]
        [TestCase("DT", 2.0)]
        [TestCase("HT", 0.5)]
        [TestCase("HT", 0.9)]
        public void TestRequiredRateSettingsRemainEditable(string acronym, double speed)
        {
            var ruleset = new OsuRuleset();
            var policy = new ProfileModPolicy { Required = new[] { new APIMod { Acronym = acronym } } };
            var selected = policy.CreateRequired(ruleset).Single();
            ((ModRateAdjust)selected).SpeedChange.Value = speed;
            if (selected is ModDoubleTime dt)
                dt.AdjustPitch.Value = true;
            else if (selected is ModHalfTime ht)
                ht.AdjustPitch.Value = true;

            var corrected = policy.Enforce(new[] { selected }, ruleset).Single();
            Assert.That(((ModRateAdjust)corrected).SpeedChange.Value, Is.EqualTo(speed));
            Assert.That(corrected is ModDoubleTime resultDt ? resultDt.AdjustPitch.Value : ((ModHalfTime)corrected).AdjustPitch.Value, Is.True);
            Assert.That(ProfileModPolicy.AllowsRateSettings(corrected), Is.True);
            Assert.That(policy.IsRequired(corrected), Is.True);
            Assert.That(policy.Enforce(Array.Empty<Mod>(), ruleset).Single().Acronym, Is.EqualTo(acronym));
            Assert.That(policy.AllowsOptional(new OsuModHidden(), ruleset), Is.False);
        }

        [Test]
        public void TestDifficultyAdjustStaysLocked()
        {
            var ruleset = new OsuRuleset();
            var policy = new ProfileModPolicy
            {
                Required = new[]
                {
                    new APIMod { Acronym = "DT" },
                    new APIMod { Acronym = "DA", Settings = new Dictionary<string, object> { ["circle_size"] = 9d } },
                },
            };
            var mods = policy.CreateRequired(ruleset);
            ((ModRateAdjust)mods[0]).SpeedChange.Value = 1.2;
            ((OsuModDifficultyAdjust)mods[1]).CircleSize.Value = 8;
            var corrected = policy.Enforce(mods, ruleset);
            Assert.That(((ModRateAdjust)corrected[0]).SpeedChange.Value, Is.EqualTo(1.2));
            Assert.That(((OsuModDifficultyAdjust)corrected[1]).CircleSize.Value, Is.EqualTo(9));
            Assert.That(ProfileModPolicy.AllowsRateSettings(corrected[1]), Is.False);
        }
    }
}
