// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneSongSelectOnlineMetadata : SongSelectTestScene
    {
        [Test]
        public void TestCancelledLookupDoesNotOverwriteCachedSelection()
        {
            int heldRequests = 0;
            int cachedRequests = 0;
            int originalCachedRequests = 0;
            IBindable<Screens.Select.SongSelect.BeatmapSetLookupResult?> lookup = null!;

            AddStep("hold one set and complete the other", () => ((DummyAPIAccess)API).HandleRequest = request =>
            {
                if (request is not GetBeatmapSetRequest lookupRequest)
                    return false;

                if (lookupRequest.ID == 100)
                {
                    heldRequests++;
                    return true;
                }

                if (lookupRequest.ID == 200)
                {
                    cachedRequests++;
                    var response = CreateAPIBeatmapSet(Beatmap.Value.BeatmapInfo);
                    response.OnlineID = 200;
                    lookupRequest.TriggerSuccess(response);
                    return true;
                }

                return false;
            });

            ImportBeatmapForRuleset(set => set.OnlineID = 100, 1, 0);
            ImportBeatmapForRuleset(set => set.OnlineID = 200, 1, 0);
            LoadSongSelect();

            AddStep("select set to cache", () =>
            {
                lookup = SongSelect.Dependencies.Get<IBindable<Screens.Select.SongSelect.BeatmapSetLookupResult?>>();
                selectSet(200);
            });
            AddUntilStep("set cached", () => lookup.Value?.Result?.OnlineID == 200);
            AddStep("remember request count", () => originalCachedRequests = cachedRequests);
            AddStep("select pending set", () => selectSet(100));
            AddUntilStep("request in progress", () => heldRequests > 0 && lookup.Value?.Status == Screens.Select.SongSelect.BeatmapSetLookupStatus.InProgress);
            AddStep("return to cached set", () => selectSet(200));
            AddWaitStep("let cancellation finish", 10);
            AddAssert("cached metadata still shown", () => lookup.Value?.Result?.OnlineID, () => Is.EqualTo(200));
            AddAssert("cached set not fetched again", () => cachedRequests, () => Is.EqualTo(originalCachedRequests));

            void selectSet(int id)
            {
                var selected = Beatmaps.GetAllUsableBeatmapSets().Single(set => set.OnlineID == id).Beatmaps.First();
                Beatmap.Value = Beatmaps.GetWorkingBeatmap(selected);
            }
        }
    }
}
