// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;

namespace osu.Game.Tests.Visual.Settings
{
    [HeadlessTest]
    public partial class TestScenePotatoModeSearch : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [TestCase(@"potato")]
        [TestCase(@"extreme")]
        [TestCase(@"mode")]
        [TestCase(@"performance")]
        [TestCase(@"extreme performance")]
        [TestCase(@"potato mode")]
        [TestCase(@"fps")]
        [TestCase(@"EXTREME")]
        public void TestSearch(string term)
        {
            PotatoModeToggleAndRestart toggle = null!;
            SearchContainer search = null!;
            SettingsItemV2 item = null!;
            AddStep(@"create search", () => Child = search = new SearchContainer
            {
                Width = 500,
                Height = 250,
                Child = toggle = new PotatoModeToggleAndRestart(),
            });
            AddUntilStep(@"toggle loaded", () => toggle.IsLoaded && toggle.ChildrenOfType<SettingsItemV2>().Any());
            AddStep(@"search for setting", () =>
            {
                item = toggle.ChildrenOfType<SettingsItemV2>().Single();
                search.SearchTerm = term;
            });
            AddUntilStep(@"setting matches", () => item.FilteringActive && item.MatchingFilter && item.IsPresent && toggle.Height > 0);
            AddStep(@"search unrelated term", () => search.SearchTerm = @"unrelated-setting-xyz");
            AddUntilStep(@"setting hidden without empty row", () => !item.MatchingFilter && toggle.Height == 0);
            AddStep(@"clear search", () => search.SearchTerm = string.Empty);
            AddUntilStep(@"setting restored", () => item.IsPresent && toggle.Height > 0);
        }
    }
}
