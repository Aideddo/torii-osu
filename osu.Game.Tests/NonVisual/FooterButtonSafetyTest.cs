// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class FooterButtonSafetyTest
    {
        [TestCase(false, Visibility.Visible, 0)]
        [TestCase(true, Visibility.Hidden, 0)]
        [TestCase(true, Visibility.Visible, 1)]
        public void TestForwardedActionHonoursButtonState(bool enabled, Visibility visibility, int expectedCalls)
        {
            int calls = 0;
            var button = new ScreenFooterButton { Action = () => calls++ };
            button.Enabled.Value = enabled;
            button.State.Value = visibility;
            ((ClickableContainer)button).Action!.Invoke();
            Assert.That(calls, Is.EqualTo(expectedCalls));
        }

        [Test]
        public void TestOptionsWithoutSelectedBeatmapDoNotCreatePopover()
        {
            var button = new FooterButtonOptions();
            Assert.That(button.GetPopover(), Is.Null);
        }
    }
}
