// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Taiko.Tests
{
    public class ToriiModRegistrationTest
    {
        [Test]
        public void TestPitchAdjustIsRegistered()
        {
            var mods = new TaikoRuleset().GetModsFor(ModType.Fun).ToArray();

            Assert.That(mods.Any(mod => mod is ModPitchAdjust), Is.True);
        }

        [Test]
        public void TestToriiSettingsAreRegistered() =>
            Assert.That(new TaikoRuleset().CreateToriiSettingsSubsection(), Is.TypeOf<ToriiTaikoSettings>());
    }
}
