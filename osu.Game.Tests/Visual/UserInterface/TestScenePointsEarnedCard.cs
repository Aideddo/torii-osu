// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Overlays.Cosmetics;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestScenePointsEarnedCard : OsuTestScene
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestDockAndDismiss(bool reducedMotion)
        {
            PointsEarnedCard card = null;
            AddStep("show rewards", () => Child = card = createCard(reducedMotion));
            AddUntilStep("card loaded", () => card.IsLoaded);
            AddAssert("expanded width", () => card.Width == 340);
            AddStep("enter gameplay", () => card.SetGameplay(true));
            AddUntilStep("compact width", () => card.Width == 196);
            AddStep("leave gameplay", () => card.SetGameplay(false));
            AddAssert("stays compact", () => card.IsCompact && card.Width == 196);
            AddStep("dismiss", () => card.Dismiss());
            AddUntilStep("dismissed", () => !card.IsAlive || card.Alpha == 0);
        }

        [Test]
        public void TestLateRewardInSmallGameplayViewport()
        {
            PointsEarnedCard card = null;
            AddStep("late reward", () =>
            {
                card = createCard(false);
                card.SetGameplay(true);
                Child = new Container { Width = 180, Height = 240, Child = card };
            });
            AddUntilStep("loaded compact", () => card.IsLoaded);
            AddAssert("fits viewport", () => card.X >= 0 && card.X + card.Width * card.Scale.X <= 180);
            AddUntilStep("automatically exits", () => card.IsExiting);
        }

        private static PointsEarnedCard createCard(bool reducedMotion) => new PointsEarnedCard(new[]
        {
            new PointsEarnedCard.Line(25, "daily_play", "streak:3"),
            new PointsEarnedCard.Line(80, "top_play", "rank:12"),
            new PointsEarnedCard.Line(15, "medal", string.Empty),
        }, 1953, reducedMotion);
    }
}
