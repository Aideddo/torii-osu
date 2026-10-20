// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Sections.Ranks;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.Visual.Online
{
    [HeadlessTest]
    public partial class TestSceneProfileScorePagination : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);

        private readonly Bindable<UserProfileData?> user = new Bindable<UserProfileData?>();
        private readonly List<APIRequest<List<SoloScoreInfo>>> requests = new List<APIRequest<List<SoloScoreInfo>>>();
        private TestScoreContainer container = null!;

        private void create(ScoreType type)
        {
            AddStep("create profile", () =>
            {
                user.Value = null;
                requests.Clear();
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is not GetUserScoresRequest scores)
                        return false;

                    requests.Add(scores);
                    return true;
                };
                Child = container = new TestScoreContainer(type, user);
            });
            AddUntilStep("loaded", () => container.IsLoaded);
            AddStep("select user", () => user.Value = profile(1));
            AddUntilStep("first request", () => requests.Count == 1);
        }

        [TestCase(ScoreType.Best)]
        [TestCase(ScoreType.Recent)]
        [TestCase(ScoreType.Firsts)]
        [TestCase(ScoreType.Pinned)]
        public void TestOverlappingPages(ScoreType type)
        {
            create(type);
            AddStep("first page plus lookahead", () => requests[0].TriggerSuccess(scores(1, 2, 3, 4, 5, 6)));
            AddUntilStep("five rows", () => container.RowIds.Length == 5);
            AddStep("show more", () => container.ChildrenOfType<ShowMoreButton>().Single().Action.Invoke());
            AddUntilStep("second request", () => requests.Count == 2);
            AddAssert("raw offset advances by five", () => container.Pages[1].Offset == 5);
            AddStep("overlapping page with repeated id", () => requests[1].TriggerSuccess(scores(5, 6, 6, 7)));
            AddUntilStep("one row per score", () => container.RowIds.SequenceEqual(new[] { "1", "2", "3", "4", "5", "6", "7" }));
            AddAssert("no more pages", () => container.ChildrenOfType<ShowMoreButton>().Single().Alpha == 0);
        }

        [Test]
        public void TestDifferentScoresOnSameBeatmapAreKept()
        {
            create(ScoreType.Recent);
            AddStep("different plays same map", () => requests[0].TriggerSuccess(scores(1, 2)));
            AddUntilStep("both plays remain", () => container.RowIds.SequenceEqual(new[] { "1", "2" }));
        }

        [Test]
        public void TestMissingIdsAreNotMerged()
        {
            create(ScoreType.Recent);
            AddStep("scores without ids", () => requests[0].TriggerSuccess(new List<SoloScoreInfo>
            {
                new SoloScoreInfo(), new SoloScoreInfo(), new SoloScoreInfo { ID = 0 }, new SoloScoreInfo { ID = 0 },
            }));
            AddUntilStep("all rows kept", () => container.RowIds.Length == 4);
        }

        [Test]
        public void TestChangingUserResetsSeenIds()
        {
            create(ScoreType.Best);
            AddStep("first user scores", () => requests[0].TriggerSuccess(scores(1, 2)));
            AddUntilStep("first user loaded", () => container.RowIds.Length == 2);
            AddStep("change profile", () => user.Value = profile(2));
            AddUntilStep("new request", () => requests.Count == 2);
            AddStep("same ids on refreshed profile", () => requests[1].TriggerSuccess(scores(1, 2)));
            AddUntilStep("rows restored", () => container.RowIds.SequenceEqual(new[] { "1", "2" }));
        }

        [Test]
        public void TestQueuedResponseDoesNotLeakIntoNewProfile()
        {
            create(ScoreType.Best);
            AddStep("queue response then change profile", () =>
            {
                requests[0].TriggerSuccess(scores(1, 2));
                user.Value = profile(2);
            });
            AddUntilStep("new request", () => requests.Count == 2);
            AddStep("new profile response", () => requests[1].TriggerSuccess(scores(3)));
            AddUntilStep("only new profile score", () => container.RowIds.SequenceEqual(new[] { "3" }));
        }

        private static UserProfileData profile(int id) => new UserProfileData(new APIUser { Id = id }, new OsuRuleset().RulesetInfo);

        private static List<SoloScoreInfo> scores(params ulong[] ids) => ids.Select(id => new SoloScoreInfo
        {
            ID = id,
            Beatmap = new APIBeatmap { OnlineID = 123 },
        }).ToList();

        private partial class TestScoreContainer : PaginatedScoreContainer
        {
            public readonly List<PaginationParameters> Pages = new List<PaginationParameters>();

            public string[] RowIds => ItemsContainer.FlowingChildren.Select(d => d.Name).ToArray();

            public TestScoreContainer(ScoreType type, Bindable<UserProfileData?> user)
                : base(type, user, "Scores")
            {
            }

            protected override APIRequest<List<SoloScoreInfo>> CreateRequest(UserProfileData user, PaginationParameters pagination)
            {
                Pages.Add(pagination);
                return base.CreateRequest(user, pagination);
            }

            protected override Drawable CreateDrawableItem(SoloScoreInfo model) => new Box
            {
                Name = model.ID?.ToString() ?? "missing",
                Width = 100,
                Height = 10,
            };
        }
    }
}
