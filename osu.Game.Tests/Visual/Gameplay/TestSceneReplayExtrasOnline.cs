// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.Rooms;
using osu.Game.Online.Solo;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.VR;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// Torii: what's saved along with a replay is uploaded once its score is submitted, and downloaded along with the replay.
    /// </summary>
    public partial class TestSceneReplayExtrasOnline : VRReplayTestScene
    {
        protected override TestPlayer CreatePlayer(Ruleset ruleset) => new SubmittingTestPlayer();

        [SetUp]
        public void SetUp()
        {
            submissionsSucceed = false;
            uploads.Clear();
            downloads.Clear();
            pendingDownload = null;
            Schedule(prepareAPI);
        }

        [Test]
        public void TestNothingUploadedForAPlayWhichWasNotSubmitted()
        {
            PlayThrough();

            AddAssert("nothing uploaded", () => uploads, () => Is.Empty);
        }

        [Test]
        public void TestExtrasUploadedForSubmittedScore()
        {
            AddStep("submissions succeed", () => submissionsSucceed = true);

            PlayThrough();

            AddUntilStep("extras uploaded for the score", () => uploads.Select(u => u.scoreId), () => Is.EqualTo(new[] { submitted_score_id }));
            AddAssert("uploaded as saved", () =>
            {
                var saved = new MemoryStream();
                Store.CopyTo(SavedScore.Hash, saved);
                return uploads.Single().extras.SequenceEqual(saved.ToArray()) && saved.Length > 0;
            });
        }

        [Test]
        public void TestExtrasDownloadedWithReplay()
        {
            PlayThrough();
            string hash = string.Empty;
            byte[] online = Array.Empty<byte>();

            AddStep("note the replay", () => hash = SavedScore.Hash);

            // As for someone else: the replay is in (a play marked as made in VR), its extras are only online.
            AddStep("extras only online", () =>
            {
                var saved = new MemoryStream();
                Store.CopyTo(hash, saved);
                online = saved.ToArray();
                Store.Delete(hash);
            });
            AddAssert("no extras", () => !Store.Exists(hash));

            AddStep("replay downloaded", () =>
            {
                var downloader = new TestScoreDownloader(Dependencies.Get<ScoreManager>(), API, Store);
                downloader.Imported(new ScoreInfo { OnlineID = submitted_score_id }, Realm.Run(r => r.Find<ScoreInfo>(Player.Score.ScoreInfo.ID)!.ToLive(Realm)));
            });

            AddUntilStep("extras asked for by the score's online ID", () => downloads, () => Is.EqualTo(new[] { submitted_score_id }));
            AddStep("server answers", () => pendingDownload!.TriggerSuccess(online));
            AddUntilStep("extras kept for the replay", () => VRReplayExtrasEncoder.Load(Store, hash)?.Frames.Count, () => Is.GreaterThan(50));

            AddStep("downloaded again", () =>
            {
                var downloader = new TestScoreDownloader(Dependencies.Get<ScoreManager>(), API, Store);
                downloader.Imported(new ScoreInfo { OnlineID = submitted_score_id }, Realm.Run(r => r.Find<ScoreInfo>(Player.Score.ScoreInfo.ID)!.ToLive(Realm)));
            });
            AddAssert("not asked for again", () => downloads, () => Has.Count.EqualTo(1));
        }

        private const long submitted_score_id = 1234;

        private bool submissionsSucceed;
        private readonly List<(long scoreId, byte[] extras)> uploads = new List<(long, byte[])>();
        private readonly List<long> downloads = new List<long>();
        private GetReplayExtrasRequest? pendingDownload;

        private void prepareAPI() => ((DummyAPIAccess)API).HandleRequest = request =>
        {
            switch (request)
            {
                case CreateSoloScoreRequest tokenRequest when submissionsSucceed:
                    tokenRequest.TriggerSuccess(new APIScoreToken { ID = 1 });
                    return true;

                case SubmitSoloScoreRequest submissionRequest when submissionsSucceed:
                    submissionRequest.TriggerSuccess(new MultiplayerScore
                    {
                        ID = submitted_score_id,
                        User = API.LocalUser.Value,
                        Rank = submissionRequest.Score.Rank,
                        TotalScore = submissionRequest.Score.TotalScore,
                        Accuracy = submissionRequest.Score.Accuracy,
                        MaxCombo = submissionRequest.Score.MaxCombo,
                        Mods = submissionRequest.Score.Mods,
                        Statistics = submissionRequest.Score.Statistics,
                        Passed = submissionRequest.Score.Passed,
                        EndedAt = DateTimeOffset.Now,
                        Position = 1,
                    });
                    return true;

                case UploadReplayExtrasRequest upload:
                    uploads.Add((upload.ScoreId, upload.Extras));
                    upload.TriggerSuccess();
                    return true;

                case GetReplayExtrasRequest download:
                    downloads.Add(download.ScoreId);
                    pendingDownload = download;
                    return true;

                default:
                    return false;
            }
        };

        private class TestScoreDownloader : ScoreModelDownloader
        {
            public TestScoreDownloader(IModelImporter<ScoreInfo> scoreManager, IAPIProvider api, ReplayExtrasStore extras)
                : base(scoreManager, api, extras)
            {
            }

            public void Imported(IScoreInfo requested, Live<ScoreInfo> imported) => OnImported(requested, imported);
        }

        /// <summary>
        /// Asks to submit its score whatever the beatmap (the one of these tests isn't one the server would know).
        /// </summary>
        private partial class SubmittingTestPlayer : TestPlayer
        {
            public SubmittingTestPlayer()
                : base(false)
            {
            }

            protected override APIRequest<APIScoreToken> CreateTokenRequest() => new CreateSoloScoreRequest(Beatmap.Value.BeatmapInfo, 0, string.Empty);

            protected override Task PrepareScoreForResultsAsync(Score score)
            {
                // A play in which nothing was hit isn't submitted: these tests skip to the end of theirs.
                score.ScoreInfo.Statistics[HitResult.Great] = 1;
                score.ScoreInfo.TotalScore = 1000;

                return base.PrepareScoreForResultsAsync(score);
            }
        }
    }
}
