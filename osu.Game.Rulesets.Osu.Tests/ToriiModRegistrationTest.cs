// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Tests
{
    public class ToriiModRegistrationTest
    {
        [Test]
        public void TestToriiModsAreRegistered()
        {
            var ruleset = new OsuRuleset();
            var funMods = ruleset.GetModsFor(ModType.Fun).ToArray();
            var conversionMods = ruleset.GetModsFor(ModType.Conversion).ToArray();

            Assert.That(funMods.Any(mod => mod is OsuModHitSounder), Is.True);
            Assert.That(funMods.Any(mod => mod is ModPitchAdjust), Is.True);
            Assert.That(conversionMods.Any(mod => mod is OsuModAmplify), Is.True);
        }
    }
}
