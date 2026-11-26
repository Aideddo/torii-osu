// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Scoring
{
    public class ScoreModelDownloader : ModelDownloader<ScoreInfo, IScoreInfo>
    {
        private readonly IAPIProvider api;
        private readonly ReplayExtrasStore? extras;

        /// <param name="scoreManager">Imports the downloaded replays.</param>
        /// <param name="api">The API to download from.</param>
        /// <param name="extras">Where what players saved along with their replays is kept: fetched too when given.</param>
        public ScoreModelDownloader(IModelImporter<ScoreInfo> scoreManager, IAPIProvider api, ReplayExtrasStore? extras = null)
            : base(scoreManager, api)
        {
            this.api = api;
            this.extras = extras;
        }

        /// <summary>
        /// Torii: a downloaded replay of a play made in VR takes along what its player saved with it (see
        /// <see cref="ReplayExtrasFile"/>), kept under the hash the replay was imported with.
        /// </summary>
        protected override void OnImported(IScoreInfo requested, Live<ScoreInfo> imported)
        {
            if (extras == null)
                return;

            var (onlineId, hash, vr) = imported.PerformRead(s => (s.OnlineID, s.Hash, s.Mods.Any(m => m is ModVR)));

            if (onlineId <= 0)
                onlineId = requested.OnlineID;

            // Only plays marked as made in VR have any; nothing is asked for the rest.
            if (!vr || onlineId <= 0 || string.IsNullOrEmpty(hash) || extras.Exists(hash))
                return;

            var request = new GetReplayExtrasRequest(onlineId);

            request.Success += () =>
            {
                try
                {
                    if (request.Extras is byte[] data && extras.Import(hash, new MemoryStream(data)))
                        Logger.Log($@"Downloaded the extras of replay {onlineId}.", LoggingTarget.Database);
                }
                catch (Exception e)
                {
                    Logger.Error(e, @"Failed to keep the extras downloaded for a replay.", LoggingTarget.Database);
                }
            };

            // Most often there simply are none (an older play, or one whose upload didn't make it).
            request.Failure += e => Logger.Log($@"No extras for replay {onlineId}: {e.Message}", LoggingTarget.Database);

            api.Queue(request);
        }

        protected override ArchiveDownloadRequest<IScoreInfo> CreateDownloadRequest(IScoreInfo score, bool minimiseDownload) => new DownloadReplayRequest(score);

        public override ArchiveDownloadRequest<IScoreInfo>? GetExistingDownload(IScoreInfo model)
            => CurrentDownloads.Find(r => r.Model.MatchesOnlineID(model));

        public bool Download(IScoreInfo model) => Download(model, false, null);
    }
}
