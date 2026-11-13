// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;
using osu.Game.Utils;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class StableModSelectionTest
    {
        [Test]
        public void TestNewModWinsWithoutDuplicateTypes()
        {
            var easy = new OsuModEasy();
            var hardRock = new OsuModHardRock();
            var result = UserModSelectOverlay.NormalizeSelection(new Mod[] { easy }, new Mod[] { easy, hardRock });
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result.Single(), Is.SameAs(hardRock));
            Assert.That(ModUtils.CheckCompatibleSet(result, out _), Is.True);
        }

        [Test]
        public void TestDuplicateCandidatesAreNormalized()
        {
            var mod = new OsuModDoubleTime();
            var result = UserModSelectOverlay.NormalizeSelection(Array.Empty<Mod>(), new Mod[] { mod, mod });
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result.Single(), Is.SameAs(mod));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPitchAdjustAndRatePitch(bool adjustPitch)
        {
            var pitch = new ModPitchAdjust();
            var hitsounds = new OsuModHitSounder();
            var rate = new OsuModDoubleTime();
            rate.AdjustPitch.Value = adjustPitch;
            var result = UserModSelectOverlay.NormalizeSelection(new Mod[] { pitch, hitsounds }, new Mod[] { pitch, hitsounds, rate });
            Assert.That(result.OfType<ModPitchAdjust>().Any(), Is.EqualTo(!adjustPitch));
            Assert.That(result, Does.Contain(hitsounds));
            Assert.That(result, Does.Contain(rate));
            Assert.That(result.Select(m => m.GetType()), Is.Unique);
            Assert.That(ModUtils.CheckCompatibleSet(result, out _), Is.True);
        }

        [Test]
        public void TestFullModSelectionRemainsAccessible()
        {
            bool opened = false;
            using var overlay = new LegacyModSelectOverlay { RequestFullModSelection = () => opened = true };
            overlay.ShowFullModSelection();
            Assert.That(opened, Is.True);
        }
    }
}
