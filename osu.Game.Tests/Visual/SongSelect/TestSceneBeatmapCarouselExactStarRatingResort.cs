// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Tests.Resources;
using osu.Game.Utils;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.SongSelect
{
    /// <summary>
    /// Covers re-sorting the carousel when exact mod-adjusted star ratings arrive after it was sorted using approximations.
    /// </summary>
    [TestFixture]
    public partial class TestSceneBeatmapCarouselExactStarRatingResort : BeatmapCarouselTestScene
    {
        private readonly TestBeatmapDifficultyCache difficultyCache = new TestBeatmapDifficultyCache();

        private readonly HashSet<BeatmapInfo> addedBeatmaps = new HashSet<BeatmapInfo>();
        private readonly HashSet<IBeatmapInfo> reportedBeatmaps = new HashSet<IBeatmapInfo>();

        private int filterCountBeforeArrivals;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<BeatmapDifficultyCache>(difficultyCache);
            return dependencies;
        }

        public TestSceneBeatmapCarouselExactStarRatingResort()
        {
            difficultyCache.TrackedDifficultyUpdated += (beatmap, _) => reportedBeatmaps.Add(beatmap);
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("select double time", () => SelectedMods.Value = new Mod[] { new OsuModDoubleTime() });
            AddStep("exact star rating is 3 above base", () => difficultyCache.ComputeDifficulty = lookup => new StarDifficulty(lookup.BeatmapInfo.StarRating + 3, 0));
            AddStep("reset tracked beatmaps", () =>
            {
                addedBeatmaps.Clear();
                reportedBeatmaps.Clear();
            });

            RemoveAllBeatmaps();
            createCarousel();
        }

        [Test]
        public void TestExactStarRatingsResortCarouselOnce()
        {
            // Double time approximations put 4.0 (≈ 5.21) before 5.0 (≈ 6.85), but the exact star ratings are the other way around.
            AddStep("set exact star ratings", () => difficultyCache.ComputeDifficulty = lookup => new StarDifficulty(lookup.BeatmapInfo.StarRating == 4.0 ? 9.0 : 6.0, 0));

            addBeatmaps(4.0, 5.0);
            sortByDifficultyWithDoubleTime();
            checkOrder(4.0, 5.0);

            AddUntilStep("re-sorted by exact star ratings", () => Carousel.PostFilterBeatmaps.Select(b => b.StarRating), () => Is.EqualTo(new[] { 5.0, 4.0 }));
            AddAssert("re-sorted once", () => Carousel.FilterCount, () => Is.EqualTo(filterCountBeforeArrivals + 1));

            waitForResortDebounce();
            AddAssert("not re-sorted again", () => Carousel.FilterCount, () => Is.EqualTo(filterCountBeforeArrivals + 1));
        }

        [Test]
        public void TestNoResortWhenDisplayedStarRatingMatchesSortedStarRating()
        {
            // Differs from the approximation used for sorting, but not at the precision star ratings are displayed at.
            AddStep("set exact star ratings", () => difficultyCache.ComputeDifficulty = lookup =>
                new StarDifficulty(StarRatingApproximator.Approximate(lookup.BeatmapInfo, lookup.Ruleset, lookup.OrderedMods).FloorToDecimalDigits(2) + 0.001, 0));

            addBeatmaps(4.0, 5.0);
            sortByDifficultyWithDoubleTime();

            waitForTrackedDifficultyUpdates();
            waitForResortDebounce();
            AddAssert("not re-sorted", () => Carousel.FilterCount, () => Is.EqualTo(filterCountBeforeArrivals));
        }

        [Test]
        public void TestNoResortWithoutDifficultyMods()
        {
            // Without difficulty mods, sorting always uses the stored star ratings, so re-sorting wouldn't change anything.
            AddStep("select no mods", () => SelectedMods.Value = Array.Empty<Mod>());

            addBeatmaps(4.0, 5.0);
            ApplyToFilterAndWaitForFilter("sort by difficulty", c =>
            {
                c.Sort = SortMode.Difficulty;
                c.Mods = Array.Empty<Mod>();
            });
            AddStep("store filter count", () => filterCountBeforeArrivals = Carousel.FilterCount);

            waitForTrackedDifficultyUpdates();
            waitForResortDebounce();
            AddAssert("not re-sorted", () => Carousel.FilterCount, () => Is.EqualTo(filterCountBeforeArrivals));
        }

        [Test]
        public void TestResortDeferredWhileScrolling()
        {
            ScheduledDelegate? scrolling = null;

            addBeatmaps(Enumerable.Range(0, 30).Select(i => 1 + i * 0.2).ToArray());
            sortByDifficultyWithDoubleTime();

            AddStep("start scrolling", () => scrolling = Scheduler.AddDelayed(() => Scroll.ScrollTo(Scroll.Current > 10 ? 0 : 20, false), 50, true));

            waitForTrackedDifficultyUpdates();
            waitForResortDebounce();
            AddAssert("not re-sorted while scrolling", () => Carousel.FilterCount, () => Is.EqualTo(filterCountBeforeArrivals));

            AddStep("stop scrolling", () => scrolling?.Cancel());
            AddUntilStep("re-sorted after scrolling stops", () => Carousel.FilterCount, () => Is.EqualTo(filterCountBeforeArrivals + 1));
        }

        [Test]
        public void TestResortDeferredWhileDragging()
        {
            addBeatmaps(Enumerable.Range(0, 30).Select(i => 1 + i * 0.2).ToArray());
            sortByDifficultyWithDoubleTime();

            AddStep("start dragging", () =>
            {
                InputManager.MoveMouseTo(Carousel);
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(Carousel, new Vector2(0, -50));
            });

            // The scroll position stops changing while the drag is held still, so only the drag itself defers the re-sort.
            waitForTrackedDifficultyUpdates();
            waitForResortDebounce();
            AddAssert("not re-sorted while dragging", () => Carousel.FilterCount, () => Is.EqualTo(filterCountBeforeArrivals));

            AddStep("stop dragging", () => InputManager.ReleaseButton(MouseButton.Left));
            AddUntilStep("re-sorted after dragging stops", () => Carousel.FilterCount, () => Is.EqualTo(filterCountBeforeArrivals + 1));
        }

        [Test]
        public void TestEachBeatmapResortsAtMostOnce()
        {
            // Exact star ratings are computed for the selected mods, which differ from the ones the carousel sorts with,
            // so sorting can never use them and they keep differing from the sorted star ratings.
            addBeatmaps(4.0, 5.0);
            ApplyToFilterAndWaitForFilter("sort by difficulty with slower double time", c =>
            {
                c.Sort = SortMode.Difficulty;
                c.Mods = new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = 1.4 } } };
            });
            AddStep("store filter count", () => filterCountBeforeArrivals = Carousel.FilterCount);

            AddUntilStep("re-sorted", () => Carousel.FilterCount, () => Is.EqualTo(filterCountBeforeArrivals + 1));
            WaitForFiltering();

            // A new instance of the same mods makes the difficulty cache recompute and report the same star ratings again,
            // like panels requesting them again after being scrolled out of view and back.
            AddStep("report star ratings again", () =>
            {
                reportedBeatmaps.Clear();
                SelectedMods.Value = new Mod[] { new OsuModDoubleTime() };
            });

            waitForTrackedDifficultyUpdates();
            waitForResortDebounce();
            AddAssert("not re-sorted again", () => Carousel.FilterCount, () => Is.EqualTo(filterCountBeforeArrivals + 1));
        }

        [Test]
        public void TestDisposedCarouselUnsubscribes()
        {
            BeatmapCarousel previousCarousel = null!;

            AddStep("store carousel", () => previousCarousel = Carousel);
            AddAssert("carousel subscribed", () => trackedDifficultyUpdatedTargets().Contains(previousCarousel));

            // Replacing the carousel disposes the previous one asynchronously.
            createCarousel();

            AddUntilStep("previous carousel unsubscribed", () => trackedDifficultyUpdatedTargets().Contains(previousCarousel), () => Is.False);
            AddAssert("new carousel subscribed", () => trackedDifficultyUpdatedTargets().Contains(Carousel));
        }

        private void createCarousel()
        {
            // CreateCarousel() replaces the test scene's children, which would otherwise dispose the difficulty cache.
            // The cache must stay loaded for its scheduler to run, so it's kept alongside the carousel.
            AddStep("detach difficulty cache", () =>
            {
                if (difficultyCache.Parent != null)
                    Remove(difficultyCache, false);
            });

            CreateCarousel();

            AddStep("attach difficulty cache", () => Add(difficultyCache));
        }

        private void addBeatmaps(params double[] starRatings) => AddStep($"add {starRatings.Length} beatmaps", () =>
        {
            BeatmapSets.AddRange(starRatings.Select(starRating =>
            {
                var set = TestResources.CreateTestBeatmapSetInfo(1);
                set.Beatmaps[0].StarRating = starRating;
                addedBeatmaps.Add(set.Beatmaps[0]);
                return set;
            }));
        });

        private void sortByDifficultyWithDoubleTime()
        {
            ApplyToFilterAndWaitForFilter("sort by difficulty with double time", c =>
            {
                c.Sort = SortMode.Difficulty;
                c.Mods = new Mod[] { new OsuModDoubleTime() };
            });

            AddStep("store filter count", () => filterCountBeforeArrivals = Carousel.FilterCount);
        }

        private void checkOrder(params double[] baseStarRatings) =>
            AddAssert("sorted by approximations", () => Carousel.PostFilterBeatmaps.Select(b => b.StarRating), () => Is.EqualTo(baseStarRatings));

        /// <summary>
        /// Waits for exact star ratings of this test's beatmaps. Bindables from previous tests' carousels can also report when mods change, so those are ignored.
        /// </summary>
        private void waitForTrackedDifficultyUpdates() => AddUntilStep("exact star ratings arrived", () => reportedBeatmaps.Any(b => b is BeatmapInfo beatmap && addedBeatmaps.Contains(beatmap)));

        /// <summary>
        /// Waits long enough for any pending re-sort to have been applied.
        /// Filtering is also waited for, as its debounce runs in real time while headless tests run faster than that.
        /// </summary>
        private void waitForResortDebounce()
        {
            double startTime = 0;

            AddStep("store time", () => startTime = Time.Current);
            AddUntilStep("wait well past re-sort debounce", () => Time.Current - startTime > BeatmapCarousel.STAR_RATING_RESORT_DEBOUNCE * 3 && !Carousel.IsFiltering);
        }

        private object?[] trackedDifficultyUpdatedTargets()
        {
            // An event's subscribers can't be read from outside its class, so read them from the compiler-generated backing field.
            var field = typeof(BeatmapDifficultyCache).GetField(nameof(BeatmapDifficultyCache.TrackedDifficultyUpdated), BindingFlags.Instance | BindingFlags.NonPublic)!;
            var handlers = (Delegate?)field.GetValue(difficultyCache);

            return handlers?.GetInvocationList().Select(d => d.Target).ToArray() ?? Array.Empty<object?>();
        }

        private partial class TestBeatmapDifficultyCache : BeatmapDifficultyCache
        {
            public Func<DifficultyCacheLookup, StarDifficulty>? ComputeDifficulty { get; set; }

            protected override Task<StarDifficulty?> ComputeValueAsync(DifficultyCacheLookup lookup, CancellationToken token = default)
                => Task.FromResult<StarDifficulty?>(ComputeDifficulty?.Invoke(lookup) ?? new StarDifficulty(lookup.BeatmapInfo.StarRating, 0));
        }
    }
}
