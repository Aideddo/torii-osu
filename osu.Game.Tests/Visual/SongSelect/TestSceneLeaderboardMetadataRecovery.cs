// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Play.Leaderboards;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneLeaderboardMetadataRecovery : OsuTestScene
    {
        [Test]
        public void TestMetadataArrivalRefreshesLeaderboard([Values] bool metadataAlreadyArrived)
        {
            var manager = new LeaderboardManager();
            var ruleset = new OsuRuleset().RulesetInfo;
            var beatmap = new BeatmapInfo(ruleset, new BeatmapDifficulty(), new BeatmapMetadata()) { MD5Hash = "checksum" };
            var set = new BeatmapSetInfo { OnlineID = 456, Beatmaps = { beatmap } };
            beatmap.BeatmapSet = set;
            BeatmapInfo snapshot = null!;
            int requests = 0;

            AddStep("prepare map and requests", () =>
            {
                Clear();
                Realm.Write(r =>
                {
                    beatmap.Ruleset = r.Find<RulesetInfo>(ruleset.ShortName) ?? r.Add(ruleset);
                    r.Add(set);
                });
                snapshot = beatmap.Detach();
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is not GetScoresRequest scoresRequest)
                        return false;

                    requests++;
                    scoresRequest.TriggerSuccess(new APIScoresCollection { Scores = new(), ScoresCount = 0 });
                    return true;
                };
                Add(manager);
            });

            if (metadataAlreadyArrived)
                AddStep("metadata arrives before fetch", updateMetadata);

            AddStep("fetch using stale snapshot", () => manager.FetchWithCriteria(new LeaderboardCriteria(snapshot, ruleset, BeatmapLeaderboardScope.Global, null)));

            if (!metadataAlreadyArrived)
            {
                AddAssert("initially unavailable", () => manager.Scores.Value?.FailState, () => Is.EqualTo(LeaderboardFailState.BeatmapUnavailable));
                AddStep("metadata arrives after fetch", updateMetadata);
            }

            AddUntilStep("leaderboard recovered", () => manager.Scores.Value != null && manager.Scores.Value.FailState == null);
            AddAssert("one scores request", () => requests, () => Is.EqualTo(1));
            AddAssert("snapshot repaired", () => snapshot.OnlineID == 123 && snapshot.Status == BeatmapOnlineStatus.Ranked);

            void updateMetadata() => Realm.Write(r =>
            {
                var stored = r.Find<BeatmapInfo>(snapshot.ID)!;
                stored.OnlineID = 123;
                stored.OnlineMD5Hash = "checksum";
                stored.Status = BeatmapOnlineStatus.Ranked;
            });
        }
    }
}
