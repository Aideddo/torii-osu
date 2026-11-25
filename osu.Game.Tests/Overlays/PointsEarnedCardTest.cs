// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Overlays.Cosmetics;

namespace osu.Game.Tests.Overlays
{
    [TestFixture]
    public class PointsEarnedCardTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestGameplayCompactsWithoutReopening(bool reducedMotion)
        {
            using var card = new PointsEarnedCard(new[] { new PointsEarnedCard.Line(25, "daily_play", "streak:3") }, 1953, reducedMotion);
            Assert.That(card.IsCompact, Is.False);
            card.SetGameplay(true);
            Assert.That(card.IsCompact, Is.True);
            card.SetGameplay(false);
            Assert.That(card.IsCompact, Is.True);
            card.SetGameplay(true);
            Assert.That(card.IsCompact, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestDismissIsTerminal(bool reducedMotion)
        {
            using var card = new PointsEarnedCard(new[] { new PointsEarnedCard.Line(25, "daily_play", "streak:3") }, 1953, reducedMotion);
            card.Dismiss();
            card.Dismiss();
            card.SetGameplay(true);
            Assert.That(card.IsExiting, Is.True);
            Assert.That(card.IsCompact, Is.False);
        }
    }
}
