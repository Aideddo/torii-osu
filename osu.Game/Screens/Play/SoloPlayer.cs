// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Diagnostics;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Online.Solo;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;

namespace osu.Game.Screens.Play
{
    public partial class SoloPlayer : SubmittingPlayer
    {
        [Cached(typeof(IGameplayLeaderboardProvider))]
        private readonly SoloGameplayLeaderboardProvider leaderboardProvider = new SoloGameplayLeaderboardProvider();

        [Resolved]
        private IAPIProvider api { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; }

        public SoloPlayer([CanBeNull] PlayerConfiguration configuration = null)
            : base(configuration)
        {
            Configuration.ShowLeaderboard = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(leaderboardProvider);
        }

        protected override APIRequest<APIScoreToken> CreateTokenRequest()
        {
            int rulesetId = Ruleset.Value.OnlineID;

            if (!Ruleset.Value.IsLegacyRuleset())
                return null;

            if (Beatmap.Value.BeatmapInfo.Status == BeatmapOnlineStatus.LocallyModified)
                return null;

            if (!BeatmapOnlineInfoRecovery.TryRecover(Beatmap.Value.BeatmapInfo, api, realm))
                return null;

            return new CreateSoloScoreRequest(Beatmap.Value.BeatmapInfo, rulesetId, Game.VersionHash);
        }

        protected override bool ShouldExitOnTokenRetrievalFailure(Exception exception) => false;

        protected override APIRequest<MultiplayerScore> CreateSubmissionRequest(Score score, long token)
        {
            IBeatmapInfo beatmap = score.ScoreInfo.BeatmapInfo!;

            Debug.Assert(beatmap.OnlineID > 0);

            return new SubmitSoloScoreRequest(score.ScoreInfo, token, beatmap.OnlineID);
        }
    }
}
